using System;
using ES2Access.UI;
using HarmonyLib;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// The place on the map the GAME has just taken the player to, remembered until the mod's cursor can
    /// be put there.
    ///
    /// Every "go and look at this" in this game - a notification's show-location, a panel's locate
    /// button, a table's double click, the quest banner's pin, the next-idle-fleet button - ends in one
    /// of three calls on <c>IGuiGameWindowService</c>, and all three land the player on the galaxy page
    /// (<c>GuiManager.cs</c> :1170-1175, :1264-1286). The camera goes; the cursor, without this, stays
    /// wherever the player left it, so the tree describes one place while the map shows another.
    ///
    /// The three calls are not equally informative, and they NEST: the richer
    /// <c>RequestGalaxyOverviewViewLevel(entity)</c> forwards to the <c>Vector3</c> overload and throws
    /// the entity away, and <c>ShowQuestLocation</c> forwards to the same overload with the marker it
    /// picked. Postfixes therefore fire inner-first, and the outer, richer capture overwrites the
    /// poorer one it caused - which is the wanted order. The frame stamp only guards the other
    /// direction: a second bare position arriving in the same frame as an entity does not demote it.
    ///
    /// One shot: the request is taken by the landing that answers it and gone, and the galaxy page
    /// forgets an unclaimed one when it goes away, so a locate that never found a page to land on
    /// cannot seize the cursor on some unrelated visit later.
    ///
    /// <see cref="Suppressed"/> is the other half of the contract. The mod moves the camera through the
    /// game's own reveal call whenever the cursor lands on a system (<c>GalaxyViewLevels.PanTo</c>), and
    /// that is the mod following the player, not the game leading them - captured, it would make every
    /// arrow key look like a locate.
    /// </summary>
    internal static class GalaxyLocate
    {
        /// <summary>What the game asked to be looked at. Whichever fields are filled, the position is
        /// always one of them, because the poorest of the three calls is the one the others go
        /// through.</summary>
        internal sealed class Request
        {
            /// <summary>The thing itself, where the game named one - a fleet, a node, a colony.</summary>
            public IGameEntityWithGalaxyPosition Entity;

            /// <summary>Where the camera was sent.</summary>
            public Vector3 Position;

            /// <summary>The quest whose marker this is, where the reveal was a quest pin. The position
            /// is that marker's: <c>ShowQuestLocation</c> picks which marker to cycle to and then makes
            /// the ordinary position request with it, so the chosen marker arrives here already.
            /// </summary>
            public Quest Quest;

            /// <summary>The step of that quest, for the same reason.</summary>
            public QuestStep Step;

            /// <summary>The frame it was made on - what tells a nested pair of calls from two unrelated
            /// ones.</summary>
            public int Frame;
        }

        /// <summary>Set while the MOD is the one moving the camera. The game's reveal calls are how the
        /// mod pans, and a pan that follows the cursor is not a place to send the cursor.</summary>
        public static bool Suppressed;

        private static readonly ModPatch Patches = new ModPatch(
            "galaxylocate",
            "the game's go-and-look-at-this calls"
        );

        private static Request _wanted;

        public static void Install()
        {
            // The concrete class, not the interface every caller names: an interface method has no
            // body to patch, and the game has exactly one implementation of this service. Each of
            // the three is optional, so a signature this game's build does not have costs the other
            // two nothing.
            Patches.Install(
                patch =>
                {
                    Hook(
                        patch,
                        "RequestGalaxyOverviewViewLevel",
                        new[] { typeof(IGameEntityWithGalaxyPosition) },
                        "RememberEntity"
                    );
                    Hook(
                        patch,
                        "RequestGalaxyOverviewViewLevel",
                        new[] { typeof(Vector3) },
                        "RememberPosition"
                    );
                    Hook(
                        patch,
                        "ShowQuestLocation",
                        new[] { typeof(Quest), typeof(QuestStep) },
                        "RememberQuest"
                    );
                }
            );
        }

        private static void Hook(ModPatch patch, string name, Type[] parameters, string postfix)
        {
            patch.Hook(
                AccessTools.Method(typeof(GuiManager), name, parameters),
                name,
                typeof(GalaxyLocate),
                null,
                postfix
            );
        }

        public static void Remove()
        {
            Patches.Remove();
            _wanted = null;
            Suppressed = false;
        }

        /// <summary>The place the game last asked to be looked at, left where it is - for a page that
        /// may need several frames before it has anything to land on.</summary>
        public static Request Peek()
        {
            return _wanted;
        }

        /// <summary>The same, and it is nobody's after that.</summary>
        public static Request Take()
        {
            Request wanted = _wanted;
            _wanted = null;
            return wanted;
        }

        /// <summary>Drop a request nobody came to collect - the page it was meant for has gone away.
        /// </summary>
        public static void Forget()
        {
            _wanted = null;
        }

        private static void RememberEntity(IGameEntityWithGalaxyPosition entityToFocus)
        {
            // Counted before the suppression gate and on both overloads: SUPPRESSED says this reveal is
            // not a place to send the CURSOR to, never that the camera stayed still, and the galaxy
            // page's record of where it last sent the camera has to stop believing itself either way
            // (<see cref="UI.GalaxyViewLevels.Moves"/>). The nested pair of calls counts twice, which
            // costs nothing: only equality with the count is ever asked.
            UI.GalaxyViewLevels.Moved();
            if (Suppressed || entityToFocus == null)
            {
                return;
            }

            Remember(entityToFocus, entityToFocus.GalaxyPosition);
        }

        private static void RememberPosition(Vector3 positionToFocusOn)
        {
            UI.GalaxyViewLevels.Moved();
            // An entity captured this frame is the same request seen from further out: the entity
            // overload calls this one. Keep the richer half.
            if (Suppressed || (_wanted != null && _wanted.Entity != null && ThisFrame(_wanted)))
            {
                return;
            }

            Remember(null, positionToFocusOn);
        }

        private static void RememberQuest(Quest quest, QuestStep step)
        {
            // The marker's own position arrived a moment ago, through the call this one makes; a quest
            // with no markers makes no such call and has nowhere to send anybody.
            if (Suppressed || _wanted == null || !ThisFrame(_wanted))
            {
                return;
            }

            try
            {
                _wanted.Quest = quest;
                _wanted.Step = step;
            }
            catch (Exception e)
            {
                Report(e);
            }
        }

        private static void Remember(IGameEntityWithGalaxyPosition entity, Vector3 position)
        {
            try
            {
                _wanted = new Request
                {
                    Entity = entity,
                    Position = position,
                    Frame = Time.frameCount,
                };
            }
            catch (Exception e)
            {
                Report(e);
            }
        }

        private static bool ThisFrame(Request request)
        {
            return request.Frame == Time.frameCount;
        }

        private static void Report(Exception e)
        {
            Patches.Report("remembering where the game located threw", e);
        }
    }
}
