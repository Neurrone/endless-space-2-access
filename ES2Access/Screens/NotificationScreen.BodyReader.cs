using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using Line = ES2Access.UI.EmpireDossier.DrawnLine;

namespace ES2Access.Screens
{
    /// <summary>What a popup that draws its own content says: its rows and cards, the regions they
    /// fall in, the tooltips grouped onto them, and the dossier it can open beside itself.</summary>
    public sealed partial class NotificationScreen
    {
        // ---- what a popup that draws its own content says ----

        /// <summary>
        /// The content area of a popup that never filled its description in, as rows: what it drew,
        /// read the way it is drawn.
        ///
        /// One row per thing, top to bottom. A control the popup drew in there - the card that opens
        /// the technology it just finished - is walked in its place among the rows, because that is
        /// where the player sees it. Everything else is text the game wrote out, and text is grouped by
        /// WHICH TOOLTIP EXPLAINS IT rather than by the line it is drawn on: the two things a
        /// technology unlocks are drawn side by side in one band, each with its own explaining tooltip,
        /// and banding them by rectangle would read the pair as one row of four labels. A group is one
        /// row - the thing's name and what kind of thing it is, read as the one line - carrying the
        /// tooltip its own widget holds. Text the game hung no tooltip on falls back to the band it is
        /// drawn in, which is the ordinary answer for a paragraph.
        ///
        /// A caption that belongs to a control declared elsewhere is left out: the control already says
        /// it, and a popup's bottom row would otherwise read twice.
        ///
        /// The content is walked in the CARDS the popup drew it in - each of them a region of its own,
        /// so that the rows under "Next Research" audibly belong to the next technology rather than to
        /// the one that just finished. Which cards those are is the game's own grouping: the card each
        /// row was drawn in is <see cref="AgeWidgets.Ancestor"/> of the cards' own container.
        ///
        /// <paramref name="lines"/> are the lines of a table the popup stamped out of a prefab, where it
        /// has one (<see cref="TableLines"/>). Text drawn inside one of them is that LINE's row rather
        /// than a row of its own, because a line of a table is one thing to the player - "Kepler, the
        /// Inspector sold your Xenobiology Lab, saving 4 dust" - and the tooltip-then-band grouping
        /// below would otherwise read one line as four.
        /// </summary>
        private static void BuildDrawnBody(
            GraphBuilder builder,
            NotificationWindow window,
            List<Control> controls,
            List<Control> inside,
            AgeTransform words,
            List<AgeTransform> lines = null
        )
        {
            List<Item> items = new List<Item>();
            foreach (Control control in inside)
            {
                items.Add(new Item { Widget = control.Widget, Control = control, IsControl = true });
            }

            foreach (List<Line> row in DrawnRows(window, controls, words, lines))
            {
                AgeTransform group = GroupOf(row, lines);
                items.Add(
                    new Item { Widget = group ?? Anchor(row), Lines = row, Group = group }
                );
            }

            if (items.Count == 0)
            {
                return;
            }

            items.Sort(DownThePage);
            List<AgeTransform> cards = Cards(items);
            MinorFactionCard met = FirstContactCard(window);
            object region = null;

            // One node per row, whatever the popup laid out side by side. The things a technology
            // unlocks, the technologies a panel suggests, the outcomes a choice offers are peers of one
            // kind: the wrap points are the content box's doing, so a sideways move buys nothing and the
            // player walks the whole content with one key. Which CARD each item was drawn in is still
            // the game's own grouping and still a region (see Cards).
            for (int index = 0; index < items.Count; index++)
            {
                Item item = items[index];
                object here = cards == null ? BodyRegion : RegionOf(cards, item.Widget);
                if (!Equals(here, region))
                {
                    builder.SetRegion(here);
                    region = here;
                }

                ControlId id = item.IsControl
                    ? Declare(builder, item.Control)
                    : AddRow(builder, item.Lines, index, item.Group, met);
                if (index == 0)
                {
                    builder.SetStart(id);
                }
            }
        }

        private static ControlId Declare(GraphBuilder builder, Control control)
        {
            Add(builder, control);
            return IdOf(control);
        }

