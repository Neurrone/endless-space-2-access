using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;

namespace ES2Access.UI
{
    /// <summary>One control a screen has decided to declare, held with the widget it was read off so
    /// the whole set can be ordered the way the game drew it rather than the way it was collected.
    /// </summary>
    public sealed class Cell
    {
        public AgeTransform Widget;
        public ControlId Id;
        public NodeVtable Vtable;
    }

    /// <summary>
    /// Declaring a panel's controls in the rows the game draws them in.
    ///
    /// Collection order is whatever the reading code happened to walk; the player's order is the one on
    /// screen. Gathering cells and emitting them through <see cref="AgeLayout.Rows"/> means a strip the
    /// engine wrapped onto a second line is walked as two lines with nothing being told, and a screen
    /// that lists its buttons in a different order from the prefab still reads left to right.
    /// </summary>
    public static class Cells
    {
        private static readonly Func<Cell, AgeTransform> CellWidget = cell => cell.Widget;

        /// <summary>
        /// Declare one control - and give it the gesture this game hangs on the controls it has switched
        /// OFF: the Ctrl+click that locates the technology the control is missing.
        ///
        /// Sixteen prefabs use that trick, and on every one of them the screen's own click is gated away
        /// (<see cref="AgeWidgets.Offered"/> answers false for exactly it), so the shared Ctrl+Enter fall
        /// back to the plain click would replay a control that does nothing. The gesture therefore has to
        /// be WIRED - and this is the one call every widget-backed control in the mod passes through, so
        /// it is wired here once instead of on every screen that draws a hintable control. A screen that
        /// wired a Ctrl gesture of its own keeps it: this only fills an empty slot, and it fills it only
        /// where the hint is actually on the declared widget (a row whose hint hangs off a CHILD names
        /// the child itself, the way the troop list's locked type does).
        ///
        /// Asked per cell per rebuild because a hint comes and goes with the player's research: it is one
        /// <c>GetComponent</c> on a widget the screen has already read several times over.
        /// </summary>
        public static void Add(
            List<Cell> cells,
            AgeTransform widget,
            ControlId id,
            NodeVtable vtable
        )
        {
            if (vtable != null && vtable.OnSelectToggle == null && AgeWidgets.Hinted(widget))
            {
                AgeTransform hint = widget;
                vtable.OnSelectToggle = () => AgeWidgets.Locate(hint);
            }

            cells.Add(new Cell { Widget = widget, Id = id, Vtable = vtable });
        }

        public static void Emit(GraphBuilder builder, List<Cell> cells)
        {
            foreach (List<Cell> row in AgeLayout.Rows(cells, CellWidget))
            {
                builder.StartRow();
                foreach (Cell cell in row)
                {
                    builder.AddItem(cell.Id, cell.Vtable);
                }

                builder.EndRow();
            }
        }

        /// <summary>A control the game drew, activated the way a mouse activates it - the shape every
        /// page's plain buttons take, so a screen that has nothing to say about a button beyond "the
        /// game drew it here" says exactly that.</summary>
        public static Cell Control(
            AgeTransform widget,
            AgeControlButton button,
            AgeTooltip tooltip,
            string text,
            string key
        )
        {
            AgeControlButton it = button;
            AgeTransform at = widget;
            // A control the game draws as a bare icon has no caption of its own; the sentence it
            // explains itself with on hover is what a sighted player reads, so it is the name here too
            // - and then the tooltip must not be announced as well, or the control says the same
            // sentence twice. The buffer still holds all of it.
            bool named = !string.IsNullOrEmpty(text);
            string caption = named ? text : CardActions.FirstLine(tooltip);
            NodeVtable vtable = GraphNodes.Button(
                () => caption,
                () => AgeWidgets.Press(it),
                // The whole availability question rather than the enable flag: this game leaves a
                // button blocked for a missing technology switched ON so a click can explain itself,
                // and the government window's validate button is one of them.
                () => AgeWidgets.Offered(at),
                tooltip,
                named ? GraphNodes.ModeFor(tooltip) : TooltipMode.None
            );
            AgeWidgets.PointAt(vtable, widget);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.Referenced(widget, key),
                Vtable = vtable,
            };
        }

