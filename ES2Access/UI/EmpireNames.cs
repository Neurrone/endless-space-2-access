using System;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// What an empire is called, to the player - named the way the surface in question DRAWS it.
    ///
    /// <c>Empire.LocalizedName</c> is the player name of a MAJOR empire and nothing else: for a
    /// pirate empire it is the raw internal name (<c>PirateEmpire#0</c>), for a minor civilization it
    /// is not the word the game writes anywhere, and for an empire the player has never met it leaks a
    /// name the player is not supposed to have. Every surface that names an empire goes through the
    /// GUI wrapper's leader-name ladder instead (<c>GuiEmpire.GetLeaderName</c>, <c>GuiEmpire.cs</c>
    /// :291-321), which answers, in order: a lesser empire "Unknown Empire"; a pirate empire
    /// "%EmpirePirateTitle" ("Pirates"); the Academy one of its two titles; a met major its
    /// <c>LocalizedName</c>; a minor civilization its faction title; and anyone the looker has not met
    /// "Unknown Empire". That last one is the reason this is the fog-safe answer as well as the
    /// correctly-worded one.
    ///
    /// The contract of this file is NOT "named with the faction". It is "named the way this surface
    /// draws it", because the game draws four different forms and each of the others is wrong
    /// somewhere:
    ///
    /// <list type="bullet">
    /// <item><see cref="Named"/> - the leader name alone, which is <c>iconPrefix:false</c>. What the
    /// game draws where a faction picture is NOT sitting beside the name: the haunted-planet sentence
    /// (<c>PlanetLabel_SystemOrbital</c> :469, <c>PlanetLabel_SystemManagement</c> :1209,
    /// <c>HauntCircleItem</c> :17), the deed-failed notification title
    /// (<c>DeedCompletedNotificationWindow</c> :55), and every map readout the game paints in empire
    /// COLOUR and names nobody in.</item>
    /// <item><see cref="WithFaction"/> - the faction symbol in front of the leader name ("Riftborn
    /// Kappa (AI)"), which is the ladder's own DEFAULT. What the game draws wherever it writes an
    /// empire into a label of its own: a system's dossier header (<c>GuiStarSystem</c> :70), a special
    /// node's (<c>GuiSpecialNode</c> :36, :52), a ground battle's opponent row
    /// (<c>GroundBattleOpponentItem</c> :40), a fleet card (<c>EmpireFleetCard</c> :78), a scan-view
    /// owner heading (<c>ScanViewDiplomacyLabel</c> :309), a mining probe
    /// (<c>PanelFeatureMiningProbe</c> :29), a marketplace advertisement (<c>AdItem</c> :24). The
    /// symbol is an inline <c>[token]</c> glyph and <see cref="AgeText.Clean"/> turns it into the
    /// faction's word. A faction with no symbol degrades to the bare name, exactly as the drawn label
    /// does.</item>
    /// <item><see cref="LeaderAndFaction"/> - "Kappa (AI) (Riftborn)" (<c>GuiEmpire</c> :361-397).
    /// What the game words where the faction is a PICTURE beside the name and there is no symbol in
    /// the string: the scoreboard line (<c>PlayerStatusLine</c> :38-47), the diplomacy sector and
    /// leader cards (<c>EmpireSector</c> :209, <c>LeaderCard</c> :388), a fleet group's tooltip
    /// category (<c>GuiFleetGroup</c> :32), the fleets panel (<c>FleetsManagementPanel</c> :144,149),
    /// and a deed's winner, whom <c>DeedItem2</c> :176-182 identifies by faction logo and nothing
    /// else.</item>
    /// <item><see cref="RawNameAndFaction"/> - "Kappa (AI) (Riftborn)" built the long way round from
    /// the raw name plus the faction symbol and the faction's own name. One label draws it
    /// (<c>CoordinationRequestLabel</c> :373-374) and it is the only form in this file that is not
    /// fog-safe, because it is the only one the game itself builds off <c>LocalizedName</c>.</item>
    /// </list>
    ///
    /// Each form has a CLAIM-scoped overload where a surface has a claim to scope it by - a system, a
    /// colony, a fleet, a ground battle. A minor civilization is named per claim
    /// (<c>GuiEmpire</c> :323-334, <c>LesserEmpire.GetLesserName</c>), and the claimless overloads
    /// flatten every claim of one minor faction into a single word, so a row about one place asks with
    /// the place's GUID exactly as the game's own label does.
    ///
    /// Asked as the player sees it: the looking empire is <c>Gui.PlayerEmpire</c> unless a caller
    /// names another, and the decoration flags are off - no colour markup and no "you" substitution,
    /// because spoken text names the player's own empire the same way it names anyone else's and the
    /// callers that want "yours" said say so themselves. Colour markup is off rather than stripped
    /// later; <see cref="AgeText.Clean"/> would remove it either way (measured over all 22 empires of
    /// a live game, 2026-09-14: every form identical with the flag on and off), and it still runs here
    /// for the <c>%key</c> the ladder can hand back and the icon glyphs a title carries.
    ///
    /// Null for no empire, for a wrapper service that is not there (off the galaxy there is none), and
    /// for anything that throws - a name is worth a frame's silence, never a frame.
    ///
    /// Main-thread only.
    /// </summary>
    public static class EmpireNames
    {
        /// <summary>What <paramref name="empire"/> is called to the player, with no faction - the form
        /// the game draws with <c>iconPrefix:false</c>. Takes the engine's base class because the event
        /// bus types an event's empires that way; every empire in a running game is the game's own
        /// subclass.</summary>
        public static string Named(Amplitude.Unity.Game.Empire empire)
        {
            return Named(empire, Looking());
        }

        /// <summary>The same, asked from a particular empire's point of view rather than the
        /// player's.</summary>
        public static string Named(Amplitude.Unity.Game.Empire empire, Empire looking)
        {
            try
            {
                GuiEmpire wrapper = Wrapper(empire);
                return wrapper == null
                    ? null
                    : AgeText.Clean(wrapper.GetLeaderName(looking, false, false, false));
            }
            catch (Exception e)
            {
                Log.Warn("empire: naming an empire threw: " + e);
                return null;
            }
        }

        /// <summary>The faction symbol in front of the leader name - the ladder's own default, and the
        /// form every label the game writes an empire into carries.</summary>
        public static string WithFaction(Amplitude.Unity.Game.Empire empire, Empire looking)
        {
            return WithFaction(Wrapper(empire), looking);
        }

        /// <summary>The same for a surface that already holds the wrapper.</summary>
        public static string WithFaction(GuiEmpire wrapper, Empire looking)
        {
            try
            {
                return wrapper == null
                    ? null
                    : AgeText.Clean(wrapper.GetLeaderName(looking, false, false, true));
            }
            catch (Exception e)
            {
                Log.Warn("empire: naming an empire threw: " + e);
                return null;
            }
        }

        /// <summary>The same, scoped to the CLAIM the surface is about - the system, colony, fleet or
        /// battle whose owner is being named. A minor civilization is named per claim, and the
        /// claimless overload would flatten every claim of one into a single word.</summary>
        public static string WithFaction(
            Amplitude.Unity.Game.Empire empire,
            GameEntityGUID claim,
            Empire looking
        )
        {
            return WithFaction(Wrapper(empire), claim, looking);
        }

        /// <summary>The claim-scoped form for a surface that already holds the wrapper.</summary>
        public static string WithFaction(GuiEmpire wrapper, GameEntityGUID claim, Empire looking)
        {
            try
            {
                return wrapper == null
                    ? null
                    : AgeText.Clean(wrapper.GetLeaderName(claim, looking, false, false, true));
            }
            catch (Exception e)
            {
                Log.Warn("empire: naming an empire threw: " + e);
                return null;
            }
        }

        /// <summary>"Kappa (AI) (Riftborn)" - the form the game words where the faction is a picture
        /// beside the name.</summary>
        public static string LeaderAndFaction(Amplitude.Unity.Game.Empire empire)
        {
            return LeaderAndFaction(Wrapper(empire), Looking());
        }

        /// <summary>The same for a surface that already holds the wrapper, asked from a particular
        /// empire's point of view.</summary>
        public static string LeaderAndFaction(GuiEmpire wrapper, Empire looking)
        {
            try
            {
                return wrapper == null
                    ? null
                    : AgeText.Clean(wrapper.GetLeaderAndFaction(looking, false, false));
            }
            catch (Exception e)
            {
                Log.Warn("empire: naming an empire threw: " + e);
                return null;
            }
        }

        /// <summary>The same, scoped to the claim the surface is about - what
        /// <c>GuiFleetGroup</c> :32 asks of a fleet.</summary>
        public static string LeaderAndFaction(
            Amplitude.Unity.Game.Empire empire,
            GameEntityGUID claim,
            Empire looking
        )
        {
            try
            {
                GuiEmpire wrapper = Wrapper(empire);
                return wrapper == null
                    ? null
                    : AgeText.Clean(wrapper.GetLeaderAndFaction(claim, looking, false, false));
            }
            catch (Exception e)
            {
                Log.Warn("empire: naming an empire threw: " + e);
                return null;
            }
        }

        /// <summary>The raw name with the faction symbol and the faction's own name after it, which is
        /// the one form <c>CoordinationRequestLabel</c> :373-374 builds and nothing else does. Not
        /// fog-safe - it is the game's own label that is not - so it belongs only where that label is
        /// what is being read back.</summary>
        public static string RawNameAndFaction(GuiEmpire wrapper)
        {
            try
            {
                if (wrapper == null || wrapper.GuiFaction == null)
                {
                    return null;
                }

                return AgeText.Clean(
                    wrapper.LocalizedName
                        + " ("
                        + wrapper.GuiFaction.GetSymbolString(false)
                        + wrapper.GuiFaction.LocalizedName
                        + ")"
                );
            }
            catch (Exception e)
            {
                Log.Warn("empire: naming an empire threw: " + e);
                return null;
            }
        }

        /// <summary>The wrapper the game names an empire through, or null where there is nothing to
        /// name or no wrapper service to name it with.</summary>
        private static GuiEmpire Wrapper(Amplitude.Unity.Game.Empire empire)
        {
            try
            {
                Empire named = empire as Empire;
                return named == null || Gui.GuiWrapperProviderService == null
                    ? null
                    : Gui.GuiWrapperProviderService.GetGuiEmpire(named);
            }
            catch (Exception e)
            {
                Log.Warn("empire: naming an empire threw: " + e);
                return null;
            }
        }

        /// <summary>Whose point of view a name is asked from when the caller does not say.</summary>
        private static Empire Looking()
        {
            return Gui.PlayerEmpire;
        }
    }
}