        /// <summary>
        /// The cards the popup drew its content in, or null where it drew just the one thing.
        ///
        /// The game's own grouping is the answer, and it is found rather than named: everything drawn
        /// in the content area sits somewhere under one container, and the children of THAT container
        /// are the cards - the completed technology with its lore and its unlocks, the next one with
        /// its own. Nothing here knows what a technology is; it knows that a popup which drew a captioned
        /// control put that caption at the head of something, and that what the caption heads is the
        /// group the game laid the control out in.
        ///
        /// Two conditions, both load-bearing. There must be MORE THAN ONE card, because a lone region is
        /// a jump key that swallows silently - a popup drawing a single table of rows keeps the one body
        /// region it always had. And one of the cards must hold a control the popup captioned, which is
        /// what tells a card apart from the pieces any panel is assembled out of: the construction
        /// report's header row and its lines are two such pieces and one report, and splitting them
        /// would announce a boundary the player cannot see.
        ///
        /// A card HOLDS what it heads. Where what the container holds is the row ITSELF - a survey's
        /// four party lines, each of them a button the game laid out inside the same table - the
        /// container is the row rather than a card around it, and calling every row a region of its own
        /// puts a jump boundary between lines the player reads as one list.
        /// </summary>
        private static List<AgeTransform> Cards(List<Item> items)
        {
            AgeTransform common = null;
            bool captioned = false;
            foreach (Item item in items)
            {
                common = common == null ? item.Widget : Meeting(common, item.Widget);
                captioned = captioned || item.IsControl;
            }

            if (!captioned || common == null)
            {
                return null;
            }

            List<AgeTransform> cards = new List<AgeTransform>();
            foreach (Item item in items)
            {
                AgeTransform card = AgeWidgets.Ancestor(item.Widget, common);
                if (card == null || ReferenceEquals(card, item.Widget))
                {
                    // A row drawn outside the cards - one of them containing all the others, say -
                    // means the popup is not laid out as cards at all, and the body is the one region
                    // it has always been.
                    return null;
                }

                if (cards.IndexOf(card) < 0)
                {
                    cards.Add(card);
                }
            }

            return cards.Count > 1 ? cards : null;
        }

        private static object RegionOf(List<AgeTransform> cards, AgeTransform widget)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (AgeWidgets.Under(widget, cards[i]))
                {
                    return "notification:body/" + i + "/" + cards[i].name;
                }
            }

