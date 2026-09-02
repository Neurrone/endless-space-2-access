using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.ES2.Speech;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The deeds the wheel hangs off its stages: the node one gets and what a deed says
    /// about its kind, its state and who has already won it.</summary>
    public sealed partial class ResearchScreen
    {
        /// <summary>
        /// The deed the game hangs on a stage's ring: a race between the empires that only one of them
        /// wins, drawn as a marker with a progress track around it.
        ///
        /// One node, read-only - there is nothing to do to a deed from here, and the game's own button
        /// on it is a debug affordance. It says what the game says: the deed's title, the game's word
        /// for what a deed is, the state its colour stands for and, once someone else has taken it,
        /// who - which the game draws as their logo and so says to nobody else. When the game has no
        /// deed for the stage it draws no marker, and there is no node.
        /// </summary>
        private void BuildDeed(
            GraphBuilder builder,
            BaseTechnologyStageItem stage,
            int quadrant,
            int index
        )
        {
            try
            {
                TechnologyStageItem ring = stage as TechnologyStageItem;
                DeedItem2 marker = ring == null ? null : ring.DeedItem;
                // Synthetic guard: the deed is read out of the quest behind the marker, so the node
                // declares no evidence and the gate has nothing to ask.
                if (marker == null || !AgeWidgets.Visible(marker.AgeTransform))
                {
                    return;
                }

                // A marker the mod cannot read the quest behind is a node with nothing to say, which
                // is worse than no node: declare it only once there are words for it.
                if (Deed(marker) == null)
                {
                    return;
                }

                DeedItem2 it = marker;
                AgeTooltip tooltip = marker.Tooltip;
                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => DeedTitle(it)),
                        Value(() => DeedKind(it)),
                        Value(() => DeedState(it)),
                    },
                    Sections = GraphNodes.Sections(null, tooltip),
                };
                if (tooltip != null)
                {
                    AgeWidgets.PointAt(vtable, tooltip.AgeTransform);
                }

                // Synthetic: a deed is read out of the quest behind the marker, which the wheel
                // draws as a dot with no words of its own.
                builder.AddItem(Nodes.Synthetic(
                    ControlId.Structural("research:deed/" + quadrant + "/" + index),
                    vtable
                ));
            }
            catch (Exception e)
            {
                Log.Warn("research: reading a stage's deed threw: " + e);
            }
        }

        // ---- what a deed says ----

        /// <summary>What the game calls the deed - the quest's own title, localized.</summary>
        private static string DeedTitle(DeedItem2 marker)
        {
            try
            {
                GuiDeed deed = Deed(marker);
                return deed == null ? null : AgeText.Clean(deed.Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The game's own word for the kind of thing this is, taken from the category the
        /// quest is filed under rather than invented: "Deed".</summary>
        private static string DeedKind(DeedItem2 marker)
        {
            try
            {
                GuiDeed deed = Deed(marker);
                string category = deed == null ? null : deed.Category;
                if (string.IsNullOrEmpty(category))
                {
                    return null;
                }

                return AgeText.Title("%" + category + "Title");
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// What the marker's colour says: one of the four words the key panel gives the deed states,
        /// and - for a deed another empire has already taken - which empire that was.
        ///
        /// The game draws the winner as their logo beside the marker, so this is the one part of a
        /// deed the mod has to put into words of its own.
        /// </summary>
        private static string DeedState(DeedItem2 marker)
        {
            MessageBuilder message = new MessageBuilder();
            try
            {
                GuiDeed deed = Deed(marker);
                if (deed == null)
                {
                    return null;
                }

                string state = ResearchText.DeedStateName(
                    DeedVisible(deed),
                    deed.IsDeedAvailable(),
                    Progress(deed.State)
                );
                message.Fragment(AgeText.Clean(Gui.Localize("%DeedState" + state + "Title")));
                if (deed.State == QuestState.Failed)
                {
                    message.ListItem(ResearchText.DeedWinner(Winner(deed)));
                }
            }
            catch (Exception) { }

            return message.Build();
        }

        /// <summary>Whether the game is showing this deed at all: an empire has to have researched the
        /// stage it hangs on before anyone learns what the race is for.</summary>
        private static bool DeedVisible(GuiDeed deed)
        {
            try
            {
                return GodGalaxyCursor.IsGuiInGodMode() || deed.IsDeedVisible();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Which empire completed this deed, out of the one quest every empire is racing on
        /// its own copy of - the same search the marker makes to pick the logo it draws.</summary>
        private static string Winner(GuiDeed deed)
        {
            try
            {
                IQuestManagementService quests =
                    Amplitude.Unity.Framework.Services.GetService<IQuestManagementService>();
                List<Quest> racing =
                    quests == null
                        ? null
                        : quests.GetQuestsByInstanceId(deed.Quest.QuestInstanceID);
                for (int i = 0; racing != null && i < racing.Count; i++)
                {
                    if (racing[i].State != QuestState.Completed)
                    {
                        continue;
                    }

                    GuiEmpire empire = Gui.GuiWrapperProviderService.GetGuiEmpire(
                        racing[i].EmpireIndex
                    );
                    return empire == null ? null : AgeText.Clean(empire.Title);
                }
            }
            catch (Exception e)
            {
                Log.Warn("research: finding a deed's winner threw: " + e);
            }

            return null;
        }

        private static ResearchText.DeedProgress Progress(QuestState state)
        {
            if (state == QuestState.InProgress)
            {
                return ResearchText.DeedProgress.InProgress;
            }

            if (state == QuestState.Completed)
            {
                return ResearchText.DeedProgress.Completed;
            }

            return state == QuestState.Failed
                ? ResearchText.DeedProgress.Failed
                : ResearchText.DeedProgress.NotStarted;
        }

        /// <summary>The wrapper the marker built for the quest it is tracking. Private to the game,
        /// like the screen's own zoom: the marker finds the deed, and asking the quest journal again
        /// every frame would be a search per frame for an answer already sitting there.</summary>
        private static GuiDeed Deed(DeedItem2 marker)
        {
            try
            {
                return TrackedDeed == null ? null : TrackedDeed.GetValue(marker) as GuiDeed;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static readonly FieldInfo TrackedDeed = GameHandlers.Field(
            typeof(DeedItem2),
            "guiDeed"
        );
    }
}
