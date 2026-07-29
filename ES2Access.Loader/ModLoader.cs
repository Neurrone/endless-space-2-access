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

        public int ReloadCount { get; private set; }

        /// <summary>Why the last load or reload failed, or null if it worked.</summary>
        public string LastReloadError { get; private set; }

        public Assembly ModAssembly
        {
            get { return _assembly; }
        }

        public void Load()
        {
            try
            {
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

                Invoke(start, new object[] { _host });
                _assembly = assembly;
                _stop = stop;
                LastReloadError = null;
                _dev.ReferenceModAssembly(assembly);
                LoaderLog.Info("Mod loaded from " + _modPath);
            }
            catch (Exception e)
            {
                _assembly = null;
                _stop = null;
                LastReloadError = e.ToString();
                LoaderLog.Error("Mod failed to load: " + e);
            }
        }

        public void Unload()
        {
            MethodInfo stop = _stop;
            _assembly = null;
            _stop = null;

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

        public void Reload()
        {
            ReloadCount++;
            Unload();
            Load();
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
