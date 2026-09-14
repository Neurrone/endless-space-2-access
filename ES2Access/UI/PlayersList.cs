using System;
using System.Collections.Generic;
using Amplitude.Extensions;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// The scoreboard the game draws beside End Turn: a line per empire in the game, with what it is
    /// called, what it is worth, how it stands with this empire and where it is in its turn.
    ///
    /// The game hangs the whole thing on the MOUSE. <c>EndTurnWindow.SpecificUpdate</c> (:906-921)
    /// shows <c>PlayersListPanel</c> only while <c>AgeManager.Instance.Cursor</c> is inside the End
    /// Turn button's radius and hides it on every other frame, and the mod moves no cursor - so for a
    /// keyboard player the panel is eight empires' worth of standings that nothing on the screen ever
    /// draws. Reading it needs two separate things: WORDS, which the mod composes, and the PICTURE,
    /// which is for whoever else is watching the screen.
    ///
    /// The words come from the MODEL - the same calls <c>PlayerStatusLine.Refresh</c> makes - and not
    /// from the row's own labels, because the panel refreshes itself only when it is shown: the labels
    /// are a picture of the standings as of the last time a mouse rested on the button, which for a
    /// keyboard session is never. The ROWS are still what is walked, so that the lines the player
    /// hears and the rows a sighted player reads are the same list in the same order, and a row the
    /// game is not drawing contributes nothing. Nothing here binds a tooltip: the game hangs none on
    /// this panel, and a carrier of the mod's own would put a panel on the screen the game never
    /// draws (owner ruling 2026-09-14). The lines reach the player as the row's own composed
    /// section, which is the same reading a written tooltip gets - said whole on landing, and once in
    /// the review buffer.
    ///
    /// The picture is <see cref="Hold"/>: the panel is shown the moment focus lands, and then held
    /// against the game's own per-frame hide. The hide is not fought with a re-show - that would cost
    /// a full refresh of every row every frame - but by putting the panel's alpha animation back at
    /// its end and its transform back to drawn, which is three writes and no refresh. After the
    /// game's first hide has run its course the panel leaves the shown-panels list the hide machinery
    /// is driven from (<c>GuiManager.Update</c>), so from then on the game's <c>Hide()</c> finds
    /// <c>Shown</c> false and does nothing at all: the hold settles rather than churning.
    ///
    /// Why not just move the cursor: <c>AgeManager.ForceCursorPosition</c> does keep the panel up -
    /// measured - but it moves the ENGINE's cursor, which makes the End Turn button the active
    /// control, so a physical click anywhere on the screen would end the turn and the player's own
    /// mouse would stop working while the cursor rested here. The mod points at widgets
    /// (<see cref="PointerFocus"/>); it does not take the mouse away.
    /// </summary>
    public static class PlayersList
    {
        private static readonly CompetitorOrbitalSlot[] NoSlots = new CompetitorOrbitalSlot[0];

        /// <summary>The ready ring's slots, walked at most once a frame - the same
        /// <c>GetChildren&lt;CompetitorOrbitalSlot&gt;()</c> the window's own refresh makes
        /// (<c>EndTurnWindow.Refresh</c> :861), and the ring is at most one slot per player.</summary>
        private static readonly FrameSweep<CompetitorOrbitalSlot> RingSlots =
            new FrameSweep<CompetitorOrbitalSlot>("players list");

        private static PlayersListPanel _held;
        private static AgeModifierSet _modifiers;

        /// <summary>The panel the End Turn window keeps its standings on, or null before a game has
        /// been created.</summary>
        public static PlayersListPanel Panel(EndTurnWindow window)
        {
            try
            {
                return window == null ? null : window.PlayersListPanel;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// A line per empire, in the panel's own row order: the name, the score, how the two empires
        /// stand (nothing for your own empire, which the game draws no relation icon for either) and
        /// where that player is in their turn - plus, on the one player the ring is blinking at, that
        /// the whole game is waiting on them (<see cref="WaitedOn"/>).
        ///
        /// Read when the row is read, never per frame.
        /// </summary>
        public static IList<string> Lines(EndTurnWindow window)
        {
            List<string> lines = new List<string>();
            try
            {
                PlayersListPanel panel = Panel(window);
                AgeTransform table = panel == null ? null : panel.PlayersTable;
                IList<AgeTransform> rows = table == null ? null : table.Children;
                Empire looking = Gui.PlayerEmpire;
                Empire waited = WaitedOn(window);
                DepartmentOfForeignAffairs foreign =
                    looking == null ? null : looking.GetAgency<DepartmentOfForeignAffairs>();
                for (int i = 0; rows != null && i < rows.Count; i++)
                {
                    AgeTransform row = rows[i];
                    // Content: the rows the game would draw. A table whose list shrank parks the
                    // spare rows invisible with their last binding still on them, and reading one
                    // would name an empire the panel is not showing.
                    if (row == null || !row.Visible)
                    {
                        continue;
                    }

                    string line = Line(
                        row.GetComponent<PlayerStatusLine>(),
                        looking,
                        foreign,
                        waited
                    );
                    if (!string.IsNullOrEmpty(line))
                    {
                        lines.Add(line);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the players list threw: " + e);
            }

            return lines;
        }

        private static string Line(
            PlayerStatusLine row,
            Empire looking,
            DepartmentOfForeignAffairs foreign,
            Empire waited
        )
        {
            GuiEmpire empire = row == null ? null : row.GuiEmpire;
            if (empire == null || row.Player == null)
            {
                return null;
            }

            string name = Name(empire, looking);
            string score = FloatExtensions.ToString(empire.GetScore());
            string state = StateWord(row.Player);
            string relation = Relation(empire.Empire, looking, foreign);
            string line = string.IsNullOrEmpty(relation)
                ? ModStrings.Format(ModStrings.GalaxyPlayerStanding, name, score, state)
                : ModStrings.Format(
                    ModStrings.GalaxyPlayerStandingWithRelation,
                    name,
                    score,
                    relation,
                    state
                );
            return waited != null && ReferenceEquals(empire.Empire, waited)
                ? line + " " + ModStrings.Get(ModStrings.GalaxyPlayerWaitedOn)
                : line;
        }

        /// <summary>
        /// What to call an empire here: the leader AND the faction, which is the game's own
        /// <c>GetLeaderAndFaction</c> - "Kappa (AI) (Riftborn)", the same string the ready ring hangs on
        /// its slots and <c>DiplomacyScreen</c> (:1326-1343) writes across its headers.
        ///
        /// The row itself draws the faction as a SYMBOL beside a leader-only name
        /// (<c>PlayerStatusLine.Refresh</c> writes <c>GetSymbolString</c>, or "?" for a faction with no
        /// symbol), and a symbol is a picture: the words for it are only in this call. An empire this
        /// one has not met answers the game's own unknown title, with no faction in it at all - there is
        /// no "(?)" to say - and your own empire answers its own name and faction, because the "you"
        /// form is asked for only where the sentence is addressed to the player.
        /// </summary>
        public static string Name(GuiEmpire empire, Empire looking)
        {
            try
            {
                return empire == null
                    ? null
                    : AgeText.Clean(empire.GetLeaderAndFaction(looking, false, false));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The one empire the game is telling everybody it is waiting on, or null when it is waiting on
        /// nobody in particular - which is every single-player game, where there is no ring to blink.
        ///
        /// This is READ off the ring rather than recomputed: <c>EndTurnWindow.Refresh</c> (:859-878)
        /// counts the slots still drawing their unready icon and, when exactly one is left in the two
        /// turn states, starts that slot's blink and resets every other. So a running blink IS the
        /// game's own answer, and a second one running means the game has not settled on anybody.
        /// </summary>
        private static Empire WaitedOn(EndTurnWindow window)
        {
            try
            {
                CompetitorOrbitalSlot[] slots = Slots(window);
                Empire found = null;
                for (int i = 0; i < slots.Length; i++)
                {
                    CompetitorOrbitalSlot slot = slots[i];
                    AgeModifierSet blink = slot == null ? null : slot.BlinkingModifierSet;
                    if (blink == null || !blink.ModifiersRunning || slot.GuiEmpire == null)
                    {
                        continue;
                    }

                    if (found != null)
                    {
                        return null;
                    }

                    found = slot.GuiEmpire.Empire;
                }

                return found;
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the ready ring threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// The ring slots the game is offering a whisper on, in the order it drew them.
        ///
        /// Each slot carries a radial button the game switches on for exactly one case - a human player
        /// who is not you (<c>CompetitorOrbitalSlot.Refresh</c>) - and clicking it focuses the chat
        /// panel with a whisper to that player already typed into it. It is the one thing on the ring a
        /// mouse can do and a keyboard could not, and in a single-player game there is no ring at all,
        /// so this answers empty and the scoreboard row stays a leaf.
        /// </summary>
        public static IList<CompetitorOrbitalSlot> Whisperers(EndTurnWindow window)
        {
            List<CompetitorOrbitalSlot> offered = new List<CompetitorOrbitalSlot>();
            try
            {
                CompetitorOrbitalSlot[] slots = Slots(window);
                for (int i = 0; i < slots.Length; i++)
                {
                    CompetitorOrbitalSlot slot = slots[i];
                    if (slot == null || slot.GuiEmpire == null || slot.Player == null)
                    {
                        continue;
                    }

                    if (AgeWidgets.Operable(AgeWidgets.Transform(slot.SlotButton)))
                    {
                        offered.Add(slot);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: reading the ready ring's whisper buttons threw: " + e);
            }

            return offered;
        }

        /// <summary>The ready ring's slots, or none where the game is not drawing the ring - which is
        /// every single-player game (<c>EndTurnWindow.cs</c> :735 makes the table visible only outside
        /// <c>SessionMode.Single</c>, and only then binds a slot per player).</summary>
        private static CompetitorOrbitalSlot[] Slots(EndTurnWindow window)
        {
            AgeTransform ring = window == null ? null : window.CompetitorsCircularTable;
            // Content read: the slots carry their last binding whether the table is drawn or not, so a
            // solo game would otherwise be read off whatever a previous multiplayer session left on
            // them - and this is also what keeps the sweep out of the frame in the solo case entirely.
            return AgeWidgets.Visible(ring) ? RingSlots.Under(ring) : NoSlots;
        }

        /// <summary>How the two empires stand, in the game's own word for it - and nothing for your
        /// own empire, which is the row the game draws no relation icon on
        /// (<c>PlayerStatusLine.Refresh</c>).</summary>
        private static string Relation(
            Empire empire,
            Empire looking,
            DepartmentOfForeignAffairs foreign
        )
        {
            if (foreign == null || empire == null || ReferenceEquals(empire, looking))
            {
                return null;
            }

            try
            {
                return EmpireDossier.StateWord(foreign.GetDiplomaticRelation(empire));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Where a player is in their turn, by the game's own mapping of the state onto a title
        /// (<c>PlayerStatusLine.Refresh</c>): the two encounter states share one word, and everything
        /// else is named after the state itself.
        ///
        /// The state is READ rather than recomputed. <c>PlayerHelper.ComputePlayerState</c> walks
        /// every running encounter per player, and this figure is asked for whenever the row is read;
        /// what makes the read current is that focus landing here shows the panel, and showing the
        /// panel is what makes the game recompute every row (<see cref="Hold"/>).
        /// </summary>
        private static string StateWord(Player player)
        {
            PlayerState state = player.State;
            string key =
                state == PlayerState.PlayingButInEncounter || state == PlayerState.ReadyButInEncounter
                    ? "%PlayerSyncInEncounterTitle"
                    : "%PlayerSync" + state + "Title";
            return AgeText.Clean(key);
        }

        /// <summary>
        /// Draw the panel, and keep drawing it, until <see cref="Release"/>.
        ///
        /// The show is INSTANT: the game's own show fades the panel in after a one-second hover delay,
        /// which is a delay for a mouse that might be passing through and nothing but a wait for a
        /// cursor that has arrived. <c>Show(instant)</c> is the game's own path for that - it puts the
        /// alpha animation straight at its end - and it refreshes every row on the way, which is what
        /// makes the states the words are read from current.
        /// </summary>
        public static void Hold(EndTurnWindow window)
        {
            PlayersListPanel panel = Panel(window);
            if (panel == null)
            {
                Release();
                return;
            }

            try
            {
                if (!ReferenceEquals(panel, _held))
                {
                    Release();
                    _held = panel;
                    _modifiers = panel.GetComponent<AgeModifierSet>();
                }

                if (panel.Shown)
                {
                    panel.RefreshNow();
                }
                else
                {
                    panel.Show(true);
                }

                Assert();
            }
            catch (Exception e)
            {
                Log.Warn("hud: showing the players list threw: " + e);
                _held = null;
                _modifiers = null;
            }
        }

        /// <summary>Per frame from the hud that holds it, because the End Turn window hides the panel
        /// on every frame the physical cursor is not on its button.</summary>
        public static void Tick()
        {
            if (_held != null)
            {
                Assert();
            }
        }

        /// <summary>Give the panel back to the game, put away the way the game's own instant hide puts
        /// it away.</summary>
        public static void Release()
        {
            PlayersListPanel panel = _held;
            AgeModifierSet modifiers = _modifiers;
            _held = null;
            _modifiers = null;
            if (panel == null)
            {
                return;
            }

            try
            {
                if (modifiers != null)
                {
                    modifiers.ResetModifiers(true, true);
                }

                panel.AgeTransform.Enable = false;
                panel.AgeTransform.Visible = false;
            }
            catch (Exception e)
            {
                Log.Warn("hud: letting go of the players list threw: " + e);
            }
        }

        private static void Assert()
        {
            try
            {
                if (_modifiers != null)
                {
                    // The alpha animation put back at its end: not running, fully drawn, and immune
                    // to the CurrentTime the window winds backwards on every frame the cursor is
                    // elsewhere (:919).
                    _modifiers.ResetModifiers(false, true);
                }

                _held.AgeTransform.Visible = true;
                _held.AgeTransform.Enable = true;
            }
            catch (Exception e)
            {
                Log.Warn("hud: holding the players list drawn threw: " + e);
                _held = null;
                _modifiers = null;
            }
        }
    }
}
