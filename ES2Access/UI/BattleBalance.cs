using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI
{
    /// <summary>
    /// THE BALANCE OF POWER, which the game draws as two arcs and writes down nowhere.
    ///
    /// It is the single most-read thing on a battle surface for a sighted player - it is what tells
    /// them whether to fight or run - and a listener gets nothing at all from a ring. So the mod says
    /// it, in the two numbers the arcs are sized from, and that makes it the one line on these
    /// surfaces with no counterpart on screen (<see cref="DeclareBalance"/> is the owner's lever for
    /// dropping it again).
    ///
    /// Four surfaces draw the same ring - the space setup and report popups, the advanced report
    /// window, and the ground popups' own version of it - and the arithmetic behind it used to live
    /// inside the popup reader while the other three reached in for it. One question, one home.
    ///
    /// The SPACE and GROUND halves ask different models the same thing: military power per side for
    /// a space battle, manpower committed or remaining for a ground one. The ground half also has to
    /// know which of the two indices is the player's, and that is a four-branch answer (attacker,
    /// defender, and a third party watching either) the window has already written down - so it is
    /// read back through reflection rather than worked out again here, because a second copy of that
    /// rule would drift.
    /// </summary>
    internal static class BattleBalance
    {
        // The two arcs of the ground gauge are sized from these, and the window keeps the two indices
        // to itself. Which of the pair is this player's side is a four-branch answer (attacker,
        // defender, and a third party watching either) the game has already written down, so it is
        // read back rather than worked out again here - a second copy of that rule would drift.
        private static readonly PropertyInfo LeftManpowerIndex = ManpowerIndex(
            "LeftEmpireManpowerIndex"
        );

        private static readonly PropertyInfo RightManpowerIndex = ManpowerIndex(
            "RightEmpireManpowerIndex"
        );

        /// <summary>
        /// The balance of power between the two sides, as the two numbers the arcs are sized from.
        ///
        /// The game draws no number here at all, and the ratio is the single most-read thing on the
        /// popup for a sighted player - it is what tells them whether to fight or run. Setup and report
        /// ask the same question of different halves of the model (what is committed vs what survived),
        /// which is the <paramref name="setup"/> flag.
        ///
        /// Internal because the ADVANCED report window draws the same ring over the same two groups
        /// (<see cref="AdvancedBattleReportScreen"/>) - one question, one home.
        /// </summary>
        internal static void Balance(
            GraphBuilder builder,
            AgeTransform group,
            EncounterGroup left,
            EncounterGroup right,
            bool setup,
            string key
        )
        {
            if (!DeclareBalance || left == null || right == null)
            {
                return;
            }

            if (
                OptionalText.Phrase(ModStrings.BattleBalance, string.Empty, 0, string.Empty)
                == null
            )
            {
                return;
            }

            EncounterGroup ours = left;
            EncounterGroup theirs = right;
            bool useSetup = setup;
            AgeTooltip tooltip = AgeWidgets.Raw(group);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => BalanceText(ours, theirs, useSetup)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            if (group != null)
            {
                AgeWidgets.PointAt(vtable, group);
            }
            else
            {
                vtable.OnFocusVisual = AgeWidgets.ReleasePointer;
            }

            // Synthetic: the balance is computed from the battle, which the popup draws only as an
            // arc with no figure on it.
            builder.AddItem(Nodes.Synthetic(ControlId.Structural(key), vtable));
        }

        /// <summary>
        /// Which side the arcs say is stronger, and by how much - phrased from the STRONGER side, in
        /// the two fleets' own names.
        ///
        /// The two military-power figures the game's own helper computes are what the arcs are sized
        /// from, and they are the wrong thing to read out: "Military power 350 against 172" hands the
        /// listener two numbers and the division, while the picture hands a sighted player the answer.
        /// So the line says the answer - who leads and by what percentage - and the numbers stay where
        /// the game put them, which is nowhere. Two equal sides are the player's own first, at 0%; a
        /// side with nothing left is the one comparison a percentage cannot make and has its own
        /// sentence.
        ///
        /// <paramref name="left"/> is the player's side wherever a caller has one, which is what
        /// decides the tie.
        /// </summary>
        internal static string BalanceText(EncounterGroup left, EncounterGroup right, bool setup)
        {
            try
            {
                float ours = GuiBattleHelpers.GetMilitaryPower(left, setup, true);
                float theirs = GuiBattleHelpers.GetMilitaryPower(right, setup, true);
                bool leading = ours >= theirs;
                string strongName = SideName(leading ? left : right);
                string weakName = SideName(leading ? right : left);
                if (string.IsNullOrEmpty(strongName) || string.IsNullOrEmpty(weakName))
                {
                    return null;
                }

                float stronger = leading ? ours : theirs;
                float weaker = leading ? theirs : ours;
                if (weaker <= 0f)
                {
                    return stronger <= 0f
                        ? OptionalText.Phrase(ModStrings.BattleBalance, strongName, 0, weakName)
                        : OptionalText.Phrase(ModStrings.BattleBalanceAll, strongName, weakName);
                }

                return OptionalText.Phrase(
                    ModStrings.BattleBalance,
                    strongName,
                    Mathf.RoundToInt((stronger / weaker - 1f) * 100f),
                    weakName
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// What one side of a battle is called: the name of the first fleet it brought that is not a
        /// reinforcement.
        ///
        /// The same name the roster header draws over that side (<c>BattleGarrisonPanel.Refresh</c>
        /// reads it off the very same garrison), taken from the setup rather than off the drawn panel
        /// so that the cinematic and the advanced-play modal - neither of which draws a roster - name
        /// the two sides the popups' way. Reinforcements are skipped because the game draws them in a
        /// table of their own underneath, and the fleet the side is known by is the one at the top.
        /// </summary>
        private static string SideName(EncounterGroup group)
        {
            try
            {
                EncounterGroupSetup setup = group == null ? null : group.Setup;
                EncounterContender leader = group == null ? null : group.Leader;
                Empire empire = leader == null ? null : leader.Empire;
                if (setup == null || setup.ContenderSetups == null || empire == null)
                {
                    return null;
                }

                for (int i = 0; i < setup.ContenderSetups.Count; i++)
                {
                    EncounterContenderSetup contender = setup.ContenderSetups[i];
                    if (contender == null || contender.ContenderIndex != empire.Index)
                    {
                        continue;
                    }

                    for (int j = 0; j < contender.GarrisonSetups.Count; j++)
                    {
                        EncounterGarrisonSetup garrison = contender.GarrisonSetups[j];
                        if (garrison == null || garrison.Reinforcement)
                        {
                            continue;
                        }

                        string named = AgeText.Clean(garrison.GarrisonLocalizedName.ToString());
                        if (!string.IsNullOrEmpty(named))
                        {
                            return named;
                        }
                    }
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The balance of power on the ground, as the two manpower figures the arcs are sized from.
        ///
        /// The same silence as the space gauge, over a different quantity: the disk between the two
        /// rosters draws one arc per side and no number anywhere, and it is the first thing a sighted
        /// player reads off this popup. The figures are the game's own, taken from the very expression
        /// that sizes the arcs, so the line and the picture can never disagree.
        /// </summary>
        internal static void GroundBalance(
            GraphBuilder builder,
            GroundBattleNotificationWindow window,
            GroundBattle battle,
            bool setup,
            string key
        )
        {
            if (!DeclareBalance || window == null || battle == null)
            {
                return;
            }

            if (OptionalText.Phrase(ModStrings.BattleGroundBalance, 0, 0) == null)
            {
                return;
            }

            AgeTransform group =
                window.BattlePowerGauge == null ? null : window.BattlePowerGauge.AgeTransform;
            // Synthetic guard: the line is composed from the battle rather than read off the gauge, so
            // it declares no evidence and the gate has nothing to ask.
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            GroundBattleNotificationWindow at = window;
            GroundBattle it = battle;
            bool useSetup = setup;
            if (GroundBalanceText(at, it, useSetup) == null)
            {
                return;
            }

            AgeTooltip tooltip = AgeWidgets.Raw(group);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => GroundBalanceText(at, it, useSetup)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, group);
            // Synthetic: the two manpower figures come out of the battle, not off the gauge - the
            // gauge is what the line is anchored to.
            builder.AddItem(Nodes.Synthetic(ControlId.Structural(key), vtable));
        }

        /// <summary>
        /// The two manpower figures the ground gauge is drawn from, in the player's own order - theirs
        /// on the right whichever side of the invasion they are on.
        ///
        /// The report reads the FINAL manpowers even where the gauge does not: a defender who
        /// surrendered leaves the game drawing a symbolic full arc against an empty one rather than the
        /// two figures, and the numbers are what the line is for.
        /// </summary>
        internal static string GroundBalanceText(
            GroundBattleNotificationWindow window,
            GroundBattle battle,
            bool setup
        )
        {
            float[] powers = setup ? CommittedManpower(battle) : RemainingManpower(battle);
            if (powers == null)
            {
                return null;
            }

            int ours = Manpower(window, powers, LeftManpowerIndex);
            int theirs = Manpower(window, powers, RightManpowerIndex);
            return ours < 0 || theirs < 0
                ? null
                : OptionalText.Phrase(ModStrings.BattleGroundBalance, ours, theirs);
        }

        /// <summary>What each side committed to the invasion - the setup gauge's own figures.</summary>
        private static float[] CommittedManpower(GroundBattle battle)
        {
            try
            {
                return battle.SpawnReport == null
                    ? null
                    : battle.SpawnReport.OpponentInitManPowers;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What each side has left - the report gauge's own figures, and only once the game
        /// says the report it computes them from is finished.</summary>
        private static float[] RemainingManpower(GroundBattle battle)
        {
            try
            {
                GroundBattleBattleReport report = battle.BattleReport;
                return report == null || !report.IsValid
                    ? null
                    : report.OpponentFinalManPowers;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>One side's manpower, rounded the way the gauge rounds it, or -1 where the window
        /// will not say which side that is.</summary>
        private static int Manpower(
            GroundBattleNotificationWindow window,
            float[] powers,
            PropertyInfo index
        )
        {
            if (index == null)
            {
                return -1;
            }

            try
            {
                int side = (int)index.GetValue(window, null);
                return side < 0 || side >= powers.Length
                    ? -1
                    : Mathf.RoundToInt(Mathf.Round(powers[side]));
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private static PropertyInfo ManpowerIndex(string name)
        {
            return GameHandlers.Property(typeof(GroundBattleNotificationWindow), name);
        }

        /// <summary>Whether the balance of power is declared as a node of its own.
        ///
        /// It is a number the game draws NOWHERE - two arcs, one per side, sized by military power - so
        /// a sighted player reads the ratio and a listener gets nothing at all. Declaring it is
        /// therefore new information rather than a re-reading; the cost is that it is the one line here
        /// with no counterpart on screen. OWNER CALL: set false to leave it out.</summary>
        private const bool DeclareBalance = true;

    }
}
