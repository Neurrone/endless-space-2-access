using System;
using System.Reflection;
using ES2Access.Core.Util;
using HarmonyLib;

namespace ES2Access.UI
{
    /// <summary>
    /// One owner for the Harmony boilerplate every hooking feature had copied: the per-load id, the
    /// install/undo pair, and the report-once flag the hook bodies complain through.
    ///
    /// The ID is what makes this worth a class. Harmony identifies a patch's owner by id ALONE, so a
    /// fixed id lets the <c>UnpatchSelf</c> of the assembly a hot reload replaced strip the patches the
    /// NEW load has just applied - the feature goes quiet, the mod looks healthy, and nothing says
    /// why. Sixteen features restated that rule in a comment and spelled the Guid out again; here the
    /// id is made where the instance is, so a seventeenth cannot forget it.
    ///
    /// A failed install is LOUD and not fatal. Unpatched, a feature is the silence the game always
    /// had; refusing to start the rest of the mod over one signature this build does not have would
    /// cost the player every other feature. So the wiring runs inside one try: anything it throws is
    /// logged against the feature's own subject and the partial patch is undone, leaving nothing half
    /// applied for the next <see cref="Remove"/> to find.
    ///
    /// <see cref="Report"/> is the other half the copies each rebuilt: a hook body runs inside the
    /// game's own call and can throw every frame, so its complaint is worth exactly once.
    /// </summary>
    internal sealed class ModPatch
    {
        private readonly string _name;
        private readonly string _subject;
        private Harmony _harmony;
        private Harmony _wiring;
        private bool _reported;

        /// <summary><paramref name="name"/> is the id's own segment (lower case, no dots);
        /// <paramref name="subject"/> is what the failure log calls the thing that could not be
        /// patched, phrased so "&lt;subject&gt; could not be patched" reads as a sentence.</summary>
        internal ModPatch(string name, string subject)
        {
            _name = name;
            _subject = subject;
        }

        /// <summary>Whether this feature's patches are on the game right now.</summary>
        internal bool Installed
        {
            get { return _harmony != null; }
        }

        /// <summary>
        /// Apply the feature's patches. <paramref name="wire"/> is called with this instance and
        /// declares them through <see cref="Prefix"/>/<see cref="Postfix"/>/<see cref="Hook"/>; it
        /// runs inside the install's own try, so a target it cannot resolve is a throw rather than a
        /// half-patched feature.
        ///
        /// Removes first, so an install is also the reinstall a hot reload needs. False when the
        /// feature is not on the game - the caller usually has nothing to do about that but may want
        /// to say so.
        /// </summary>
        internal bool Install(Action<ModPatch> wire)
        {
            Remove();
            Harmony harmony = new Harmony(
                "endless.space2.access." + _name + "." + Guid.NewGuid().ToString("N")
            );

            _wiring = harmony;
            try
            {
                wire(this);
                _harmony = harmony;
                return true;
            }
            catch (Exception e)
            {
                Log.Error(_subject + " could not be patched: " + e);
                try
                {
                    harmony.UnpatchSelf();
                }
                catch (Exception undo)
                {
                    Log.Warn("and the partial patch could not be undone: " + undo.Message);
                }

                return false;
            }
            finally
            {
                _wiring = null;
            }
        }

        /// <summary>A prefix on a target the feature REQUIRES: a null target, or a hook method this
        /// assembly does not have, fails the whole install rather than leaving the feature half
        /// wired.</summary>
        internal void Prefix(MethodBase target, Type hooks, string method)
        {
            Apply(target, hooks, method, null);
        }

        /// <summary>A postfix on a target the feature REQUIRES - see <see cref="Prefix"/>.</summary>
        internal void Postfix(MethodBase target, Type hooks, string method)
        {
            Apply(target, hooks, null, method);
        }

        /// <summary>
        /// One of several targets, patched where the game has it and logged-and-skipped where it does
        /// not, so a signature this build lacks costs the feature that hook and not the others. False
        /// when it was skipped.
        ///
        /// <paramref name="named"/> is what the log calls the missing method, because a null target
        /// carries no name of its own. Pass null for the fix this target does not want.
        /// </summary>
        internal bool Hook(MethodBase target, string named, Type hooks, string prefix, string postfix)
        {
            if (target == null)
            {
                Log.Warn(_subject + ": the game has no " + named + " with that signature");
                return false;
            }

            Apply(target, hooks, prefix, postfix);
            return true;
        }

        /// <summary>Take the feature's patches back off the game and arm <see cref="Report"/> again.
        /// Safe on a feature that never installed, and safe twice.</summary>
        internal void Remove()
        {
            Harmony harmony = _harmony;
            _harmony = null;
            _reported = false;
            if (harmony == null)
            {
                return;
            }

            try
            {
                harmony.UnpatchSelf();
            }
            catch (Exception e)
            {
                Log.Error(_subject + " could not be unpatched: " + e);
            }
        }

        /// <summary>A hook body's complaint, once per install: these run inside the game's own call
        /// and a broken one throws on every frame that reaches it.</summary>
        internal void Report(string message, Exception e)
        {
            if (_reported)
            {
                return;
            }

            _reported = true;
            Log.Warn(message + ": " + e);
        }

        private void Apply(MethodBase target, Type hooks, string prefix, string postfix)
        {
            if (_wiring == null)
            {
                throw new InvalidOperationException(
                    _subject + ": patches are declared from inside Install"
                );
            }

            if (target == null)
            {
                throw new MissingMethodException(
                    _subject + ": no game method to patch with " + (prefix ?? postfix)
                );
            }

            _wiring.Patch(target, Hook(hooks, prefix), Hook(hooks, postfix));
        }

        /// <summary>One of this feature's own hook methods, by name - resolved here so a rename shows
        /// up as a failed install with the name in it rather than as a feature that quietly does
        /// nothing.</summary>
        private static HarmonyMethod Hook(Type hooks, string method)
        {
            if (string.IsNullOrEmpty(method))
            {
                return null;
            }

            MethodInfo found = hooks.GetMethod(
                method,
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public
            );
            if (found == null)
            {
                throw new MissingMethodException(hooks.FullName, method);
            }

            return new HarmonyMethod(found);
        }
    }
}
