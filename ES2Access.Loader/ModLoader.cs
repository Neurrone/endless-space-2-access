using System;
using System.IO;
using System.Reflection;
using ES2Access.Loader.Dev;

namespace ES2Access.Loader
{
    /// <summary>
    /// Owns the mod assembly: finds it next to the loader, starts it, stops it, and swaps a
    /// freshly built one in when POST /reload asks.
    ///
    /// The assembly is read into a byte array and loaded from memory, never from the file, so
    /// ES2Access.dll stays unlocked and `dotnet build` can overwrite it while the game is
    /// running - that is the whole point of the split. Mono cannot unload an assembly, so the
    /// old ones stay in the process; this is a development loop, and leaking a few hundred
    /// kilobytes per reload is the price of not restarting the game.
    ///
    /// A reload validates the new build completely before it touches the running one, so a mod
    /// that does not compile into something loadable costs nothing: the old mod keeps speaking and
    /// /loader/status reports why the swap was refused. Only once the new assembly is known good
    /// is the old one stopped.
    ///
    /// Nothing here is allowed to take the loader down. A mod that throws in Start, in Stop, or
    /// that is not there at all, leaves the dev server up and the failure readable from
    /// /loader/status.
    /// </summary>
    internal sealed class ModLoader
    {
        private const string ModFileName = "ES2Access.dll";
        private const string EntryTypeName = "ES2Access.ModEntry";

        private readonly LoaderPlugin _plugin;
        private readonly DevServer _dev;
        private readonly ModHost _host;
        private readonly string _modPath;

        private Assembly _assembly;
        private MethodInfo _stop;

        public ModLoader(LoaderPlugin plugin, DevServer dev, string pluginDirectory)
        {
            _plugin = plugin;
            _dev = dev;
            _host = new ModHost(plugin, dev, pluginDirectory);
            _modPath = Path.Combine(pluginDirectory, ModFileName);
        }

        public bool ModLoaded
        {
            get { return _assembly != null; }
        }

        /// <summary>Reloads that actually swapped the mod out.</summary>
        public int ReloadCount { get; private set; }

        /// <summary>Reloads refused because the new build did not validate. The mod that was
        /// running at the time still is.</summary>
        public int FailedReloadCount { get; private set; }

        /// <summary>Why the last load or reload failed, or null if it worked.</summary>
        public string LastReloadError { get; private set; }

        /// <summary>When the file that produced the bytes now running was last written, or null
        /// while no mod is loaded. Compared against <see cref="ModFileOnDiskWrittenUtc"/> it
        /// answers "am I looking at the build I just made" without guessing.</summary>
        public DateTime? ModFileWrittenUtc { get; private set; }

        /// <summary>When the deployed file was last written, read now rather than remembered.</summary>
        public DateTime? ModFileOnDiskWrittenUtc
        {
            get
            {
                try
                {
                    return File.Exists(_modPath)
                        ? (DateTime?)File.GetLastWriteTimeUtc(_modPath)
                        : null;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public Assembly ModAssembly
        {
            get { return _assembly; }
        }

        public void Load()
        {
            Prepared prepared = Prepare();
            if (prepared == null)
            {
                LoaderLog.Error("Mod failed to load: " + LastReloadError);
                return;
            }

            Activate(prepared);
        }

        public void Unload()
        {
            MethodInfo stop = _stop;
            _assembly = null;
            _stop = null;
            ModFileWrittenUtc = null;

            if (stop != null)
            {
                try
                {
                    Invoke(stop, null);
                }
                catch (Exception e)
                {
                    LastReloadError = e.ToString();
                    LoaderLog.Error("Mod threw while stopping; unwinding it anyway: " + e);
                }
            }

            // Whatever the mod did or did not take down itself, the loader now holds nothing of
            // its; otherwise a route or a pump from the dead assembly would outlive it.
            _host.UnregisterAllModRoutes();
            _plugin.SetModUpdateHandler(null);
            _plugin.StopModCoroutines();
        }

        /// <summary>
        /// Swap in whatever is on disk now. The new build is read, loaded and inspected first: if
        /// any of that fails the running mod is left alone, because a broken build is far more
        /// common than a broken game and losing a working mod to it wastes a whole restart.
        ///
        /// Past that point the swap is committed. Start throwing still leaves no mod running -
        /// the resources a mod holds (the speech backend, the route table, the frame pump) are
        /// single-slot, so the old and new mod can never genuinely overlap - but the loader
        /// survives it and says so.
        /// </summary>
        public void Reload()
        {
            Prepared prepared = Prepare();
            if (prepared == null)
            {
                FailedReloadCount++;
                LoaderLog.Error(
                    "Reload refused, the running mod is untouched: " + LastReloadError
                );
                return;
            }

            ReloadCount++;
            Unload();
            Activate(prepared);
        }

        /// <summary>Everything the new mod needs, obtained without disturbing the old one.</summary>
        private sealed class Prepared
        {
            public Assembly Assembly;
            public MethodInfo Start;
            public MethodInfo Stop;
            public DateTime? WrittenUtc;
        }

        // Read-only with respect to the running mod: on failure it reports and nothing has moved.
        private Prepared Prepare()
        {
            try
            {
                // Taken before the bytes, so a build landing mid-read shows up as stale rather
                // than as the build we are about to run.
                DateTime? writtenUtc = ModFileOnDiskWrittenUtc;
                Assembly assembly = Assembly.Load(File.ReadAllBytes(_modPath));
                Type entry = assembly.GetType(EntryTypeName);
                if (entry == null)
                {
                    throw new TypeLoadException(
                        ModFileName + " has no " + EntryTypeName + "; it is not a mod assembly"
                    );
                }

                MethodInfo start = entry.GetMethod(
                    "Start",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(ModHost) },
                    null
                );
                MethodInfo stop = entry.GetMethod(
                    "Stop",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    Type.EmptyTypes,
                    null
                );
                if (start == null || stop == null)
                {
                    throw new MissingMethodException(
                        EntryTypeName + " must declare public static Start(ModHost) and Stop()"
                    );
                }

                return new Prepared
                {
                    Assembly = assembly,
                    Start = start,
                    Stop = stop,
                    WrittenUtc = writtenUtc,
                };
            }
            catch (Exception e)
            {
                LastReloadError = e.ToString();
                return null;
            }
        }

        private void Activate(Prepared prepared)
        {
            try
            {
                Invoke(prepared.Start, new object[] { _host });
                _assembly = prepared.Assembly;
                _stop = prepared.Stop;
                ModFileWrittenUtc = prepared.WrittenUtc;
                LastReloadError = null;
                _dev.ReferenceModAssembly(prepared.Assembly);
                LoaderLog.Info("Mod loaded from " + _modPath);
            }
            catch (Exception e)
            {
                _assembly = null;
                _stop = null;
                ModFileWrittenUtc = null;
                LastReloadError = e.ToString();
                LoaderLog.Error("Mod failed to start: " + e);

                // Start may have registered routes or the pump before throwing; a dead assembly
                // must not keep serving them.
                _host.UnregisterAllModRoutes();
                _plugin.SetModUpdateHandler(null);
                _plugin.StopModCoroutines();
            }
        }

        // Reflection wraps whatever the mod threw; the wrapper says nothing worth logging.
        private static void Invoke(MethodInfo method, object[] arguments)
        {
            try
            {
                method.Invoke(null, arguments);
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException ?? e;
            }
        }
    }
}
