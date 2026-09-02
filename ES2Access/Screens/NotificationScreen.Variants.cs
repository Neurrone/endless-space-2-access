using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The few things about a popup that looking at the screen cannot answer: the
    /// per-kind registry and the accessors and readers that go with it.</summary>
    public sealed partial class NotificationScreen
    {
        // ---- the few things about a popup that looking at the screen cannot answer ----

        /// <summary>
        /// What one kind of popup has that no amount of measuring will find. Everything else about all
        /// sixty of them is read off what they draw; these four are the exceptions, and each is an
        /// exception for the same reason - the screen shows the RESULT of a decision the popup's own code
        /// made, and the result looks identical to something else.
        ///
        /// <see cref="Tables"/>: a container the popup refills every turn by cloning one line. On screen
        /// that is a stack of rows, exactly like a stack of rows the popup laid out by hand, and only the
        /// popup's code says which it is - so it says it (<see cref="ReadTableSheet"/>).
        ///
        /// <see cref="Choices"/>: a set the player picks exactly ONE of, wired by hand rather than with
        /// a <c>GuiRadioGroup</c> - the popup's own code unticks the others. A hand-wired set is
        /// indistinguishable on screen from a row of independent boxes, and calling a one-of-five choice
        /// a set of tick boxes tells the player they may have none or all of them.
        ///
        /// <see cref="Confirm"/>: the button that puts the choice into effect where the popup drew it as
        /// a bare tick. It is the same shape as every other unlabelled click-catcher a popup is built
        /// out of, so the shared rule drops it - and dropping it leaves a keyboard player unable to
        /// finish a choice at all.
        ///
        /// <see cref="Words"/>: the label holding what the popup SAYS, for one that did not use the
        /// shared description - a diplomat's message is typed out into a label of its own. Without this
        /// the message reads as one more thing drawn in the body rather than as what the player was
        /// interrupted to hear.
        ///
        /// <see cref="Body"/>: the whole content, for a popup whose content is a MODEL rather than text.
        /// The two battle popups are the case and are the reason it exists: a roster is fleets each
        /// holding ships, a fleet's strength is a coloured arc with no number written on it, a ship's
        /// health is a bar, and what became of a ship is a sentence the game wrote into the row's
        /// tooltip. None of that is text drawn in a band, so no amount of measuring finds it. A popup
        /// with a body owns every control it added as well (<see cref="NotificationBody"/>), because the
        /// shared reading would otherwise declare the same buttons a second time. A body may still
        /// declare <see cref="Choices"/>: the shared collection skips them with everything else, and the
        /// body places them among its own rows through <see cref="BuildChoices"/>, so a popup that is
        /// half model and half choice (the ground battle's outcome) reads each half once.
        ///
        /// <see cref="Gateways"/>: a button that leaves this popup for a page of its own - the negotiation
        /// table, a minor faction's diplomacy, the score screen, the academy. It is the same shape as
        /// <see cref="Confirm"/> and is listed for the same reason: the popup may have drawn it as a bare
        /// icon, and the shared rule drops a control with no words on it. Unlike Confirm the game has no
        /// single word for these, so each is named by whatever the popup DID write - the caption, else the
        /// sentence its tooltip opens with - and the mod's own phrase only where the popup wrote neither.
        /// A gateway the shared reading already found is not declared twice.
        ///
        /// <see cref="Expanders"/>: the tick the popup drew as a bare "+" that folds its detail panel
        /// out and away. It has no caption - what it is for is written in its tooltip - so the shared
        /// rule drops it, and dropping it leaves the detail of a damage report, of an obliterator
        /// strike, of a pirate mission and of both sides of a war breakdown unreachable by keyboard
        /// while the popup happily keeps drawing the "+". A real toggle rather than a button: the panel
        /// stays out until the player folds it back, so the state is worth announcing.
        ///
        /// <see cref="Cards"/>: controls the popup drew as CARDS - a picture with the words scattered
        /// around it rather than written on it. The shared rule names a control from the labels it holds,
        /// and a card holds none: its title, its category and its cost are laid out beside the disk the
        /// click target actually is, so the rule drops the control for having no words and reads the
        /// words as loose text belonging to nobody. Only the popup's code knows which label is the name
        /// of the thing, which is the branch it came from and which is the price - so it says so, and
        /// hands back a finished control.
        ///
        /// A popup with no entry here is read entirely by the shared rules, which is the case for most
        /// of them. A stage adding a popup adds one entry and touches nothing else.
        /// </summary>
        private sealed class Variant
        {
            public Func<NotificationWindow, AgePrimitiveLabel> Words;
            public Func<NotificationWindow, IList<AgeTransform>> Tables;
            public Func<NotificationWindow, IList<AgeTransform>> Choices;
            public Func<NotificationWindow, IList<Control>> Cards;
            public Func<NotificationWindow, IList<AgeControlToggle>> Expanders;
            public Func<NotificationWindow, AgeControl> Confirm;
            public Func<NotificationWindow, IList<Gateway>> Gateways;
            public Action<NotificationBody> Body;
        }

        /// <summary>One button out of a popup and into a page of its own: the widget, and the mod's own
        /// name for where it goes, used only where the popup named it nowhere at all.</summary>
        private struct Gateway
        {
            public AgeTransform Widget;
            public string NameKey;
        }

        private static readonly Dictionary<Type, Variant> Variants = Register();

        private static Dictionary<Type, Variant> Register()
        {
            Dictionary<Type, Variant> variants = new Dictionary<Type, Variant>();

            // Reports the game fills by cloning a line. Where the prefab also draws a band of captions
            // over them, each is read as a table; where it does not, a line at a time.
            variants.Add(
                typeof(BailiffReportNotificationWindow),
                new Variant
                {
                    Tables = w => Some(((BailiffReportNotificationWindow)w).BailiffReportLinesTable),
                }
            );
            variants.Add(
                typeof(LawCancelledNotificationWindow),
                new Variant
                {
                    Tables = w => Some(((LawCancelledNotificationWindow)w).LawCancelledLinesTable),
                }
            );
            variants.Add(
                typeof(PopulationChangeNotificationWindow),
                new Variant
                {
                    Tables = w =>
                        Some(((PopulationChangeNotificationWindow)w).PopulationChangeLinesTable),
                }
            );
            variants.Add(
                typeof(TradingBlockadeNotificationWindow),
                new Variant
                {
                    Tables = w => Some(((TradingBlockadeNotificationWindow)w).TradingBlockadeLineTable),
                }
            );
            variants.Add(
                typeof(TreatiesCancelledNotificationWindow),
                new Variant
                {
                    Tables = w =>
                        Some(((TreatiesCancelledNotificationWindow)w).TreatyCancelledLinesTable),
                }
            );
            variants.Add(
                typeof(RelicsCollectionCompletedNotificationWindow),
                new Variant
                {
                    Tables = w =>
                        Some(
                            ((RelicsCollectionCompletedNotificationWindow)w)
                                .RelicsCollectionCompletedLinesTable
                        ),
                }
            );
            variants.Add(
                typeof(RelicsCollectionCanceledNotificationWindow),
                new Variant
                {
                    Tables = w =>
                        Some(
                            ((RelicsCollectionCanceledNotificationWindow)w)
                                .RelicsCollectionCanceledLinesTable
                        ),
                }
            );
            // The systems that have run out of things to build. The prefab draws one caption over the
            // lines rather than a band of them, so they stay rows - and each line is a BUTTON that opens
            // that system's management view (<c>ConstructionQueueEmptyNotificationLine.OnSelectSystemCb</c>),
            // which is why declaring the container matters here beyond the reading: it is what tells the
            // row which widget carries the click.
            variants.Add(
                typeof(ConstructionQueueEmptyNotificationWindow),
                new Variant
                {
                    Tables = w =>
                        Some(
                            ((ConstructionQueueEmptyNotificationWindow)w)
                                .ConstructionQueueEmptyLinesTable
                        ),
                }
            );
            variants.Add(
                typeof(ElectionSurveyNotificationWindow),
                new Variant
                {
                    Tables = w => Some(((ElectionSurveyNotificationWindow)w).PoliticalSupportLinesTable),
                }
            );

            // Reports whose tables sit behind a breakdown toggle: the toggle is the game's own box (it
            // is in no radio group and turns one thing on and off), and what it unfolds is these. The
            // toggle itself is declared as well - the popup draws it as a bare "+" with its purpose in
            // its tooltip, so nothing else here would find it, and without it the whole panel is
            // unreachable.
            variants.Add(
                typeof(DisplacementReportNotificationWindow),
                new Variant
                {
                    Tables = w =>
                        Some(
                            ((DisplacementReportNotificationWindow)w).ImprovementsTable,
                            ((DisplacementReportNotificationWindow)w).PopulationsTable
                        ),
                    Expanders = w => Unfolds(((DisplacementReportNotificationWindow)w).ReportToggle),
                }
            );
            variants.Add(
                typeof(IonWaveReportNotificationWindow),
                new Variant
                {
                    Tables = w => Some(((IonWaveReportNotificationWindow)w).ShipLinesTable),
                    Expanders = w => Unfolds(((IonWaveReportNotificationWindow)w).ReportToggle),
                }
            );
            variants.Add(
                typeof(ObliteratorAttackReportNotificationWindow),
                new Variant
                {
                    Expanders = w =>
                        Unfolds(((ObliteratorAttackReportNotificationWindow)w).ReportToggle),
                }
            );
            variants.Add(
                typeof(ObliteratorVictimReportNotificationWindow),
                new Variant
                {
                    Tables = w =>
                        Some(
                            ((ObliteratorVictimReportNotificationWindow)w).ShipsTable,
                            ((ObliteratorVictimReportNotificationWindow)w).ImprovementsTable,
                            ((ObliteratorVictimReportNotificationWindow)w).PopulationsTable
                        ),
                    Expanders = w =>
                        Unfolds(((ObliteratorVictimReportNotificationWindow)w).ReportToggle),
                }
            );
            // The pirates' blockade report (Vaulters): what they pillaged and what your cut of it was,
            // each a container the popup refills by cloning a resource item. Both sit inside the details
            // the report's own toggle unfolds.
            variants.Add(
                typeof(PirateMissionReportNotificationWindow),
                new Variant
                {
                    Tables = w =>
                        Some(
                            ((PirateMissionReportNotificationWindow)w).RawLeechedResourcesTable,
                            ((PirateMissionReportNotificationWindow)w).PlayerLeechedResourcesTable
                        ),
                    Expanders = w =>
                        Unfolds(((PirateMissionReportNotificationWindow)w).MissionReportToggle),
                }
            );
            variants.Add(
                typeof(ForceTruceProposedNotificationWindow),
                new Variant
                {
                    Tables = w =>
                        Some(
                            ((ForceTruceProposedNotificationWindow)w).WinnerBreakdownTable,
                            ((ForceTruceProposedNotificationWindow)w).LooserBreakdownTable
                        ),
                    Expanders = w =>
                        Unfolds(
                            ((ForceTruceProposedNotificationWindow)w).WinnerBreakdownToggle,
                            ((ForceTruceProposedNotificationWindow)w).LooserBreakdownToggle
                        ),
                }
            );

            // A narrative event asking the player which way to take it: a set of cards the popup keeps
            // exclusive itself (<c>NarrativeEventBegunNotificationWindow.RefreshChoiceItem</c>
            // :266-279 writes every sibling's state from the one chosen index), each card a picture
            // with its title, its description and the dossier of what it is about hung on it as a
            // tooltip. Without this the whole choice is unreachable, since a card carries no caption
            // of its own.
            variants.Add(
                typeof(NarrativeEventBegunNotificationWindow),
                new Variant
                {
                    Choices = w => Some(((NarrativeEventBegunNotificationWindow)w).ChoiceTable),
                }
            );

            // The quest popup draws who is racing for it and what it pays, both as cloned lines.
            variants.Add(
                typeof(QuestBegunNotificationWindow),
                new Variant
                {
                    Tables = w => QuestTables((QuestBegunNotificationWindow)w),
                    Confirm = w => ((QuestBegunNotificationWindow)w).ValidateButton,
                }
            );

            // Choices the popup keeps exclusive itself.
            variants.Add(
                typeof(HeroRecruitmentNotificationWindow),
                new Variant
                {
                    Choices = w => Some(((HeroRecruitmentNotificationWindow)w).HeroCardsTable),
                    Confirm = w => ((HeroRecruitmentNotificationWindow)w).ValidateButton,
                }
            );
            // The five battle popups: everything they show is a model, so each writes its own body.
            variants.Add(
                typeof(BattleSetupNotificationWindow),
                new Variant { Body = BattleNotifications.Setup }
            );
            variants.Add(
                typeof(BattleReportNotificationWindow),
                new Variant { Body = BattleNotifications.Report }
            );
            variants.Add(
                typeof(GroundBattleSetupNotificationWindow),
                new Variant { Body = BattleNotifications.GroundSetup }
            );
            variants.Add(
                typeof(GroundBattleReportNotificationWindow),
                new Variant { Body = BattleNotifications.GroundReport }
            );

            // What to do with a system the invasion has just taken. A model like the four above it: the
            // system it is about is a header of pictures and bare numbers (its level in a badge, its
            // people as one icon per species, its improvements and its wonders as an icon beside a
            // figure), and the decision itself is a row of cards the popup keeps exclusive. So the body
            // is the mod's and the CHOICE is still the shared one - declared from inside the body
            // (<see cref="BuildChoices"/>) so it is read once, in its place among the header rows.
            variants.Add(
                typeof(GroundBattleOutcomeSelectionNotificationWindow),
                new Variant
                {
                    Body = BattleNotifications.GroundOutcome,
                    Choices = w =>
                        Some(((GroundBattleOutcomeSelectionNotificationWindow)w).OutcomesTable),
                }
            );
            variants.Add(
                typeof(HackingOperationOutcomeSelectionNotificationWindow),
                new Variant
                {
                    // The outcome, and then the parameter it takes: the second set only exists while the
                    // popup has unfolded it over the first.
                    Choices = w =>
                        Some(
                            ((HackingOperationOutcomeSelectionNotificationWindow)w).OutcomesTable,
                            ((HackingOperationOutcomeSelectionNotificationWindow)w).ParametersTable
                        ),
                    Confirm = w =>
                        AgeWidgets.Button(
                            ((HackingOperationOutcomeSelectionNotificationWindow)w).ValidateButton
                        ),
                }
            );

            // A deed pays out in the same cloned reward lines the quest popup uses.
            variants.Add(
                typeof(DeedCompletedNotificationWindow),
                new Variant
                {
                    Tables = w =>
                        Some(
                            ((DeedCompletedNotificationWindow)w).RewardsTable == null
                                ? null
                                : ((DeedCompletedNotificationWindow)w).RewardsTable.RewardsTable
                        ),
                }
            );

            // A diplomat says their piece into a label of their own rather than into the shared one, and
            // an offer is a list of terms - a line per thing each side gives - drawn in the same panel
            // the negotiation table uses.
            variants.Add(
                typeof(DiplomaticInteractionNotificationWindow),
                new Variant
                {
                    Words = w => ((DiplomaticInteractionNotificationWindow)w).MoodMessageLabel,
                    Tables = w => Terms((DiplomaticInteractionNotificationWindow)w),
                }
            );

            // The popups that are a DOOR as well as a report. Each draws a button leading somewhere the
            // player can act on what they have just been told, and each of those buttons is the only route
            // there from here - so if the shared caption rule drops it for being drawn as a bare icon, the
            // popup becomes a dead end. The lists a report draws are declared beside them.

            // A relation changed. Where an ALLY dragged this empire into a war it did not agree to, the
            // popup offers the way to renounce the alliance - straight into the negotiation table with the
            // term already picked (OnNegotiationScreenCb). It also draws a line per member of each
            // alliance involved, as cloned lines.
            variants.Add(
                typeof(DiplomaticRelationChangeNotificationWindow),
                new Variant
                {
                    Tables = w =>
                        Some(
                            ((DiplomaticRelationChangeNotificationWindow)w).MyAllianceTable,
                            ((DiplomaticRelationChangeNotificationWindow)w).TheirAllianceTable
                        ),
                    Gateways = w =>
                        Out(
                            To(
                                ((DiplomaticRelationChangeNotificationWindow)w).DidNotAgreeWarButton,
                                ModStrings.NotifyOpenNegotiation
                            )
                        ),
                }
            );

            // A minor faction has been met: the button opens its diplomacy, which is where it is bought,
            // bribed or assimilated. The game hides it once the faction has been integrated, so a drawn
            // button is always a live route.
            variants.Add(
                typeof(MinorEmpireMetNotificationWindow),
                new Variant
                {
                    Gateways = w =>
                        Out(
                            To(
                                ((MinorEmpireMetNotificationWindow)w).NegotiationButton,
                                ModStrings.NotifyOpenMinorFaction
                            )
                        ),
                }
            );

            // An empire is out of the game - and where it is the PLAYER's, this popup is the end of their
            // game: it refuses to be dismissed or minimised at all, and its one button ends the session
            // and opens the score screen. Nothing has to be done about the two buttons the game neuters
            // (its Dismiss and Minimize handlers return without acting, :67-81): measured, the prefab
            // HIDES them in that case along with the browsing arrows and the pop-up-again box, so the
            // shared reading drops them for being undrawn and the popup offers the one route it has. What
            // the popup cannot say for itself is that the empire is the player's own - see
            // OwnElimination.
            variants.Add(
                typeof(EmpireEliminatedNotificationWindow),
                new Variant
                {
                    Gateways = w =>
                        Out(
                            To(
                                ((EmpireEliminatedNotificationWindow)w).ScoreScreenButton,
                                ModStrings.NotifyOpenScoreScreen
                            )
                        ),
                }
            );

            // The academy asking the player to decide something: a set of choices it keeps exclusive
            // itself, a validate button drawn as a tick, the roles it has handed out as cloned lines, and
            // the way into the academy's own screen.
            variants.Add(
                typeof(ContextualAcademyDiplomaticExchangeUpdateNotificationWindow),
                new Variant
                {
                    Choices = w =>
                        Some(
                            ((ContextualAcademyDiplomaticExchangeUpdateNotificationWindow)w).ChoiceTable
                        ),
                    Confirm = w =>
                        ((ContextualAcademyDiplomaticExchangeUpdateNotificationWindow)w).ValidateButton,
                    Tables = w => Roles((ContextualAcademyDiplomaticExchangeUpdateNotificationWindow)w),
                    Gateways = w =>
                        Out(
                            To(
                                AgeWidgets.Transform(
                                    (
                                        (ContextualAcademyDiplomaticExchangeUpdateNotificationWindow)w
                                    ).academyScreen
                                ),
                                ModStrings.NotifyOpenAcademy
                            )
                        ),
                }
            );

            // Research finished, and with nothing left in the queue the popup offers what to research
            // next: one card per branch of the technology tree, each a picture with its branch, its
            // technology and its cost drawn AROUND it. Clicking one queues that research at once.
            variants.Add(
                typeof(TechnologyUnlockedNotificationWindow),
                new Variant { Cards = ResearchSuggestions.Cards }
            );

            // Nothing is being researched at all: a popup of its own, drawing the SAME suggestions panel
            // over its own description - which the game hides while the panel has anything to offer. The
            // player is being asked the identical question, so it is read identically.
            variants.Add(
                typeof(TechnologyNeededNotificationWindow),
                new Variant { Cards = ResearchSuggestions.Cards }
            );

            // The academy having granted a role: the same roles panel the exchange popup above draws,
            // in a popup of its own, so the same cloned lines read the same way.
            variants.Add(
                typeof(AcademyRoleNotificationWindow),
                new Variant { Tables = w => Roles((AcademyRoleNotificationWindow)w) }
            );

            return variants;
        }

        private static IList<AgeTransform> Some(params AgeTransform[] widgets)
        {
            return widgets;
        }

        /// <summary>The roles the academy has handed out, which its popup draws as cloned lines inside a
        /// panel of its own - and only while the academy is in the state that shows them.</summary>
        private static IList<AgeTransform> Roles(
            ContextualAcademyDiplomaticExchangeUpdateNotificationWindow window
        )
        {
            AcademyRolesReportPanel panel = window.RoleLineTable;
            return Some(
                panel == null || !AgeWidgets.Visible(window.RolesPanel)
                    ? null
                    : panel.RoleLineTable
            );
        }

        /// <summary>The same panel in the popup that exists only to report a role - it is the whole
        /// content there, so its own visibility is the gate rather than a wrapper the popup shows and
        /// hides.</summary>
        private static IList<AgeTransform> Roles(AcademyRoleNotificationWindow window)
        {
            AcademyRolesReportPanel panel = window.RoleLineTable;
            return Some(
                panel == null || !AgeWidgets.Visible(panel.AgeTransform)
                    ? null
                    : panel.RoleLineTable
            );
        }

        /// <summary>The terms of a diplomatic offer: the ones that bind both sides, then what each side
        /// gives. Three tables of cloned lines rather than one, so they read a term at a time.</summary>
        private static IList<AgeTransform> Terms(DiplomaticInteractionNotificationWindow window)
        {
            NegotiationContributionPanel panel = window.ContributionPanel;
            return panel == null
                ? Some()
                : Some(panel.SymmetricalTermsTable, panel.MyTermsTable, panel.HisTermsTable);
        }

        /// <summary>The quest popup's cloned lines: who else is after this quest, what it pays, and the
        /// standings where it is a race. Each panel is a component of its own, and a quest that has none
        /// of them leaves the field unset.</summary>
        private static IList<AgeTransform> QuestTables(QuestBegunNotificationWindow window)
        {
            return Some(
                window.QuestParticipants == null ? null : window.QuestParticipants.ParticipantsTable,
                window.RewardsTable == null ? null : window.RewardsTable.RewardsTable,
                window.PodiumTable == null ? null : window.PodiumTable.PodiumLineTable
            );
        }

        /// <summary>What this popup declares about itself, the popup's own kind first - a variant
        /// registered against a base window serves every popup built on it (the two force-truce
        /// popups, the obliterator reports).</summary>
        private static Variant VariantOf(NotificationWindow window)
        {
            if (window == null)
            {
                return null;
            }

            for (
                Type type = window.GetType();
                type != null && type != typeof(NotificationWindow);
                type = type.BaseType
            )
            {
                Variant variant;
                if (Variants.TryGetValue(type, out variant))
                {
                    return variant;
                }
            }

            return null;
        }

        /// <summary>The body this popup writes for itself, or null where the shared reading answers for
        /// it - which is every popup but the battles.</summary>
        private static Action<NotificationBody> BodyOf(NotificationWindow window)
        {
            Variant variant = VariantOf(window);
            return variant == null ? null : variant.Body;
        }

        /// <summary>Let the popup write its own content. It is given the builder mid-build, with the body
        /// region already open and the popup's words already declared above it, and anything it throws
        /// leaves the strips around it intact rather than losing the whole popup.</summary>
        private static void Write(
            Action<NotificationBody> body,
            GraphBuilder builder,
            NotificationWindow window,
            ControlId lead
        )
        {
            try
            {
                body(
                    new NotificationBody
                    {
                        Builder = builder,
                        Window = window,
                        Lead = lead,
                    }
                );
            }
            catch (Exception e)
            {
                Log.Warn("notification: writing a popup's own body threw: " + e);
            }
        }

        /// <summary>The lines of a hand-wired choice: the cards, outcomes or parameters the popup laid out
        /// in the container it fills with them, and only the ones the player can currently see. The line
        /// rather than the switch inside it, because the line is the whole of what is being chosen - the
        /// words on it, and the reason the game gives for refusing it.</summary>
        private static List<AgeTransform> ChoiceWidgets(NotificationWindow window)
        {
            List<AgeTransform> lines = new List<AgeTransform>();
            Variant variant = VariantOf(window);
            if (variant == null || variant.Choices == null)
            {
                return lines;
            }

            try
            {
                foreach (AgeTransform container in variant.Choices(window))
                {
                    // Flow control: a container the popup is not drawing holds none of this notification's choices.
                    if (container == null || !AgeWidgets.Visible(container))
                    {
                        continue;
                    }

                    List<AgeTransform> children = container.Children;
                    for (int i = 0; children != null && i < children.Count; i++)
                    {
                        if (Switch(children[i]) != null)
                        {
                            lines.Add(children[i]);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("notification: reading a popup's choices threw: " + e);
            }

            return lines;
        }

        /// <summary>
        /// A choice the popup keeps exclusive itself, as controls. Declared because the popup SAYS it is
        /// one, not because it wrote a caption on it: the narrative event's cards are pictures on empty
        /// panels with no words anywhere inside them, and dropping them would leave the player unable to
        /// choose at all. The card's title is its name and everything else written on it is content the
        /// player reviews a line at a time (<see cref="ChoiceName"/>, <see cref="ChoiceDetail"/>).
        /// </summary>
        private static void AddChoices(List<Control> controls, List<AgeTransform> choices)
        {
            for (int i = 0; i < choices.Count; i++)
            {
                AgeTransform choice = choices[i];
                AgeControlToggle switched = Switch(choice);
                if (switched == null || string.IsNullOrEmpty(switched.OnSwitchMethod))
                {
                    continue;
                }

                if (Has(controls, switched.AgeTransform))
                {
                    continue;
                }

                int before = controls.Count;
                Add(
                    controls,
                    "choice/" + i + "/" + choice.name,
                    null,
                    switched,
                    null,
                    null,
                    true,
                    choice.AgeTooltip
                );

                if (controls.Count == before)
                {
                    continue;
                }

                AgeTransform card = choice;
                AgeTransform widget = switched.AgeTransform;
                Control added = controls[controls.Count - 1];
                added.Card = card;

                // A choice drawn as a prefab this mod already has a typed reader for is read by that
                // reader instead of by the label harvest. The harvest reads the labels a card happens
                // to lay out, in the order they are laid out, so a composed card loses the pairing
                // between a caption and its figure ("Level", then "2"), the words the card draws
                // nowhere at all (a mastery's name is on its tooltip wrapper), and which band a
                // heading belongs to. Which reading a card gets is knowledge about the prefab, so it
                // is decided here rather than guessed from the labels.
                HeroDetailedCard hero = HeroCard(choice);
                if (hero != null)
                {
                    added.Details = HeroCards.Sections(hero);
                    // And the pages the card keeps behind its own icons, which the typed reading puts
                    // in the buffer as captions with nothing behind them: each becomes a node.
                    added.Dossiers = HeroCards.Dossiers(hero);
                }
                else
                {
                    added.Drawn = () => ChoiceDetail(widget, card);
                }

                controls[controls.Count - 1] = added;
            }
        }

        /// <summary>The hero card a choice is drawn as, where it is drawn as one - on the choice
        /// itself where the recruitment popup puts the toggle on the card, else the one inside it.
        /// </summary>
        private static HeroDetailedCard HeroCard(AgeTransform choice)
        {
            try
            {
                HeroDetailedCard own = choice.GetComponent<HeroDetailedCard>();
                return own != null ? own : choice.GetComponentInChildren<HeroDetailedCard>();
            }
            catch (Exception e)
            {
                Log.Warn("notification: looking for a choice's hero card threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// The same choice, declared where the popup's OWN body says it goes.
        ///
        /// A popup that writes its own body owns every control it added (<see cref="Variant.Body"/>), so
        /// the shared collection skips its <see cref="Variant.Choices"/> along with everything else it
        /// might have found. A body with a one-of-N among its rows therefore asks for it here rather
        /// than building one of its own: same cards, same names, same refusals, and the same second
        /// click that validates - one reading of a choice, wherever the choice is declared from.
        /// </summary>
        internal static void BuildChoices(GraphBuilder builder, NotificationWindow window)
        {
            try
            {
                List<Control> controls = new List<Control>();
                AddChoices(controls, ChoiceWidgets(window));

                AgeTransform root = Root(window);
                for (int i = controls.Count - 1; i >= 0; i--)
                {
                    // Spoken count: what is left of this list is what the popup says it is offering.
                    if (!Painted(controls[i].Widget, root))
                    {
                        controls.RemoveAt(i);
                    }
                }

                controls.Sort(ReadingOrder);
                for (int i = 0; i < controls.Count; i++)
                {
                    Add(builder, controls[i]);
                }
            }
            catch (Exception e)
            {
                Log.Warn("notification: declaring a body's choices threw: " + e);
            }
        }

        /// <summary>The toggle one line of a choice carries - the line itself where the game made the
        /// whole card the switch, else the one inside it.</summary>
        private static AgeControlToggle Switch(AgeTransform line)
        {
            if (line == null || !AgeWidgets.Visible(line))
            {
                return null;
            }

            AgeControlToggle toggle = line.GetComponent<AgeControlToggle>();
            if (toggle == null)
            {
                toggle = line.GetComponentInChildren<AgeControlToggle>(true);
            }

            // Different widget: the toggle a line is worked by, which is only the answer while the line draws it.
            return toggle != null && AgeWidgets.Visible(toggle.AgeTransform) ? toggle : null;
        }

        /// <summary>What to call a control the popup drew with no words on it: whatever it DID write -
        /// the caption, else the sentence its tooltip opens with - and the mod's own phrase only where
        /// the popup wrote neither. Used for the buttons a popup draws as bare icons (a gateway, an
        /// expander), none of which the shared caption rule can name.</summary>
        private static string WordlessName(AgeTransform widget, string nameKey)
        {
            // The one-level question: "wordless" is about what the popup wrote ON the icon, and an
            // expander drawn over a detail panel it wraps has plenty of words UNDER it.
            string caption = Captioned(widget);
            if (!string.IsNullOrEmpty(caption))
            {
                return caption;
            }

            string hinted = CardActions.FirstLine(AgeWidgets.Raw(widget));
            return string.IsNullOrEmpty(hinted) ? OptionalText.Phrase(nameKey) : hinted;
        }

        /// <summary>The clickable control a popup's gateway field stands on - its own, else the one inside
        /// it, since these fields are plain transforms and the prefab decides which.</summary>
        private static AgeControlButton Clickable(AgeTransform widget)
        {
            try
            {
                if (widget == null || !AgeWidgets.Visible(widget))
                {
                    return null;
                }

                AgeControlButton button =
                    AgeWidgets.Button(widget) ?? widget.GetComponentInChildren<AgeControlButton>(true);
                // Different widget: the button inside the widget, which is only the answer while the popup draws it.
                return button != null && AgeWidgets.Visible(button.AgeTransform) ? button : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static readonly Gateway[] NoGateways = new Gateway[0];

        private static readonly Control[] NoCards = new Control[0];

        /// <summary>The cards this popup drew, finished by the code that knows what its words mean.
        /// </summary>
        private static IList<Control> CardControls(NotificationWindow window)
        {
            Variant variant = VariantOf(window);
            if (variant == null || variant.Cards == null)
            {
                return NoCards;
            }

            try
            {
                return variant.Cards(window) ?? NoCards;
            }
            catch (Exception e)
            {
                Log.Warn("notification: reading a popup's cards threw: " + e);
                return NoCards;
            }
        }

        private static IList<Gateway> Gateways(NotificationWindow window)
        {
            Variant variant = VariantOf(window);
            if (variant == null || variant.Gateways == null)
            {
                return NoGateways;
            }

            try
            {
                return variant.Gateways(window) ?? NoGateways;
            }
            catch (Exception e)
            {
                Log.Warn("notification: looking for a popup's gateways threw: " + e);
                return NoGateways;
            }
        }

        private static IList<Gateway> Out(params Gateway[] gateways)
        {
            return gateways;
        }

        /// <summary>The ticks this popup folds its detail panels out with, where it has any.</summary>
        private static IList<AgeControlToggle> Expanders(NotificationWindow window)
        {
            Variant variant = VariantOf(window);
            if (variant == null || variant.Expanders == null)
            {
                return NoExpanders;
            }

            try
            {
                return variant.Expanders(window) ?? NoExpanders;
            }
            catch (Exception e)
            {
                Log.Warn("notification: looking for a popup's expanders threw: " + e);
                return NoExpanders;
            }
        }

        private static readonly AgeControlToggle[] NoExpanders = new AgeControlToggle[0];

        private static IList<AgeControlToggle> Unfolds(params AgeControlToggle[] toggles)
        {
            return toggles;
        }

        private static Gateway To(AgeTransform widget, string nameKey)
        {
            return new Gateway { Widget = widget, NameKey = nameKey };
        }

        private static AgeControl Confirm(NotificationWindow window)
        {
            Variant variant = VariantOf(window);
            if (variant == null || variant.Confirm == null)
            {
                return null;
            }

            try
            {
                return variant.Confirm(window);
            }
            catch (Exception e)
            {
                Log.Warn("notification: looking for the confirm button threw: " + e);
                return null;
            }
        }

        /// <summary>What the game calls the button that puts a choice into effect. Its own word for it,
        /// from its own localization - the mod invents nothing here, and a build whose localization has
        /// no such word leaves the button to its tooltip.</summary>
        private static string ConfirmName()
        {
            try
            {
                return AgeText.Title(ConfirmTitleKey);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private const string ConfirmTitleKey = "%NotificationValidateTitle";

        /// <summary>
        /// The label the popup put its words in: its own where it named one, else the shared description
        /// every notification has - and none at all where the window does not HOLD the label it was
        /// wired to.
        ///
        /// That last case is a leftover in the prefab rather than a state the popup is in. Four of the
        /// sixty-nine notification windows point the shared description at a label they left out of
        /// their layout entirely - parented to nothing, parked at a 45x20 corner of the screen - and two
        /// of those still carry the skeleton's own key on it: the deed report's says
        /// <c>%NotificationDeedCompletedDescription</c>, which localizes to "You have achieved this
        /// legendary Deed!" on the very popup announcing that somebody else got there first. Every other
        /// test passes such a label. It is marked visible; the chain above it hides nothing because it
        /// has no chain; its text resolves to a whole sentence rather than a template with a hole in it.
        /// Asking whether the WINDOW holds it is the only question that catches it - and a label the
        /// window does not hold is a label nobody ever saw.
        /// </summary>
        private static AgePrimitiveLabel DescriptionLabel(NotificationWindow window)
        {
            Variant variant = VariantOf(window);
            AgePrimitiveLabel own = null;
            if (variant != null && variant.Words != null)
            {
                try
                {
                    own = variant.Words(window);
                }
                catch (Exception e)
                {
                    Log.Warn("notification: looking for the popup's own words threw: " + e);
                }
            }

            // Content: which label holds the popup's OWN words rather than the description it falls back to.
            AgePrimitiveLabel wired =
                own != null
                && AgeWidgets.Visible(own.AgeTransform)
                && !string.IsNullOrEmpty(AgeText.Label(own))
                    ? own
                    : Value(window, NotificationDescription) as AgePrimitiveLabel;
            return wired != null && Held(window, wired.AgeTransform) ? wired : null;
        }

    }
}
