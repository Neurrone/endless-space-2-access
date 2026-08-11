using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// The popups the game interrupts a turn with - a battle report, a treaty offered, an empire
    /// introducing itself - made navigable, all sixty-odd of them at once.
    ///
    /// They are sixty separate windows in the game, but every one of them derives from the same base
    /// and inherits the same skeleton from it: a title, a description, and the same handful of
    /// controls for dismissing it, putting it aside, walking to the next one and deciding whether
    /// this kind should pop up at all. Reading that skeleton off the base class makes every
    /// notification navigable without knowing which one arrived. What a particular popup adds on top
    /// - Accept and Refuse on a treaty, Empire Information on an introduction - is found by looking
    /// for the controls it put a caption on, because a caption is the game saying "this is a thing
    /// the player chooses".
    ///
    /// The popup is walked the way it is drawn: the strip of controls above the words, the words
    /// themselves, and the strip below them. Which strip a control belongs to is read off the
    /// rectangle the game drew it at rather than from a list of names, so a control a popup adds of
    /// its own - Empire Information up beside the portrait, Accept and Refuse along the bottom - is
    /// walked in the strip the player sees it in without anything here knowing it exists.
    ///
    /// Not every popup fills that description in. A window that draws its own content - the research
    /// report, with a card per technology and a line per thing it unlocked - leaves the shared
    /// description label parked under a container it has hidden, still holding the raw template the
    /// game would have filled ("Research has been completed: {0}"). A sentence with a hole in it is
    /// not what the popup says, so a description whose label the player cannot see, or which still
    /// carries an unfilled slot, is treated as absent: not spoken, not a control, not in a buffer.
    /// What such a popup says is then read off what it DRAWS, one row per drawn thing - a card's
    /// title, a paragraph of lore, each unlocked item with its own explaining tooltip - in the order
    /// it is drawn in, with the controls it drew among them walked in their place.
    ///
    /// Saying something and drawing something are not alternatives. A popup can do both - an election
    /// survey writes a sentence about the poll and then draws the poll - so the words LEAD the body
    /// rather than standing in for it, and everything the popup drew follows them under those same
    /// rules. A popup whose words are all it has draws nothing to follow them with and is read
    /// exactly as it always was.
    ///
    /// Some of them draw a TABLE - the construction report, a line per system with what it finished
    /// and what it starts next, under the captions the game wrote across the top. That is read as a
    /// table (<see cref="GraphSheet"/>): the captions are not a row of words to walk past but the
    /// names of the columns, spoken as the edge crossed to reach one, and up and down walk the lines
    /// reading the whole of each. A popup is found to be drawing one rather than told: its content
    /// is a scrolling list whose lines are things the game wired a click to, under a single band of
    /// captions that nothing else in the content is outside of, and every line's pieces sit under
    /// one caption each.
    ///
    /// The words are a control in their own right and the one focus starts on: what the notification
    /// says is the reason it interrupted, so arriving reads its title and then lands on its text.
    /// Every other control speaks its own tooltip on focus and carries it as review-buffer content -
    /// the arrows say what browsing does, the box says what popping up automatically means, and each
    /// is one sentence the game wrote for exactly that purpose - while the text carries the whole
    /// notification, so a long report can be re-read from where the words are.
    ///
    /// A popup that can open a panel beside itself - the dossier behind Empire Information - gets a
    /// Tab stop for it while it is open, because that is what the panel is to the player: somewhere
    /// else to be, there only while it is on screen.
    ///
    /// Walking to the next notification keeps the same screen up with different words in it, so the
    /// change is watched for and announced rather than being left silent.
    ///
    /// Escape belongs to the game: the window is an input handler and turns it into Minimize.
    /// </summary>
    public sealed class NotificationScreen : Screen
    {
        /// <summary>How far up a parent chain to look before deciding it is not a chain.</summary>
        private const int MaxAncestors = 64;

        /// <summary>How deep inside one cell of a drawn table to look for what it is showing. Three,
        /// measured against the construction report: a cell is a group holding a picture and a label,
        /// and the deepest word in one is two levels down.</summary>
        private const int MaxCellDepth = 3;

        /// <summary>The key the body's table emits its cells under.</summary>
        private const string SheetKey = "notification:table:";

        // The four bands Alt+Up/Down jump between, top to bottom as the popup draws them. All four
        // sit in the one stop the popup already is: regions are a faster way to cross it, not a
        // second Tab stop competing with the first for the keyboard.
        private static readonly object TopRegion = "notification:top";
        private static readonly object InfoRegion = "notification:empire-info";
        private static readonly object BodyRegion = "notification:body";
        private static readonly object BottomRegion = "notification:bottom";

        private GuiManager _gui;
        private NotificationWindow[] _windows;
        private NotificationWindow _showing;
        private bool _up;
        private string _title;
        private string _description;

        public override string Key
        {
            get { return "screen.notification"; }
        }

        /// <summary>Over the game's own view and the tutorial that annotates it, under the
        /// confirmation box.</summary>
        public override int Layer
        {
            get { return 40; }
        }

        /// <summary>What happened. Spoken on arrival, ahead of the text focus lands on, which says
        /// what it means - so the two together read as the popup reads, and neither says the other's
        /// half twice.</summary>
        public override string ScreenName
        {
            get
            {
                string title = Title(Current());
                return string.IsNullOrEmpty(title)
                    ? ModStrings.Get(ModStrings.ScreenNotification)
                    : title;
            }
        }

        /// <summary>
        /// Ours from the moment a notification popup has finished animating in until the last one is
        /// gone.
        ///
        /// The two halves are deliberate. Arriving waits out the animation, because the popup's own
        /// labels still hold the previous notification's words until the game refreshes them and a
        /// screen that arrived a frame early would announce them. Standing down does not wait for
        /// anything: browsing to the next notification hides one popup and shows another, which
        /// starts a fresh animation, and a screen that asked "has it finished animating" again would
        /// stand down for the length of that fade and let the galaxy underneath announce itself
        /// between two notifications.
        ///
        /// The gui manager already tracks whether any of them is showing, and it only ever answers
        /// yes just after one has opened, so the question that costs something - which of the sixty
        /// is it - is only asked when the cheap one says there is an answer. The windows themselves
        /// are found once and remembered: they are created with the rest of the interface and live
        /// as long as it does.
        /// </summary>
        public override bool IsActive()
        {
            try
            {
                NotificationWindow window = Current();
                _up = window != null && (_up || Ready(window));
                return _up;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Escape belongs to the game: the popup is an input handler and its own exit route
        /// is what turns the key into Minimize.</summary>
        public override bool Back()
        {
            return false;
        }

        /// <summary>Arrival says the notification, so the watch starts from what was just said.
        /// </summary>
        public override void OnPush()
        {
            Remember(Current());
        }

        public override void OnPop()
        {
            _up = false;
            _title = null;
            _description = null;
        }

        /// <summary>Walking to the next notification swaps the words inside the same popup - or
        /// swaps the popup for a different kind of one without the screen ever standing down - so
        /// what is on it is watched rather than assumed.</summary>
        public override void OnUpdate()
        {
            try
            {
                NotificationWindow window = Current();
                if (window != null && !Ready(window))
                {
                    // Mid-animation the popup's labels are the skeleton's rather than this
                    // notification's - the title still a template with its hole in it - and nothing is
                    // remembered either, so the change is announced when the words are the real ones.
                    return;
                }

                string title = Title(window);
                string description = Description(window);
                if (title == _title && description == _description)
                {
                    return;
                }

                _title = title;
                _description = description;

                // A popup that draws its own content has no words but its title, and a title read
                // twice is a stutter rather than an emphasis.
                string words = Words(window);
                Voice.Say(
                    new MessageBuilder()
                        .ListItem(title)
                        .ListItem(string.Equals(words, title) ? null : words)
                        .Build(),
                    false
                );
            }
            catch (Exception e)
            {
                Log.Warn("notification: watching the shown notification threw: " + e);
            }
        }

        public override void Build(GraphBuilder builder)
        {
            NotificationWindow window = Current();
            if (window == null)
            {
                return;
            }

            // The words the popup was given, and whether it drew them anywhere the player can see -
            // which are two questions. A popup that never filled its description in has neither, and
            // one that draws its own content instead may still answer the first from the notification
            // itself while drawing nothing that says it.
            string description = Description(window);
            AgePrimitiveLabel label =
                description == null ? null : Value(window, NotificationDescription) as AgePrimitiveLabel;
            AgeTransform words =
                label != null && Visible(label.AgeTransform) ? label.AgeTransform : null;

            List<Control> controls = Controls(window);
            List<Control> above = new List<Control>();
            List<Control> inside = new List<Control>();
            List<Control> below = new List<Control>();
            Sort(window, controls, words, above, inside, below);

            above.Sort(ReadingOrder);
            below.Sort(ReadingOrder);

            // One stop, four regions walked top to bottom the way the popup is drawn - Alt+Up/Down
            // jump straight between them, and the ordinary row wiring Strip and the words node already
            // set up still walks every control in between one at a time. The empire-info region is
            // declared here, between the top strip and the words, because that is where the panel
            // actually opens - beside the portrait, above the description - and it is simply absent
            // from this list on a build where BuildEmpireInfo finds nothing to say.
            builder.SetRegion(TopRegion);
            Strip(builder, above);

            BuildEmpireInfo(builder, window);

            builder.SetRegion(BodyRegion);

            // What the popup SAYS leads what it DRAWS rather than standing in for it. A popup can do
            // both - an election survey writes a sentence over its chart of who is voting for whom -
            // and while the words answered for the whole body, everything such a popup drew was read
            // as nothing at all. The words are the first row of the content now, and whatever else the
            // popup drew follows under exactly the rules that read a popup with no words to say.
            ControlId lead = null;
            if (words != null)
            {
                // Declared outside the rows: the notification's text is a block of words, not one item
                // of a list, so it takes no place in a count. The builder wires whatever is drawn above
                // and below it to it.
                lead = WordsId(label);
                builder.AddNode(
                    lead,
                    new NodeVtable
                    {
                        // No role word: the text is not a control the player works, it is what they
                        // were interrupted to read.
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => Words(Current())),
                        },
                        Sections = GraphNodes.Sections(() => Content(Current()), null),

                        // Nothing is hovered while the player is on the words: there is no control
                        // under the cursor to light up, and no tooltip of a neighbouring one to leave
                        // hanging over the popup.
                        OnFocusVisual = ReleasePointer,
                    }
                );
            }

            // The words are already a row of their own, so they are not among the text the popup drew
            // - to either reading of it. A popup that has nothing but its words to show draws no rows
            // at all and is exactly what it was.
            Sheet sheet = ReadSheet(window, controls, inside, words);
            if (sheet == null)
            {
                BuildDrawnBody(builder, window, controls, inside, words);
            }
            else
            {
                BuildSheet(builder, window, sheet, lead);
            }

            builder.SetRegion(BottomRegion);
            Strip(builder, below);

            if (lead != null)
            {
                // Arriving reads the notification, so focus lands on what it says whatever it drew
                // underneath - set last, because the content declared after the words names its own
                // starting place and would otherwise win.
                builder.SetStart(lead);
            }
        }

        /// <summary>
        /// Which of the popup's three bands each control is drawn in.
        ///
        /// With the words in front of it that is one question - above them or below them - and it is
        /// the one asked as long as the popup has words the player can see. A popup that draws its own
        /// content instead has no such divider, and the answer comes from the skeleton every one of
        /// them is built out of: the arrows and the pop-up-again box sit beside the title along the
        /// top, dismissing and putting aside sit along the bottom, and whatever the popup drew between
        /// the two is its content - including the buttons it added there, which are walked among the
        /// rows rather than swept into a strip they are not drawn in.
        /// </summary>
        private static void Sort(
            NotificationWindow window,
            List<Control> controls,
            AgeTransform words,
            List<Control> above,
            List<Control> inside,
            List<Control> below
        )
        {
            List<AgeTransform> top = words == null ? TopRails(window, controls) : null;
            List<AgeTransform> bottom = words == null ? BottomRails(controls) : null;
            foreach (Control control in controls)
            {
                if (words != null)
                {
                    (AgeLayout.Band(control.Widget, words) > 0 ? below : above).Add(control);
                }
                else if (AtOrAbove(control.Widget, top))
                {
                    above.Add(control);
                }
                else if (AtOrBelow(control.Widget, bottom))
                {
                    below.Add(control);
                }
                else
                {
                    inside.Add(control);
                }
            }
        }

        /// <summary>
        /// What marks the top of the popup, for one with no words in the middle to measure from: the
        /// title, which is drawn across the whole of the bar the browsing arrows and the pop-up-again
        /// box sit in. The controls themselves are rails too, because the arrows are drawn one above
        /// the other rather than side by side - either of them alone would put the other in the wrong
        /// band - and because a popup with no title still has them.
        /// </summary>
        private static List<AgeTransform> TopRails(
            NotificationWindow window,
            List<Control> controls
        )
        {
            List<AgeTransform> rails = new List<AgeTransform>();
            AgePrimitiveLabel title = Value(window, NotificationTitle) as AgePrimitiveLabel;
            if (title != null && Visible(title.AgeTransform))
            {
                rails.Add(title.AgeTransform);
            }

            Rails(rails, controls, "next", "previous", "auto-popup");
            return rails;
        }

        /// <summary>The row of buttons along the bottom: dismissing the popup, putting it aside,
        /// showing where it happened.</summary>
        private static List<AgeTransform> BottomRails(List<Control> controls)
        {
            List<AgeTransform> rails = new List<AgeTransform>();
            Rails(rails, controls, "dismiss", "minimize", "show-location");
            return rails;
        }

        private static void Rails(
            List<AgeTransform> rails,
            List<Control> controls,
            params string[] keys
        )
        {
            foreach (Control control in controls)
            {
                if (Array.IndexOf(keys, control.Key) >= 0)
                {
                    rails.Add(control.Widget);
                }
            }
        }

        /// <summary>Whether the widget is drawn level with one of the top rails or clear above them -
        /// which is what puts it in the top strip rather than in the content below it.</summary>
        private static bool AtOrAbove(AgeTransform widget, List<AgeTransform> rails)
        {
            foreach (AgeTransform rail in rails ?? Empty)
            {
                if (AgeLayout.Band(widget, rail) <= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The same for the bottom strip.</summary>
        private static bool AtOrBelow(AgeTransform widget, List<AgeTransform> rails)
        {
            foreach (AgeTransform rail in rails ?? Empty)
            {
                if (AgeLayout.Band(widget, rail) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static readonly List<AgeTransform> Empty = new List<AgeTransform>();

        /// <summary>Whether a widget is drawn between the two strips - which is where the popup's own
        /// content is.</summary>
        private static bool InBody(
            AgeTransform widget,
            List<AgeTransform> top,
            List<AgeTransform> bottom
        )
        {
            return !AtOrAbove(widget, top) && !AtOrBelow(widget, bottom);
        }

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
        /// the one that just finished. Which cards those are is the game's own grouping: see
        /// <see cref="Card"/>.
        /// </summary>
        private static void BuildDrawnBody(
            GraphBuilder builder,
            NotificationWindow window,
            List<Control> controls,
            List<Control> inside,
            AgeTransform words
        )
        {
            List<Item> items = new List<Item>();
            foreach (Control control in inside)
            {
                items.Add(new Item { Widget = control.Widget, Control = control, IsControl = true });
            }

            foreach (List<Line> row in DrawnRows(window, controls, words))
            {
                items.Add(new Item { Widget = Anchor(row), Lines = row });
            }

            if (items.Count == 0)
            {
                return;
            }

            items.Sort(DownThePage);
            List<AgeTransform> cards = Cards(items);
            object region = null;
            for (int index = 0; index < items.Count; index++)
            {
                Item item = items[index];
                object here = cards == null ? BodyRegion : RegionOf(cards, item.Widget);
                if (!Equals(here, region))
                {
                    builder.SetRegion(here);
                    region = here;
                }

                ControlId id;
                if (item.IsControl)
                {
                    id = IdOf(item.Control);
                    Add(builder, item.Control);
                }
                else
                {
                    id = AddRow(builder, item.Lines, index);
                }

                if (index == 0)
                {
                    builder.SetStart(id);
                }
            }
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
                AgeTransform card = Card(common, item.Widget);
                if (card == null)
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

        /// <summary>Which card a widget was drawn in: the ancestor of it that the cards' own container
        /// holds.</summary>
        private static AgeTransform Card(AgeTransform common, AgeTransform widget)
        {
            AgeTransform at = widget;
            for (int depth = 0; at != null && depth < MaxAncestors; depth++)
            {
                if (ReferenceEquals(at.Parent, common))
                {
                    return at;
                }

                at = at.Parent;
            }

            return null;
        }

        private static object RegionOf(List<AgeTransform> cards, AgeTransform widget)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (IsUnder(widget, cards[i]))
                {
                    return "notification:body/" + i + "/" + cards[i].name;
                }
            }

            return BodyRegion;
        }

        private static bool IsUnder(AgeTransform widget, AgeTransform ancestor)
        {
            AgeTransform at = widget;
            for (int depth = 0; at != null && depth < MaxAncestors; depth++)
            {
                if (ReferenceEquals(at, ancestor))
                {
                    return true;
                }

                at = at.Parent;
            }

            return false;
        }

        /// <summary>Where two widgets' chains meet - the innermost thing the popup drew both of them
        /// inside.</summary>
        private static AgeTransform Meeting(AgeTransform first, AgeTransform second)
        {
            AgeTransform at = first;
            for (int depth = 0; at != null && depth < MaxAncestors; depth++)
            {
                if (IsUnder(second, at))
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
        /// paragraph is read once.</summary>
        private static ControlId AddRow(GraphBuilder builder, List<Line> row, int index)
        {
            List<Line> it = row;
            AgeTooltip tooltip = Explains(it[0].Tooltip, RowText(it));
            AgeTransform hover = tooltip == null ? null : Holder(tooltip);
            NodeVtable vtable = new NodeVtable
            {
                // No role word and no state: this is something the game wrote down for the player to
                // read, not a control they work.
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => RowText(it)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
                OnFocusVisual =
                    hover == null ? ReleasePointer : () => PointerFocus.MoveTo(hover, tooltip),
                OnBlurVisual = ReleasePointer,
            };

            ControlId id = ControlId.Referenced(
                it[0].Widget,
                "notification:body/" + index + "/" + it[0].Widget.name
            );
            builder.AddItem(id, vtable);
            return id;
        }

        /// <summary>The text the popup drew in its content area, grouped into the rows it reads as.
        /// </summary>
        private static List<List<Line>> DrawnRows(
            NotificationWindow window,
            List<Control> controls,
            AgeTransform words
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

            List<AgeTransform> top = TopRails(window, controls);
            List<AgeTransform> bottom = BottomRails(controls);
            List<Line> loose = new List<Line>();
            List<AgeTooltip> explained = new List<AgeTooltip>();
            Dictionary<AgeTooltip, List<Line>> groups = new Dictionary<AgeTooltip, List<Line>>();
            foreach (Line line in lines)
            {
                if (
                    !InBody(line.Widget, top, bottom)
                    || PartOf(line.Widget, controls)
                    || ReferenceEquals(line.Widget, words)
                )
                {
                    continue;
                }

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

        /// <summary>One thing drawn in the content area: a control the popup added there, or a row of
        /// text it wrote.</summary>
        private struct Item
        {
            public AgeTransform Widget;
            public Control Control;
            public bool IsControl;
            public List<Line> Lines;
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
            AgeTransform holder = row[0].Tooltip == null ? null : Holder(row[0].Tooltip);
            return holder ?? row[0].Widget;
        }

        /// <summary>The widget a tooltip is attached to - what has to be pointed at for the game to
        /// draw it, which for a row's explaining tooltip is never the label inside the row.</summary>
        private static AgeTransform Holder(AgeTooltip tooltip)
        {
            try
            {
                return tooltip.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The tooltip, unless its words are the words already being read - the game both
        /// prints a technology's description under its card and offers the same text on hover, and
        /// saying it twice is not saying it better. A tooltip the game assembles as it draws it has
        /// nothing to compare, and is always kept.</summary>
        private static AgeTooltip Explains(AgeTooltip tooltip, string text)
        {
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

        private static AgeTransform Root(NotificationWindow window)
        {
            try
            {
                return window.gameObject.GetComponent<AgeTransform>();
            }
            catch (Exception e)
            {
                Log.Warn("notification: looking for the window's transform threw: " + e);
                return null;
            }
        }

        // ---- the table a popup drew its content as ----

        /// <summary>The table a popup drew: the captions across the top, left to right, and one row
        /// per line the game drew under them.</summary>
        private sealed class Sheet
        {
            public List<Line> Headers;
            public List<SheetRow> Rows;
        }

        /// <summary>One line of that table: the thing the game wired the click to, and its pieces
        /// already paired with the caption each was drawn under - a slot per caption, empty where the
        /// line drew nothing in that column.</summary>
        private struct SheetRow
        {
            public AgeTransform Widget;
            public AgeTransform[] Cells;
        }

        /// <summary>
        /// Whether the popup's content is a TABLE, and what its columns and lines are.
        ///
        /// Nothing here knows which popup it is looking at. A table is what the game draws when it
        /// has a list of things and a fact or two about each: a SCROLLING list whose lines are things
        /// a click does something with, under a band of CAPTIONS written across the top of it. Both
        /// halves have to be there, and two further conditions keep a popup that merely has a
        /// scrolling paragraph in it - the research report's lore - from being read as a grid: nothing
        /// the popup drew in its content may sit outside the list except those captions, and every
        /// line's pieces must fall one to a caption, left to right. A popup that fails any of it is
        /// read the ordinary way, as the rows it drew.
        ///
        /// The popup's own words are not among what it drew: they lead the body as a row of their own
        /// (<paramref name="words"/>), so a popup that says a sentence over a table still has a table
        /// rather than losing it to a caption band the sentence would have broken up.
        /// </summary>
        private static Sheet ReadSheet(
            NotificationWindow window,
            List<Control> controls,
            List<Control> inside,
            AgeTransform words
        )
        {
            try
            {
                AgeTransform root = Root(window);
                if (root == null)
                {
                    return null;
                }

                List<Line> drawn = new List<Line>();
                Read(root, drawn, null, 0);

                List<AgeTransform> top = TopRails(window, controls);
                List<AgeTransform> bottom = BottomRails(controls);
                List<Line> body = new List<Line>();
                foreach (Line line in drawn)
                {
                    if (
                        InBody(line.Widget, top, bottom)
                        && !PartOf(line.Widget, controls)
                        && !ReferenceEquals(line.Widget, words)
                    )
                    {
                        body.Add(line);
                    }
                }

                foreach (AgeControlScrollView view in root.GetComponentsInChildren<AgeControlScrollView>(true))
                {
                    AgeTransform widget = view == null ? null : view.AgeTransform;
                    if (widget == null || !Visible(widget) || !InBody(widget, top, bottom))
                    {
                        continue;
                    }

                    Sheet sheet = SheetIn(widget, body, inside);
                    if (sheet != null)
                    {
                        return sheet;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("notification: looking for a table threw: " + e);
            }

            return null;
        }

        /// <summary>The table this scrolling list is the body of, or null where it is not one.
        /// </summary>
        private static Sheet SheetIn(AgeTransform view, List<Line> body, List<Control> inside)
        {
            // A control the popup captioned and drew OUTSIDE the list is content this reading would
            // drop, so the popup is not a table - it is a page with a list on it.
            foreach (Control control in inside)
            {
                if (!IsUnder(control.Widget, view))
                {
                    return null;
                }
            }

            List<Line> headers = new List<Line>();
            List<Line> within = new List<Line>();
            foreach (Line line in body)
            {
                (IsUnder(line.Widget, view) ? within : headers).Add(line);
            }

            // Column names are written on one line across the top of the list, and they are the only
            // words outside it; one of them alone is a heading rather than a set of columns.
            if (
                headers.Count < 2
                || within.Count == 0
                || AgeLayout.Rows(headers, LineWidget).Count != 1
                || AgeLayout.Band(headers[0].Widget, view) >= 0
            )
            {
                return null;
            }

            headers.Sort(AcrossTheRow);

            List<AgeTransform> lines = RowWidgets(view);
            if (lines.Count == 0)
            {
                return null;
            }

            // Words inside the list that belong to no line would be dropped by a reading that walks
            // lines - a footer, a heading the game left in there.
            foreach (Line line in within)
            {
                if (!InAny(line.Widget, lines))
                {
                    return null;
                }
            }

            List<SheetRow> rows = new List<SheetRow>();
            foreach (AgeTransform line in lines)
            {
                AgeTransform[] cells = Columns(line, headers);
                if (cells == null)
                {
                    return null;
                }

                rows.Add(new SheetRow { Widget = line, Cells = cells });
            }

            return new Sheet { Headers = headers, Rows = rows };
        }

        /// <summary>The lines of a scrolling list: the things in it the game wired a click to and
        /// wrote something on. What is in there for the look of it - the frame, the scrollbar - is
        /// wired to nothing and says nothing, and is not a line.</summary>
        private static List<AgeTransform> RowWidgets(AgeTransform view)
        {
            List<AgeTransform> lines = new List<AgeTransform>();
            Collect(view, lines, 0);
            lines.Sort(DownTheTable);
            return lines;
        }

        private static void Collect(AgeTransform widget, List<AgeTransform> lines, int depth)
        {
            if (widget == null || depth > MaxAncestors || !widget.Visible)
            {
                return;
            }

            if (depth > 0 && Wired(widget) && Draws(widget, 0))
            {
                lines.Add(widget);
                return;
            }

            List<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                Collect(children[i], lines, depth + 1);
            }
        }

        private static bool Wired(AgeTransform widget)
        {
            AgeControlButton button = AgeWidgets.Button(widget);
            return button != null && !string.IsNullOrEmpty(button.OnActivateMethod);
        }

        /// <summary>Whether the game wrote anything the player can see inside this.</summary>
        private static bool Draws(AgeTransform widget, int depth)
        {
            if (widget == null || depth > MaxCellDepth || !widget.Visible)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(AgeText.Label(widget.GetComponent<AgePrimitiveLabel>())))
            {
                return true;
            }

            List<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                if (Draws(children[i], depth + 1))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>A line's pieces, one per column: which caption each was drawn under, answered by
        /// the rectangles the game laid them out at. Null where the line does not read as a row of
        /// that table - two pieces landing in one column, or running back across the page, is the
        /// answer that the captions are not columns over these lines at all.</summary>
        private static AgeTransform[] Columns(AgeTransform line, List<Line> headers)
        {
            AgeTransform[] cells = new AgeTransform[headers.Count];
            int filled = 0;
            int last = -1;
            List<AgeTransform> children = line.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child == null || !child.Visible || !Draws(child, 0))
                {
                    continue;
                }

                int column = ColumnOf(child, headers);
                if (column < 0 || column <= last)
                {
                    return null;
                }

                cells[column] = child;
                last = column;
                filled++;
            }

            return filled > 1 && cells[0] != null ? cells : null;
        }

        /// <summary>Which caption a piece of a line was drawn under: the one it shares most of its
        /// width with, and none where it shares width with no caption at all.</summary>
        private static int ColumnOf(AgeTransform cell, List<Line> headers)
        {
            Rect it = cell.GetGlobalPosition();
            int best = -1;
            float most = 0f;
            for (int i = 0; i < headers.Count; i++)
            {
                Rect header = headers[i].Widget.GetGlobalPosition();
                float shared = Mathf.Min(it.xMax, header.xMax) - Mathf.Max(it.xMin, header.xMin);
                if (shared > most)
                {
                    most = shared;
                    best = i;
                }
            }

            return best;
        }

        private static bool InAny(AgeTransform widget, List<AgeTransform> ancestors)
        {
            for (int i = 0; i < ancestors.Count; i++)
            {
                if (IsUnder(widget, ancestors[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The table, as a table: a line per row, the line's first piece the row itself and the rest
        /// its columns.
        ///
        /// The row is a BUTTON, because that is what the game made the line - clicking it is how a
        /// player opens the system a construction finished in - and Enter is that click. It reads the
        /// whole line, so walking down the table hears each system with what it finished and what it
        /// starts next; the columns beside it are there to walk across when one of those is the thing
        /// being compared, and each says the caption it is under as the edge crossed to reach it
        /// rather than repeating it in every row.
        ///
        /// The words the popup opened with, where it had any, are the row above the first one - the
        /// table continues below them - and the strips around the whole thing are joined to it by the
        /// builder, which knows a seam is a ROW rather than a node.
        /// </summary>
        private static void BuildSheet(
            GraphBuilder builder,
            NotificationWindow window,
            Sheet sheet,
            ControlId lead
        )
        {
            string[] columns = new string[sheet.Headers.Count - 1];
            for (int i = 1; i < sheet.Headers.Count; i++)
            {
                columns[i - 1] = sheet.Headers[i].Text;
            }

            GraphSheet table = new GraphSheet(builder, SheetKey);
            table.Region(Title(window), columns);
            table.Follows(lead);
            foreach (SheetRow row in sheet.Rows)
            {
                List<KeyValuePair<int, NodeVtable>> cells =
                    new List<KeyValuePair<int, NodeVtable>>();
                for (int c = 1; c < row.Cells.Length; c++)
                {
                    if (row.Cells[c] != null)
                    {
                        cells.Add(new KeyValuePair<int, NodeVtable>(c, CellNode(row, c)));
                    }
                }

                table.RowAt(RowNode(row), row.Widget, cells);
            }

            table.Finish();
            if (lead == null)
            {
                // What the popup drew is all it has, so focus lands on its first line.
                builder.SetStart(table.FirstRow);
            }
        }

        /// <summary>The row itself: what the line says, all of it, and the game's own click.</summary>
        private static NodeVtable RowNode(SheetRow row)
        {
            AgeTransform widget = row.Widget;
            AgeTransform[] cells = row.Cells;
            NodeVtable vtable = GraphNodes.Button(
                () => CellText(cells[0]),
                () => AgeWidgets.Press(widget),
                () => AgeWidgets.Operable(widget),
                AgeWidgets.Raw(widget)
            );

            for (int c = 1; c < cells.Length; c++)
            {
                AgeTransform cell = cells[c];
                if (cell != null)
                {
                    // Not watched: a notification is a report of something that has already happened,
                    // and nothing in it changes under a standing cursor.
                    vtable.Announcements.Add(GraphNodes.ValuePart(() => CellText(cell), false));
                }
            }

            AgeWidgets.Point(vtable, AgeWidgets.Button(widget));
            return vtable;
        }

        /// <summary>One column of a row: what the game drew in it and the tooltips it hung there - a
        /// constructible's dossier, assembled as it is drawn, so indicated rather than read out and
        /// carried in the buffer. It does not say its own caption: the sheet says that as the edge the
        /// player crossed to get here.</summary>
        private static NodeVtable CellNode(SheetRow row, int column)
        {
            AgeTransform cell = row.Cells[column];
            AgeTransform name = row.Cells[0];
            List<AgeTooltip> tooltips = Tooltips(cell);
            NodeSection[] sections = new NodeSection[tooltips.Count];
            for (int i = 0; i < tooltips.Count; i++)
            {
                sections[i] = GraphNodes.TooltipSection(tooltips[i]);
            }

            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.ValuePart(() => CellText(cell), false),
                },
                Sections = GraphNodes.Sections(sections),
                SearchText = () => CellText(name),
            };

            // The tooltip hangs off the picture inside the cell rather than the cell, and pointing at
            // anything else draws nothing.
            AgeTooltip tooltip = tooltips.Count == 0 ? null : tooltips[tooltips.Count - 1];
            AgeTransform hover = tooltip == null ? null : Holder(tooltip);
            vtable.OnFocusVisual =
                hover == null ? ReleasePointer : () => PointerFocus.MoveTo(hover, tooltip);
            vtable.OnBlurVisual = ReleasePointer;
            return vtable;
        }

        /// <summary>The tooltips the game hung inside one column, in the order it drew them.</summary>
        private static List<AgeTooltip> Tooltips(AgeTransform cell)
        {
            List<AgeTooltip> tooltips = new List<AgeTooltip>();
            CollectTooltips(cell, tooltips, 0);
            return tooltips;
        }

        private static void CollectTooltips(AgeTransform widget, List<AgeTooltip> into, int depth)
        {
            if (widget == null || depth > MaxCellDepth || !widget.Visible)
            {
                return;
            }

            AgeTooltip tooltip = widget.AgeTooltip;
            if (tooltip != null && !into.Contains(tooltip))
            {
                into.Add(tooltip);
            }

            List<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                CollectTooltips(children[i], into, depth + 1);
            }
        }

        /// <summary>
        /// What one column of a row says: what the game wrote in it, read across the way it is drawn.
        ///
        /// A bare number is the one thing that cannot be read as it stands, because what it counts is
        /// drawn beside it as a picture rather than written: the construction table puts an hourglass
        /// in front of the turns a build has left, and "3" on its own is a number the player has to
        /// guess the units of. Where the column draws that hourglass, its number is said as the turns
        /// it stands for - and where the game put a word there instead of a number ("[infinite]"), the
        /// word is what it says, because the game has already answered.
        /// </summary>
        private static string CellText(AgeTransform cell)
        {
            if (cell == null)
            {
                return null;
            }

            List<AgePrimitiveLabel> labels = new List<AgePrimitiveLabel>();
            bool turns = false;
            ReadCell(cell, labels, ref turns, 0);
            labels.Sort(AcrossTheControl);

            MessageBuilder message = new MessageBuilder();
            foreach (AgePrimitiveLabel label in labels)
            {
                string text = AgeText.Label(label);
                message.ListItem(
                    turns && IsCount(text)
                        ? ModStrings.Format(ModStrings.GalaxyTurnsRemaining, text)
                        : text
                );
            }

            return message.Build();
        }

        private static void ReadCell(
            AgeTransform widget,
            List<AgePrimitiveLabel> labels,
            ref bool turns,
            int depth
        )
        {
            if (widget == null || depth > MaxCellDepth || !widget.Visible)
            {
                return;
            }

            AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
            if (label != null && !string.IsNullOrEmpty(AgeText.Label(label)))
            {
                labels.Add(label);
            }

            turns = turns || IsTurnIcon(widget);

            List<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                ReadCell(children[i], labels, ref turns, depth + 1);
            }
        }

        /// <summary>Whether the picture drawn here is the game's own turn symbol - which is what tells
        /// a number beside it apart from every other number a table can hold.</summary>
        private static bool IsTurnIcon(AgeTransform widget)
        {
            try
            {
                AgePrimitiveImage image = widget.GetComponent<AgePrimitiveImage>();
                Texture texture = image == null ? null : image.Texture;
                string key;
                return texture != null
                    && IconTable.TryKeyForPicture(texture.name, out key)
                    && key == ModStrings.IconTurn;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsCount(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsDigit(text[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static readonly Comparison<Line> AcrossTheRow = delegate(Line a, Line b)
        {
            return AgeLayout.ReadingOrder(a.Widget, b.Widget);
        };

        private static readonly Comparison<AgeTransform> DownTheTable = delegate(
            AgeTransform a,
            AgeTransform b
        )
        {
            return AgeLayout.TopThenLeft(a, b);
        };

        // ---- the dossier a popup can open beside itself ----

        /// <summary>
        /// The panel a popup opens when the player ticks Empire Information: who this empire is, what
        /// its faction is about, what it is good at. It is somewhere else to be rather than more of the
        /// popup - the game draws it as a sheet of its own, beside the popup rather than inside it - so
        /// it is A REGION OF ITS OWN while the box is ticked, and stops existing when it is unticked.
        /// The tick box that opened it is still what closes it, so the cursor is never left standing in
        /// a panel that has gone. A region rather than a second Tab stop: the panel is still part of
        /// the one place this popup is, reached by walking down from the top strip exactly as every
        /// other control here is, with Alt+Down/Up there only to cross it in one step.
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
            if (panel == null || !Open(panel))
            {
                return;
            }

            List<Line> lines = new List<Line>();
            Read(panel.AgeTransform, lines, null, 0);
            if (lines.Count == 0)
            {
                return;
            }

            builder.SetRegion(InfoRegion);
            int index = 0;
            foreach (List<Line> row in AgeLayout.Rows(lines, LineWidget))
            {
                List<Line> it = row;
                AgeTooltip tooltip = it[0].Tooltip;
                AgeTransform under = it[0].Owner;
                NodeVtable vtable = new NodeVtable
                {
                    // No role word and no state: every one of these is something the game wrote down
                    // for the player to read, not a control they work.
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => RowText(it)),
                    },
                    Sections = GraphNodes.Sections(null, tooltip),
                    OnFocusVisual = () => PointerFocus.MoveTo(null, tooltip, under),
                    OnBlurVisual = ReleasePointer,
                };
                builder.AddItem(
                    ControlId.Referenced(
                        it[0].Widget,
                        "notification:empire-info/" + index + "/" + it[0].Widget.name
                    ),
                    vtable
                );
                index++;
            }
        }

        /// <summary>One line the panel draws: the label's own transform - which is the rectangle the
        /// rows are worked out from, and what has to be scrolled into view - and the widget the game
        /// hung the explaining tooltip on, which for a table row is the row rather than its
        /// label.</summary>
        private struct Line
        {
            public AgeTransform Widget;
            public AgeTransform Owner;
            public AgeTooltip Tooltip;
            public string Text;
        }

        private static readonly Func<Line, AgeTransform> LineWidget = line => line.Widget;

        /// <summary>What one drawn line says. A line the game wrote as prose keeps the prose - its own
        /// wrapping is where the words ran out, not punctuation - while two labels drawn side by side
        /// (an empire and how it gets on with you) are two facts, and read as two.</summary>
        private static string RowText(List<Line> row)
        {
            MessageBuilder message = new MessageBuilder();
            for (int i = 0; i < row.Count; i++)
            {
                message.ListItem();
                foreach (string line in AgeText.Lines(row[i].Text))
                {
                    message.Fragment(line);
                }
            }

            return message.Build();
        }

        /// <summary>Everything the panel is showing, in the order it is laid out. A hidden branch is
        /// skipped rather than read: the panel keeps a block per kind of empire and hides the ones this
        /// one has nothing to say for.</summary>
        private static void Read(
            AgeTransform widget,
            List<Line> lines,
            AgeTooltip inherited,
            int depth
        )
        {
            if (depth > MaxAncestors)
            {
                return;
            }

            AgeTooltip tooltip = widget.AgeTooltip ?? inherited;
            AgeTransform owner = widget.AgeTooltip != null ? widget : null;
            string text = AgeText.Label(widget.GetComponent<AgePrimitiveLabel>());
            if (!string.IsNullOrEmpty(text))
            {
                lines.Add(
                    new Line
                    {
                        Widget = widget,
                        Owner = owner ?? widget,
                        Tooltip = tooltip,
                        Text = text,
                    }
                );
            }

            List<AgeTransform> children = widget.Children;
            for (int i = 0; i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child != null && child.Visible)
                {
                    Read(child, lines, tooltip, depth + 1);
                }
            }
        }

        /// <summary>The dossier panel a popup carries, whichever popup it is - the same panel serves
        /// the introduction, a diplomatic offer and the negotiation table.</summary>
        private static NegotiationEmpireInfoPanel InfoPanel(NotificationWindow window)
        {
            try
            {
                return window.gameObject.GetComponentInChildren<NegotiationEmpireInfoPanel>(true);
            }
            catch (Exception e)
            {
                Log.Warn("notification: looking for the empire panel threw: " + e);
                return null;
            }
        }

        private static bool Open(NegotiationEmpireInfoPanel panel)
        {
            try
            {
                return panel.Shown && Visible(panel.AgeTransform);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>One strip of controls: left and right walk it, and up and down reach the strips
        /// above and below because they are separate rows.</summary>
        private static void Strip(GraphBuilder builder, List<Control> controls)
        {
            if (controls.Count == 0)
            {
                return;
            }

            builder.StartRow();
            foreach (Control control in controls)
            {
                Add(builder, control);
            }

            builder.EndRow();
        }

        private static ControlId WordsId(AgePrimitiveLabel label)
        {
            return ControlId.Referenced(label, "notification:words");
        }

        private static void Add(GraphBuilder builder, Control control)
        {
            Control it = control;
            NodeVtable vtable;
            if (it.Toggle == null)
            {
                vtable = GraphNodes.Button(
                    () => Caption(it),
                    () => Press(it),
                    () => Enabled(it.Widget),
                    it.Widget.AgeTooltip
                );
            }
            else if (InRadioGroup(it.Toggle))
            {
                vtable = GraphNodes.Radio(
                    () => Caption(it),
                    () => State(it.Toggle),
                    () => Press(it),
                    () => Enabled(it.Widget),
                    null,
                    it.Widget.AgeTooltip
                );
            }
            else
            {
                vtable = GraphNodes.Checkbox(
                    () => Caption(it),
                    () => State(it.Toggle),
                    () => Press(it),
                    () => Enabled(it.Widget),
                    it.Widget.AgeTooltip
                );
            }

            vtable.OnFocusVisual = () =>
                PointerFocus.MoveTo(it.Button, it.Widget.AgeTooltip, it.Widget);
            vtable.OnBlurVisual = ReleasePointer;
            builder.AddItem(IdOf(it), vtable);
        }

        private static ControlId IdOf(Control control)
        {
            return ControlId.Referenced(control.Widget, "notification:" + control.Key);
        }

        /// <summary>Whether a toggle is one of a set the game lets the player pick exactly one of -
        /// the choice cards a quest offers - rather than a box of its own, like the one that pins the
        /// quest. The game answers this itself: <c>GuiRadioGroup.Load</c> re-points every toggle it
        /// owns at its own object, so a toggle whose switch target carries a <c>GuiRadioGroup</c> is a
        /// member of that group, and one wired to anything else is not.</summary>
        private static bool InRadioGroup(AgeControlToggle toggle)
        {
            try
            {
                return toggle.OnSwitchObject != null
                    && toggle.OnSwitchObject.GetComponent<GuiRadioGroup>() != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static readonly Comparison<Control> ReadingOrder = delegate(Control a, Control b)
        {
            return AgeLayout.ReadingOrder(a.Widget, b.Widget);
        };

        /// <summary>One control of the popup: the widget, how to name it when the game did not, and
        /// - when it is a toggle - the state it carries.</summary>
        private struct Control
        {
            public string Key;
            public AgeTransform Widget;
            public AgeControlButton Button;
            public AgeControlToggle Toggle;
            public string NameKey;
        }

        /// <summary>
        /// The controls the popup is currently offering: the ones every notification has - dismissing
        /// it, putting it aside, showing where it happened, walking to its neighbours, deciding
        /// whether this kind should interrupt again - and whatever this particular one asks the player
        /// to decide. Found in no particular order, because where they are drawn is what decides
        /// where they are walked.
        /// </summary>
        private static List<Control> Controls(NotificationWindow window)
        {
            List<Control> controls = new List<Control>();
            try
            {
                AgeControlButton dismiss = Button(window, DismissButton);
                AgeControlButton showLocation = Button(window, ShowLocationButton);
                AgeControlButton minimize = Button(window, MinimizeButton);
                AgeControlButton previous = Button(window, PreviousNotificationButton);
                AgeControlButton next = Button(window, NextNotificationButton);
                AgeControlToggle autoPopup = Toggle(window, AutoPopupToggle);

                Add(controls, "dismiss", dismiss, ModStrings.NotifyDismiss);
                foreach (AgeControl extra in Extras(window))
                {
                    Add(controls, extra.name, extra as AgeControlButton, extra as AgeControlToggle, null);
                }

                Add(controls, "show-location", showLocation, ModStrings.NotifyShowLocation);
                Add(controls, "minimize", minimize, ModStrings.NotifyMinimize);
                Add(controls, "previous", previous, ModStrings.NotifyPrevious);
                Add(controls, "next", next, ModStrings.NotifyNext);
                Add(controls, "auto-popup", null, autoPopup, ModStrings.NotifyAutoPopup);
            }
            catch (Exception e)
            {
                Log.Warn("notification: reading the controls threw: " + e);
            }

            return controls;
        }

        private static void Add(
            List<Control> controls,
            string key,
            AgeControlButton button,
            string nameKey
        )
        {
            Add(controls, key, button, null, nameKey);
        }

        private static void Add(
            List<Control> controls,
            string key,
            AgeControlButton button,
            AgeControlToggle toggle,
            string nameKey
        )
        {
            AgeControl control = toggle == null ? (AgeControl)button : toggle;
            if (control == null || !Visible(control.AgeTransform))
            {
                return;
            }

            controls.Add(
                new Control
                {
                    Key = key,
                    Widget = control.AgeTransform,
                    Button = button,
                    Toggle = toggle,
                    NameKey = nameKey,
                }
            );
        }

        /// <summary>
        /// What this particular notification added to the shared skeleton. A popup's own answers are
        /// the controls it wired a handler to and wrote a caption on: the caption is what tells them
        /// apart from the invisible click-catchers every popup is built out of - the sheet behind it
        /// that minimises it, the bar it is dragged by, the text area that finishes the typing
        /// animation - none of which is a thing the player chooses.
        /// </summary>
        private static List<AgeControl> Extras(NotificationWindow window)
        {
            List<AgeControl> extras = new List<AgeControl>();
            AgeControl[] declared = Declared(window);
            foreach (AgeControl control in window.gameObject.GetComponentsInChildren<AgeControl>(true))
            {
                AgeControlButton button = control as AgeControlButton;
                AgeControlToggle toggle = control as AgeControlToggle;
                bool wired =
                    (button != null && !string.IsNullOrEmpty(button.OnActivateMethod))
                    || (toggle != null && !string.IsNullOrEmpty(toggle.OnSwitchMethod));
                if (
                    !wired
                    || !Visible(control.AgeTransform)
                    || string.IsNullOrEmpty(CaptionOf(control.AgeTransform))
                    || Array.IndexOf(declared, control) >= 0
                )
                {
                    continue;
                }

                extras.Add(control);
            }

            return extras;
        }

        private static AgeControl[] Declared(NotificationWindow window)
        {
            return new AgeControl[]
            {
                Button(window, ModalButton),
                Button(window, DismissButton),
                Button(window, ShowLocationButton),
                Button(window, MinimizeButton),
                Button(window, PreviousNotificationButton),
                Button(window, NextNotificationButton),
                Toggle(window, AutoPopupToggle),
            };
        }

        /// <summary>The control's name: the caption the game wrote on it, else the name this mod has
        /// for the role it plays - the game draws the browsing arrows and the pop-up-again box as
        /// icons and never names them.</summary>
        private static string Caption(Control control)
        {
            string caption = CaptionOf(control.Widget);
            if (!string.IsNullOrEmpty(caption))
            {
                return caption;
            }

            return control.NameKey == null ? null : ModStrings.Get(control.NameKey);
        }

        /// <summary>What the game wrote on a control, all of it: a card drawn as a heading and a name
        /// beside it - "Just Completed", "Xenobiology" - is one button saying both, and reading only
        /// the first of them names the shelf instead of the thing on it. Read across the way they are
        /// drawn rather than in the order the widget tree happens to list them.</summary>
        private static string CaptionOf(AgeTransform widget)
        {
            try
            {
                List<AgePrimitiveLabel> labels = new List<AgePrimitiveLabel>();
                foreach (AgePrimitiveLabel label in widget.GetChildren<AgePrimitiveLabel>(false))
                {
                    labels.Add(label);
                }

                labels.Sort(AcrossTheControl);

                MessageBuilder caption = new MessageBuilder();
                foreach (AgePrimitiveLabel label in labels)
                {
                    caption.ListItem(AgeText.Label(label));
                }

                return caption.Build();
            }
            catch (Exception e)
            {
                Log.Warn("notification: reading a control's caption threw: " + e);
                return null;
            }
        }

        private static readonly Comparison<AgePrimitiveLabel> AcrossTheControl = delegate(
            AgePrimitiveLabel a,
            AgePrimitiveLabel b
        )
        {
            return AgeLayout.ReadingOrder(a.AgeTransform, b.AgeTransform);
        };

        /// <summary>
        /// Press a control the way the engine presses it - the object and the method name its own
        /// mouse handler sends to - so the game runs its own handler with no click that could land
        /// on whatever the mouse is over. A toggle carries its new state into that handler, which is
        /// where the game reads it back from, so the state is flipped first exactly as a click does.
        /// </summary>
        private static void Press(Control control)
        {
            try
            {
                if (control.Toggle != null)
                {
                    control.Toggle.State = !control.Toggle.State;
                    Send(
                        control.Toggle.OnSwitchObject,
                        control.Toggle.OnSwitchMethod,
                        control.Toggle.gameObject
                    );
                    return;
                }

                Send(
                    control.Button.OnActivateObject,
                    control.Button.OnActivateMethod,
                    control.Button.gameObject
                );
            }
            catch (Exception e)
            {
                Log.Warn("notification: pressing " + control.Key + " threw: " + e);
            }
        }

        private static void Send(GameObject target, string method, GameObject sender)
        {
            if (target != null && !string.IsNullOrEmpty(method))
            {
                target.SendMessage(method, sender, SendMessageOptions.DontRequireReceiver);
            }
        }

        /// <summary>
        /// What the popup says, as one spoken line - what the player was interrupted to read. A popup
        /// that carries everything in its title has that read instead of nothing.
        ///
        /// The game wraps a report over as many lines as the popup is wide, so its line breaks are
        /// where the words ran out and not punctuation. They are joined with a space, which is the
        /// prose the game wrote; a comma between them would read a full stop as "disabled., Once you".
        /// </summary>
        private static string Words(NotificationWindow window)
        {
            MessageBuilder message = new MessageBuilder();
            foreach (string line in AgeText.Lines(Description(window)))
            {
                message.Fragment(line);
            }

            return message.Build() ?? Title(window);
        }

        /// <summary>The notification as the review buffer holds it: its title, then its description a
        /// line at a time - a battle report is written as exactly those lines.</summary>
        private static IList<string> Content(NotificationWindow window)
        {
            List<string> lines = new List<string>();
            string title = Title(window);
            if (!string.IsNullOrEmpty(title))
            {
                lines.Add(title);
            }

            foreach (string line in AgeText.Lines(Description(window)))
            {
                lines.Add(line);
            }

            return lines;
        }

        /// <summary>The label the popup is showing, else the notification's own title - a popup that
        /// keeps its description in a scroll view it has hidden still has one to read.</summary>
        private static string Title(NotificationWindow window)
        {
            return Text(window, NotificationTitle, true);
        }

        private static string Description(NotificationWindow window)
        {
            return Text(window, NotificationDescription, false);
        }

        /// <summary>
        /// The title or the description: what the popup drew, else what the notification itself says.
        ///
        /// The description is held to two conditions the title is not. It has to be somewhere the
        /// player can SEE - a window that draws its own content parks the shared label under a
        /// container it has hidden, and what is written on a hidden label is the leftovers of a
        /// skeleton, not the popup's words - and it has to be FILLED IN: a notification that never
        /// overrode its description leaves the template with the hole still in it, both on the label
        /// and in what the notification answers, and either way "Research has been completed: {0}"
        /// tells the player nothing they were interrupted for. Titles are formatted properly by every
        /// popup there is, so neither condition is asked of them.
        /// </summary>
        private static string Text(NotificationWindow window, PropertyInfo label, bool title)
        {
            if (window == null)
            {
                return null;
            }

            try
            {
                AgePrimitiveLabel drawn = Value(window, label) as AgePrimitiveLabel;
                string text =
                    title || (drawn != null && Visible(drawn.AgeTransform))
                        ? AgeText.Label(drawn)
                        : null;
                if (!string.IsNullOrEmpty(text) && (title || !Unwritten(text)))
                {
                    return text;
                }

                GuiNotification notification = window.GuiNotification;
                if (notification == null)
                {
                    return null;
                }

                string written = AgeText.Clean(
                    title ? notification.GetTitle() : notification.GetDescription()
                );
                return title || !Unwritten(written) ? written : null;
            }
            catch (Exception e)
            {
                Log.Warn("notification: reading the text threw: " + e);
                return null;
            }
        }

        /// <summary>
        /// Whether the text is a sentence nobody ever wrote: a template with the hole still in it
        /// ("Research has been completed: {0}"), or a key the game has no words for at all - the stage
        /// notification asks for a description the localization files never gave it, and what comes
        /// back is the key itself. Both are the same thing to a listener: the popup saying nothing
        /// while sounding as though it said something.
        /// </summary>
        private static bool Unwritten(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            try
            {
                if (Gui.IsLocalizationKey(text))
                {
                    return true;
                }
            }
            catch (Exception) { }

            return Unfilled(text);
        }

        /// <summary>Whether the text still has a hole in it where the game would have put something -
        /// the <c>{0}</c> of a template nobody filled in.</summary>
        private static bool Unfilled(string text)
        {
            for (int i = 0; text != null && i < text.Length; i++)
            {
                if (text[i] != '{')
                {
                    continue;
                }

                int j = i + 1;
                while (j < text.Length && char.IsDigit(text[j]))
                {
                    j++;
                }

                if (j > i + 1 && j < text.Length && text[j] == '}')
                {
                    return true;
                }
            }

            return false;
        }

        private void Remember(NotificationWindow window)
        {
            _title = Title(window);
            _description = Description(window);
        }

        /// <summary>The popup that is up, if one is. The window that answered last time is asked
        /// first, so the usual frame costs one property read.</summary>
        private NotificationWindow Current()
        {
            GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
            if (gui == null || !gui.IsAnyNotificationVisible)
            {
                _showing = null;
                return null;
            }

            if (Up(_showing))
            {
                return _showing;
            }

            _showing = null;
            foreach (NotificationWindow window in Windows(gui))
            {
                if (Up(window))
                {
                    _showing = window;
                    break;
                }
            }

            return _showing;
        }

        /// <summary>Showing - which the game means from the first frame of the popup's fade in to the
        /// first frame of its fade out.</summary>
        private static bool Up(NotificationWindow window)
        {
            try
            {
                return window != null && window.Shown;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Showing and done animating, which is when its labels hold this notification's
        /// words rather than the last one's.</summary>
        private static bool Ready(NotificationWindow window)
        {
            try
            {
                return window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Every notification popup there is. They are built with the rest of the interface
        /// and hang off it, so finding them is a one-off; the interface being rebuilt is what makes
        /// the answer stale.</summary>
        private NotificationWindow[] Windows(GuiManager gui)
        {
            if (_windows == null || !ReferenceEquals(_gui, gui))
            {
                _gui = gui;
                _windows = gui.gameObject.GetComponentsInChildren<NotificationWindow>(true);
            }

            return _windows;
        }

        private static readonly Action ReleasePointer = PointerFocus.Release;

        // The base class keeps its skeleton behind protected properties, so every popup type is read
        // through the one set of accessors rather than sixty sets of fields.
        private static readonly PropertyInfo ModalButton = Member("ModalButton");
        private static readonly PropertyInfo DismissButton = Member("DismissButton");
        private static readonly PropertyInfo MinimizeButton = Member("MinimizeButton");
        private static readonly PropertyInfo ShowLocationButton = Member("ShowLocationButton");
        private static readonly PropertyInfo NextNotificationButton = Member(
            "NextNotificationButton"
        );
        private static readonly PropertyInfo PreviousNotificationButton = Member(
            "PreviousNotificationButton"
        );
        private static readonly PropertyInfo AutoPopupToggle = Member("AutoPopupToggle");
        private static readonly PropertyInfo NotificationTitle = Member("NotificationTitle");
        private static readonly PropertyInfo NotificationDescription = Member(
            "NotificationDescription"
        );

        private static PropertyInfo Member(string name)
        {
            try
            {
                return typeof(NotificationWindow).GetProperty(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );
            }
            catch (Exception e)
            {
                Log.Warn("notification: looking up " + name + " threw: " + e);
                return null;
            }
        }

        private static object Value(NotificationWindow window, PropertyInfo member)
        {
            if (window == null || member == null)
            {
                return null;
            }

            try
            {
                return member.GetValue(window, null);
            }
            catch (Exception e)
            {
                Log.Warn("notification: reading " + member.Name + " threw: " + e);
                return null;
            }
        }

        private static AgeControlButton Button(NotificationWindow window, PropertyInfo member)
        {
            return Value(window, member) as AgeControlButton;
        }

        private static AgeControlToggle Toggle(NotificationWindow window, PropertyInfo member)
        {
            return Value(window, member) as AgeControlToggle;
        }

        private static bool State(AgeControlToggle toggle)
        {
            try
            {
                return toggle != null && toggle.State;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // A control inside a group the popup has collapsed is still marked visible itself, so the
        // chain above it is what says whether the player can see it.
        private static bool Visible(AgeTransform widget)
        {
            try
            {
                AgeTransform at = widget;
                for (int depth = 0; at != null && depth < MaxAncestors; depth++)
                {
                    if (!at.Visible)
                    {
                        return false;
                    }

                    at = at.Parent;
                }

                return widget != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool Enabled(AgeTransform widget)
        {
            try
            {
                return widget != null && widget.Enable;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
