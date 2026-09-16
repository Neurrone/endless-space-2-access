using System;
using System.Collections.Generic;
using System.Text;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;
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
            List<AgeTransform> lines,
            Headings heads,
            ref int open
        )
        {
            List<Item> items = new List<Item>();
            foreach (Control control in inside)
            {
                items.Add(new Item { Widget = control.Widget, Control = control, IsControl = true });
            }

            foreach (List<Line> row in DrawnRows(window, controls, words, lines, heads))
            {
                AgeTransform group = GroupOf(row, lines);
                items.Add(
                    new Item { Widget = group ?? Anchor(row), Lines = row, Group = group }
                );
            }

            // A block the popup drew nothing in is still a block: placed by its heading, which is the
            // only part of it on screen, and given the one row there is to put in it. Only while the
            // popup is DRAWING that heading - a report folded away keeps every word of its headings at
            // alpha 0, and a block nobody can see is not a block the player is in.
            AgeTransform root = Root(window);
            List<int> empty = heads == null ? null : heads.Empty();
            for (int i = 0; empty != null && i < empty.Count; i++)
            {
                AgeTransform heading = heads.Widget(empty[i]);
                // Content: whether the popup is DRAWING this heading, which decides whether there is a
                // block to be in at all. A report folded away keeps its headings Visible at alpha 0
                // (docs/notifications.md, the fade), so the gate's visibility walk answers yes and a
                // collapsed report would name two blocks and say "None" in both.
                if (Painted(heading, root))
                {
                    items.Add(new Item { Widget = heading, IsEmpty = true, Which = empty[i] });
                }
            }

            if (items.Count == 0)
            {
                Close(builder, ref open);
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                item.Chain = Chain(item.Widget, root);
                items[i] = item;
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

                // The block this row was drawn in, where the popup captioned one. A heading names what
                // it heads: the rows under it are read inside a level of their own, announced as focus
                // enters it, and jumped to as a region of its own - so the caption is heard once, where
                // the player meets what it is about, rather than as a row in front of it.
                int under = item.IsEmpty
                    ? item.Which
                    : heads == null ? -1 : heads.Over(item.Widget);
                if (under != open)
                {
                    Close(builder, ref open);
                    Open(builder, heads, under, ref open);
                    region = builder.Region;
                }

                object here =
                    open >= 0
                        ? heads.Region(open)
                        : cards == null ? BodyRegion : RegionOf(cards, item.Widget);
                if (!Equals(here, region))
                {
                    builder.SetRegion(here);
                    region = here;
                }

                ControlId id = item.IsEmpty
                    ? Nothing(builder, item.Widget, heads.Region(item.Which))
                    : item.IsControl
                        ? Declare(builder, item.Control)
                        : AddRow(builder, item.Lines, index, item.Group, met, heads);
                if (index == 0)
                {
                    builder.SetStart(id);
                }
            }

            Close(builder, ref open);
        }

        private static ControlId Declare(GraphBuilder builder, Control control)
        {
            Add(builder, control);
            return IdOf(control);
        }

        /// <summary>The one row of a block the game filled with nothing, standing under the heading that
        /// names it (owner ruling 2026-08-28, the same answer the battle report's empty wreckage blocks
        /// give). "None" rather than the figure: the heading already carries it - "Improvements
        /// destroyed: 0" - and a row repeating it reads the nought twice.</summary>
        private static ControlId Nothing(GraphBuilder builder, AgeTransform heading, object key)
        {
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => ModStrings.Get(ModStrings.None)),
                },
            };
            ControlId id = ControlId.For(heading, key + "/nothing");
            builder.AddItem(Nodes.Drawn(id, vtable, heading));
            return id;
        }

        /// <summary>
        /// The headings a popup declared over its content, and what became of each.
        ///
        /// A heading over a BLOCK names the block and is no row of its own - the standing rule for
        /// every drawn caption (<see cref="Captions"/>, which declares it and keeps its one exception:
        /// a caption carrying an explanation stays a row as well, inside the block, because a level's
        /// name has no review buffer behind it). A heading over a single VALUE names the row that
        /// value reads as instead, which is the only thing a level would have in it.
        ///
        /// A block the game filled with NOTHING is not a third case (owner ruling 2026-08-28): the
        /// heading still names it and the block gets the one row there is to put in it, so a player
        /// steps into the same shape however the game's data came out (the battle report's wreckage
        /// blocks, captioned "Improvements Destroyed: 0" exactly as this popup's are). A value whose
        /// label the popup is NOT drawing is the one place a heading keeps its own row: there is no
        /// row for it to name.
        ///
        /// Which is which is answered once per build, while the drawn lines are being read
        /// (<see cref="Fill"/>), because both answers need the same list of what the popup painted.
        /// </summary>
        private sealed class Headings
        {
            private readonly IList<Heading> _declared;

            private readonly bool[] _fills;

            /// <summary>Labels a CONTROL took its name from - the title of the panel a fold unfolds.
            /// The control says it, so the label is not a row as well.</summary>
            private readonly List<AgeTransform> _named;

            /// <summary>Tables whose lines the popup drew with no name on them.</summary>
            private readonly IList<AgeTransform> _wordless;

            public Headings(
                IList<Heading> declared,
                List<AgeTransform> named,
                IList<AgeTransform> wordless
            )
            {
                _declared = declared;
                _fills = new bool[declared.Count];
                _named = named;
                _wordless = wordless;
            }

            /// <summary>The tooltip that says what a table line stands for, where the popup wrote it
            /// nowhere on the line itself - the game object hung on the line is the only word for it.
            /// </summary>
            public AgeTooltip Stands(AgeTransform group)
            {
                for (int i = 0; group != null && i < _wordless.Count; i++)
                {
                    if (AgeWidgets.Under(group, _wordless[i]))
                    {
                        return AgeWidgets.Raw(group);
                    }
                }

                return null;
            }

            /// <summary>Whether this drawn line is a heading or a name a control already says, and so
            /// is not a row of the body: always for a heading over a block, which names the block, and
            /// for one over a value only while that value is drawn for it to name.</summary>
            public bool Silent(AgeTransform widget)
            {
                for (int i = 0; i < _named.Count; i++)
                {
                    if (ReferenceEquals(_named[i], widget))
                    {
                        return true;
                    }
                }

                for (int i = 0; i < _declared.Count; i++)
                {
                    AgePrimitiveLabel heading = _declared[i].Label;
                    if (heading != null && ReferenceEquals(heading.AgeTransform, widget))
                    {
                        return !_declared[i].Value || _fills[i];
                    }
                }

                return false;
            }

            /// <summary>The headings whose block the popup drew nothing in, in declaration order: each
            /// one still names its block, and the block is given its one row.</summary>
            public List<int> Empty()
            {
                List<int> empty = new List<int>();
                for (int i = 0; i < _declared.Count; i++)
                {
                    if (!_fills[i] && !_declared[i].Value && _declared[i].Label != null)
                    {
                        empty.Add(i);
                    }
                }

                return empty;
            }

            /// <summary>The heading itself - what an empty block is placed by, since nothing else of it
            /// is drawn.</summary>
            public AgeTransform Widget(int index)
            {
                return _declared[index].Label.AgeTransform;
            }

            /// <summary>Whether a heading is one at all this build: something the popup is drawing under
            /// it. Asked of every painted line and control before any of them is placed.</summary>
            public bool Heading(AgeTransform widget)
            {
                for (int i = 0; i < _declared.Count; i++)
                {
                    AgePrimitiveLabel heading = _declared[i].Label;
                    if (heading != null && ReferenceEquals(heading.AgeTransform, widget))
                    {
                        return true;
                    }
                }

                return false;
            }

            public void Fill(AgeTransform widget)
            {
                if (Heading(widget))
                {
                    return;
                }

                for (int i = 0; i < _declared.Count; i++)
                {
                    if (!_fills[i] && AgeWidgets.Under(widget, _declared[i].Block))
                    {
                        _fills[i] = true;
                    }
                }
            }

            /// <summary>The heading whose BLOCK this was drawn in, or -1 - a heading over a single value
            /// names the row itself (<see cref="Names"/>) rather than opening a level around it.
            /// </summary>
            public int Over(AgeTransform widget)
            {
                for (int i = 0; i < _declared.Count; i++)
                {
                    if (!_declared[i].Value && AgeWidgets.Under(widget, _declared[i].Block))
                    {
                        return i;
                    }
                }

                return -1;
            }

            /// <summary>The caption naming this row, for a heading drawn over one value.</summary>
            public AgePrimitiveLabel Names(List<Line> row)
            {
                for (int i = 0; i < _declared.Count; i++)
                {
                    if (!_fills[i] || !_declared[i].Value)
                    {
                        continue;
                    }

                    for (int j = 0; j < row.Count; j++)
                    {
                        if (AgeWidgets.Under(row[j].Widget, _declared[i].Block))
                        {
                            return _declared[i].Label;
                        }
                    }
                }

                return null;
            }

            public object Region(int index)
            {
                AgeTransform block = _declared[index].Block;
                return "notification:body/under/" + (block == null ? index.ToString() : block.name);
            }
        }

        /// <summary>The headings this popup declared, together with the labels its controls have taken
        /// their names from - both of them things the drawn reading must not say a second time.</summary>
        private static Headings Headed(NotificationWindow window, List<Control> controls)
        {
            IList<Heading> declared = DeclaredHeadings(window);
            IList<AgeTransform> wordless = Wordless(window);
            List<AgeTransform> named = new List<AgeTransform>();
            IList<Expander> folds = Expanders(window);
            for (int i = 0; i < folds.Count; i++)
            {
                AgePrimitiveLabel title = folds[i].Title;
                AgeControlToggle toggle = folds[i].Toggle;
                if (
                    title != null
                    && toggle != null
                    && controls != null
                    && Has(controls, toggle.AgeTransform)
                )
                {
                    named.Add(title.AgeTransform);
                }
            }

            return declared.Count == 0 && named.Count == 0 && wordless.Count == 0
                ? null
                : new Headings(declared, named, wordless);
        }

        /// <summary>Open the level a heading names, where what follows is drawn under one - through the
        /// shared caption rule, so a heading carrying an explanation keeps its row inside the block it
        /// names and every other one does not. The level is a region as well, opened before the push so
        /// that such a row falls inside it.</summary>
        private static void Open(GraphBuilder builder, Headings heads, int index, ref int open)
        {
            if (index < 0 || heads == null)
            {
                return;
            }

            object region = heads.Region(index);
            builder.SetRegion(region);
            if (Captions.Push(builder, heads.Widget(index), region))
            {
                open = index;
            }
        }

        /// <summary>Close the level a heading opened, where one is open - on every path out of the body,
        /// since a level left open adopts everything the popup declares after it.</summary>
        private static void Close(GraphBuilder builder, ref int open)
        {
            Captions.Pop(builder, open >= 0);
            open = -1;
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
            MinorFactionCard card = null,
            Headings heads = null
        )
        {
            List<Line> it = row;
            string caption = CardCaption(row, card);
            // The heading the popup drew over this one value, and the name of the thing a table line
            // stands for where the popup wrote none on it. Both are the same shape as the card caption
            // below - a name for words that are only a value - and both are read when the row is read.
            AgePrimitiveLabel headed = heads == null ? null : heads.Names(row);
            AgeTooltip stands = heads == null ? null : heads.Stands(group);
            // The row's words are composed here only where a tooltip is actually going to be compared
            // against them. Explains answers null for a row with no tooltip without ever looking at
            // the text, and Explaining's loop never runs when the line carries none - and most drawn
            // rows carry none, so most rows were building a MessageBuilder, a split per piece and a
            // joined string for nothing. What the row SAYS is still composed by the label part, when
            // the player lands on it.
            AgeTooltip own = group == null ? it[0].Tooltip : null;
            List<AgeTooltip> explaining = group == null
                ? Single(own == null ? null : Explains(own, RowText(it)))
                : Explaining(group, it);
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
            // A row the game wrote as several lines SAYS several lines - one part each, in drawn
            // order (<see cref="RowLines"/>), which is what puts each of them on its own line of the
            // review buffer and nowhere twice. Null for the ordinary row of one line, which keeps
            // saying the one thing it always said.
            List<string> said = RowLines(it);
            Func<string> head = said == null ? (Func<string>)(() => RowText(it)) : () => said[0];
            NodeVtable vtable = clicked == null
                ? new NodeVtable
                {
                    // No role word and no state: this is something the game wrote down for the player to
                    // read, not a control they work.
                    Announcements = new List<NodeAnnouncement> { GraphNodes.LabelPart(head) },
                }
                : GraphNodes.Button(
                    head,
                    () => AgeWidgets.Press(clicked),
                    () => AgeWidgets.Operable(clicked)
                );

            // A figure drawn with its name somewhere else: on a bare ICON beside it (the minor-faction
            // card), in the heading the popup drew over it ("Victors"), or on the line's own tooltip
            // and nowhere on the line at all (the obliterator's dead). The words are read as the value
            // and the name as the name, so the row says "Ally, None" rather than "None" - each of them
            // declared for the rows that have it, never by a rule over every popup.
            Func<string> names = Naming(caption, headed, stands);
            if (names != null)
            {
                vtable.Announcements.Insert(0, GraphNodes.LabelPart(names));
                vtable.Announcements[1] = GraphNodes.ValuePart(head);
            }

            // The rest of the row's lines, after whichever part is leading: not watched, because these
            // are the words the game WROTE on the row rather than a state that settles under the
            // cursor, and a row whose words changed is a row this build made again.
            for (int i = 1; said != null && i < said.Count; i++)
            {
                int at = i;
                List<string> lines = said;
                vtable.Announcements.Add(GraphNodes.ValuePart(() => lines[at], false));
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

        /// <summary>What a row of bare values is called, read when the row is read: whichever of the
        /// three the popup gave it, or null for a row whose own words already name it.</summary>
        private static Func<string> Naming(
            string caption,
            AgePrimitiveLabel heading,
            AgeTooltip stands
        )
        {
            if (!string.IsNullOrEmpty(caption))
            {
                string word = caption;
                return () => word;
            }

            if (heading != null)
            {
                AgePrimitiveLabel said = heading;
                return () => AgeText.Label(said);
            }

            if (stands != null)
            {
                AgeTooltip about = stands;
                return () => AgeWidgets.TooltipTitle(about);
            }

            return null;
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
        private static List<AgeTooltip> Explaining(AgeTransform group, List<Line> row)
        {
            List<AgeTooltip> kept = new List<AgeTooltip>();
            List<AgeTooltip> found = Tooltips(group);
            if (found.Count == 0)
            {
                return kept;
            }

            string text = RowText(row);
            for (int i = 0; i < found.Count; i++)
            {
                if (Explains(found[i], text) != null)
                {
                    kept.Add(found[i]);
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
            List<AgeTransform> tableLines = null,
            Headings heads = null
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
            AddBadges(window, lines);
            AddNotes(window, lines);

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
            List<Line> kept = new List<Line>();
            foreach (Line line in lines)
            {
                if (
                    !InBody(line.Widget, title, buttons)
                    // A line inside a panel the popup has folded away is not a line: the detail of a
                    // damage report sits behind a "+" at alpha 0 and keeps every word it last held.
                    || !Painted(line.Widget, root)
                    || PartOf(line.Widget, controls)
                    || IsWords(line, words)
                    || AgeWidgets.Under(line.Widget, dossier)
                )
                {
                    continue;
                }

                kept.Add(line);
            }

            // Which of the popup's headings head anything this build - asked of everything it is
            // drawing before any of it is placed, and of the words as well, since a popup whose heading
            // stands over the very text it SAYS ("Description" over the lore) is heading that.
            if (heads != null)
            {
                heads.Fill(words);
                for (int i = 0; i < kept.Count; i++)
                {
                    heads.Fill(kept[i].Widget);
                }

                for (int i = 0; i < controls.Count; i++)
                {
                    heads.Fill(controls[i].Widget);
                }
            }

            foreach (Line line in kept)
            {
                // A heading the popup drew over something is that thing's name, said where the thing is
                // read; a name a control has already taken is the control's. Neither is a row as well.
                if (heads != null && heads.Silent(line.Widget))
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

            // A line that drew ONE thing is still a line: the empires taking part in a quest are a
            // line each, laid out two abreast in a box that scrolls, and banding them by rectangle
            // read two empires as one row and paired them by where the box happened to wrap. One
            // empire, one row - which is also what the alliance popups' member lines read as.
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

        /// <summary>The lines the popup drew as PICTURES, added to the ones it wrote out.
        ///
        /// A picture holds no text, so the walk above never saw it, and the row it becomes says
        /// nothing of its own: what it is a picture of is the game object behind it, read for its word
        /// when the row is read (<see cref="EmpireDossier.RowText"/>). From here on it is a drawn line
        /// like any other - dropped where the popup is not painting it, placed by its own rectangle,
        /// and carrying the tooltip the game hung on the icon as the explanation of the state it just
        /// named.</summary>
        private static void AddBadges(NotificationWindow window, List<Line> lines)
        {
            IList<Badge> badges = Badges(window);
            for (int i = 0; i < badges.Count; i++)
            {
                Badge badge = badges[i];
                lines.Add(
                    new Line
                    {
                        Widget = badge.Widget,
                        Tooltip = badge.Widget.AgeTooltip,
                        Relation = badge.Data,
                    }
                );
            }
        }

        /// <summary>The lines the popup drew as a picture with the whole of what it means written on
        /// the picture's TOOLTIP, added to the ones it wrote out.
        ///
        /// The walk above never saw them for the same reason it never saw a badge - a picture holds no
        /// text - and a badge's own answer is no use here: there is no game object behind the picture
        /// to be read for a word, only the sentence the game hung on it. So the sentence IS the line,
        /// and the tooltip travels with it; the row then finds the tooltip says exactly what the row
        /// says and drops it (<see cref="AddRow"/>), which is what keeps the sentence from being read
        /// twice. From there it is a drawn line like any other - dropped where the popup is not
        /// painting it, placed by its own rectangle.</summary>
        private static void AddNotes(NotificationWindow window, List<Line> lines)
        {
            IList<AgeTransform> notes = Notes(window);
            for (int i = 0; i < notes.Count; i++)
            {
                AgeTransform note = notes[i];
                string said = note == null ? null : Sentence(AgeWidgets.DrawnTooltipLines(note));
                if (string.IsNullOrEmpty(said))
                {
                    continue;
                }

                lines.Add(
                    new Line
                    {
                        Widget = note,
                        Tooltip = AgeWidgets.Raw(note),
                        Text = said,
                    }
                );
            }
        }

        /// <summary>A tooltip's lines as one piece of text, written the way a label with the same
        /// words in it would hold them - the row reads it back a line at a time.</summary>
        private static string Sentence(IList<string> lines)
        {
            StringBuilder said = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                if (string.IsNullOrEmpty(lines[i]))
                {
                    continue;
                }

                if (said.Length > 0)
                {
                    said.Append('\n');
                }

                said.Append(lines[i]);
            }

            return said.ToString();
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
        /// A line of one piece is a line like any other (see <see cref="DrawnRows"/>), and the row has
        /// to know which widget it came out of whether the game wrote one word on it or four: the
        /// systems whose construction queue has run dry are each drawn as a name and nothing else, and
        /// each line is the button that opens that system.</summary>
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

            return group;
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

            /// <summary>A block the popup captioned and then drew nothing in, and which heading it is:
            /// the block is read as its name and one row saying so.</summary>
            public bool IsEmpty;

            public int Which;

            /// <summary>The boxes the popup drew this inside, outermost first, down to the widget
            /// itself (<see cref="Chain"/>).</summary>
            public List<AgeTransform> Chain;
        }

        /// <summary>The boxes from just under the popup's root down to the widget, outermost first.
        /// </summary>
        private static List<AgeTransform> Chain(AgeTransform widget, AgeTransform root)
        {
            List<AgeTransform> chain = new List<AgeTransform>();
            AgeTransform at = widget;
            for (
                int depth = 0;
                at != null && !ReferenceEquals(at, root) && depth < MaxAncestors;
                depth++
            )
            {
                chain.Add(at);
                at = at.Parent;
            }

            chain.Reverse();
            return chain;
        }

        /// <summary>
        /// Down the page, box by box. Two things drawn in DIFFERENT boxes are ordered by the boxes -
        /// the outermost pair that the popup laid out apart - and only two things in the same box by
        /// their own rectangles. A box that scrolls lays its lines out past its own bottom edge: the
        /// quest popup's participants, two abreast in a list three lines tall, run on under the reward
        /// group drawn below the list, and ordering the lines by rectangle alone put the fourth pair of
        /// empires among the podium.
        ///
        /// Two boxes drawn SIDE BY SIDE are read left to right, whatever their top edges say. A band
        /// laid out across the popup does not align its boxes: the truce's war-score disk hangs eight
        /// pixels below the two empires it sits between, and by top edge alone it was read after both
        /// of them - a figure about the pair, read past the second of them.
        /// </summary>
        private static readonly Comparison<Item> DownThePage = delegate(Item a, Item b)
        {
            int depth = 0;
            while (
                depth < a.Chain.Count
                && depth < b.Chain.Count
                && ReferenceEquals(a.Chain[depth], b.Chain[depth])
            )
            {
                depth++;
            }

            if (depth < a.Chain.Count && depth < b.Chain.Count)
            {
                int order = AgeLayout.SameRow(a.Chain[depth], b.Chain[depth])
                    ? AgeLayout.LeftThenTop(a.Chain[depth], b.Chain[depth])
                    : AgeLayout.TopThenLeft(a.Chain[depth], b.Chain[depth]);
                if (order != 0)
                {
                    return order;
                }
            }

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
            int frame = Time.frameCount;
            if (_partOfFrame != frame)
            {
                PartOfVerdicts.Clear();
                _partOfFrame = frame;
            }

            PairKey key = new PairKey(widget, controls);
            bool part;
            if (PartOfVerdicts.TryGetValue(key, out part))
            {
                return part;
            }

            part = Inside(widget, controls);
            PartOfVerdicts[key] = part;
            return part;
        }

        private static readonly Dictionary<PairKey, bool> PartOfVerdicts =
            new Dictionary<PairKey, bool>();

        private static int _partOfFrame = -1;

        /// <summary>The ancestry walk itself, which the memo above pays for once per (widget, control
        /// list) per frame. The three readers of the drawn lines are handed the ONE control list the
        /// build made, so they ask the same question of the same line and used to walk it each.
        /// </summary>
        private static bool Inside(AgeTransform widget, List<Control> controls)
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

        /// <summary>The lines the game wrote this row as, where it wrote more than one - the rule
        /// <see cref="Content"/> applies to the popup's lead words, applied to a drawn row. A
        /// description written as a bullet list is one line per bullet and a report is the lines it
        /// was written as, so the row SAYS them one at a time and the review buffer walks them one at
        /// a time, each of them once.
        ///
        /// Null for a row of one line, which is the shape every such row has always had: its readout
        /// is that line already, and a second copy of it is the thing the player has to skip past.
        ///
        /// The split is what the row's words were kept out of a build for, so the common row - one
        /// label, no wrapping the game put there itself - answers before any of it happens.</summary>
        private static List<string> RowLines(List<Line> row)
        {
            if (row.Count == 1 && !Written(row[0].Text))
            {
                return null;
            }

            List<string> lines = new List<string>();
            for (int i = 0; i < row.Count; i++)
            {
                IList<string> written = AgeText.Lines(row[i].Text);
                for (int j = 0; j < written.Count; j++)
                {
                    lines.Add(written[j]);
                }
            }

            return lines.Count > 1 ? lines : null;
        }

        /// <summary>Whether the game put a line break in this label's text itself - the cheap question
        /// that keeps <see cref="RowLines"/> from splitting every single-label row in the popup.
        /// </summary>
        private static bool Written(string text)
        {
            return text != null && text.IndexOf('\n') >= 0;
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
        /// Every line keeps its OWN rectangle. A line the popup shows through a scrolling window used
        /// to be re-measured at that window (<see cref="AgeWidgets.Clipped"/>) so that a paragraph
        /// laid out taller than its viewport - the quest popup's lore - was not level with the button
        /// bar and dropped from a content area worked out from rectangles. Nothing works the content
        /// area out that way any more: <see cref="InBody"/> asks the CONTAINERS, and a label is under
        /// the same bars whether it is measured at itself or at the window it shows through. What the
        /// re-measurement did instead was sort, key, group and band that line at the window, and in a
        /// box taller than its viewport there is always one such line - a different one each time the
        /// box scrolls, which is what focusing a row does. That made the row count and the cursor's
        /// place move under the player as they walked.
        /// </summary>
        private static void Walk(
            AgeTransform widget,
            List<Line> lines,
            AgeTooltip inherited,
            int depth
        )
        {
            EmpireDossier.Read(widget, lines, inherited, depth);
        }

        /// <summary>Whether a drawn line is the popup's own words, which lead the body as a row of their
        /// own and are not among what it drew.</summary>
        private static bool IsWords(Line line, AgeTransform words)
        {
            return ReferenceEquals(line.Widget, words);
        }

        /// <summary>The dossier panel a popup carries, whichever popup it is - the same panel serves
        /// the introduction, a diplomatic offer and the negotiation table.</summary>
        private static NegotiationEmpireInfoPanel InfoPanel(NotificationWindow window)
        {
            return EmpireDossier.Panel(window);
        }

        private static bool Open(NegotiationEmpireInfoPanel panel)
        {
            return EmpireDossier.Open(panel);
        }

    }
}
