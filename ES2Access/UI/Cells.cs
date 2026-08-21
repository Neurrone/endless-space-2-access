using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.UI.Input;

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

        /// <summary>What the host reads this cell under, where the reading order is a fact about the
        /// THING and not about where the game drew it - the ship designer's slots, grouped by the type
        /// of module each takes (<see cref="ES2Access.Core.UI.SlotOrder"/>). Null everywhere else, and
        /// the drawn order is then the whole of the answer.</summary>
        public string[] Order;
    }

    /// <summary>
    /// Declaring a panel's controls in the order the game drew them.
    ///
    /// Collection order is whatever the reading code happened to walk; the player's order is the one on
    /// screen. Gathering cells and banding them through <see cref="AgeLayout.Rows"/> means a screen that
    /// lists its buttons in a different order from the prefab still reads left to right, top to bottom.
    ///
    /// What the bands are FOR is the choice each host makes. The default is <see cref="EmitLinear"/>:
    /// one node per row, because a bar of buttons, a strip of toggles, a grid of cards or of stat
    /// figures are peers of one kind and the wrap points are a rendering accident, not columns.
    /// <see cref="Emit"/> - a graph row per drawn band, walked sideways - is reserved for a real table,
    /// whose columns are a fact of the game's own data. <see cref="EmitRow"/> is the third case: a set
    /// the screen KNOWS is one line that the layout box wrapped anyway.
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
        /// be WIRED - and this is the one call that takes a whole VTABLE, so it is wired here
        /// once instead of on every screen that draws a hintable control. It is NOT every cell: the
        /// factory methods below (<see cref="Control"/>, <see cref="Readout"/>, <see cref="AddControl"/>,
        /// <see cref="AddReadout"/>, <see cref="AddStat"/>) hand the caller a finished <see cref="Cell"/>
        /// and are appended to the list directly, so a control declared that way keeps the shared fall
        /// back to its plain click - which is right where the game's own handler branches on the held
        /// Control and does more than the jump (the government window's Validate is the worked case:
        /// <c>GovernmentModalWindow.OnValidateCb</c> :379-395 activates the hint AND closes the window,
        /// and only the replayed click gets both). The other way a widget becomes
        /// a node is a card's own button (<see cref="CardActions.Emit"/>), which wires the same three
        /// lines for the same reason: keep the two in step, or the family that skips them answers the
        /// gesture with silence. A screen that
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
                // And a line saying so, since the gesture is the one thing a control in this state
                // still does and nothing on the control says it. Gated on the hint being live, so it
                // goes with the technology the player has just researched.
                NodeHints.Add(
                    vtable,
                    ModStrings.HintMissingTechnology,
                    UiActions.SelectToggle,
                    0,
                    () => AgeWidgets.Hinted(hint)
                );
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

        /// <summary>
        /// One node per cell, in the order the game drew them, and no rows at all.
        ///
        /// This is the DEFAULT for a band the game draws side by side - a window's bottom row of
        /// buttons, a strip of filter toggles, a grid of stat figures, a grid of cards. Those members
        /// are peers of one kind, so a sideways move buys nothing and the wrap points are a rendering
        /// accident: the player walks them with one key. <see cref="Emit"/> is for the other case only -
        /// a real table, whose columns are a fact of the game's own data.
        ///
        /// The reading ORDER is still the drawn one (<see cref="AgeLayout.Rows"/> bands the cells and
        /// they are emitted band by band, left to right), which is why this takes the same list
        /// <see cref="Emit"/> does and differs only in not opening a row.
        ///
        /// Flattening happens at the HOST's own emit call: a collector shared between screens fills a
        /// caller-owned list and belongs to no host's layout, so the host swaps which emitter it calls
        /// rather than the collector growing a mode flag.
        /// </summary>
        public static void EmitLinear(GraphBuilder builder, List<Cell> cells)
        {
            foreach (List<Cell> row in AgeLayout.Rows(cells, CellWidget))
            {
                for (int i = 0; i < row.Count; i++)
                {
                    builder.AddItem(row[i].Id, row[i].Vtable);
                }
            }
        }

        /// <summary>
        /// The cells in the order the game DREW them, flattened into one list rather than emitted.
        ///
        /// For the host that has to reorder that reading order before it declares anything - the ship
        /// designer's slots, which are grouped by the type of module each takes and keep the drawn order
        /// only inside one type. Everyone else emits straight from <see cref="EmitLinear"/>, which is
        /// this walk and the emit in one.
        /// </summary>
        public static void Drawn(List<Cell> cells, List<Cell> into)
        {
            into.Clear();
            foreach (List<Cell> row in AgeLayout.Rows(cells, CellWidget))
            {
                for (int i = 0; i < row.Count; i++)
                {
                    into.Add(row[i]);
                }
            }
        }

        /// <summary>
        /// The same cells as ONE row, in the order they were declared, whatever their rectangles say.
        ///
        /// <see cref="Emit"/> reads the rows off the screen, which is right wherever the game's layout
        /// IS the reading order. It is wrong where the game WRAPS one line of related controls onto
        /// several - a strip of icons that goes to a second line as soon as there is a third of them
        /// (the election's representative table) - because which line an icon landed on is a fact about
        /// the box it was drawn in, not about the thing being read. A screen that knows the set is one
        /// line says so here, and the declaration order is then the order across it.
        ///
        /// <paramref name="rowKey"/> and <paramref name="positions"/> are <see cref="GraphBuilder.StartRow"/>'s
        /// own two arguments, passed through for the family of rows that repeats down a panel: a shared
        /// key gives the column-preserving step between them, and <paramref name="positions"/> false is
        /// for a row whose members are a THING and the things hanging off it rather than a bar of
        /// choices - counting "1 of 3" across a winner and its two badges answers a question nobody
        /// asked. Such a row says where it sits as a row instead
        /// (<see cref="ES2Access.Core.UI.Graph.NodeVtable.Row"/>).
        /// </summary>
        public static void EmitRow(
            GraphBuilder builder,
            List<Cell> cells,
            object rowKey = null,
            bool positions = true
        )
        {
            if (cells.Count == 0)
            {
                return;
            }

            builder.StartRow(rowKey, positions);
            foreach (Cell cell in cells)
            {
                builder.AddItem(cell.Id, cell.Vtable);
            }

            builder.EndRow();
        }

        /// <summary>
        /// One half of a panel the game draws as a band of switches over a list of things, under the word
        /// the mod puts over it: the star system's constructibles, the ship hangar, the mod library.
        ///
        /// <paramref name="regions"/> is the WHOLE panel's answer, asked once by the caller and handed to
        /// both halves: a lone region is a region jump that swallows the key silently, so a panel drawing
        /// only one of its halves declares neither.
        ///
        /// <paramref name="emit"/> is how the half reads - <see cref="Emit"/> for the band the game drew
        /// as one line, <see cref="EmitLinear"/> for the list under it, <see cref="EmitRow"/> for a band
        /// the layout box wrapped that is still one line.
        ///
        /// A half the stop's own name already covers takes no word of its own - that half is a bare
        /// <see cref="GraphBuilder.SetRegion"/> at the caller, not a call here.
        /// </summary>
        public static void EmitRegion(
            GraphBuilder builder,
            object regionKey,
            string nameKey,
            bool regions,
            List<Cell> cells,
            Action<GraphBuilder, List<Cell>> emit
        )
        {
            if (cells.Count == 0)
            {
                return;
            }

            if (regions)
            {
                builder.SetRegion(regionKey);
                builder.PushContext(ModStrings.Get(nameKey));
            }

            try
            {
                emit(builder, cells);
            }
            finally
            {
                if (regions)
                {
                    builder.PopContext();
                }
            }
        }

        /// <summary>The emitters as arguments, so a caller of <see cref="EmitRegion"/> names which one its
        /// half reads by rather than passing a flag that has to be decoded there.</summary>
        public static readonly Action<GraphBuilder, List<Cell>> AsDrawnRows = Emit;
        public static readonly Action<GraphBuilder, List<Cell>> OnePerRow = EmitLinear;

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