        /// <summary>
        /// A line the mod reads as ONE thing that the game DREW out of several pieces, with every
        /// explanation it hung on those pieces - the empire's faction line, whose name is on the label
        /// and whose description is on that label's own tooltip while the icon beside it explains what
        /// the line is.
        ///
        /// The pieces are not separate nodes, so nothing else can reach their tooltips; the line reads
        /// them in the order the game drew them and the pointer is aimed at the last one's owner, which
        /// is what makes it DRAW (aiming at the container draws nothing at all).
        /// </summary>
        public static Cell Readout(AgeTransform widget, string key)
        {
            List<AgeTooltip> tooltips = Gathered(widget);
            AgeTransform at = widget;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TextOf(at)),
                },
                Sections = GraphNodes.SectionsFor(tooltips),
            };
            AgeWidgets.PointAt(vtable, widget, Last(tooltips));
            return new Cell
            {
                Widget = widget,
                Id = ControlId.Referenced(widget, key),
                Vtable = vtable,
            };
        }

        /// <summary>The same for a line the game made CLICKABLE. The words it is called by are its own
        /// caption where it drew one and the sentence the last-drawn tooltip opens with otherwise, which
        /// is then not announced a second time.</summary>
        public static Cell Control(
            AgeTransform widget,
            AgeControlButton button,
            string text,
            string key
        )
        {
            List<AgeTooltip> tooltips = Gathered(widget);
            AgeTooltip last = Last(tooltips);
            AgeControlButton it = button;
            AgeTransform at = widget;
            bool named = !string.IsNullOrEmpty(text);
            string caption = named ? text : CardActions.FirstLine(last);
            NodeVtable vtable = GraphNodes.Button(
                () => caption,
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Offered(at)
            );
            vtable.Sections = GraphNodes.SectionsFor(
                tooltips,
                null,
                named ? (TooltipMode?)null : TooltipMode.None
            );
            AgeWidgets.Point(vtable, button, last, widget);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.Referenced(widget, key),
                Vtable = vtable,
            };
        }

        // Reused rather than allocated per line: these run inside a per-frame panel walk. Safe as one
        // buffer because a caller consumes it before the next line is read - the sections built from it
        // capture the tooltips, never the list.
        private static readonly List<AgeTooltip> Scratch = new List<AgeTooltip>(4);

        private static List<AgeTooltip> Gathered(AgeTransform widget)
        {
            Scratch.Clear();
            AgeWidgets.Tooltips(widget, Scratch);
            return Scratch;
        }

        private static AgeTooltip Last(List<AgeTooltip> tooltips)
        {
            return tooltips.Count == 0 ? null : tooltips[tooltips.Count - 1];
        }

        /// <summary>The drawn control at <paramref name="widget"/>, if there is one to declare - a
        /// widget the game is not drawing, or one with no button on it, contributes nothing.</summary>
        public static void AddControl(List<Cell> cells, AgeTransform widget, string key)
        {
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlButton button = AgeWidgets.Button(widget);
            if (button != null)
            {
                cells.Add(
                    Control(widget, button, AgeWidgets.Raw(widget), AgeWidgets.TextOf(widget), key)
                );
            }
        }

        /// <summary>The drawn readout at <paramref name="widget"/>, if it says anything - a band the
        /// game has emptied contributes nothing rather than an unnamed node.</summary>
        public static void AddReadout(List<Cell> cells, AgeTransform widget, string key)
        {
            if (
                widget != null
                && AgeWidgets.Visible(widget)
                && !string.IsNullOrEmpty(AgeWidgets.TextOf(widget))
            )
            {
                cells.Add(Readout(widget, AgeWidgets.Raw(widget), key));
            }
        }

        /// <summary>
        /// A figure the game draws as a number beside a bare symbol, named at declaration time.
        ///
        /// The caption is the game's OWN title for the statistic (<paramref name="titleKey"/>, a
        /// <c>%…Title</c> string), because the only thing on screen is the value: the sentence the game
        /// hangs on the row is a gloss it wrote IN ADDITION to the word, and a node named from it says
        /// what the number means without ever saying what it is. Where the game keeps no title at all,
        /// that sentence becomes the name as a last resort - and is then not announced a second time.
        ///
        /// The tooltip is the label's own where it has one and the group's around it otherwise, which is
        /// where these panels put it (<c>ShipDesignBasePanel.Refresh</c> writes it with
        /// <c>GetComponentInParent</c>), and the pointer is aimed at whichever of the two owns it so the
        /// drawn tooltip appears.
        ///
        /// <paramref name="row"/> is what the figure is laid out and keyed by, and defaults to the group
        /// the game drew it in - which is the rect a reader sees it occupy. A strip of columns drawn as
        /// several labels of ONE group (the ship designer's range figures) has to pass its own label
        /// instead, or the columns collide on one id and one rect.
        /// </summary>
        public static void AddStat(
            List<Cell> cells,
            AgePrimitiveLabel label,
            string titleKey,
            string key,
            AgeTransform row = null
        )
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeTooltip own = AgeWidgets.Raw(widget);
            AgeTransform group = widget.Parent;
            AgeTooltip tooltip = own ?? AgeWidgets.Raw(group);
            AgeTransform owner = own != null || group == null ? widget : group;
            AgeTransform laid = row ?? group ?? widget;
            string caption = AgeText.Clean(titleKey);
            bool named = !string.IsNullOrEmpty(caption) && caption[0] != '%';
            AgeTooltip tip = tooltip;
            AgeTransform at = widget;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(
                        named ? (Func<string>)(() => caption) : (() => CardActions.FirstLine(tip))
                    ),
                    GraphNodes.ValuePart(() => AgeWidgets.TextOf(at)),
                },
                Sections = GraphNodes.Sections(
                    null,
                    tooltip,
                    named ? (TooltipMode?)null : TooltipMode.None
                ),
            };
            AgeWidgets.PointAt(vtable, owner);
            cells.Add(
                new Cell { Widget = laid, Id = ControlId.Referenced(laid, key), Vtable = vtable }
            );
        }

        /// <summary>A line the player reads rather than works: whatever words the game drew in it, and
        /// its own tooltip.</summary>
        public static Cell Readout(AgeTransform widget, AgeTooltip tooltip, string key)
        {
            AgeTransform at = widget;
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TextOf(at)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            return new Cell
            {
                Widget = widget,
                Id = ControlId.Referenced(widget, key),
                Vtable = vtable,
            };
        }
    }
}
