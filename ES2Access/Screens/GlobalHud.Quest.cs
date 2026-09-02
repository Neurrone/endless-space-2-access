using System;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The pinned quest, and the bar a collapsed tutorial leaves beside it.</summary>
    public sealed partial class GlobalHud
    {
        // ---- the pinned quest ----

        /// <summary>
        /// The quest the game is tracking, as the panel in the top right corner shows it: what it is
        /// called, how it is going, and what has to be done next.
        ///
        /// Three nodes at most, because the game draws three things to click: the panel itself, which
        /// opens the journal on this quest, and the two bare icons on it - the marker that takes the
        /// camera to wherever the quest is happening, and the pin that lets it go. They are drawn, so
        /// they are walked; neither is captioned, so each is named by the mod and explains itself with
        /// the game's own tooltip. An icon the game is not drawing is not a node: the marker is hidden
        /// outright for a quest with nowhere to point at, and a node that took the camera nowhere would
        /// teach the player that part of the panel is decoration.
        ///
        /// The stop is there only while the game draws the panel, which is two questions and not one:
        /// the game hides the whole window behind any full screen it opens, and it draws nothing at
        /// all while no quest is pinned. Neither state gets a placeholder - a stop saying "no quest"
        /// is a stop the player walks past to learn what a glance would have told them.
        /// </summary>
        public void Quest(GraphBuilder builder)
        {
            PinnedQuestPanel panel = QuestPanel();
            if (panel == null)
            {
                return;
            }

            PinnedQuestPanel it = panel;
            AgeTooltip hint = panel.QuestObjectiveTooltip;
            AgeControlButton open = AgeWidgets.Button(panel.AgeTransform);
            NodeVtable vtable = GraphNodes.Button(
                () => AgeText.FullLabel(it.QuestTitleLabel),
                () => AgeWidgets.Press(open),
                null,
                hint
            );
            vtable.Announcements.Add(GraphNodes.ValuePart(() => QuestProgress(it)));
            vtable.Announcements.Add(
                GraphNodes.ValuePart(() => AgeText.FullLabel(it.QuestObjectiveLabel))
            );
            // The panel's OWN sentence - what this corner of the screen is and what clicking it does -
            // is written on a tooltip the mod deliberately does not point at (below), so it exists
            // nowhere else; it reads first in the buffer, ahead of the objective's, and speaks nothing
            // (measured 2026-08-23: it was uncovered).
            vtable.Sections = GraphNodes.Sections(
                GraphNodes.TooltipDetails(AgeWidgets.Raw(panel.AgeTransform)),
                hint
            );
            // The panel is the thing that lights up, but the tooltip worth reading hangs off the
            // objective's own label inside it - pointing at the panel would leave the review buffer
            // waiting on a tooltip the game never drew.
            AgeWidgets.Point(
                vtable,
                open,
                hint,
                hint == null ? panel.AgeTransform : hint.AgeTransform
            );

            builder.BeginStop(QuestStop);
            // The panel carries no caption of its own (below), so the word is the mod's: without it
            // Tab lands on a quest title with nothing saying which corner of the screen it came from.
            builder.PushContext(ModStrings.Get(ModStrings.HudQuestPanel));
            // Synthetic: the row stands for the pinned QUEST, read off the panel's binding rather than
            // off any one widget it draws.
            builder.AddItem(Nodes.Synthetic(ControlId.For(panel.PinnedQuest, "hud:quest"), vtable));
            AddQuestButton(
                builder,
                panel.ShowLocationButton,
                ModStrings.HudQuestShowLocation,
                "hud:quest/location"
            );
            AddQuestButton(builder, panel.UnpinButton, ModStrings.HudQuestUnpin, "hud:quest/unpin");
            builder.PopContext();
        }

        /// <summary>One of the icons the panel draws on itself, where the game is drawing it. Drawn AND
        /// enabled: the game hides the marker for a quest with nowhere to point at without ever
        /// switching it off, so asking about enablement alone declares a control the player cannot see
        /// and the game will not act on.</summary>
        private static void AddQuestButton(
            GraphBuilder builder,
            AgeControlButton button,
            string nameKey,
            string key
        )
        {
            AgeTransform widget = AgeWidgets.Transform(button);
            // Synthetic guard: the node declared below carries no widget, so this test is the whole of
            // its existence check - nothing downstream will ask again.
            if (widget == null || !AgeWidgets.Visible(widget) || !AgeWidgets.Operable(widget))
            {
                return;
            }

            AgeControlButton it = button;
            NodeVtable vtable = GraphNodes.Button(
                () => ModStrings.Get(nameKey),
                () => AgeWidgets.Press(it),
                null,
                AgeWidgets.Raw(widget)
            );
            AgeWidgets.PointAt(vtable, widget);
            // Synthetic: mod-authored - the quest strip's own button, which the HUD draws nothing
            // separate for.
            builder.AddItem(Nodes.Synthetic(ControlId.Structural(key), vtable));
        }

        /// <summary>How the quest is going, in the game's own word for it - "Ongoing", or the count of
        /// what is done out of what is needed where the objective has one. The panel hides this label
        /// outright while a quest is waiting on the player to choose between objectives.</summary>
        private static string QuestProgress(PinnedQuestPanel panel)
        {
            try
            {
                return AgeWidgets.Visible(panel.QuestProgressLabel.AgeTransform)
                    ? AgeText.FullLabel(panel.QuestProgressLabel)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Which quest the game is tracking has changed - a quest finished and the journal
        /// pinned the next one, or the player let this one go. Said wherever they are standing,
        /// because the panel is drawn over every page and nothing else reports the change.</summary>
        private void AnnounceQuest()
        {
            if (!_questChanged)
            {
                return;
            }

            _questChanged = false;
            try
            {
                Voice.Say(QuestAnnouncement(), false);
            }
            catch (Exception e)
            {
                Log.Warn("hud: announcing the pinned quest threw: " + e);
            }
        }

        /// <summary>The panel's own words where the game is drawing them, and the quest's title on its
        /// own where it is not - the journal can pin a quest while a full screen is covering the
        /// panel. Nothing pinned is its own sentence rather than an empty one.</summary>
        private string QuestAnnouncement()
        {
            PinnedQuestPanel panel = QuestPanel();
            if (panel != null)
            {
                return ModStrings.Format(
                    ModStrings.HudQuestPinned,
                    new MessageBuilder()
                        .ListItem(AgeText.FullLabel(panel.QuestTitleLabel))
                        .ListItem(QuestProgress(panel))
                        .Build()
                );
            }

            Quest quest = ActiveQuest();
            return quest == null
                ? ModStrings.Get(ModStrings.HudQuestUnpinned)
                : ModStrings.Format(
                    ModStrings.HudQuestPinned,
                    AgeText.Clean(new GuiQuest(quest).Title)
                );
        }

        /// <summary>Listen to the player empire's journal for the tracked quest changing. Subscribed
        /// when the page arrives and given back when it leaves, so the mod holds no subscription
        /// nobody is listening to and a hot reload - which pops every page - leaves none behind.
        /// </summary>
        private void WatchQuests()
        {
            ForgetQuests();
            try
            {
                Empire empire = PlayerEmpire();
                DepartmentOfInternalAffairs affairs =
                    empire == null ? null : empire.GetAgency<DepartmentOfInternalAffairs>();
                QuestJournal journal = affairs == null ? null : affairs.QuestJournal;
                if (journal == null)
                {
                    return;
                }

                _journal = journal;
                journal.ActiveQuestChange += OnActiveQuestChange;
            }
            catch (Exception e)
            {
                Log.Warn("hud: watching the quest journal threw: " + e);
            }
        }

        private void ForgetQuests()
        {
            try
            {
                if (_journal != null)
                {
                    _journal.ActiveQuestChange -= OnActiveQuestChange;
                }
            }
            catch (Exception e)
            {
                Log.Warn("hud: releasing the quest journal threw: " + e);
            }

            _journal = null;
        }

        /// <summary>Only sets state: what the change should say is worked out - and said - from the
        /// per-frame pump, which is also where the panel has finished rewriting itself.</summary>
        private void OnActiveQuestChange(object sender, QuestJournalChangeEventArgs e)
        {
            _questChanged = true;
        }

        private Quest ActiveQuest()
        {
            try
            {
                return _journal == null ? null : _journal.ActiveQuest;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The panel while the game is really showing a quest on it. Three answers have to
        /// agree: the window is up at all (the game hides it behind every full screen it opens), the
        /// panel still holds a quest (it drops it the moment it starts fading out), and nothing above
        /// it in the tree has been hidden.</summary>
        private static PinnedQuestPanel QuestPanel()
        {
            try
            {
                PinnedQuestWindow window = GameWindows.Shown<PinnedQuestWindow>();
                if (window == null)
                {
                    return null;
                }

                PinnedQuestPanel panel = window.PinnedQuestPanel;
                // Flow control: a null answer is how the caller hears that the quest strip is not on
                // the page, and skips its whole context.
                return panel != null
                    && panel.PinnedQuest != null
                    && AgeWidgets.Visible(panel.AgeTransform)
                    ? panel
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the collapsed tutorial ----

        /// <summary>The bar a collapsed tutorial leaves at the top of the right-hand edge - its title,
        /// its close button, the arrow that brings it back. The tutorial screen knows how to read it;
        /// what this decides is WHERE it sits on the eleven pages that share the HUD's right-hand edge:
        /// among that edge's own fixtures, above the notification icons, which is where it is drawn.
        ///
        /// Every other page gets it appended instead, by <see cref="Screen.BuildShared"/>, and on the
        /// same condition: exactly while the game is drawing the bar. This stop is what
        /// <c>GraphBuilder.DeclaredStop</c> answers for, so a page that placed the bar itself is never
        /// given a second one.</summary>
        public void Tutorial(GraphBuilder builder)
        {
            builder.BeginStop(TutorialStop);
            TutorialScreen.BuildCollapsedBar(builder);
        }

    }
}
