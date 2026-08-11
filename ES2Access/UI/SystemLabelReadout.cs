using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// Everything the map writes on a star system's own label, read off the label itself.
    ///
    /// The label is the game's one-glance summary of a system: how many people live there, what it is
    /// building, what has been found in the ground, and a row of small pictures for everything that
    /// has gone right or wrong there. Almost all of it is drawn as bare numbers and wordless icons,
    /// which is why none of it can be read as text - the words the game would have written live in
    /// each icon's own tooltip, or in the wrapper hung on that tooltip, and that is where every line
    /// here comes from. Nothing is read off the simulation: what the label is not drawing is not said.
    ///
    /// The division follows the mod's usual one. Two SHORT numbers the label puts in front of the
    /// player - the population and the sleeper count - are spoken parts of the system's readout. The
    /// rest is a buffer section in the label's own drawn order, because it is a page of detail and
    /// hearing it on every pass through a hundred systems is exactly what a review buffer exists to
    /// avoid. The clickable ones are child nodes (<see cref="Actions"/>), like any other card's
    /// buttons.
    ///
    /// Cost: every reader here is called only while a system is focused (a spoken part) or while its
    /// buffer is being filled, never in the per-frame walk over the galaxy's systems.
    /// </summary>
    public static class SystemLabelReadout
    {
        /// <summary>How many people live there - the number the label writes beside the star, and the
        /// one thing on it a player scanning their empire reads first.</summary>
        public static string Population(StarSystemLabel label)
        {
            try
            {
                return label == null || !AgeWidgets.Visible(label.PopulationCountGroup)
                    ? null
                    : ModStrings.Format(
                        ModStrings.GalaxySystemPopulation,
                        AgeText.Label(label.PopulationCountLabel)
                    );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>How many of the player's own agents are hidden in that population. The label draws
        /// this only where there is at least one, and the game's word for them - in the sentence it
        /// explains the count with - is "sleepers".</summary>
        public static string Sleepers(StarSystemLabel label)
        {
            try
            {
                return label == null || !AgeWidgets.Visible(label.TraitorCountGroup)
                    ? null
                    : ModStrings.Format(
                        ModStrings.GalaxySystemSleepers,
                        AgeText.Label(label.TraitorCountLabel)
                    );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The label read top to bottom, one line per thing it is drawing: the contextual icons that
        /// flank the name, then the lines down the body of the label in the order the map arranges
        /// them, then what the label hangs underneath it.
        ///
        /// The counts already spoken as parts of the system's readout are not repeated here, and
        /// neither is anything the system's own tooltip already says - the population figure is in
        /// that tooltip, and a buffer that says it twice is worse than one that says it once.
        /// </summary>
        public static IList<string> Lines(StarSystemLabel label)
        {
            List<string> lines = new List<string>();
            if (label == null)
            {
                return lines;
            }

            try
            {
                // Left of the name: what is happening AT the system.
                Say(lines, label.ContextualIconBattle);
                Say(lines, label.ContextualIconPortal);
                Say(lines, label.ContextualIconBlockedFleetPortal);
                Say(lines, label.ContextualIconHonorZone);
                Say(lines, label.ContextualIconWonder);
                Say(lines, label.ContextualIconDetectionProbe);
                Say(lines, label.ContextualIconTemple);
                Say(lines, label.ContextualIconSlumberingRuins);

                // Right of the name, minus the three the game draws there as BUTTONS: those are child
                // nodes of the system and would otherwise be said twice.
                Say(lines, label.ContextualIconBlackout);
                Say(lines, label.ContextualIconSiege);
                Say(lines, label.ContextualIconInvasion);
                Say(lines, label.ContextualIconJuggernautEffects);
                Say(lines, label.GivenToAcademyGroup);

                AddRebellion(lines, label);
                AddConstruction(lines, label);
                AddKingOfTheHill(lines, label);
                AddDeposits(lines, label.DepositsMainTable);
                AddDeposits(lines, label.DepositsSecondaryTable);
                AddTable(lines, label.HomeAndTradingTable);
                Say(lines, label.MinorRelationPraiseGroup);
                Say(lines, label.MinorRelationQuestStartedGroup);
                AddMinorRelation(lines, label);
                AddTable(lines, label.HauntCirclesTable);
                AddPirates(lines, label);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's label threw: " + e);
            }

            return lines;
        }

        /// <summary>
        /// The buttons the label draws on a system, in the order it draws them: the two conversions and
        /// the pirate-mark buy-out among the icons beside the name, then the diplomacy button and the
        /// hacking beacon underneath.
        ///
        /// Every one of them is a wordless icon that the game explains in a sentence rather than names,
        /// so each is called by a phrase of this mod's and the game's sentence arrives with it - these
        /// carry plain tooltips, so the readout speaks the sentence rather than only indicating it.
        /// Each is declared while DRAWN and refuses while the game is refusing it, with the game's own
        /// reason: what a conversion would cost and why it cannot be bought out today is written into
        /// exactly that tooltip.
        ///
        /// Every one of the three buy-outs raises the game's own confirmation box, which speaks through
        /// the message-box screen like every other one; nothing is spent by pressing the button itself.
        /// </summary>
        public static void Actions(List<CardActions.CardAction> found, StarSystemLabel label)
        {
            if (label == null)
            {
                return;
            }

            try
            {
                CardActions.AddRefusable(
                    found,
                    AgeWidgets.Transform(label.PirateMarkBuyoutButton),
                    Mod(ModStrings.GalaxySystemPirateMarkBuyout)
                );
                CardActions.AddRefusable(
                    found,
                    AgeWidgets.Transform(label.PacificConversionButton),
                    Mod(ModStrings.GalaxySystemConversionBuyout)
                );
                CardActions.AddRefusable(
                    found,
                    AgeWidgets.Transform(label.AcademyConversionButton),
                    Mod(ModStrings.GalaxySystemAcademyBuyout)
                );
                // One button for whichever diplomacy this system is: a minor civilization's, the
                // Academy's, or another empire's. The game draws one control for all three and says
                // which in its tooltip, so the node is named once and the sentence tells them apart.
                CardActions.AddRefusable(
                    found,
                    label.DiplomacyButton,
                    Mod(ModStrings.GalaxySystemDiplomacy)
                );
                CardActions.AddRefusable(
                    found,
                    HackingBeacon(label),
                    Mod(ModStrings.GalaxySystemHackingBeacon)
                );
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system label's buttons threw: " + e);
            }
        }

        /// <summary>The beacon button is not a field of the label's: the game makes one and parks it
        /// among the bottom buttons the first time a beacon is charging here.</summary>
        private static AgeTransform HackingBeacon(StarSystemLabel label)
        {
            try
            {
                AgeTransform group = label.BottomButtonsGroup;
                IList<AgeTransform> children = group == null ? null : group.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    if (
                        child != null
                        && child.GetComponent<StarSystemLabelHackingBeaconButton>() != null
                    )
                    {
                        return child;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        /// <summary>How close the system is to rising up, and how long there is to do something about
        /// it. The label draws a ring and a turn count and writes no word for either, so the phrase is
        /// the mod's; the ring is read as the proportion it is drawn at.</summary>
        private static void AddRebellion(List<string> lines, StarSystemLabel label)
        {
            RebellionGroup group = label.RebellionGroup;
            if (group == null || !AgeWidgets.Visible(group.RebellionStatusGroup))
            {
                return;
            }

            Add(
                lines,
                ModStrings.Format(
                    ModStrings.GalaxySystemRebellion,
                    Percent(group.RebellionGauge),
                    AgeText.Label(group.RebellionTimerLabel)
                )
            );
        }

        /// <summary>What the system is building and how long it has left, or the cross the game draws
        /// over the slot when the queue is empty. The label draws the thing as its own picture and its
        /// name lives in the wrapper on the tooltip behind it.</summary>
        private static void AddConstruction(List<string> lines, StarSystemLabel label)
        {
            AgeTransform group = label.QueuedConstructionGroup;
            if (!AgeWidgets.Visible(group))
            {
                return;
            }

            if (
                label.NoConstructionCross != null
                && AgeWidgets.Visible(label.NoConstructionCross.AgeTransform)
            )
            {
                Add(lines, ModStrings.Get(ModStrings.GalaxySystemNothingBuilding));
                return;
            }

            Add(
                lines,
                ModStrings.Format(
                    ModStrings.GalaxySystemBuilding,
                    AgeWidgets.TooltipTitle(AgeWidgets.Raw(group)),
                    AgeText.Label(label.ConstructionTurnsLabel)
                )
            );
        }

        /// <summary>A line per team racing for the system, named by the wrapper the game hangs on the
        /// team's own icon.</summary>
        private static void AddKingOfTheHill(List<string> lines, StarSystemLabel label)
        {
            AgeTransform table = label.KingOfTheHillTable;
            if (!AgeWidgets.Visible(table))
            {
                return;
            }

            IList<AgeTransform> rows = table.Children;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                if (AgeWidgets.Visible(rows[i]))
                {
                    Add(lines, AgeWidgets.ItemText(rows[i]));
                }
            }
        }

        /// <summary>What has been found in the ground here. The label draws one tinted picture per kind
        /// of deposit and nothing else, and the kind's name is on the wrapper behind it.</summary>
        private static void AddDeposits(List<string> lines, AgeTransform table)
        {
            if (!AgeWidgets.Visible(table))
            {
                return;
            }

            IList<AgeTransform> items = table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                if (AgeWidgets.Visible(items[i]))
                {
                    Add(lines, AgeWidgets.ItemText(items[i]));
                }
            }
        }

        /// <summary>How the player stands with the civilization living here - the ring the label draws
        /// around the diplomacy button, read as the proportion it is drawn at.</summary>
        private static void AddMinorRelation(List<string> lines, StarSystemLabel label)
        {
            ProgressCircularDiffGauge gauge = label.MinorRelationGauge;
            if (gauge == null || !AgeWidgets.Visible(gauge.AgeTransform))
            {
                return;
            }

            Add(lines, ModStrings.Format(ModStrings.GalaxySystemMinorRelation, Percent(gauge)));
        }

        /// <summary>What a pirate lair is doing: how long until it sends out its next fleet - the game's
        /// own sentence for the timer, with the timer's own value - and how strong the pirates have
        /// grown.</summary>
        private static void AddPirates(List<string> lines, StarSystemLabel label)
        {
            PirateGroup pirates = label.PirateGroup;
            if (pirates == null || !AgeWidgets.Visible(pirates.AgeTransform))
            {
                return;
            }

            if (AgeWidgets.Visible(pirates.PirateFleetTimerGroup))
            {
                Add(
                    lines,
                    new MessageBuilder()
                        .ListItem(First(pirates.PirateFleetTimerGroup))
                        .ListItem(AgeText.Label(pirates.PirateFleetTimerLabel))
                        .Build()
                );
            }

            if (AgeWidgets.Visible(pirates.PiratePowerGroup))
            {
                Add(
                    lines,
                    ModStrings.Format(
                        ModStrings.GalaxySystemPiratePower,
                        AgeText.Label(pirates.PiratePowerLabel)
                    )
                );
            }
        }

        /// <summary>Every drawn item of a row of icons, in the order the row arranges them - the strip
        /// of standing icons under the name (home system, marketplace, golden age...) and the haunted
        /// planets. Each says whatever it has to say: its own sentence where the game wrote one, the
        /// name off its wrapper where it wrote a picture instead.</summary>
        private static void AddTable(List<string> lines, AgeTransform table)
        {
            if (!AgeWidgets.Visible(table))
            {
                return;
            }

            IList<AgeTransform> items = table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                Say(lines, items[i]);
            }
        }

        /// <summary>What one drawn picture on the label says, in the game's own words: the sentence in
        /// its tooltip where the tooltip carries one, and the title of the wrapper behind it where the
        /// tooltip is the assembled kind and carries none. A picture the game has not drawn says
        /// nothing - its tooltip still holds whatever was written for the last system to need it.
        /// </summary>
        private static void Say(List<string> lines, AgeTransform widget)
        {
            if (!AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            if (AgeWidgets.Readable(tooltip) != null)
            {
                IList<string> words = AgeText.Lines(AgeText.Tooltip(tooltip));
                for (int i = 0; words != null && i < words.Count; i++)
                {
                    Add(lines, words[i]);
                }

                return;
            }

            Add(lines, AgeWidgets.ItemText(widget));
        }

        /// <summary>The first thing a picture's own tooltip says - the whole of it where the game wrote
        /// one sentence, and the opening line where it wrote several.</summary>
        private static string First(AgeTransform widget)
        {
            AgeTooltip tooltip = AgeWidgets.Readable(AgeWidgets.Raw(widget));
            IList<string> words =
                tooltip == null ? null : AgeText.Lines(AgeText.Tooltip(tooltip));
            return words == null || words.Count == 0 ? null : words[0];
        }

        /// <summary>A ring gauge as the proportion it is DRAWN at. The game fills these by sweeping an
        /// angle and keeps no number anywhere on the widget, so the angle is the reading - which is also
        /// exactly what a player sees.</summary>
        private static string Percent(ProgressCircularGauge gauge)
        {
            try
            {
                AgePrimitiveSector main = gauge == null ? null : gauge.MainGauge;
                if (main == null || !main.AgeTransform.Visible)
                {
                    return "0";
                }

                float degrees = main.MaxAngle - main.MinAngle + 2f * gauge.AngularJitter;
                int percent = (int)Math.Round(100.0 * degrees / 360.0);
                if (percent < 0)
                {
                    percent = 0;
                }
                else if (percent > 100)
                {
                    percent = 100;
                }

                return percent.ToString();
            }
            catch (Exception)
            {
                return "0";
            }
        }

        private static Func<string> Mod(string key)
        {
            return () => ModStrings.Get(key);
        }

        private static void Add(List<string> lines, string line)
        {
            if (!string.IsNullOrEmpty(line) && !lines.Contains(line))
            {
                lines.Add(line);
            }
        }
    }
}
