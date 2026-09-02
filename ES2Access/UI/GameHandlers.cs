using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// Reaching a game class's own non-public method or field by name, with ONE answer for a miss.
    ///
    /// Every screen that replays a handler the game wired to a button, or reads a field the game never
    /// exposed, wrote this lookup again - and the copies disagreed about failure. Most logged and
    /// returned null; one threw <c>MissingMethodException</c>; several said nothing at all. The same
    /// missing member was therefore a silent no-op on five pages and a crash on the sixth, which is
    /// the wrong split twice over: a name this build does not have means the mod has drifted from the
    /// game, and drifting is not a reason to take a page down in front of the player. null here, and
    /// the caller's own null test decides what is offered.
    ///
    /// Logged ONCE per member. These are resolved from static initialisers and from per-frame readers
    /// alike, and a miss in a per-frame reader writes one line per frame forever.
    ///
    /// Public members are searched too: what makes a member interesting here is that the game never
    /// meant it to be called from outside, which is not the same as it being marked private.
    /// </summary>
    public static class GameHandlers
    {
        private const BindingFlags Members =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        /// <summary>The game's own method of that name, or null - see the class summary for what a
        /// null means and why it is not a throw.</summary>
        public static MethodInfo Method(Type type, string name)
        {
            return Method(type, name, null);
        }

        /// <summary>The overload with this exact argument list, for a name the game overloads.
        /// <paramref name="arguments"/> of <c>Type.EmptyTypes</c> means the no-argument one; null means
        /// "whichever one there is", which throws where the game has two.</summary>
        public static MethodInfo Method(Type type, string name, Type[] arguments)
        {
            if (type == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            try
            {
                MethodInfo method =
                    arguments == null
                        ? type.GetMethod(name, Members)
                        : type.GetMethod(name, Members, null, arguments, null);
                if (method == null)
                {
                    Missing(type, name, null);
                }

                return method;
            }
            catch (Exception e)
            {
                Missing(type, name, e);
                return null;
            }
        }

        /// <summary>The game's own field of that name, or null - the same failure policy as
        /// <see cref="Method(Type, string)"/>.</summary>
        public static FieldInfo Field(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            try
            {
                FieldInfo field = type.GetField(name, Members);
                if (field == null)
                {
                    Missing(type, name, null);
                }

                return field;
            }
            catch (Exception e)
            {
                Missing(type, name, e);
                return null;
            }
        }

        private static readonly Dictionary<string, bool> Reported = new Dictionary<string, bool>();

        private static void Missing(Type type, string name, Exception e)
        {
            string member = type.Name + "." + name;
            if (Reported.ContainsKey(member))
            {
                return;
            }

            Reported[member] = true;
            Log.Warn(
                e == null
                    ? "the game has no " + member + " for this build"
                    : "looking up " + member + " threw: " + e
            );
        }
    }
}
