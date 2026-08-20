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
                // Under the name, ahead of the icons that flank it: who is living here, where that is
                // more than one empire.
                AddEmpireBars(lines, label);

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
                // The strip of standing icons, which is also where the label parks the metaplot's
                // "special battle rules apply here" picture (it is a child of that table, so it is
                // read by the walk below rather than by a reader of its own).
                AddTable(lines, label.HomeAndTradingTable);
                AddExplorationWinner(lines, label);
                Say(lines, label.MinorRelationPraiseGroup);
                Say(lines, label.MinorRelationQuestStartedGroup);
                AddMinorRelation(lines, label);
                AddTable(lines, label.HauntCirclesTable);
                AddGarrisons(lines, label);
                AddPirates(lines, label);
                AddAcademy(lines, label);
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
        /// team's own icon - and then the score, which the row draws as a strip of lit gauge parts with
        /// the figure written nowhere on it. The game keeps that figure in the sentence it writes into
        /// the row's OWN tooltip (<c>KingOfTheHillScoreLine.RefreshTooltip</c>), which also says who is
        /// winning and how many turns are left, so the sentence is the reading.</summary>
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
                    AddContent(lines, rows[i]);
                }
            }
        }

        /// <summary>
        /// Who has a colony at a system more than one empire is living in.
        ///
        /// This is the one thing on the label with no words anywhere near it at all: a row of little
        /// bars under the name, each tinted with an empire's colour and carrying no text, no picture
        /// and no tooltip (<c>StarSystemLabel.RefreshEmpireColoredBar</c> :1847-1892 sets a TintColor
        /// and nothing else). So the row is read in two halves - the bars say HOW MANY and the game's
        /// own colony repository says WHO, walked with the same filter and in the same order the
        /// writer uses: the player's own first, then the rest as the repository hands them over.
        ///
        /// The colours themselves cannot be the reading. Every minor civilization in the game is
        /// painted the same grey (measured: twelve of them at 0.627, 0.627, 0.627), so a bar names no
        /// one on its own - which is also why <see cref="EmpireColors"/>, which does name a drawn
        /// colour, is no use here.
        ///
        /// Nothing is said where the row is a single bar, and that is every ordinary system: one
        /// owner is the star's own dossier's business, and an unexplored or unclaimed system draws
        /// one bar in a flat colour that stands for nobody. Nothing is said either where the two
        /// halves disagree about how many there are - the row is then showing something this reader
        /// does not understand, and a guess is worse than silence.
        /// </summary>
        private static void AddEmpireBars(List<string> lines, StarSystemLabel label)
        {
            AgeTransform table = label.EmpireColoredBarsTable;
            if (!AgeWidgets.Visible(table))
            {
                return;
            }

            // The table is pooled and never shrinks: the writer lights the bars it wants and retires
            // the rest by setting their alpha to zero, so alpha is what "drawn" means here.
            int drawn = 0;
            IList<AgeTransform> bars = table.Children;
            for (int i = 0; bars != null && i < bars.Count; i++)
            {
                if (bars[i] != null && bars[i].Alpha > 0f)
                {
                    drawn++;
                }
            }

            if (drawn < 2)
            {
                return;
            }

            IList<string> holders = Holders(label);
            if (holders.Count != drawn)
            {
                return;
            }

            MessageBuilder named = new MessageBuilder();
            for (int i = 0; i < holders.Count; i++)
            {
                named.ListItem(holders[i]);
            }

            Add(lines, ModStrings.Format(ModStrings.GalaxySystemEmpireBars, named.Build()));
        }

        /// <summary>The empires the bar row is drawn from, in the row's own order - the same colonies
        /// <c>RefreshEmpireColoredBar</c> takes a colour from (:1851-1867): the ones that are neither
        /// lost nor a ghost and that the player has seen, with the player's own put in front.</summary>
        private static IList<string> Holders(StarSystemLabel label)
        {
            List<string> names = new List<string>();
            StarSystemNode node = label.StarSystemNode;
            if (node == null)
            {
                return names;
            }

            IColonizedStarSystemRepositoryService repository =
                Amplitude.Unity.Framework.Services.GetService<IColonizedStarSystemRepositoryService>();
            IList<ColonizedStarSystem> here =
                repository == null ? null : repository.GetValuesAsAList(node.NodePosition);
            for (int i = 0; here != null && i < here.Count; i++)
            {
                ColonizedStarSystem colony = here[i];
                if (
                    colony.State == StarSystemState.Lost
                    || colony.State == StarSystemState.Ghost
                    || (int)colony.Visibility[Gui.PlayerEmpire] < 1
                )
                {
                    continue;
                }

                string called = AgeText.Clean(colony.Empire.LocalizedName);
                if (colony.Empire == Gui.PlayerEmpire)
                {
                    names.Insert(0, called);
                }
                else
                {
                    names.Add(called);
                }
            }

            return names;
        }

        /// <summary>
        /// Which empire got to a special node first, in the game's own sentence for it.
        ///
        /// This is the OTHER contest the metaplot runs on a node - a race to discover it, where King
        /// of the Hill is a race to hold it - and the label draws the result as one small badge with
        /// the winner's emblem on it. The sentence naming them
        /// (<c>StarSystemLabelExplorationWinner.Refresh</c>) is written into a tooltip on a piece
        /// INSIDE that badge rather than on the badge itself, so the reading goes through the badge's
        /// own component: the outer transform carries no tooltip at all and a walk of the group would
        /// read silence.
        ///
        /// The badge hides itself while nobody has won yet, so the group being drawn is not enough on
        /// its own - each badge is asked whether it is drawn as well.
        /// </summary>
        private static void AddExplorationWinner(List<string> lines, StarSystemLabel label)
        {
            AgeTransform group = label.ExplorationWinnerGroup;
            if (!AgeWidgets.Visible(group))
            {
                return;
            }

            IList<AgeTransform> badges = group.Children;
            for (int i = 0; badges != null && i < badges.Count; i++)
            {
                StarSystemLabelExplorationWinner badge =
                    badges[i] == null
                        ? null
                        : badges[i].GetComponent<StarSystemLabelExplorationWinner>();
                if (badge == null || !AgeWidgets.Visible(badges[i]))
                {
                    continue;
                }

                AddWords(lines, AgeText.Lines(AgeText.Tooltip(AgeWidgets.Readable(badge.Tooltip))));
            }
        }

        /// <summary>What has been found in the ground here, and whether the system is working it. The
        /// label draws one tinted picture per kind of deposit and nothing else: the kind's name is on
        /// the wrapper behind it, and whether it is being exploited is the LIT-or-faded state the game
        /// paints that picture in (<see cref="Exploited"/>). A deposit whose item the game drew some
        /// other way keeps the bare name rather than being called idle on a guess.</summary>
        private static void AddDeposits(List<string> lines, AgeTransform table)
        {
            if (!AgeWidgets.Visible(table))
            {
                return;
            }

            IList<AgeTransform> items = table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                if (!AgeWidgets.Visible(items[i]))
                {
                    continue;
                }

                string name = AgeWidgets.ItemText(items[i]);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                int exploited = Exploited(items[i]);
                Add(
                    lines,
                    exploited < 0
                        ? name
                        : ModStrings.Format(
                            exploited > 0
                                ? ModStrings.GalaxySystemDepositExploited
                                : ModStrings.GalaxySystemDepositIdle,
                            name
                        )
                );
            }
        }

        /// <summary>Whether the system is working a deposit - read off the same flag the label paints
        /// the picture from (<c>StarSystemLabelDepositItem.Bind</c> sets the image's Enable from
        /// <c>GuiResourceDepositGroup.IsExploited</c>), so the word and the picture cannot disagree.
        /// -1 where the item is not one of those, which is the only honest answer: the game's reasons
        /// for not exploiting a deposit are drawn in a tooltip nobody is hovering.</summary>
        private static int Exploited(AgeTransform item)
        {
            try
            {
                StarSystemLabelDepositItem deposit =
                    item == null ? null : item.GetComponent<StarSystemLabelDepositItem>();
                AgeTransform image =
                    deposit == null || deposit.ResourceImage == null
                        ? null
                        : deposit.ResourceImage.AgeTransform;
                return image == null ? -1 : (image.Enable ? 1 : 0);
            }
            catch (Exception)
            {
                return -1;
            }
        }

        /// <summary>
        /// How many ships are standing at the system, split the way the label splits them: one lozenge
        /// for the player's own side and one for everyone else's
        /// (<c>DualGarrisonsLabelButtons.Bind</c>), each drawn only where that side has any.
        ///
        /// The count phrase for the FLEETS parked here is said on the system itself
        /// (<c>FleetPresence.At</c>) and each fleet is a node of its own, so these two numbers are the
        /// one thing the buttons say that nothing else does - and they are ship counts, not fleet
        /// counts. The buttons themselves are deliberately not offered: their click selects the first
        /// garrison of the side, which is what the per-fleet nodes already do properly.
        /// </summary>
        private static void AddGarrisons(List<string> lines, StarSystemLabel label)
        {
            DualGarrisonsLabelButtons buttons = label.DualGarrisonsButtons;
            if (buttons == null || !AgeWidgets.Visible(buttons.AgeTransform))
            {
                return;
            }

            AddShipCount(
                lines,
                buttons.FriendlyGarrisonsButton,
                ModStrings.GalaxySystemFriendlyShip,
                ModStrings.GalaxySystemFriendlyShips
            );
            AddShipCount(
                lines,
                buttons.HostileGarrisonsButton,
                ModStrings.GalaxySystemHostileShip,
                ModStrings.GalaxySystemHostileShips
            );
        }

        private static void AddShipCount(
            List<string> lines,
            GarrisonsLabelButton button,
            string oneKey,
            string manyKey
        )
        {
            if (button == null || !AgeWidgets.Visible(button.AgeTransform))
            {
                return;
            }

            string drawn = AgeText.Label(button.ShipCountLabel);
            if (string.IsNullOrEmpty(drawn))
            {
                return;
            }

            int count;
            Add(
                lines,
                int.TryParse(drawn, out count)
                    ? ModStrings.Plural(oneKey, manyKey, count)
                    : ModStrings.Format(manyKey, drawn)
            );
        }

        /// <summary>
        /// What the Academy is at a system it has been given: the level it has reached, how far the
        /// next one is, and what it is counting down to.
        ///
        /// The level is a bare number and the progress is a ring with no figure on it at all, so the
        /// sentence around both is the mod's and the ring is read as the proportion it is drawn at. The
        /// countdown is the other way round: the game writes what the number MEANS into the
        /// countdown's own tooltip - a different sentence per state
        /// (<c>AcademyGroup.RefreshTimerLabel</c>) - so its own words are used, with the number it
        /// draws beside them.
        ///
        /// The group's own tooltip is NOT read: the label binds the whole system's dossier onto it
        /// (<c>StarSystemLabel</c> :1777), which the system's star tooltip already carries.
        /// </summary>
        private static void AddAcademy(List<string> lines, StarSystemLabel label)
        {
            AcademyGroup academy = label.AcademyGroup;
            if (academy == null || !AgeWidgets.Visible(academy.AgeTransform))
            {
                return;
            }

            if (AgeWidgets.Visible(academy.AcademyPowerTracker))
            {
                Add(
                    lines,
                    ModStrings.Format(
                        ModStrings.GalaxySystemAcademyLevel,
                        AgeText.Label(academy.AcademyPowerLabel),
                        Percent(academy.AcademyPowerGauge)
                    )
                );
            }

            if (!AgeWidgets.Visible(academy.AcademyRolesCountdown))
            {
                return;
            }

            string says = First(academy.AcademyRolesCountdown);
            if (string.IsNullOrEmpty(says))
            {
                return;
            }

            Add(
                lines,
                new MessageBuilder()
                    .ListItem(says)
                    .ListItem(AgeText.Label(academy.AcademyRolesCountdownLabel))
                    .Build()
            );
        }

        /// <summary>Every line a widget carries in its OWN tooltip, where the words are on the widget.
        /// A class-backed tooltip is left alone: its text only exists while the tooltip window is
        /// drawing it, and nothing on a label is drawn while a buffer is being filled.</summary>
        private static void AddContent(List<string> lines, AgeTransform widget)
        {
            AgeTooltip tooltip = AgeWidgets.Readable(AgeWidgets.Raw(widget));
            IList<string> words =
                tooltip == null ? null : AgeText.Lines(AgeText.Tooltip(tooltip));
            for (int i = 0; words != null && i < words.Count; i++)
            {
                Add(lines, words[i]);
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

        /// <summary>
        /// What one drawn picture on the label says, in the game's own words: the sentence in its
        /// tooltip where the tooltip carries one, the title of the wrapper behind it where the tooltip
        /// is the assembled kind, and - last - the assembled tooltip's OWN content where it carries
        /// some and there is no wrapper to name it from. A picture the game has not drawn says nothing:
        /// its tooltip still holds whatever was written for the last system to need it.
        ///
        /// The third case is not an exception to the rule that an assembled tooltip's content field
        /// holds authoring leftovers (<see cref="AgeWidgets.Readable"/>) - it is the case where the
        /// game filled that field itself and hung something on the tooltip that is NOT a
        /// <c>GuiWrapper</c>, so neither of the first two answers exists. The invasion icon is the one
        /// on this label: <c>StarSystemLabel.RefreshInvasionContextualIcon</c> :731-743 sets Class
        /// "GroundBattle", a <c>GuiGroundBattle</c> target (which is an
        /// <c>IGroundBattleInfoProvider</c>, not a wrapper) and a real sentence in Content, and the
        /// icon read as silence until this last fallback existed. It is tried LAST precisely so that a
        /// picture the wrapper already names keeps the name and never trades it for a leftover.
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
                AddWords(lines, AgeText.Lines(AgeText.Tooltip(tooltip)));
                return;
            }

            string named = AgeWidgets.ItemText(widget);
            if (!string.IsNullOrEmpty(named))
            {
                Add(lines, named);
                return;
            }

            AddWords(lines, AgeText.Lines(AgeText.Tooltip(tooltip)));
        }

        private static void AddWords(List<string> lines, IList<string> words)
        {
            for (int i = 0; words != null && i < words.Count; i++)
            {
                Add(lines, words[i]);
            }
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