            return BodyRegion;
        }

        /// <summary>Where two widgets' chains meet - the innermost thing the popup drew both of them
        /// inside.</summary>
        private static AgeTransform Meeting(AgeTransform first, AgeTransform second)
        {
            AgeTransform at = first;
            for (int depth = 0; at != null && depth < MaxAncestors; depth++)
            {
                if (AgeWidgets.Under(second, at))
                {
                    return at;
                }

                at = at.Parent;
            }

            return null;
        }

        /// <summary>One drawn row of the content area: what it says, and the tooltip the game hung on
        /// it. A tooltip that only repeats the words already in the row - the lore paragraph, which the
        /// game both prints and offers on hover - is not a second thing to say and is dropped, so the
        /// paragraph is read once.
        ///
        /// <paramref name="group"/> is the table line the row was read out of, where it is one. A line
        /// of a table hangs an explanation on each of its pieces - what the improvement that was sold
        /// does, what the action it names means - and the row that reads the whole line carries ALL of
        /// them, in the order the game drew them, because the row is now the only place those
        /// explanations are reachable from.</summary>
        private static ControlId AddRow(
            GraphBuilder builder,
            List<Line> row,
            int index,
            AgeTransform group = null,
            MinorFactionCard card = null
        )
        {
            List<Line> it = row;
            string caption = CardCaption(row, card);
            List<AgeTooltip> explaining = group == null
                ? Single(Explains(it[0].Tooltip, RowText(it)))
                : Explaining(group, RowText(it));
            // Through the sink: the row points at the LAST explanation drawn along it, which is the one
            // a hover on the line raises, and every other one used to be a section on this row - words
            // the row promised and the game would only ever draw for the one it points at. Each becomes
            // an entry of its own, aimed at the piece a mouse would have pointed at.
            TooltipChildren.Carried carried = TooltipChildren.Split(explaining);
            AgeTooltip tooltip = carried.Own;
            AgeTransform hover = tooltip == null ? null : AgeWidgets.TooltipOwner(tooltip);

            // A table line the game wired a click to is a control the player works, exactly as it is in
            // a popup whose captions let the same lines read as a sheet (<see cref="RowNode"/>) - so it
            // says so and Enter is the game's own click, whichever reading the popup's captions bought.
            AgeTransform clicked = group != null && Wired(group) ? group : null;
            NodeVtable vtable = clicked == null
                ? new NodeVtable
                {
                    // No role word and no state: this is something the game wrote down for the player to
                    // read, not a control they work.
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => RowText(it)),
                    },
                }
                : GraphNodes.Button(
                    () => RowText(it),
                    () => AgeWidgets.Press(clicked),
                    () => AgeWidgets.Operable(clicked)
                );

            if (caption != null)
            {
                // A figure the card draws with its caption on a bare ICON beside it: the words are
                // read as the value and the game's own title for them as the name, so the row says
                // "Ally, None" rather than "None". Declared for the two rows this card has, not by a
                // rule over every popup - the pairing is a fact about this prefab.
                string word = caption;
                vtable.Announcements.Insert(0, GraphNodes.LabelPart(() => word));
                vtable.Announcements[1] = GraphNodes.ValuePart(() => RowText(it));
            }

            vtable.Sections = GraphNodes.Sections(GraphNodes.TooltipSection(tooltip));
            vtable.OnFocusVisual =
                hover == null
                    ? AgeWidgets.ReleasePointer
                    : () => PointerFocus.MoveTo(hover, tooltip);
            vtable.OnBlurVisual = AgeWidgets.ReleasePointer;
            vtable.PointsAt = () => hover == null ? null : tooltip;

            AgeTransform named = group ?? it[0].Widget;
            string key = "notification:body/" + index + "/" + named.name;
            ControlId id = ControlId.For(named, key);
            TooltipChildren.Declare(
                builder,
                Nodes.Drawn(id, vtable, named),
                key,
                carried.Children
            );
            return id;
        }

        /// <summary>The card the "you have met a minor civilization" popup draws, or null on every
        /// other popup.</summary>
        private static MinorFactionCard FirstContactCard(NotificationWindow window)
        {
            try
            {
                MinorEmpireMetNotificationWindow met = window as MinorEmpireMetNotificationWindow;
                return met == null ? null : met.MinorFactionCard;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The game's own caption for one of the two rows of that card whose words are a VALUE with no
        /// name.
        ///
        /// The card draws "None" and "UNKNOWN" beside bare icons and puts the caption on the icons'
        /// tooltips (<c>MinorFactionCard.Refresh</c> :74-111), so the generic reading - which names a
        /// row by the words in it - says the value and never what it is of. The two titles are the same
        /// ones the minor-diplomacy window's own rows are captioned by.
        /// </summary>
        private static string CardCaption(List<Line> row, MinorFactionCard card)
        {
            try
            {
                if (card == null || row == null)
                {
                    return null;
                }

                if (Holds(row, card.AllyLabel))
                {
                    return AgeText.Title("%MinorFactionCurrentAllyTitle");
                }

                return Holds(row, card.RelationLabel)
                    ? AgeText.Title("%MinorFactionRelationTitle")
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool Holds(List<Line> row, AgePrimitiveLabel label)
        {
            AgeTransform at = label == null ? null : label.AgeTransform;
            for (int i = 0; at != null && i < row.Count; i++)
            {
                if (ReferenceEquals(row[i].Widget, at))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<AgeTooltip> Single(AgeTooltip tooltip)
        {
            List<AgeTooltip> one = new List<AgeTooltip>();
            if (tooltip != null)
            {
                one.Add(tooltip);
            }

            return one;
        }

        /// <summary>The tooltips inside one table line that say something the line does not already
        /// say.</summary>
        private static List<AgeTooltip> Explaining(AgeTransform group, string text)
        {
            List<AgeTooltip> kept = new List<AgeTooltip>();
            foreach (AgeTooltip tooltip in Tooltips(group))
            {
                if (Explains(tooltip, text) != null)
                {
                    kept.Add(tooltip);
                }
            }

            return kept;
        }

        /// <summary>The text the popup drew in its content area, grouped into the rows it reads as.
        /// </summary>
        private static List<List<Line>> DrawnRows(
            NotificationWindow window,
            List<Control> controls,
            AgeTransform words,
            List<AgeTransform> tableLines = null
        )
        {
            List<List<Line>> rows = new List<List<Line>>();
            AgeTransform root = Root(window);
            if (root == null)
            {
                return rows;
            }

            List<Line> lines = new List<Line>();
            Read(root, lines, null, 0);

            List<AgeTransform> title = TitleBar(window, controls);
            List<AgeTransform> buttons = ButtonBar(controls);
            AgeTransform dossier = Dossier(window);
            List<Line> loose = new List<Line>();
            List<AgeTooltip> explained = new List<AgeTooltip>();
            Dictionary<AgeTooltip, List<Line>> groups = new Dictionary<AgeTooltip, List<Line>>();
            List<AgeTransform> banded = new List<AgeTransform>();
            Dictionary<AgeTransform, List<Line>> byLine =
                new Dictionary<AgeTransform, List<Line>>();
            List<Line> rest = new List<Line>();
            foreach (Line line in lines)
            {
                if (
                    !InBody(line.Widget, title, buttons)
                    // A line inside a panel the popup has folded away is not a line: the detail of a
                    // damage report sits behind a "+" at alpha 0 and keeps every word it last held.
                    // Asked of the label itself rather than of where it is measured, since a clipped
                    // line is measured at the scrolling window it shows through.
                    || !Painted(line.Owner, root)
                    || PartOf(line.Widget, controls)
                    || IsWords(line, words)
                    || AgeWidgets.Under(line.Widget, dossier)
                )
                {
                    continue;
                }

                AgeTransform group = In(line.Widget, tableLines);
                if (group == null)
                {
                    rest.Add(line);
                    continue;
                }

                List<Line> pieces;
                if (!byLine.TryGetValue(group, out pieces))
                {
                    pieces = new List<Line>();
                    byLine.Add(group, pieces);
                    banded.Add(group);
                }

                pieces.Add(line);
            }

            // A line that drew ONE thing is one thing wherever it sits: the empires in an alliance are
            // a line each and are drawn side by side, and reading them a line at a time would say
            // "Sophons" three rows running. Only a line that drew SEVERAL pieces is a row of its own,
            // because those pieces are one fact between them.
            for (int i = banded.Count - 1; i >= 0; i--)
            {
                List<Line> pieces = byLine[banded[i]];
                if (pieces.Count < 2)
                {
                    rest.AddRange(pieces);
                    byLine.Remove(banded[i]);
                    banded.RemoveAt(i);
                }
            }

            foreach (Line line in rest)
            {
                if (line.Tooltip == null)
                {
                    loose.Add(line);
                    continue;
                }

                List<Line> group;
                if (!groups.TryGetValue(line.Tooltip, out group))
                {
                    group = new List<Line>();
                    groups.Add(line.Tooltip, group);
                    explained.Add(line.Tooltip);
                }

                group.Add(line);
            }

            foreach (AgeTransform line in banded)
            {
                List<Line> pieces = byLine[line];
                pieces.Sort(AcrossTheRow);
                rows.Add(pieces);
            }

            foreach (AgeTooltip tooltip in explained)
            {
                List<Line> group = groups[tooltip];
                group.Sort(DownTheRow);
                rows.Add(group);
            }

            foreach (List<Line> band in AgeLayout.Rows(loose, LineWidget))
            {
                rows.Add(band);
            }

            return rows;
        }

        /// <summary>The dossier the popup has open beside itself, where it has one open. Its lines are a
        /// region of their own (<see cref="BuildEmpireInfo"/>), so the body must not read them a second
        /// time: the panel is drawn level with the content, and a popup that draws its own content would
        /// otherwise say the whole dossier twice.</summary>
        private static AgeTransform Dossier(NotificationWindow window)
        {
            NegotiationEmpireInfoPanel panel = InfoPanel(window);
            return panel == null || !Open(panel) ? null : panel.AgeTransform;
        }

        /// <summary>Which of the table's lines this widget was drawn inside, or null where it was drawn
        /// outside all of them - a caption band, a totals footer.</summary>
        private static AgeTransform In(AgeTransform widget, List<AgeTransform> lines)
        {
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                if (AgeWidgets.Under(widget, lines[i]))
                {
                    return lines[i];
                }
            }

            return null;
        }

        /// <summary>The table line a whole row was read out of - all of its pieces and nothing else's.
        ///
        /// A row of one piece is normally not a line: see the reading in <see cref="DrawnRows"/>. The
        /// exception is a line the game WIRED A CLICK to - the systems whose construction queue has run
        /// dry, each drawn as its name and nothing else and each a button that opens that system. Such a
        /// line is the control the row is, so the row has to know which widget it came out of whether the
        /// game wrote one word on it or four.</summary>
        private static AgeTransform GroupOf(List<Line> row, List<AgeTransform> lines)
        {
            AgeTransform group = In(row[0].Widget, lines);
            for (int i = 1; group != null && i < row.Count; i++)
            {
                if (!ReferenceEquals(In(row[i].Widget, lines), group))
                {
                    return null;
                }
            }

            return row.Count > 1 || (group != null && Wired(group)) ? group : null;
        }

        /// <summary>One thing drawn in the content area: a control the popup added there, or a row of
        /// text it wrote.</summary>
        private struct Item
        {
            public AgeTransform Widget;
            public Control Control;
            public bool IsControl;
            public List<Line> Lines;

            /// <summary>The table line this row was read out of, where the popup drew one.</summary>
            public AgeTransform Group;
        }

        private static readonly Comparison<Item> DownThePage = delegate(Item a, Item b)
        {
            return AgeLayout.TopThenLeft(a.Widget, b.Widget);
        };

        private static readonly Comparison<Line> DownTheRow = delegate(Line a, Line b)
        {
            return AgeLayout.TopThenLeft(a.Widget, b.Widget);
        };

        /// <summary>Where a row of text is drawn: the widget the game hung its tooltip on where there
        /// is one - which is the whole thing, not the first words in it - else the first line.</summary>
        private static AgeTransform Anchor(List<Line> row)
        {
            AgeTransform holder =
                row[0].Tooltip == null ? null : AgeWidgets.TooltipOwner(row[0].Tooltip);
            return holder ?? row[0].Widget;
        }

        /// <summary>The tooltip, unless its words are the words already being read - the game both
        /// prints a technology's description under its card and offers the same text on hover, and
        /// saying it twice is not saying it better. A tooltip the game assembles as it draws it has
        /// nothing to compare, and is always kept - unless it is one the game could never draw
        /// anything for, which explains nothing to anybody.</summary>
        private static AgeTooltip Explains(AgeTooltip tooltip, string text)
        {
            if (AgeWidgets.NeverDraws(tooltip))
            {
                return null;
            }

            // Only a tooltip whose words ARE its content field can repeat the row. A class-backed
            // one is assembled at draw time and its content holds the row's own words as authoring
            // leftovers - the quest reward's improvement card carries the reward's name there - so
            // comparing that field threw away the one place the reward was explained.
            if (tooltip != null && AgeWidgets.Readable(tooltip) == null)
            {
                return tooltip;
            }

            string written = AgeText.Tooltip(tooltip);
            if (string.IsNullOrEmpty(written) || string.IsNullOrEmpty(text))
            {
                return tooltip;
            }

            if (string.Equals(written, text))
            {
                return null;
            }

            string said = TextUtil.LettersAndDigits(text);
            string offered = TextUtil.LettersAndDigits(written);
            if (said.Length == 0 || offered.Length == 0)
            {
                return tooltip;
            }

            return said.IndexOf(offered, StringComparison.Ordinal) >= 0
                || offered.IndexOf(said, StringComparison.Ordinal) >= 0
                ? null
                : tooltip;
        }

        /// <summary>Whether this widget is part of a control that is being declared in its own right,
        /// whose caption already says what the widget says.</summary>
        private static bool PartOf(AgeTransform widget, List<Control> controls)
        {
            AgeTransform at = widget;
            for (int depth = 0; at != null && depth < MaxAncestors; depth++)
            {
                foreach (Control control in controls)
                {
                    if (ReferenceEquals(control.Widget, at))
                    {
                        return true;
                    }
                }

                at = at.Parent;
            }

            return false;
        }

        // ---- the dossier a popup can open beside itself ----

        /// <summary>
        /// The panel a popup opens when the player ticks Empire Information: who this empire is, what
        /// its faction is about, what it is good at. It is somewhere else to be rather than more of the
        /// popup - the game draws it as a sheet of its own, beside the popup rather than inside it - so
        /// it is A REGION OF ITS OWN while the box is ticked, and stops existing when it is unticked.
        /// The tick box that opened it is still what closes it, so the cursor is never left standing in
        /// a panel that has gone. A region of the CONTENT stop rather than a stop of its own: the panel
        /// is what the popup is showing, walked with the rest of it, with Alt+Down/Up there only to
        /// cross it in one step.
        ///
        /// Its contents are read off what is drawn rather than out of the panel's fields: it is a page
        /// of prose and headings, a different set of them per empire (a computer-run rival adds what it
        /// is like to deal with, an empire you have met adds who else it has met), and every one of
        /// them is a line the game has already written and laid out. One drawn line is one row here,
        /// which is also how it scrolls - the sheet is taller than its viewport, and the cursor brings
        /// itself into view.
        ///
        /// A faction trait says what it does in a tooltip the game assembles as it draws it - a Class
        /// tooltip, per the rule in <see cref="GraphNodes.ModeFor"/> - so a trait's row indicates
        /// having one rather than reading it outright, and carries the drawn tooltip as review-buffer
        /// content regardless.
        /// </summary>
        private static void BuildEmpireInfo(GraphBuilder builder, NotificationWindow window)
        {
            NegotiationEmpireInfoPanel panel = InfoPanel(window);
            if (!Open(panel))
            {
                return;
            }

            EmpireDossier.Build(builder, panel, "notification:empire-info/", InfoRegion);
        }

        private static readonly Func<Line, AgeTransform> LineWidget = EmpireDossier.LineWidget;

        /// <summary>What one drawn line says - <see cref="EmpireDossier.RowText"/>, which is where the
        /// rule lives now that the dossier reader is shared with the negotiation table.</summary>
        private static string RowText(List<Line> row)
        {
            return EmpireDossier.RowText(row);
        }

        /// <summary>
        /// Everything the popup has drawn under <paramref name="widget"/>, appended to
        /// <paramref name="lines"/>.
        ///
        /// One build asks this of the popup's ROOT three times - the sheet reader looking for a table,
        /// the reader of what is drawn outside that table, and the reader of the rows a popup with no
        /// table has - and the answer cannot differ between them: the walk reads the widget tree and
        /// changes nothing in it. So the root walk is made once a frame and the askers are handed a
        /// copy (<see cref="Line"/> is a value, so a copy is a copy and nobody can disturb anybody
        /// else's list).
        ///
        /// Held for the frame and no longer, and re-walked the moment the root changes: a popup that
        /// has been rebound to the next notification has drawn different words under the same root.
        /// </summary>
        private static void Read(
            AgeTransform widget,
            List<Line> lines,
            AgeTooltip inherited,
            int depth
        )
        {
            if (inherited != null || depth != 0)
            {
                Walk(widget, lines, inherited, depth);
                return;
            }

            int frame = UnityEngine.Time.frameCount;
            if (_drawnFrame != frame || !ReferenceEquals(_drawnRoot, widget))
            {
                Drawn.Clear();
                Walk(widget, Drawn, null, 0);
                _drawnFrame = frame;
                _drawnRoot = widget;
            }

            for (int i = 0; i < Drawn.Count; i++)
            {
                lines.Add(Drawn[i]);
            }
        }

        private static readonly List<Line> Drawn = new List<Line>();

        private static int _drawnFrame = -1;

        private static AgeTransform _drawnRoot;

        /// <summary>
        /// Everything a subtree is showing, in the order it is laid out - hoisted to
        /// <see cref="EmpireDossier.Read"/>, which the popup body and the dossier both walk with.
        ///
        /// Each line is then measured where the popup DRAWS it (<see cref="AgeWidgets.Clipped"/>). A
        /// paragraph the game laid out taller than the scrolling window it shows it through - the quest
        /// popup's lore - keeps a rectangle that runs off the bottom of the popup, and this screen works
        /// out its content area as what lies between the two strips: measured whole, such a paragraph is
        /// level with the buttons along the bottom and is dropped from the body altogether. The line
        /// still says all of it - the game holds the whole string whatever it shows - and it is still
        /// the label's own line; only where it is measured changes.
        /// </summary>
        private static void Walk(
            AgeTransform widget,
            List<Line> lines,
            AgeTooltip inherited,
            int depth
        )
        {
            int from = lines.Count;
            EmpireDossier.Read(widget, lines, inherited, depth);
            for (int i = from; i < lines.Count; i++)
            {
                Line line = lines[i];
                AgeTransform shown = AgeWidgets.Clipped(line.Widget);
                if (!ReferenceEquals(shown, line.Widget))
                {
                    line.Widget = shown;
                    lines[i] = line;
                }
            }
        }

        /// <summary>Whether a drawn line is the popup's own words, which lead the body as a row of their
        /// own and are not among what it drew. Asked of the label itself (<c>Owner</c>) as well as of
        /// where the line is measured, because a description shown through a scrolling window is
        /// measured at the window.</summary>
        private static bool IsWords(Line line, AgeTransform words)
        {
            return ReferenceEquals(line.Widget, words) || ReferenceEquals(line.Owner, words);
        }

        /// <summary>The dossier panel a popup carries, whichever popup it is - the same panel serves
        /// the introduction, a diplomatic offer and the negotiation table.</summary>
        private static NegotiationEmpireInfoPanel InfoPanel(NotificationWindow window)
        {
            return EmpireDossier.Panel(window == null ? null : window.gameObject);
        }

        private static bool Open(NegotiationEmpireInfoPanel panel)
        {
            return EmpireDossier.Open(panel);
        }

    }
}
