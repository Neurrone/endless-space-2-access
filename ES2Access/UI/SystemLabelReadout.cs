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
                // Left of the name: what is happening AT the system.
                Picture(found, label.ContextualIconBattle);
                Picture(found, label.ContextualIconPortal);
                Picture(found, label.ContextualIconBlockedFleetPortal);
                Picture(found, label.ContextualIconHonorZone);
                Picture(found, label.ContextualIconWonder);
                Picture(found, label.ContextualIconDetectionProbe);
                Picture(found, label.ContextualIconTemple);
                Picture(found, label.ContextualIconSlumberingRuins);

                // Right of the name, minus the three the game draws there as BUTTONS: those are child
                // nodes of the system already (<see cref="Actions"/>).
                Picture(found, label.ContextualIconBlackout);
                Picture(found, label.ContextualIconSiege);
                Picture(found, label.ContextualIconInvasion);
                Picture(found, label.ContextualIconJuggernautEffects);
                Picture(found, label.GivenToAcademyGroup);

                // The population line: how many of the people here are the player's own agents. The
                // COUNT is spoken with the system's readout; this is the sentence the game explains it
                // with, which is on the picture and reaches nobody without a node to stand on.
                StarSystemLabel drawn = label;
                Picture(found, label.TraitorCountGroup, () => Sleepers(drawn));

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
                            : rebellion.RebellionGauge.AgeTransform
                    );
                    Picture(
                        found,
                        rebellion.RebellionTimerLabel == null
                            ? null
                            : rebellion.RebellionTimerLabel.AgeTransform
                    );
                }

                // What the system is building. The picture's own dossier is the constructible's, which
                // is the thing this node exists to hand over: the label promises that dossier and,
                // until this node existed, never offered it to anybody without a mouse.
                Picture(found, label.QueuedConstructionGroup, () => Building(drawn));

                // A line per team racing for the system, named by the wrapper the game hangs on the
                // team's own icon. The score is a strip of lit gauge parts with the figure written
                // nowhere on it, and the game keeps that figure in the row's own tooltip
                // (<c>KingOfTheHillScoreLine.RefreshTooltip</c>) along with who is winning and how many
                // turns are left - so the row's sentence is the whole reading.
                Table(found, label.KingOfTheHillTable);
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
                Table(found, label.HomeAndTradingTable);
                ExplorationWinners(found, label);
                Table(found, label.HauntCirclesTable);
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
        /// </summary>
        private static void Picture(List<TooltipChildren.Dossier> found, AgeTransform widget)
        {
            Picture(found, widget, null);
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
                Picture(found, label.BlackoutIcon);
                Picture(found, label.BestSystemIcon);
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
            if (string.IsNullOrEmpty(Wrote(it)))
            {
                return;
            }

            found.Add(
                new TooltipChildren.Dossier
                {
                    Name = () => Wrote(it),
                    Tooltip = it,
                    Anchor = widget,
                    Carrier = widget,
                }
            );
        }

        /// <summary>The first line the game wrote into a tooltip itself, whatever renderer the tooltip
        /// names - which is what <c>CardActions.FirstLine</c> answers only for the plain ones.</summary>
        private static string Wrote(AgeTooltip tooltip)
        {
            // A re-composed reader, and only for the NAME: the words themselves still reach the player
            // through the door, as the node's own sections, in whatever loudness the tooltip's kind
            // says. This rung exists because the door's own first-line rung
            // (<c>CardActions.FirstLine</c>) answers only for a tooltip with no class on it.
            IList<string> words = AgeText.Lines(AgeText.Tooltip(tooltip));
            return words == null || words.Count == 0 ? null : words[0];
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

                TooltipChildren.AddPlain(found, badge.Tooltip, badges[i]);
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

            string power = AgeWidgets.DrawnLabel(
                pirates.PiratePowerGroup,
                pirates.PiratePowerLabel
            );
            if (power != null)
            {
                Add(lines, ModStrings.Format(ModStrings.GalaxySystemPiratePower, power));
            }
        }

        /// <summary>Every drawn item of a row of icons, one child node each, in the order the row
        /// arranges them - the strip of standing icons under the name (home system, marketplace, golden
        /// age...) and the haunted planets.</summary>
        private static void Table(List<TooltipChildren.Dossier> found, AgeTransform table)
        {
            if (!AgeWidgets.Visible(table))
            {
                return;
            }

            IList<AgeTransform> items = table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                Picture(found, items[i]);
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
