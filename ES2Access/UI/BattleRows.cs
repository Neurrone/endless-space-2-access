using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// The rows every battle surface is built out of, in one place.
    ///
    /// Five screens draw the same fight - the setup popup, the two report popups, the advanced setup
    /// window and the advanced report (<c>BattleNotifications</c>, <c>AdvancedEncounterPlayScreen</c>,
    /// <c>AdvancedBattleReportScreen</c>, <c>SpaceBattleScreen</c>, <c>GroundBattleViewScreen</c>) - and
    /// each of them used to carry its own copy of these seven factories. Copies drift: one of them
    /// added the refusal reading, one added the name-repeat guard, one gained a try/catch after a
    /// throw, and nothing said which reading was the intended one. They are one reading now.
    ///
    /// The naming rule is deliberately NOT here. What a battle control is called differs per screen -
    /// the game titles some of them, draws words on others, and names two of the advanced window's
    /// switches nowhere at all - so each screen hands in the name it derived and these build the row
    /// around it.
    ///
    /// The title keys the screens share sit here too, for the same reason: a key spelled in two files
    /// is a key that can be corrected in one of them.
    /// </summary>
    public static class BattleRows
    {
        // ---- the game's own titles, shared by more than one battle surface ----

        /// <summary>The battle plan chosen for a fight that is being SET UP.</summary>
        public const string SetupPlanTitleKey = "%NotificationBattleSetupSelectedPlayTitle";

        /// <summary>The battle plan a finished fight was fought under.</summary>
        public const string ReportPlanTitleKey = "%NotificationBattleReportSelectedPlayTitle";

        /// <summary>The button that starts the fight.</summary>
        public const string StartTitleKey = "%NotificationBattleSetupStartButtonTitle";

        /// <summary>The button that runs away from it.</summary>
        public const string RetreatTitleKey = "%NotificationBattleSetupRetreatButtonTitle";

        /// <summary>The box that decides whether the cinematic is watched at all.</summary>
        public const string WatchToggleTitleKey = "%NotificationBattleSetupWatchToggleTitle";

        /// <summary>The button that puts a popup's chosen outcome into effect.</summary>
        public const string ValidateTitleKey = "%NotificationValidateTitle";

        // ---- rows ----

        /// <summary>
        /// A line the game wrote and is showing, read as it stands.
        ///
        /// <paramref name="explains"/> is for a label whose dossier the game hung on some other widget;
        /// <paramref name="details"/> is anything the row went and got that the game did not draw, and
        /// <paramref name="sayDetails"/> decides whether the player is handed them as the row is read or
        /// left to find them in the review buffer. The default is the buffer, because a row whose
        /// further words are a second reading of something already on screen would say the screen back;
        /// saying them is for words that are the POINT of the row - the sentence behind an outcome
        /// word, which is what the player wanted when they landed on it. It is a fact about the row,
        /// not about a tooltip: the tooltip that comes after answers for its own loudness by its own
        /// kind.
        /// </summary>
        public static void Note(
            GraphBuilder builder,
            AgePrimitiveLabel label,
            string key,
            AgeTooltip explains = null,
            Func<IList<string>> details = null,
            bool sayDetails = false
        )
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            if (widget == null || string.IsNullOrEmpty(AgeText.Label(label)))
            {
                return;
            }

            AgePrimitiveLabel it = label;
            AgeTooltip tooltip = explains ?? AgeWidgets.Raw(widget);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it)),
                },
                Sections = sayDetails
                    ? GraphNodes.SpokenSections(details, tooltip)
                    : GraphNodes.Sections(details, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget, tooltip);
            builder.AddItem(Nodes.Drawn(ControlId.For(label, key), vtable, label));
        }

        /// <summary>
        /// The same line where the game drew a wordless icon next to it.
        ///
        /// The label and the icon are TWO HOVER SURFACES, and which of them the engine would really
        /// draw is the engine's answer rather than the prefab's: the row points at the one that draws -
        /// its own where it has one, the icon's where it has not - and whichever it is not pointing at
        /// becomes a nested entry of its own, which is what every second hover surface in the mod does.
        /// A row pointing at nothing releases the pointer rather than leaving a neighbour's tooltip
        /// standing over it.
        /// </summary>
        public static void NoteBeside(
            GraphBuilder builder,
            AgePrimitiveLabel label,
            string key,
            AgeTransform beside = null
        )
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            if (widget == null || string.IsNullOrEmpty(AgeText.Label(label)))
            {
                return;
            }

            AgePrimitiveLabel it = label;
            AgeTooltip own = AgeWidgets.Raw(widget);
            AgeTooltip badge = AgeWidgets.Raw(beside);
            bool drawn = AgeWidgets.Draws(own);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it)),
                },
            };
            AgeTooltip aimed = drawn ? own : badge;
            vtable.Sections = GraphNodes.SectionsFor(vtable, aimed);
            if (aimed == null)
            {
                vtable.OnFocusVisual = AgeWidgets.ReleasePointer;
            }

            List<TooltipChildren.Dossier> dossiers = new List<TooltipChildren.Dossier>(1);
            if (drawn)
            {
                // Both kinds through one sink, exactly as the nesting sink itself asks: only one of
                // the two tests can pass for a given tooltip, and asking both is what makes the icon
                // an entry whichever kind the prefab hung on it.
                TooltipChildren.Add(dossiers, badge, beside);
                TooltipChildren.AddPlain(dossiers, badge, beside);
            }

            TooltipChildren.Declare(
                builder,
                Nodes.Drawn(ControlId.For(label, key), vtable, label),
                key,
                dossiers
            );
        }

        /// <summary>
        /// A number the game drew beside a picture, under the game's own name for what the picture
        /// means.
        ///
        /// <paramref name="line"/> is the ROW the figure sits in where the game drew one, and the node
        /// then stands on the row rather than on the label inside it - which is also where the drawn
        /// caption is read from (<see cref="RowTitle"/>). A figure the game drew on its own passes
        /// null. <paramref name="reading"/> is for a figure whose drawn string says something other
        /// than what it looks like; everything else reads the label as it stands.
        /// </summary>
        public static void Value(
            GraphBuilder builder,
            AgeTransform line,
            AgePrimitiveLabel value,
            string titleKey,
            string key,
            Func<string> reading = null
        )
        {
            AgeTransform widget = line ?? (value == null ? null : value.AgeTransform);
            if (widget == null || !AgeWidgets.Visible(widget) || value == null)
            {
                return;
            }

            AgePrimitiveLabel it = value;
            AgeTransform row = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            Func<string> said = reading ?? (() => AgeText.Label(it));
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => RowTitle(row, it, titleKey)),
                    GraphNodes.ValuePart(said, false),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(Nodes.Drawn(ControlId.For(value, key), vtable, value));
        }

        /// <summary>
        /// What the game DREW over one of these figures, and only failing that the title it ships for
        /// the row.
        ///
        /// The two ground popups share one row prefab and one pair of panel classes, and the report's
        /// prefab captions the same line with a different word: the setup says "Assigned" and the
        /// report says "Remaining", because after the fighting the figure is what is LEFT rather than
        /// what was committed. Neither panel class rewrites that caption, so the only place the
        /// difference exists is the drawing - and a row named from the shared title key told the player
        /// the wrong thing on the report for as long as the key was the only source.
        /// </summary>
        private static string RowTitle(AgeTransform line, AgePrimitiveLabel value, string titleKey)
        {
            string drawn = null;
            try
            {
                List<AgeTransform> children = line == null ? null : line.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    AgePrimitiveLabel label =
                        child == null ? null : child.GetComponent<AgePrimitiveLabel>();
                    if (label == null || ReferenceEquals(label, value))
                    {
                        continue;
                    }

                    drawn = AgeText.Label(label);
                    break;
                }
            }
            catch (Exception) { }

            return string.IsNullOrEmpty(drawn) ? AgeText.Clean(titleKey) : drawn;
        }

        /// <summary>Who is fighting for this side, in the game's own "leader of faction" form, and the
        /// hero commanding it where there is one - the portrait carries the hero's whole dossier, so the
        /// row indicates having one and the buffer holds it.</summary>
        public static void Leader(GraphBuilder builder, BattleGroupInfoPanel panel, string prefix)
        {
            if (panel == null)
            {
                return;
            }

            try
            {
                Note(builder, panel.MainLeaderName, prefix + "/leader");
                AgePrimitiveImage portrait = panel.MainHeroPortrait;
                AgeTransform widget = portrait == null ? null : portrait.AgeTransform;
                if (widget == null)
                {
                    return;
                }

                AgeTooltip tooltip = AgeWidgets.Raw(widget);
                NodeVtable vtable = new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => AgeWidgets.TooltipTitle(tooltip)),
                    },
                    Sections = GraphNodes.Sections(null, tooltip),
                };
                AgeWidgets.PointAt(vtable, widget);
                builder.AddItem(
                    Nodes.Drawn(ControlId.For(portrait, prefix + "/hero"), vtable, portrait)
                );
            }
            catch (Exception e)
            {
                Log.Warn("battle: reading a side's leader threw: " + e);
            }
        }

        /// <summary>A button a battle surface drew as an icon, under whatever name the screen derived
        /// for it. Its availability is the game's own test rather than the enable flag, and a refusal
        /// reads with the game's own reason - without repeating the button's name back, which
        /// <see cref="GraphNodes.AddRefusal"/> is what guards.</summary>
        public static void Command(
            List<Cell> cells,
            AgeTransform widget,
            Func<string> name,
            string key
        )
        {
            if (widget == null)
            {
                return;
            }

            AgeTransform it = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            Func<bool> enabled = () => AgeWidgets.Offered(it);
            NodeVtable vtable = GraphNodes.Button(
                name,
                () => AgeWidgets.Press(it),
                enabled,
                tooltip
            );
            GraphNodes.AddRefusal(vtable, tooltip, enabled);
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(cells, widget, ControlId.For(widget, key), vtable);
        }

        /// <summary>A box the player ticks, under whatever name the screen derived for it. The watch
        /// box is the one control here that decides what the player MEETS - with it off the battle is
        /// over before it starts - so it says its state like any other box rather than being left to
        /// the tooltip.</summary>
        public static void Checkbox(
            List<Cell> cells,
            AgeControlToggle toggle,
            Func<string> name,
            string key
        )
        {
            AgeTransform widget = AgeWidgets.Transform(toggle);
            if (toggle == null)
            {
                return;
            }

            AgeControlToggle it = toggle;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Checkbox(
                name,
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Offered(widget),
                tooltip
            );
            AgeWidgets.Point(vtable, it, tooltip, widget);
            Cells.Add(cells, widget, ControlId.For(toggle, key), vtable);
        }

        /// <summary>
        /// How long is left, for a fight the game is timing.
        ///
        /// Never watched: the countdown would otherwise talk over every other thing on the surface -
        /// and a countdown announcing itself under a standing cursor would talk over the plan the
        /// player is choosing - so it is there to be asked. A gauge with no number on it is all the
        /// game draws, so the figure comes from the notification's own clock.
        /// </summary>
        public static void Countdown(
            List<Cell> cells,
            AgeTransform gauge,
            Func<float> ratio,
            string key
        )
        {
            if (gauge == null || ratio == null)
            {
                return;
            }

            if (OptionalText.Phrase(ModStrings.BattleTimeLeft, 0) == null)
            {
                return;
            }

            Func<float> left = ratio;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => TimeLeft(left)),
                },
                OnFocusVisual = AgeWidgets.ReleasePointer,
            };
            Cells.Add(cells, gauge, ControlId.Structural(key), vtable);
        }

        /// <summary>The countdown's own words, as a percentage of the time allowed. A clock that threw
        /// says nothing rather than saying a wrong number.</summary>
        public static string TimeLeft(Func<float> ratio)
        {
            try
            {
                return OptionalText.Phrase(
                    ModStrings.BattleTimeLeft,
                    UnityEngine.Mathf.Clamp(
                        UnityEngine.Mathf.RoundToInt(ratio() * 100f),
                        0,
                        100
                    )
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Which band the rows that follow belong to - announced once as focus enters, so a roster is
        /// audibly yours or theirs without every row saying so. A build with no such phrase opens no
        /// level at all, which is why every caller closes with <see cref="Close"/>.
        ///
        /// <paramref name="positions"/> is off by default because most of these bands are not one
        /// numbered set: a side's leader line, its plans and its flotilla rows share a level, and a
        /// stamp across all of them would count things that are not peers. A band whose rows ARE a set
        /// asks for it.
        /// </summary>
        public static bool Context(GraphBuilder builder, string nameKey, bool positions = false)
        {
            string name = OptionalText.Phrase(nameKey);
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            builder.PushContext(name, null, positions);
            return true;
        }

        /// <summary>Close the level <see cref="Context"/> opened, where it opened one.</summary>
        public static void Close(GraphBuilder builder, bool opened)
        {
            if (opened)
            {
                builder.PopContext();
            }
        }
    }
}
