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
    /// player - the population and the sleeper count - are spoken parts of the system's readout. Every
    /// PICTURE on the label is a child node of the system's row (<see cref="Icons"/>), because each of
    /// them explains itself in a tooltip of its own and a node carries exactly one tooltip: folding a
    /// dozen of them into one buffer said all their words and offered none of their explanations. What
    /// stays in the buffer is what the label draws as a NUMBER or a bar rather than as a picture - a
    /// gauge read as the proportion it is drawn at, a count off a lozenge - which has no tooltip of its
    /// own to raise and nowhere else to go. The clickable pictures are child nodes as well
    /// (<see cref="Actions"/>), like any other card's buttons.
    ///
    /// Cost: every reader here is called only while a system is focused (a spoken part) or while its
    /// buffer is being filled, never in the per-frame walk over the galaxy's systems.
    ///
    /// Every visibility test in this file is a CONTENT test and none of them decides whether a node
    /// exists: nothing here calls <c>AddItem</c> or <c>Cells.Add</c>. They answer "did the label draw
    /// this line, so should the reading say it" - the label leaves the previous binding's words in a
    /// row it has switched off, so "what is drawn" is the only honest source. The clickable pieces are
    /// gated where they become nodes instead (<see cref="CardActions.AddRefusable"/>).
    /// </summary>
    public static class SystemLabelReadout
    {
        /// <summary>The buttons a drawn group wires, swept once per group per frame. A system label is
        /// POOLED - the map rebinds it to whichever star it is drawing - so the sweep is kept for the
        /// frame and no longer.</summary>
        private static readonly FrameSweep<AgeControlButton> GroupButtons =
            new FrameSweep<AgeControlButton>("galaxy");

        /// <summary>
        /// Which of a system row's named regions a picture on this label belongs in.
        ///
        /// The label draws everything it has to say in one strip of icons round a name, and the
        /// player walking it wants the four different questions apart: what is happening here now,
        /// what I can DO here, what is in the ground, and what the place permanently is. Only the
        /// walk can sort them, because the answer is a fact about the WIDGET - which prefab field
        /// the picture was read off - and never about the words on the tooltip behind it, which say
        /// what the game is explaining rather than what kind of thing it is explaining.
        ///
        /// The row's other three regions (planets, star lanes, fleets) hold no dossiers, so they are
        /// not named here: they are whole families of rows the tree builds elsewhere.
        /// </summary>
        public enum Region
        {
            /// <summary>What is happening AT the system now - a battle, a siege, an invasion, a
            /// rebellion building, a race being run for the place.</summary>
            Status,

            /// <summary>A door: the game wired a click on this very picture, so the node is a button
            /// and pressing it goes somewhere.</summary>
            Actions,

            /// <summary>What has been found in the ground here.</summary>
            Resources,

            /// <summary>What the place permanently IS - a home system, a wonder, a temple, a portal,
            /// the seat of a trading company.</summary>
            Details,
        }

        /// <summary>How many people live there - the number the label writes beside the star, and the
        /// one thing on it a player scanning their empire reads first.</summary>
        public static string Population(StarSystemLabel label)
        {
            try
            {
                string drawn =
                    label == null
                        ? null
                        : AgeWidgets.DrawnLabel(
                            label.PopulationCountGroup,
                            label.PopulationCountLabel
                        );
                return drawn == null
                    ? null
                    : ModStrings.Format(ModStrings.GalaxySystemPopulation, drawn);
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
                string drawn =
                    label == null
                        ? null
                        : AgeWidgets.DrawnLabel(label.TraitorCountGroup, label.TraitorCountLabel);
                return drawn == null
                    ? null
                    : ModStrings.Format(ModStrings.GalaxySystemSleepers, drawn);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// What the label draws as a figure rather than as a picture, in its own top-to-bottom order.
        ///
        /// Everything with a tooltip behind it has left this buffer and become a child node
        /// (<see cref="Icons"/>). What is left is the label's wordless arithmetic: the row of empire
        /// bars, the two relation pictures the label hangs by the population line, the rings read as
        /// the proportion they are drawn at, and the counts the garrison lozenges and the pirate and
        /// academy groups write as bare numbers.
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
                // Under the name: who is living here, where that is more than one empire.
                AddEmpireBars(lines, label);

                // How close the place is to rising up, and how long there is to do something about it -
                // an angle and a turn count, neither of them written anywhere as words. The two
                // SENTENCES the label hangs beside them are children of the row (<see cref="Icons"/>).
                Add(lines, Rebellion(label));

                // The two pictures the label parks on the population line rather than in the strip
                // below. They carry the game's own sentence and nothing else, so they say it here
                // rather than costing the player two stops that repeat one line each.
                Say(lines, label.MinorRelationPraiseGroup);
                Say(lines, label.MinorRelationQuestStartedGroup);
                AddMinorRelation(lines, label);
                AddGarrisons(lines, label);
                AddAcademy(lines, label);
                // The pirate LAIR's figures are not here: the game draws them on a control it lets the
                // player click, so the lair is a button child of the system and says its own numbers
                // (<see cref="Actions"/>). One source per fact. The Academy is NOT a button (owner
                // ruling 2026-09-02) and its figures stay lines of this readout.
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's label threw: " + e);
            }

            return lines;
        }

        /// <summary>
        /// The buttons the label draws on a system, in the order it draws them: the two conversions and
        /// the pirate-mark buy-out among the icons beside the name, then the bottom-button row - the
        /// diplomacy button and the pirate lair (measured: <c>BottomButtons</c> lays its children out
        /// in exactly that order) - and the hacking beacon the game parks there last. The Academy's
        /// group is drawn in that row too and is NOT a button (owner ruling 2026-09-02): it is read as
        /// text by <see cref="AddAcademy"/>.
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
                //
                // KEPT WHILE FADED, and measured rather than reasoned (2026-08-27, Osulo - the fixture's
                // one minor-civilization system - stepped through all thirteen camera rungs with the
                // label drawn at every one of them): the prefab leaves this button Visible and fades it
                // to alpha 0, settled, at rungs 0, 1 and 12, and holds it at alpha 1 at rungs 2 to 11.
                // So the chain test took the node away at BOTH ends of the ladder - and rung 12 is the
                // one the map parks at coming into a system and the one the fixture's own galaxy
                // overview sits on. Measured on the graph: 8 children under the system at rung 6 with
                // "Diplomacy, button, 1 of 8" among them, 7 and no Diplomacy at rungs 0 and 12. Pressing
                // it does not need it drawn - the press is the control's own handler, sent directly
                // (<see cref="AgeWidgets.Press"/>) - so the route stays real wherever the camera is.
                CardActions.AddRefusable(
                    found,
                    label.DiplomacyButton,
                    Mod(ModStrings.GalaxySystemDiplomacy),
                    true
                );
                AddPirateLair(found, label);
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

        /// <summary>
        /// THE PIRATE LAIR IS A BUTTON (owner ruling 2026-09-02), and what it is doing is its own
        /// announcement rather than three lines of the system's readout.
        ///
        /// The game draws the lair as a group in the label's bottom-button row and wires a click on the
        /// icon inside it (<c>PirateLairIconGroup</c> → <c>StarSystemLabel.OnClickDiplomacyButton</c>
        /// :3138, which opens <c>PirateDiplomacyModalWindow</c> for a system the pirates hold :3155-3159
        /// - measured on the prefab, all 86 labels, and the group is drawn exactly while
        /// <c>CanShowPirateLairGroup</c> :573 says the colony here IS a lair, so the branch the click
        /// takes is never in doubt).
        ///
        /// The NODE is the clickable child and not the group: the group's own tooltip is the whole
        /// system's dossier (<c>BindLabelTooltip</c> :1760), which the system's star already carries,
        /// and the child carries none at all - so the button says its own words and repeats nothing.
        /// Availability rides the game's own switch, which sits on the GROUP
        /// (<c>PirateGroup.Refresh</c> :65 - the pirate content shared, and this empire not a pirate
        /// hater) and is reached by the ancestor walk <see cref="AgeWidgets.Offered"/> already makes.
        /// </summary>
        private static void AddPirateLair(List<CardActions.CardAction> found, StarSystemLabel label)
        {
            PirateGroup pirates = label.PirateGroup;
            AgeTransform click = pirates == null ? null : Clickable(pirates.AgeTransform);
            if (click == null)
            {
                return;
            }

            PirateGroup it = pirates;
            CardActions.AddRefusable(
                found,
                click,
                Mod(ModStrings.GalaxySystemPirateLair),
                true,
                () => PirateReading(it)
            );
        }

        /// <summary>What the lair is doing, as the button's own value: how long until it sends its next
        /// fleet - the game's own sentence for the timer, with the timer's own reading, which is "-"
        /// where the lair is already holding as many fleets as it may - and how strong the pirates have
        /// grown, with how far they are through the level they are on (the gauge's angle, which is the
        /// only place that progress is drawn).</summary>
        private static string PirateReading(PirateGroup pirates)
        {
            try
            {
                MessageBuilder said = new MessageBuilder();
                if (AgeWidgets.Visible(pirates.PirateFleetTimerGroup))
                {
                    // A LABEL FALLBACK: the countdown group draws a bare number and the game wrote
                    // no caption anywhere on the label, so the tooltip's opening sentence is the
                    // only word that says what the number counts. The tooltip's own words still
                    // reach the player through the door, on the icon's own node.
                    said.ListItem(
                            CardActions.FirstLine(AgeWidgets.Raw(pirates.PirateFleetTimerGroup))
                        )
                        .ListItem(AgeText.Label(pirates.PirateFleetTimerLabel));
                }

                string power = AgeWidgets.DrawnLabel(
                    pirates.PiratePowerGroup,
                    pirates.PiratePowerLabel
                );
                if (power != null)
                {
                    said.ListItem(
                        ModStrings.Format(
                            ModStrings.GalaxySystemPiratePower,
                            power,
                            Percent(pirates.PiratePowerGauge)
                        )
                    );
                }

                return said.Build();
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's pirate lair threw: " + e);
                return null;
            }
        }

        /// <summary>The transform of the one control a drawn GROUP wires a click on - asked of the
        /// control the prefab really carries, never of a name, so a group whose clickable piece is
        /// re-cut needs nothing here. Null where the group wires none.</summary>
        private static AgeTransform Clickable(AgeTransform group)
        {
            try
            {
                AgeControlButton[] buttons = GroupButtons.Under(group);
                for (int i = 0; i < buttons.Length; i++)
                {
                    if (!string.IsNullOrEmpty(buttons[i].OnActivateMethod))
                    {
                        return buttons[i].AgeTransform;
                    }
                }
            }
            catch (Exception) { }

            return null;
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

        /// <summary>
        /// The pictures the label draws ABOVE its deposit strip, one child node each, in the order the
        /// label lays them out: the icons that flank the name, the two that sit on the population line,
        /// and the construction queue.
        ///
        /// Split in two around the deposits because the deposit children are not read off this label
        /// at all - they come from the planets, so that a system's deposits stay reachable from a
        /// camera the label's own strip is too far out to draw (<c>GalaxyHudScreen.AddDeposits</c>) -
        /// and the two halves put them back where the label draws them.
        /// </summary>
        public static void IconsAboveDeposits(
            List<TooltipChildren.Dossier> found,
            StarSystemLabel label
        )
        {
            if (found == null || label == null)
            {
                return;
            }

            try
            {
                // Left of the name, and the two questions the one strip answers side by side: what is
                // happening AT the system, and what the place standingly IS.
                Picture(found, label.ContextualIconBattle, Region.Status);
                Picture(found, label.ContextualIconPortal, Region.Details);
                Picture(found, label.ContextualIconBlockedFleetPortal, Region.Status);
                Picture(found, label.ContextualIconHonorZone, Region.Details);
                Picture(found, label.ContextualIconWonder, Region.Details);
                Picture(found, label.ContextualIconDetectionProbe, Region.Details);
                Picture(found, label.ContextualIconTemple, Region.Details);
                Picture(found, label.ContextualIconSlumberingRuins, Region.Details);

                // Right of the name, minus the three the game draws there as BUTTONS: those are child
                // nodes of the system already (<see cref="Actions"/>).
                Picture(found, label.ContextualIconBlackout, Region.Status);
                Picture(found, label.ContextualIconSiege, Region.Status);
                Picture(found, label.ContextualIconInvasion, Region.Status);
                Picture(found, label.ContextualIconJuggernautEffects, Region.Status);
                Picture(found, label.GivenToAcademyGroup, Region.Status);

                // The population line: how many of the people here are the player's own agents. The
                // COUNT is spoken with the system's readout; this is the sentence the game explains it
                // with, which is on the picture and reaches nobody without a node to stand on.
                StarSystemLabel drawn = label;
                Picture(found, label.TraitorCountGroup, Region.Status, () => Sleepers(drawn));

                // The rebellion's two hover targets - the ring and the countdown - each with the
                // sentence the game wrote for it. The numbers are the row's own buffer line
                // (<see cref="Rebellion"/>).
                RebellionGroup rebellion = label.RebellionGroup;
                // Flow control: the group is what the game switches on and off, and whether its two
                // pictures are walked at all is that one answer rather than two.
                if (rebellion != null && AgeWidgets.Visible(rebellion.RebellionStatusGroup))
                {
                    Picture(
                        found,
                        rebellion.RebellionGauge == null
                            ? null
                            : rebellion.RebellionGauge.AgeTransform,
                        Region.Status
                    );
                    Picture(
                        found,
                        rebellion.RebellionTimerLabel == null
                            ? null
                            : rebellion.RebellionTimerLabel.AgeTransform,
                        Region.Status
                    );
                }

                // What the system is building. The picture's own dossier is the constructible's, which
                // is the thing this node exists to hand over: the label promises that dossier and,
                // until this node existed, never offered it to anybody without a mouse.
                //
                // AND IT IS THE SYSTEM'S ONE DOOR INTO ITS OWN PAGE where the label is drawing it
                // (owner ruling 2026-09-02): the slot's click is the very handler the name-line button
                // sends (<c>StarSystemLabel.OnRequestManagementView</c> :3002), so the row offers this
                // node and not a second "Manage system" beside it - which is why it belongs to the
                // ACTIONS region and takes a key that says what it is rather than where it fell in the
                // walk (<c>GalaxyHudScreen.AddInside</c> reads both).
                int queue = found.Count;
                Picture(found, label.QueuedConstructionGroup, Region.Actions, () => Building(drawn));
                Keyed(found, queue, QueueKey);

                // A line per team racing for the system, named by the wrapper the game hangs on the
                // team's own icon. The score is a strip of lit gauge parts with the figure written
                // nowhere on it, and the game keeps that figure in the row's own tooltip
                // (<c>KingOfTheHillScoreLine.RefreshTooltip</c>) along with who is winning and how many
                // turns are left - so the row's sentence is the whole reading.
                Table(found, label.KingOfTheHillTable, Region.Status);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system label's icons threw: " + e);
            }
        }

        /// <summary>The pictures the label draws BELOW its deposit strip, one child node each: the
        /// strip of standing icons (which is also where the label parks the metaplot's "special battle
        /// rules apply here" picture and the latent hacking beacon), the exploration-winner badges, and
        /// the haunted planets.</summary>
        public static void IconsBelowDeposits(
            List<TooltipChildren.Dossier> found,
            StarSystemLabel label
        )
        {
            if (found == null || label == null)
            {
                return;
            }

            try
            {
                HomeAndTrading(found, label);
                ExplorationWinners(found, label);
                Table(found, label.HauntCirclesTable, Region.Details);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system label's icons threw: " + e);
            }
        }

        /// <summary>
        /// One drawn picture as a child node of the system's row - the single door every icon on this
        /// label comes through.
        ///
        /// Three kinds of tooltip hang on these pictures and each needs a different answer, which is
        /// why they are tried in this order: a dossier the renderer assembles from a wrapper (the
        /// construction queue's constructible, a deposit's resource group), named off that wrapper; the
        /// single sentence the game wrote into the picture's own tooltip, named off the sentence; and
        /// last the one that is neither - a renderer-assembled tooltip whose target is not a wrapper
        /// and whose Content the game filled in itself (the invasion icon's ground battle), which has
        /// no name anywhere but its own first line.
        ///
        /// A picture the game has not drawn is not collected at all: its tooltip still holds whatever
        /// was written for the last system to need it. One the game IS drawing but has left nothing in
        /// is not collected either - a node for it would be a stop with nothing to say.
        ///
        /// Drawn means PAINTED, not merely visible: from the orbital band the map keeps the whole
        /// nameplate bound and fades its lines to nothing (measured - every line of every label sits at
        /// alpha 0 at camera step 12, and at alpha 1 from step 11 out), so a visibility test alone
        /// would offer the player a dozen pictures the map has replaced with orbital cards, each of
        /// them pointing at a widget no tooltip can be drawn from.
        ///
        /// <paramref name="name"/> is for a picture this mod has its own words for, which outrank the
        /// naming ladder while they answer: the queue's "Building X, N turns" is what the label draws
        /// AROUND the picture, and the ladder can only ever reach what is on it.
        ///
        /// EVERY WAY IN NAMES A REGION (<see cref="Region"/>). There is deliberately no overload that
        /// leaves it out: an entry nobody stamped belongs to no region, and a region is what the row's
        /// emit reads to find its nodes - so a picture collected without one would be silently dropped
        /// rather than misfiled, which is the failure a walk cannot see.
        /// </summary>
        private static void Picture(
            List<TooltipChildren.Dossier> found,
            AgeTransform widget,
            Region region
        )
        {
            Picture(found, widget, region, null);
        }

        private static void Picture(
            List<TooltipChildren.Dossier> found,
            AgeTransform widget,
            Region region,
            Func<string> name
        )
        {
            int at = found.Count;
            Picture(found, widget, name);
            In(found, at, region);
        }

        /// <summary>The structural key the construction slot's node takes under its system - a name
        /// for the thing rather than its place in the walk, because it is the row's door into the
        /// system's page and a door has to keep its key across the icons appearing and disappearing
        /// beside it.</summary>
        public const string QueueKey = "queue";

        /// <summary>Stamp the region of the system's row on every entry a walk step just collected -
        /// the one place a dossier is told which of the row's named blocks it belongs in, and public
        /// because two of those blocks are filled from outside this file (the star's own dossier and
        /// the deposits, both read off the galaxy model rather than off the label).</summary>
        public static void In(List<TooltipChildren.Dossier> found, int from, Region region)
        {
            for (int i = from; i < found.Count; i++)
            {
                TooltipChildren.Dossier entry = found[i];
                entry.Region = region;
                found[i] = entry;
            }
        }

        /// <summary>Give the entries the last step collected a structural key of their own.</summary>
        private static void Keyed(List<TooltipChildren.Dossier> found, int from, string key)
        {
            for (int i = from; i < found.Count; i++)
            {
                TooltipChildren.Dossier entry = found[i];
                entry.Key = key;
                found[i] = entry;
            }
        }

        /// <summary>
        /// The strip of standing icons under the deposits, sorted into the two regions it draws into
        /// one line: what is being DONE to the place - it is decaying, and the metaplot's battle rules
        /// are in force here - against what the place permanently is, a home system or a trading
        /// company's seat.
        ///
        /// Matched against the label's own fields rather than against anything the pictures say
        /// (<see cref="Region"/>), and by ancestry rather than by identity, because the strip's item
        /// is whatever the prefab wrapped the group in. Anything the prefab adds to the line that
        /// neither field names reads as part of the place, which is what the rest of the line is - the
        /// latent hacking beacon parked here is exactly that.
        /// </summary>
        private static void HomeAndTrading(
            List<TooltipChildren.Dossier> found,
            StarSystemLabel label
        )
        {
            AgeTransform table = label.HomeAndTradingTable;
            // Flow control, exactly as in <see cref="Table"/>: the line is what the game switches on
            // and off, and a switched-off one still holds the last system's icons - so whether its
            // items are walked at all is that one answer rather than one per item.
            if (!AgeWidgets.Visible(table))
            {
                return;
            }

            IList<AgeTransform> items = table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AgeTransform item = items[i];
                bool happening =
                    AgeWidgets.Under(label.DecayingSystemGroup, item)
                    || AgeWidgets.Under(label.MetaplotBattleRulesGroup, item);
                Picture(found, item, happening ? Region.Status : Region.Details);
            }
        }

        /// <summary>
        /// The pictures a SCAN label draws round a star's name, one child node each - the same one
        /// door the ordinary map label's icons go through (<see cref="Picture"/>), so they are named,
        /// aimed and deduped by the same rules.
        ///
        /// Two of them on an install without the hacking content: the blackout mark and the
        /// best-system star, which are the only icons the scan prefab wires that carry words of their
        /// own. The hacking icons beside them are a DLC gate the game switches off outright for a
        /// session without it, and the waypoint and starting-point marks belong to an operation being
        /// plotted; both wait for a fixture that has them (roadmap).
        /// </summary>
        public static void ScanIcons(List<TooltipChildren.Dossier> found, ScanNodeLabel label)
        {
            if (found == null || label == null)
            {
                return;
            }

            try
            {
                Picture(found, label.BlackoutIcon, Region.Status);
                Picture(found, label.BestSystemIcon, Region.Status);
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading a lens label's icons threw: " + e);
            }
        }

        private static void Picture(
            List<TooltipChildren.Dossier> found,
            AgeTransform widget,
            Func<string> name
        )
        {
            // Flow control, and the whole of the collection's own existence test: a picture the label
            // is not showing contributes NO entry, and an entry that was never collected is not a node
            // the gate could have dropped. It is also a content read - which tooltip a pooled strip
            // item is holding depends on it being this system's.
            //
            // BOUND, not painted (owner ruling 2026-09-01, option (a) re-ratified). The map fades the
            // whole nameplate away at the orbital view - the population line, the trading line and the
            // deposits line all sit at alpha 0 at spoken level 13 while staying bound to the system -
            // and a painted test therefore took a system's queue, its deposits and every one of its
            // icon children away exactly where the player has come in to look at them. The ANCESTOR
            // alpha is what that animation moves; a pooled strip item the game has retired sets its
            // OWN alpha to nought, and that is still the test, so a leftover holding the previous
            // system's tooltip is kept out as it always was.
            if (!AgeWidgets.Visible(widget) || widget.Alpha <= 0f)
            {
                return;
            }

            int at = found.Count;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            if (AgeWidgets.Draws(tooltip))
            {
                TooltipChildren.Add(found, tooltip, widget);
                if (found.Count == at)
                {
                    TooltipChildren.AddPlain(found, tooltip, widget);
                }

                if (found.Count == at)
                {
                    Assembled(found, tooltip, widget);
                }
            }

            Unvouched(found, at);
            if (name != null)
            {
                if (found.Count > at)
                {
                    Rename(found, at, name);
                }
                else
                {
                    // A picture with words of the mod's own and no tooltip behind them at all - the
                    // cross the game draws over an empty construction slot. The row would otherwise
                    // lose the one thing the label is saying there.
                    found.Add(
                        new TooltipChildren.Dossier
                        {
                            Name = name,
                            Anchor = widget,
                        }
                    );
                }
            }

            Clickable(found, at, widget);
        }

        /// <summary>
        /// WHERE THE GAME WIRED A CLICK ON THE PICTURE, the child node is a BUTTON (owner ruling
        /// 2026-09-02).
        ///
        /// Several of this label's "decorative" pictures are controls: the construction slot opens the
        /// system's management page (<c>StarSystemLabel.OnRequestManagementView</c> :3002, the same
        /// handler the name-line button sends - so the queue and <c>AddManagementView</c> are two of
        /// the game's own doors into one page), the battle mark and the invasion mark each open their
        /// own notification (<c>OnClickBattleIcon</c> :3007, <c>OnClickGroundBattleIcon</c> :3024).
        /// Their tooltips say so out loud - the empty slot's is "Click to queue up a Construction on
        /// this system" (:2044-2049) - which was being read to a player who had no way to do it.
        ///
        /// Asked of the CONTROL and never of a list of widget names: whatever the prefab wires is
        /// pressable and whatever it does not is not, so a picture that becomes clickable in a patch
        /// needs nothing here. Every entry the walk collected from this picture is marked, because
        /// they are all readings OF that one clickable picture.
        /// </summary>
        private static void Clickable(
            List<TooltipChildren.Dossier> found,
            int from,
            AgeTransform widget
        )
        {
            AgeControlButton click = AgeWidgets.Button(widget);
            if (click == null || string.IsNullOrEmpty(click.OnActivateMethod))
            {
                return;
            }

            for (int i = from; i < found.Count; i++)
            {
                TooltipChildren.Dossier entry = found[i];
                entry.Clicks = click;
                found[i] = entry;
            }
        }

        /// <summary>
        /// Take the CARRIER off the entries this walk has just collected, so they stand on the walk's
        /// own existence test and the central gate has nothing to ask
        /// (<c>TooltipChildren.Stands</c> declares a carrier-less dossier
        /// <see cref="Nodes.Synthetic"/>, the same shape <c>AddRevealed</c> uses for a
        /// reveal-on-hover strip).
        ///
        /// The gate asks the RENDERER, and the renderer's answer here is the wrong one: at the orbital
        /// view the map fades the whole nameplate to nothing while leaving it bound to the system, so
        /// every picture on it reads "not drawn" exactly where the player has come in to read the
        /// place (measured: the gate dropped the construction queue and the home icon at spoken level
        /// 13, "ancestor faded to nothing and settled"). Existence is ruled to follow the BOUND label
        /// rather than the paint (owner ruling 2026-09-01, option (a)), and the test above is what
        /// says so - it is a per-widget question the gate's ancestor walk cannot express.
        ///
        /// The POINTER is untouched: the node still aims at the picture, so the game draws the dossier
        /// exactly where a mouse would raise it.
        /// </summary>
        private static void Unvouched(List<TooltipChildren.Dossier> found, int from)
        {
            for (int i = from; i < found.Count; i++)
            {
                TooltipChildren.Dossier entry = found[i];
                entry.Carrier = null;
                found[i] = entry;
            }
        }

        /// <summary>The third kind: a tooltip the renderer assembles whose target is not a wrapper, so
        /// nothing names it but the sentence the game wrote into it. Deduped against the entries
        /// already collected, because the two doors above dedupe and this one has to as well - the game
        /// clones one tooltip across several pieces of a label.</summary>
        private static void Assembled(
            List<TooltipChildren.Dossier> found,
            AgeTooltip tooltip,
            AgeTransform widget
        )
        {
            for (int i = 0; i < found.Count; i++)
            {
                if (AgeWidgets.SameTooltip(found[i].Tooltip, tooltip))
                {
                    return;
                }
            }

            AgeTooltip it = tooltip;
            // A RE-COMPOSED READER, and only for the NAME: the words themselves still reach the player
            // through the door, as the node's own sections, in whatever loudness the tooltip's kind
            // says. Read with the readable-only gate OFF, because this door's whole subject is a
            // tooltip that names a renderer and yet carries a written sentence - the plain reading
            // would answer nothing here and the entry would go unnamed.
            if (string.IsNullOrEmpty(CardActions.FirstLine(it, false)))
            {
                return;
            }

            found.Add(
                new TooltipChildren.Dossier
                {
                    // The same re-composed NAME the gate above tested for, resolved at speak time so
                    // an entry renamed by the game is renamed here too.
                    Name = () => CardActions.FirstLine(it, false),
                    Tooltip = it,
                    Anchor = widget,
                    Carrier = widget,
                }
            );
        }

        /// <summary>Give the entry just collected this mod's own words for the picture, keeping the
        /// naming ladder as the answer for the moment they run out - and taking whatever they say out
        /// of the entry's own sections, so the dossier does not repeat the name it was just called by.
        ///
        /// The ladder itself is deliberately left in place: an entry's <c>Rungs</c> are what its
        /// SIBLINGS read to find out whether they answer to the same word, and an entry whose rungs are
        /// dropped is read back through its own name - which is this wrapper, reading the set again.
        /// </summary>
        private static void Rename(
            List<TooltipChildren.Dossier> found,
            int at,
            Func<string> name
        )
        {
            TooltipChildren.Dossier entry = found[at];
            Func<string> climbed = entry.Name;
            Func<string> mine = name;
            entry.Name = () =>
            {
                string said = mine();
                return string.IsNullOrEmpty(said) ? (climbed == null ? null : climbed()) : said;
            };
            entry.Unsaid = name;
            found[at] = entry;
        }

        /// <summary>How close the system is to rising up, and how long there is to do something about
        /// it. The label draws a ring and a turn count and writes no word for either, so the phrase is
        /// the mod's; the ring is read as the proportion it is drawn at. The two SENTENCES the game
        /// hangs on the ring and the countdown are children of the row instead
        /// (<see cref="Icons"/>) - one node each, as two hover targets always are.</summary>
        private static string Rebellion(StarSystemLabel label)
        {
            RebellionGroup group = label.RebellionGroup;
            if (group == null || !AgeWidgets.Visible(group.RebellionStatusGroup))
            {
                return null;
            }

            return ModStrings.Format(
                ModStrings.GalaxySystemRebellion,
                Percent(group.RebellionGauge),
                AgeText.Label(group.RebellionTimerLabel)
            );
        }

        /// <summary>What the system is building and how long it has left, or the cross the game draws
        /// over the slot when the queue is empty. The label draws the thing as its own picture and its
        /// name lives in the wrapper on the tooltip behind it. This is what the queue's own child node
        /// is CALLED (<see cref="Icons"/>); the dossier behind the picture is what it carries.</summary>
        private static string Building(StarSystemLabel label)
        {
            AgeTransform group = label.QueuedConstructionGroup;
            if (!AgeWidgets.Visible(group))
            {
                return null;
            }

            if (
                label.NoConstructionCross != null
                && AgeWidgets.Visible(label.NoConstructionCross.AgeTransform)
            )
            {
                return ModStrings.Get(ModStrings.GalaxySystemNothingBuilding);
            }

            return ModStrings.Format(
                ModStrings.GalaxySystemBuilding,
                AgeWidgets.TooltipTitle(AgeWidgets.Raw(group)),
                AgeText.Label(label.ConstructionTurnsLabel)
            );
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
                    || (int)colony.Visibility[Gui.PlayerEmpire]
                        < (int)EntityVisibility.Layer.Known
                )
                {
                    continue;
                }

                string called = EmpireNames.Named(colony.Empire);
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
        private static void ExplorationWinners(
            List<TooltipChildren.Dossier> found,
            StarSystemLabel label
        )
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
                if (badge == null || !AgeWidgets.Painted(badges[i]))
                {
                    continue;
                }

                // Who won the race to explore the place: a standing fact about it, like the wonder
                // and the temple beside it.
                int at = found.Count;
                TooltipChildren.AddPlain(found, badge.Tooltip, badges[i]);
                In(found, at, Region.Details);
            }
        }

        /// <summary>What has been found in the ground here, said on the deposit's own child node: the
        /// kind's name off the wrapper behind the picture, plus whether the system is working it, which
        /// is the LIT-or-faded state the game paints that picture in (<see cref="Exploited"/>). A
        /// deposit the label is drawing no picture for - the map draws that strip only from close
        /// enough, and the node exists at every distance - keeps the bare name rather than being called
        /// idle on a guess, and so does one whose item the game drew some other way.</summary>
        public static string DepositName(string named, AgeTooltip icon)
        {
            if (string.IsNullOrEmpty(named))
            {
                return named;
            }

            int exploited = Exploited(AgeWidgets.TooltipOwner(icon));
            return exploited < 0
                ? named
                : ModStrings.Format(
                    exploited > 0
                        ? ModStrings.GalaxySystemDepositExploited
                        : ModStrings.GalaxySystemDepositIdle,
                    named
                );
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
            string drawn =
                button == null
                    ? null
                    : AgeWidgets.DrawnLabel(button.AgeTransform, button.ShipCountLabel);
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
        /// next one is, and what it is counting down to. THE ACADEMY IS NOT A BUTTON (owner ruling
        /// 2026-09-02, withdrawing the button round 5 wired onto the clickable icon inside this
        /// group) - the group is read here as text, as it always was.
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

            // A LABEL FALLBACK, as on the pirate countdown: the group draws a bare figure and the
            // words naming it are on its own tooltip's first line and nowhere else. The tooltip goes
            // on reaching the player through the door as the icon's own node.
            string says = CardActions.FirstLine(AgeWidgets.Raw(academy.AcademyRolesCountdown));
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

        /// <summary>Every drawn item of a row of icons, one child node each, in the order the row
        /// arranges them - the strip of standing icons under the name (home system, marketplace, golden
        /// age...) and the haunted planets.</summary>
        private static void Table(
            List<TooltipChildren.Dossier> found,
            AgeTransform table,
            Region region
        )
        {
            if (!AgeWidgets.Visible(table))
            {
                return;
            }

            IList<AgeTransform> items = table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                Picture(found, items[i], region);
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
                AddWords(lines, AgeText.ContentLines(tooltip));
                return;
            }

            string named = AgeWidgets.ItemText(widget);
            if (!string.IsNullOrEmpty(named))
            {
                Add(lines, named);
                return;
            }

            AddWords(lines, AgeText.ContentLines(tooltip));
        }

        private static void AddWords(List<string> lines, IList<string> words)
        {
            for (int i = 0; words != null && i < words.Count; i++)
            {
                Add(lines, words[i]);
            }
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
