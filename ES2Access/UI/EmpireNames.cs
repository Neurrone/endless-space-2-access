using System;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// What an empire is called, to the player - the mod's one answer, and the game's own.
    ///
    /// <c>Empire.LocalizedName</c> is the player name of a MAJOR empire and nothing else: for a
    /// pirate empire it is the raw internal name (<c>PirateEmpire#0</c>), for a minor civilization it
    /// is not the word the game writes anywhere, and for an empire the player has never met it leaks a
    /// name the player is not supposed to have. The game itself never draws it. Every surface that
    /// names an empire goes through the GUI wrapper's leader-name ladder instead
    /// (<c>GuiEmpire.GetLeaderName</c>, <c>GuiEmpire.cs</c>:291-321), which answers, in order: a
    /// lesser empire "Unknown Empire"; a pirate empire "%EmpirePirateTitle" ("Pirates"); the Academy
    /// one of its two titles; a met major its <c>LocalizedName</c>; a minor civilization its faction
    /// title; and anyone the looker has not met "Unknown Empire". That last one is the reason this is
    /// the fog-safe answer as well as the correctly-worded one.
    ///
    /// Asked as the player sees it: the looking empire is <c>Gui.PlayerEmpire</c>, and the three
    /// decoration flags are off - no colour markup, no icon prefix, and no "you" substitution, because
    /// spoken text names the player's own empire the same way it names anyone else's and the callers
    /// that want "yours" said say so themselves. <see cref="AgeText.Clean"/> still runs, for the
    /// <c>%key</c> the ladder can hand back and the glyphs a title can carry.
    ///
    /// Null for no empire, for a wrapper service that is not there (off the galaxy there is none), and
    /// for anything that throws - a name is worth a frame's silence, never a frame.
    ///
    /// Main-thread only.
    /// </summary>
    public static class EmpireNames
    {
        /// <summary>What <paramref name="empire"/> is called to the player, or null when there is
        /// nothing to call it. Takes the engine's base class because the event bus types an event's
        /// empires that way; every empire in a running game is the game's own subclass.</summary>
        public static string Named(Amplitude.Unity.Game.Empire empire)
        {
            try
            {
                Empire named = empire as Empire;
                if (named == null || Gui.GuiWrapperProviderService == null)
                {
                    return null;
                }

                GuiEmpire wrapper = Gui.GuiWrapperProviderService.GetGuiEmpire(named);
                return wrapper == null
                    ? null
                    : AgeText.Clean(
                        wrapper.GetLeaderName(Gui.PlayerEmpire, false, false, false)
                    );
            }
            catch (Exception e)
            {
                Log.Warn("empire: naming an empire threw: " + e);
                return null;
            }
        }
    }
}
