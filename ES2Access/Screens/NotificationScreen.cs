using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;
using Line = ES2Access.UI.EmpireDossier.DrawnLine;

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
    /// The game builds those tables two ways and both read as tables. One is a scrolling list, found from
    /// the screen. The other is a container the popup refills every turn by CLONING one line - the
    /// inspector's report, the laws that lapsed, the systems that lost population - and that one cannot be
    /// found from the screen at all: a stack of clones and a stack of hand-laid-out rows look the same, so
    /// the popup's own code names its container (<see cref="Variant"/>). Whether the columns have NAMES is
    /// then the prefab's decision rather than the code's, and it is asked of the screen: a single band of
    /// captions drawn clear above the container, with nothing else of the popup's out there and every
    /// line's pieces falling one to a caption, makes it a table with named columns; anything less leaves
    /// each line as the one row it looks like, its pieces joined and every explanation hung inside it
    /// carried along. What the popup wrote UNDER the lines - the inspector's report ends with what all of
    /// it came to - is the table's footer and reads as the full-width row it is drawn as.
    ///
    /// A line of a table is one thing to the player, so text drawn inside one is that line's row rather
    /// than a row of its own - unless the line drew a single thing, in which case it already is one and
    /// the empires of an alliance, a line each drawn side by side, still read as the one row they look
    /// like.
    ///
    /// Exclusivity is likewise something the screen cannot show. Some popups let the player pick one of
    /// several and keep that exclusive by hand - unticking the others in their own code - rather than with
    /// a <c>GuiRadioGroup</c>, and a hand-wired set is pixel-for-pixel a row of independent boxes. Those
    /// sets are named the same way, and they are declared because the popup SAYS they are a choice rather
    /// than because it wrote a caption on them: a card whose words are all nested inside it has no caption
    /// to the shared rule, and dropping it would leave a keyboard player unable to choose at all. What is
    /// written on the card, all of it, is its name. Picking is not doing - the popup wants the choice
    /// confirmed - so the button that confirms is declared even where the game drew it as a bare tick
    /// (under the game's own word for it), and where a popup draws no such button the game's own second
    /// click on the choice is what confirms and takes the double-click chord (Ctrl+Alt+Enter).
    ///
    /// The words are a control in their own right and the one focus starts on: what the notification
    /// says is the reason it interrupted, so arriving reads its title and then lands on its text.
    /// Every other control speaks its own tooltip on focus and carries it as review-buffer content -
    /// the arrows say what browsing does, the box says what popping up automatically means, and each
    /// is one sentence the game wrote for exactly that purpose - while the text carries the whole
    /// notification, so a long report can be re-read from where the words are.
    ///
    /// Not every popup puts its words in the shared description: a diplomat's message is typed out into a
    /// label of the popup's own. That label is named by the popup too, so the message leads the body and is
    /// what arriving reads, rather than turning up as one more thing drawn in the middle.
    ///
    /// A popup that can open a panel beside itself - the dossier behind Empire Information - gets a
    /// Tab stop for it while it is open, because that is what the panel is to the player: somewhere
    /// else to be, there only while it is on screen. Its lines are that region's and nobody else's: they
    /// are drawn level with the content, so a popup that draws its own content leaves them out of it.
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

        /// <summary>Where the engine itself puts notifications: above the screens, below every modal
        /// (the game's own draw-order ladder is Screens &lt; Notifications &lt; ModalWindows — measured
        /// via AgeScreen.SortingOrder and the tutorials' TutorialPopupLayer scale). The modals a
        /// notification OPENS (negotiation, battle report) sit far above and are unaffected; what
        /// this number fixes is a modal and a popup up together, where the game draws the modal on
        /// top and the mod used to read the popup. Owner ruling: the draw order decides.
        /// </summary>
        public override int Layer
        {
            get { return 18; }
        }

        /// <summary>What happened. Spoken on arrival, ahead of the text focus lands on, which says
        /// what it means - so the two together read as the popup reads, and neither says the other's
        /// half twice. The one popup that is the end of the player's game says so here
        /// (<see cref="OwnElimination"/>), because arriving is when that has to be heard and the
        /// popup's own words do not say it.</summary>
        public override string ScreenName
        {
            get
            {
                NotificationWindow window = Current();
                string title = Title(window);
                return new MessageBuilder()
                    .ListItem(
                        string.IsNullOrEmpty(title)
                            ? ModStrings.Get(ModStrings.ScreenNotification)
                            : title
                    )
                    .ListItem(OwnElimination(window))
                    .Build();
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
            AgePrimitiveLabel label = description == null ? null : DescriptionLabel(window);
            AgeTransform words =
                label != null && Visible(label.AgeTransform) ? label.AgeTransform : null;

            // A popup whose content is a MODEL rather than text writes its own body, and then it owns
            // every control it added as well - so only the shared skeleton is collected here.
            Action<NotificationBody> body = BodyOf(window);
            List<Control> controls = Controls(window, body == null);
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

                // What the popup offers on hovering its words - the dossier of the resource an
                // expedition turned up, which the game hangs on the block it drew the description in
                // rather than on the label. Read the same way a drawn row's explanation is: carried in
                // the buffer, indicated or spoken by its own kind, and pointed AT on focus so the game
                // draws it at all.
                List<AgeTooltip> explaining = WordsTooltips(label, Words(window));
                AgeTooltip explains =
                    explaining.Count == 0 ? null : explaining[explaining.Count - 1];
                AgeTransform hover = explains == null ? null : Holder(explains);
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
                        Sections = GraphNodes.SectionsFor(explaining, () => Content(Current())),

                        // Where the words explain nothing, nothing is hovered: there is no control
                        // under the cursor to light up, and no tooltip of a neighbouring one to leave
                        // hanging over the popup.
                        OnFocusVisual =
                            hover == null
                                ? ReleasePointer
                                : () => PointerFocus.MoveTo(hover, explains),
                        OnBlurVisual = ReleasePointer,
                    }
                );
            }

            if (body != null)
            {
                Write(body, builder, window, lead);
            }
            else
            {
                // The words are already a row of their own, so they are not among the text the popup drew
                // - to either reading of it. A popup that has nothing but its words to show draws no rows
                // at all and is exactly what it was.
                Sheet sheet = ReadSheet(window, controls, inside, words);
                if (sheet == null)
                {
                    // A popup whose lines the game stamped out of a prefab rather than scrolled: the same
                    // table, found from what the popup DECLARES it fills rather than from a scroll view.
                    sheet = ReadTableSheet(window, controls, inside, words);
                }

                if (sheet == null)
                {
                    BuildDrawnBody(builder, window, controls, inside, words, TableLines(window));
                }
                else
                {
                    BuildSheet(builder, window, sheet, lead);
                }
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
                    id = AddRow(builder, item.Lines, index, item.Group);
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
            AgeTransform group = null
        )
        {
            List<Line> it = row;
            List<AgeTooltip> explaining = group == null
                ? Single(Explains(it[0].Tooltip, RowText(it)))
                : Explaining(group, RowText(it));
            NodeSection[] sections = new NodeSection[explaining.Count];
            for (int i = 0; i < explaining.Count; i++)
            {
                sections[i] = GraphNodes.TooltipSection(explaining[i]);
            }

            AgeTooltip tooltip = explaining.Count == 0 ? null : explaining[explaining.Count - 1];
            AgeTransform hover = tooltip == null ? null : Holder(tooltip);
            NodeVtable vtable = new NodeVtable
            {
                // No role word and no state: this is something the game wrote down for the player to
                // read, not a control they work.
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => RowText(it)),
                },
                Sections = GraphNodes.Sections(sections),
                OnFocusVisual =
                    hover == null ? ReleasePointer : () => PointerFocus.MoveTo(hover, tooltip),
                OnBlurVisual = ReleasePointer,
            };

            AgeTransform named = group ?? it[0].Widget;
            ControlId id = ControlId.Referenced(
                named,
                "notification:body/" + index + "/" + named.name
            );
            builder.AddItem(id, vtable);
            return id;
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

            List<AgeTransform> top = TopRails(window, controls);
            List<AgeTransform> bottom = BottomRails(controls);
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
                    !InBody(line.Widget, top, bottom)
                    || PartOf(line.Widget, controls)
                    || ReferenceEquals(line.Widget, words)
                    || IsIn(line.Widget, dossier)
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

        private static bool IsIn(AgeTransform widget, AgeTransform ancestor)
        {
            return ancestor != null && IsUnder(widget, ancestor);
        }

        /// <summary>Which of the table's lines this widget was drawn inside, or null where it was drawn
        /// outside all of them - a caption band, a totals footer.</summary>
        private static AgeTransform In(AgeTransform widget, List<AgeTransform> lines)
        {
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                if (IsUnder(widget, lines[i]))
                {
                    return lines[i];
                }
            }

            return null;
        }

        /// <summary>The table line a whole row was read out of - all of its pieces and nothing else's.
        /// A row of one piece is never one: see the reading in <see cref="DrawnRows"/>.</summary>
        private static AgeTransform GroupOf(List<Line> row, List<AgeTransform> lines)
        {
            if (row.Count < 2)
            {
                return null;
            }

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

            /// <summary>What the popup wrote UNDER the lines - the inspector's report ends with what all
            /// of it came to. Not a line of the table: it stands outside the rows, and reads as the
            /// full-width row it is drawn as.</summary>
            public List<List<Line>> Footer;
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

            for (int i = 0; sheet.Footer != null && i < sheet.Footer.Count; i++)
            {
                List<Line> band = sheet.Footer[i];
                table.Line(
                    new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => RowText(band)),
                        },
                        OnFocusVisual = ReleasePointer,
                    }
                );
            }

            table.Finish();
            if (lead == null)
            {
                // What the popup drew is all it has, so focus lands on its first line.
                builder.SetStart(table.FirstRow);
            }
        }

        /// <summary>The row itself: what the line says, all of it, and the game's own click where the
        /// game put one there. A table whose lines do nothing - what the inspector sold, which laws
        /// lapsed - has rows the player reads rather than works, and says no role word for a button that
        /// is not there.</summary>
        private static NodeVtable RowNode(SheetRow row)
        {
            AgeTransform widget = row.Widget;
            AgeTransform[] cells = row.Cells;
            NodeVtable vtable = Wired(widget)
                ? GraphNodes.Button(
                    () => CellText(cells[0]),
                    () => AgeWidgets.Press(widget),
                    () => AgeWidgets.Operable(widget),
                    AgeWidgets.Raw(widget)
                )
                : new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => CellText(cells[0])),
                    },
                    Sections = GraphNodes.Sections(null, AgeWidgets.Raw(widget)),
                };

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

            AgeControlButton click = AgeWidgets.Button(widget);
            if (click != null)
            {
                AgeWidgets.Point(vtable, click);
            }
            else
            {
                AgeWidgets.PointAt(vtable, widget);
            }

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

        // ---- the table a popup stamped out of a prefab ----

        /// <summary>
        /// The table a popup filled by CLONING a line: the inspector's report, the laws that lapsed, the
        /// systems that lost population.
        ///
        /// These are the same thing to the player as the scrolling table <see cref="ReadSheet"/> finds -
        /// a list of things and a fact or two about each - but the game builds them the other way round:
        /// no scroll view, and instead a container the popup names in its own code and refills every
        /// refresh from one prefab. That container is what <see cref="TableWidgets"/> declares, per popup,
        /// and its visible children are the lines.
        ///
        /// Whether the columns are NAMED is the prefab's decision rather than the code's: the popup
        /// writes its captions into labels the prefab lays out above the container, and nothing in the
        /// class says whether it has any. So this asks the screen: a single band of two or more words
        /// drawn clear above the container, with nothing else of the popup's own drawn out there, and
        /// every line's pieces falling one to a caption. Where that band exists the table reads as a
        /// table, columns spoken as the edge crossed to reach them; where it does not, the popup keeps
        /// the rows it always had, each line joined into the one row it looks like.
        ///
        /// What the popup wrote UNDER the container is the table's footer - the inspector's report ends
        /// with what all of it came to - and reads as the full-width row it is drawn as, after the lines.
        /// </summary>
        private static Sheet ReadTableSheet(
            NotificationWindow window,
            List<Control> controls,
            List<Control> inside,
            AgeTransform words
        )
        {
            try
            {
                List<AgeTransform> tables = TableWidgets(window, controls);

                // One container, or the captions above one of them would be read as captions over all
                // of them. A popup that drew two tables keeps its rows.
                if (tables == null || tables.Count != 1)
                {
                    return null;
                }

                AgeTransform table = tables[0];

                // A control the popup captioned and drew OUTSIDE the table is content a table reading
                // would drop, exactly as for a scrolling one.
                foreach (Control control in inside)
                {
                    if (!IsUnder(control.Widget, table))
                    {
                        return null;
                    }
                }

                // And a line the popup wired a click to is a control in its own right, walked in the band
                // it was drawn in. Where the popup HAS words, that band is a strip rather than the content
                // - the words are what divides the popup then - and the lines are already declared there:
                // reading them as a table too would declare every line twice. The rows stay.
                foreach (Control control in controls)
                {
                    if (IsUnder(control.Widget, table) && !Has(inside, control.Widget))
                    {
                        return null;
                    }
                }

                List<AgeTransform> lines = TableRows(table);
                if (lines.Count == 0)
                {
                    return null;
                }

                List<Line> headers = null;
                List<List<Line>> footer = null;
                foreach (
                    List<Line> band in AgeLayout.Rows(Outside(window, controls, words, table), LineWidget)
                )
                {
                    int where = AgeLayout.Band(band[0].Widget, table);
                    if (where < 0)
                    {
                        if (headers != null)
                        {
                            // Two bands above: one of them is a heading rather than a set of columns, and
                            // nothing here can tell which. The rows are the safe reading.
                            return null;
                        }

                        headers = band;
                    }
                    else if (where > 0)
                    {
                        if (footer == null)
                        {
                            footer = new List<List<Line>>();
                        }

                        footer.Add(band);
                    }
                    else
                    {
                        // Words drawn level with the lines but outside them would be dropped.
                        return null;
                    }
                }

                if (headers == null || headers.Count < 2)
                {
                    return null;
                }

                headers.Sort(AcrossTheRow);

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

                return new Sheet { Headers = headers, Rows = rows, Footer = footer };
            }
            catch (Exception e)
            {
                Log.Warn("notification: looking for a prefab table threw: " + e);
                return null;
            }
        }

        /// <summary>The text the popup drew in its content area OUTSIDE its table - the captions over it,
        /// the totals under it.</summary>
        private static List<Line> Outside(
            NotificationWindow window,
            List<Control> controls,
            AgeTransform words,
            AgeTransform table
        )
        {
            List<Line> outside = new List<Line>();
            AgeTransform root = Root(window);
            if (root == null)
            {
                return outside;
            }

            List<Line> drawn = new List<Line>();
            Read(root, drawn, null, 0);

            List<AgeTransform> top = TopRails(window, controls);
            List<AgeTransform> bottom = BottomRails(controls);
            AgeTransform dossier = Dossier(window);
            foreach (Line line in drawn)
            {
                if (
                    InBody(line.Widget, top, bottom)
                    && !PartOf(line.Widget, controls)
                    && !ReferenceEquals(line.Widget, words)
                    && !IsUnder(line.Widget, table)
                    && !IsIn(line.Widget, dossier)
                )
                {
                    outside.Add(line);
                }
            }

            return outside;
        }

        /// <summary>The lines of a prefab table: the children of the container the popup wrote something
        /// in. A line the game refilled with nothing - a clone it keeps around for the next turn and has
        /// hidden - is not a line.</summary>
        private static List<AgeTransform> TableRows(AgeTransform table)
        {
            List<AgeTransform> rows = new List<AgeTransform>();
            List<AgeTransform> children = table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child != null && Visible(child) && Draws(child, 0))
                {
                    rows.Add(child);
                }
            }

            rows.Sort(DownTheTable);
            return rows;
        }

        /// <summary>Every line of every prefab table this popup drew, for the reading that keeps rows: a
        /// line is one row whichever table it came from, so a popup with two of them (the improvements
        /// and the populations an obliterator destroyed) still reads a line at a time.</summary>
        private static List<AgeTransform> TableLines(NotificationWindow window)
        {
            List<AgeTransform> tables = TableWidgets(window, null);
            if (tables == null)
            {
                return null;
            }

            List<AgeTransform> lines = new List<AgeTransform>();
            for (int i = 0; i < tables.Count; i++)
            {
                lines.AddRange(TableRows(tables[i]));
            }

            return lines.Count == 0 ? null : lines;
        }

        /// <summary>The containers this popup fills with cloned lines, as the popup's own code names
        /// them, and only while the player can see them - a report panel a breakdown toggle has folded
        /// away draws nothing.</summary>
        private static List<AgeTransform> TableWidgets(
            NotificationWindow window,
            List<Control> controls
        )
        {
            Variant variant = VariantOf(window);
            if (variant == null || variant.Tables == null)
            {
                return null;
            }

            List<AgeTransform> tables = new List<AgeTransform>();
            List<AgeTransform> top = controls == null ? null : TopRails(window, controls);
            List<AgeTransform> bottom = controls == null ? null : BottomRails(controls);
            foreach (AgeTransform table in variant.Tables(window))
            {
                if (table == null || !Visible(table))
                {
                    continue;
                }

                if (controls != null && !InBody(table, top, bottom))
                {
                    continue;
                }

                tables.Add(table);
            }

            return tables;
        }

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

        /// <summary>Everything a subtree is showing, in the order it is laid out - hoisted to
        /// <see cref="EmpireDossier.Read"/>, which the popup body and the dossier both walk with.</summary>
        private static void Read(
            AgeTransform widget,
            List<Line> lines,
            AgeTooltip inherited,
            int depth
        )
        {
            EmpireDossier.Read(widget, lines, inherited, depth);
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

        /// <summary>
        /// What the popup offers on hovering its words.
        ///
        /// A notification whose sentence names a thing offers that thing's dossier on hover - "your
        /// empire now has access to Bluecap Mold" comes with the resource's stat block - and the game
        /// hangs it not on the label but on the BLOCK it drew the label in
        /// (<c>LuxuryDiscoveredNotificationWindow.ResourceTooltip</c> sits on the description group).
        /// So the walk starts at the words and goes up for as long as the container is nothing but the
        /// words' own block (<see cref="Wraps"/>), which is what reaches that group and stops before
        /// the one above it that also holds the picture beside the text.
        ///
        /// A tooltip that only repeats the words is not a second thing to say, the same as anywhere
        /// else on this screen.
        /// </summary>
        private static List<AgeTooltip> WordsTooltips(AgePrimitiveLabel label, string text)
        {
            List<AgeTooltip> kept = new List<AgeTooltip>();
            try
            {
                AgeTransform at = label == null ? null : label.AgeTransform;
                for (int depth = 0; at != null && depth < MaxAncestors; depth++)
                {
                    AgeTooltip tooltip = at.AgeTooltip;
                    if (
                        tooltip != null
                        && !kept.Contains(tooltip)
                        && Explains(tooltip, text) != null
                    )
                    {
                        kept.Add(tooltip);
                    }

                    AgeTransform parent = at.Parent;
                    if (parent == null || !Wraps(parent, at))
                    {
                        break;
                    }

                    at = parent;
                }
            }
            catch (Exception e)
            {
                Log.Warn("notification: looking for the words' explanation threw: " + e);
            }

            return kept;
        }

        /// <summary>Whether the container is drawn as nothing but this widget's own block - its
        /// rectangle the widget's, grown by the margins the widget was laid out with. That is exactly
        /// how the popup family sizes the group it draws its description in, and it is what tells that
        /// group apart from the container above it, which is drawn around the picture as well.
        /// </summary>
        private static bool Wraps(AgeTransform container, AgeTransform widget)
        {
            Rect inner = widget.GetGlobalPosition();
            Rect outer = container.GetGlobalPosition();
            return outer.xMin >= inner.xMin - widget.PixelMarginLeft - Slack
                && outer.yMin >= inner.yMin - widget.PixelMarginTop - Slack
                && outer.xMax <= inner.xMax + widget.PixelMarginRight + Slack
                && outer.yMax <= inner.yMax + widget.PixelMarginBottom + Slack;
        }

        /// <summary>How far a block may miss the widget it was sized to and still be that widget's
        /// block: a pixel of rounding, not a row of anything.</summary>
        private const float Slack = 2f;

        private static void Add(GraphBuilder builder, Control control)
        {
            Control it = control;
            AgeTooltip explains = Tip(it);
            NodeVtable vtable;
            if (it.Toggle == null)
            {
                vtable = GraphNodes.Button(
                    () => Caption(it),
                    () => Press(it),
                    () => Enabled(it.Widget),
                    explains
                );
            }
            else if (it.Radio || InRadioGroup(it.Toggle))
            {
                vtable = GraphNodes.Radio(
                    () => Caption(it),
                    () => State(it.Toggle),
                    () => Press(it),
                    () => Enabled(it.Widget),
                    null,
                    explains
                );

                // Picking is not doing: the popup wants the choice CONFIRMED, and where it draws no
                // button for that the game's own second click is what confirms - so the choice takes
                // the double-click chord for it. None of the four handlers it can reach
                // (<c>NarrativeEventBegunNotificationWindow.OnChoiceDoubleClick</c> :322-329,
                // <c>QuestBegunNotificationWindow.OnObjectiveValidated</c> :410-413 and the two
                // contextual-exchange windows' <c>OnChoiceDoubleClick</c>) reads the modifiers the
                // player is still holding while it runs.
                AgeControlToggle again = it.Toggle;
                if (
                    again.UseDoubleClick
                    && again.OnDoubleClickObject != null
                    && !string.IsNullOrEmpty(again.OnDoubleClickMethod)
                )
                {
                    vtable.OnDoubleClick = () =>
                        Send(again.OnDoubleClickObject, again.OnDoubleClickMethod, again.gameObject);
                }
            }
            else
            {
                vtable = GraphNodes.Checkbox(
                    () => Caption(it),
                    () => State(it.Toggle),
                    () => Press(it),
                    () => Enabled(it.Widget),
                    explains
                );
            }

            if (it.Radio && it.Toggle != null)
            {
                // A card in a set has no button to light up: its own toggle carries the hover.
                AgeWidgets.Point(vtable, it.Toggle, explains, it.Widget);
            }
            else
            {
                vtable.OnFocusVisual = () =>
                    PointerFocus.MoveTo(it.Button, explains, it.Widget);
                vtable.OnBlurVisual = ReleasePointer;
            }

            HandBackOnMinimize(it, vtable);
            builder.AddItem(IdOf(it), vtable);
        }

        /// <summary>
        /// Putting the popup aside hands the player back to the icon it came from, not to wherever they
        /// were standing when it arrived.
        ///
        /// A notification pops up on its own - the game raises it, most often on the turn the player has
        /// just ended - so the cursor underneath is on whatever they last touched, and closing the popup
        /// restores it. Measured: minimising put focus back on End Turn, one Enter from ending another
        /// turn. Minimise is the one control here that means "not now": the popup goes to the strip of
        /// icons and stays there, so the strip is where the player is now, and its own stop is what
        /// remembers which icon. Every other exit - Done, Inspect, the buttons that open a page - is going
        /// somewhere, and those keep the landing the page they opened chose.
        /// </summary>
        private static void HandBackOnMinimize(Control control, NodeVtable vtable)
        {
            if (control.Key != MinimizeKey || vtable.OnActivate == null)
            {
                return;
            }

            Action press = vtable.OnActivate;
            vtable.OnActivate = () =>
            {
                press();
                GraphNavigator navigator = ModEntry.Navigator;
                if (navigator != null)
                {
                    navigator.LandOnStopAfterClose(GlobalHud.NotificationStop);
                }
            };
        }

        private const string MinimizeKey = "minimize";

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

            /// <summary>The name the GAME has for this control where it wrote none on it - the confirm
            /// button a choice popup draws as a tick.</summary>
            public string Name;

            /// <summary>One of a set the player picks exactly one of, where the popup wired that
            /// exclusivity by hand instead of with a <c>GuiRadioGroup</c>.</summary>
            public bool Radio;

            /// <summary>The tooltip that explains this control where the game hung it somewhere other
            /// than on the control - a choice card's reason for refusing sits on the CARD, and the switch
            /// that refuses is a piece inside it.</summary>
            public AgeTooltip Tip;
        }

        /// <summary>The tooltip a control speaks and carries: its own where it has one, else the one the
        /// game hung on what the control is a piece of.</summary>
        private static AgeTooltip Tip(Control control)
        {
            return control.Widget.AgeTooltip ?? control.Tip;
        }

        /// <summary>
        /// The controls the popup is currently offering: the ones every notification has - dismissing
        /// it, putting it aside, showing where it happened, walking to its neighbours, deciding
        /// whether this kind should interrupt again - and whatever this particular one asks the player
        /// to decide. Found in no particular order, because where they are drawn is what decides
        /// where they are walked.
        ///
        /// <paramref name="own"/> is false for a popup that writes its own body: the skeleton is still the
        /// screen's, and everything the popup added of its own is the body's - declaring both would give
        /// the same button two nodes under two ids.
        /// </summary>
        private static List<Control> Controls(NotificationWindow window, bool own = true)
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
                List<AgeTransform> choices = own
                    ? ChoiceWidgets(window)
                    : new List<AgeTransform>();
                foreach (
                    AgeControl extra in own ? (IList<AgeControl>)Extras(window) : NoExtras
                )
                {
                    Add(
                        controls,
                        extra.name,
                        extra as AgeControlButton,
                        extra as AgeControlToggle,
                        null,
                        null,
                        In(extra.AgeTransform, choices) != null
                    );
                }

                // A choice the popup keeps exclusive itself is declared because the popup SAYS it is one,
                // not because it wrote a caption on it: a card whose words are all nested inside it -
                // a hero's name, class and politics each in its own group - has no caption to the shared
                // rule, and dropping it would leave the player unable to choose at all. What is written
                // on the card, all of it, is its name.
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

                    Add(
                        controls,
                        "choice/" + i + "/" + choice.name,
                        null,
                        switched,
                        null,
                        CardCaption(choice),
                        true,
                        choice.AgeTooltip
                    );
                }

                // The buttons that leave the popup for a page of their own, for a popup that drew one with
                // no words on it. Skipped where the shared rule already found it.
                foreach (Gateway gateway in own ? Gateways(window) : NoGateways)
                {
                    AgeControlButton button = Clickable(gateway.Widget);
                    if (button == null || Has(controls, button.AgeTransform))
                    {
                        continue;
                    }

                    Add(
                        controls,
                        "gateway/" + gateway.Widget.name,
                        button,
                        null,
                        null,
                        GatewayName(button.AgeTransform, gateway.NameKey)
                    );
                }

                // The button that puts the choice into effect, for a popup that drew it as a tick with
                // no words on it: the game has a name for it even where it wrote none there.
                AgeControl confirm = own ? Confirm(window) : null;
                if (confirm != null && !Has(controls, confirm.AgeTransform))
                {
                    Add(
                        controls,
                        "confirm",
                        confirm as AgeControlButton,
                        confirm as AgeControlToggle,
                        null,
                        ConfirmName()
                    );
                }

                Add(controls, "show-location", showLocation, ModStrings.NotifyShowLocation);
                Add(controls, MinimizeKey, minimize, ModStrings.NotifyMinimize);
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
            string nameKey,
            string name = null,
            bool radio = false,
            AgeTooltip tip = null
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
                    Name = name,
                    Radio = radio,
                    Tip = tip,
                }
            );
        }

        private static bool Has(List<Control> controls, AgeTransform widget)
        {
            foreach (Control control in controls)
            {
                if (ReferenceEquals(control.Widget, widget))
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>What a popup that owns its own body adds to the skeleton, as far as the shared
        /// reading is concerned: nothing.</summary>
        private static readonly AgeControl[] NoExtras = new AgeControl[0];

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
        /// shared reading would otherwise declare the same buttons a second time.
        ///
        /// <see cref="Gateways"/>: a button that leaves this popup for a page of its own - the negotiation
        /// table, a minor faction's diplomacy, the score screen, the academy. It is the same shape as
        /// <see cref="Confirm"/> and is listed for the same reason: the popup may have drawn it as a bare
        /// icon, and the shared rule drops a control with no words on it. Unlike Confirm the game has no
        /// single word for these, so each is named by whatever the popup DID write - the caption, else the
        /// sentence its tooltip opens with - and the mod's own phrase only where the popup wrote neither.
        /// A gateway the shared reading already found is not declared twice.
        ///
        /// A popup with no entry here is read entirely by the shared rules, which is the case for most
        /// of them. A stage adding a popup adds one entry and touches nothing else.
        /// </summary>
        private sealed class Variant
        {
            public Func<NotificationWindow, AgePrimitiveLabel> Words;
            public Func<NotificationWindow, IList<AgeTransform>> Tables;
            public Func<NotificationWindow, IList<AgeTransform>> Choices;
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
            // is in no radio group and turns one thing on and off), and what it unfolds is these.
            variants.Add(
                typeof(DisplacementReportNotificationWindow),
                new Variant
                {
                    Tables = w =>
                        Some(
                            ((DisplacementReportNotificationWindow)w).ImprovementsTable,
                            ((DisplacementReportNotificationWindow)w).PopulationsTable
                        ),
                }
            );
            variants.Add(
                typeof(IonWaveReportNotificationWindow),
                new Variant
                {
                    Tables = w => Some(((IonWaveReportNotificationWindow)w).ShipLinesTable),
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
            // The four battle popups: everything they show is a model, so each writes its own body.
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

            variants.Add(
                typeof(GroundBattleOutcomeSelectionNotificationWindow),
                new Variant
                {
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
                                NegotiationGatewayKey
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
                                MinorFactionGatewayKey
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
                                ScoreScreenGatewayKey
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
                                Transform(
                                    (
                                        (ContextualAcademyDiplomaticExchangeUpdateNotificationWindow)w
                                    ).academyScreen
                                ),
                                AcademyGatewayKey
                            )
                        ),
                }
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

        /// <summary>The mod's own names for where a gateway button goes, used only where the popup wrote no
        /// caption and no tooltip on it. Asked for optionally, so a build without the phrase leaves the
        /// button to whatever the game did write rather than reading a key aloud.</summary>
        private const string NegotiationGatewayKey = "notify.open-negotiation";
        private const string MinorFactionGatewayKey = "notify.open-minor-faction";
        private const string ScoreScreenGatewayKey = "notify.open-score-screen";
        private const string AcademyGatewayKey = "notify.open-academy";

        /// <summary>The roles the academy has handed out, which its popup draws as cloned lines inside a
        /// panel of its own - and only while the academy is in the state that shows them.</summary>
        private static IList<AgeTransform> Roles(
            ContextualAcademyDiplomaticExchangeUpdateNotificationWindow window
        )
        {
            AcademyRolesReportPanel panel = window.RoleLineTable;
            return Some(
                panel == null || !Visible(window.RolesPanel) ? null : panel.RoleLineTable
            );
        }

        /// <summary>The same panel in the popup that exists only to report a role - it is the whole
        /// content there, so its own visibility is the gate rather than a wrapper the popup shows and
        /// hides.</summary>
        private static IList<AgeTransform> Roles(AcademyRoleNotificationWindow window)
        {
            AcademyRolesReportPanel panel = window.RoleLineTable;
            return Some(panel == null || !Visible(panel.AgeTransform) ? null : panel.RoleLineTable);
        }

        private static AgeTransform Transform(AgeControl control)
        {
            try
            {
                return control == null ? null : control.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
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
                    if (container == null || !Visible(container))
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

        /// <summary>The toggle one line of a choice carries - the line itself where the game made the
        /// whole card the switch, else the one inside it.</summary>
        private static AgeControlToggle Switch(AgeTransform line)
        {
            if (line == null || !Visible(line))
            {
                return null;
            }

            AgeControlToggle toggle = line.GetComponent<AgeControlToggle>();
            if (toggle == null)
            {
                toggle = line.GetComponentInChildren<AgeControlToggle>(true);
            }

            return toggle != null && Visible(toggle.AgeTransform) ? toggle : null;
        }

        /// <summary>What a gateway button is called: the caption where the popup wrote one, else the
        /// sentence its tooltip opens with, else the mod's own name for where it goes. Null is a complete
        /// answer for the first two - the shared naming falls through to whatever is left.</summary>
        private static string GatewayName(AgeTransform widget, string nameKey)
        {
            string caption = CaptionOf(widget);
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
                if (widget == null || !Visible(widget))
                {
                    return null;
                }

                AgeControlButton button =
                    AgeWidgets.Button(widget) ?? widget.GetComponentInChildren<AgeControlButton>(true);
                return button != null && Visible(button.AgeTransform) ? button : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static readonly Gateway[] NoGateways = new Gateway[0];

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
                string title = AgeText.Clean(Gui.Localize(ConfirmTitleKey));
                return string.IsNullOrEmpty(title) || Gui.IsLocalizationKey(title) ? null : title;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private const string ConfirmTitleKey = "%NotificationValidateTitle";

        /// <summary>The label the popup put its words in: its own where it named one, else the shared
        /// description every notification has.</summary>
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

            return own != null && Visible(own.AgeTransform) && !string.IsNullOrEmpty(AgeText.Label(own))
                ? own
                : Value(window, NotificationDescription) as AgePrimitiveLabel;
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

            if (!string.IsNullOrEmpty(control.Name))
            {
                return control.Name;
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

        /// <summary>Everything written on a card, in the order it is laid out. The shared rule reads the
        /// labels a control carries DIRECTLY, which is all of them for a control the game drew flat; a
        /// card is built out of groups instead - a portrait block, a stats block - and the words are the
        /// whole of what the player is choosing between.</summary>
        private static string CardCaption(AgeTransform widget)
        {
            List<AgePrimitiveLabel> labels = new List<AgePrimitiveLabel>();
            Labels(widget, labels, 0);
            labels.Sort(AcrossTheControl);

            MessageBuilder caption = new MessageBuilder();
            foreach (AgePrimitiveLabel label in labels)
            {
                caption.ListItem(AgeText.Label(label));
            }

            return caption.Build();
        }

        private static void Labels(AgeTransform widget, List<AgePrimitiveLabel> into, int depth)
        {
            if (widget == null || depth > MaxAncestors || !widget.Visible)
            {
                return;
            }

            AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
            if (label != null && !string.IsNullOrEmpty(AgeText.Label(label)))
            {
                into.Add(label);
            }

            List<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                Labels(children[i], into, depth + 1);
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
        /// line at a time - a battle report is written as exactly those lines - and, for the popup that
        /// ends the player's game, what that means (<see cref="OwnElimination"/>): arriving speaks it as
        /// part of the screen's name, so it is here to be re-read and nowhere in the spoken readout,
        /// which would otherwise say it twice.</summary>
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

            string ending = OwnElimination(window);
            if (!string.IsNullOrEmpty(ending))
            {
                lines.Add(ending);
            }

            return lines;
        }

        /// <summary>
        /// The one thing this popup family cannot say for itself: that the empire just knocked out is the
        /// PLAYER's, so the game is over and the score screen is the only way on.
        ///
        /// The game writes one sentence for both cases - <c>%NotificationEmpireEliminatedDescriptionKnown</c>,
        /// "The empire of {0} has been eliminated", with the player's own leader and empire in the hole - and
        /// nothing else. Measured: the two prefab group sets the window swaps
        /// (<c>EmpireEliminatedNotificationWindow.Refresh</c> :52-65) hold no text at all. Elimination shows
        /// <c>ScoreScreenButton</c> and the full-screen <c>BackgroundGroup</c>; the normal case shows the
        /// minimize and dismiss buttons, the pop-up-again box and the browsing arrows. So on the player's own
        /// defeat the difference a sighted player sees is which BUTTONS are there - and the mod's reading
        /// already drops the hidden ones - while the words say the same thing either way. This sentence is
        /// the mod's, because the game has none.
        ///
        /// Both halves of the gate are asked. The game's own test says whose elimination it is
        /// (<c>NotificationEmpireEliminated.EliminatedEmpire</c>, which is what <c>Refresh</c> decides with),
        /// and the DRAWN test says the window has already changed character - announcing the end of a game
        /// one frame before the popup swaps its buttons would be reading a decision the player cannot see yet.
        /// </summary>
        private static string OwnElimination(NotificationWindow window)
        {
            try
            {
                EmpireEliminatedNotificationWindow elimination =
                    window as EmpireEliminatedNotificationWindow;
                if (elimination == null || !AnyDrawn(elimination.EliminationGroups))
                {
                    return null;
                }

                NotificationEmpireEliminated notification = elimination.NotificationEmpireEliminated;
                return notification != null && notification.EliminatedEmpire == Gui.PlayerEmpire
                    ? ModStrings.Get(ModStrings.NotifyOwnElimination)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Whether the popup is drawing any of a group set it switches on and off as a set.
        /// </summary>
        private static bool AnyDrawn(AgeTransform[] groups)
        {
            for (int i = 0; groups != null && i < groups.Length; i++)
            {
                if (Visible(groups[i]))
                {
                    return true;
                }
            }

            return false;
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
                AgePrimitiveLabel drawn = title
                    ? Value(window, label) as AgePrimitiveLabel
                    : DescriptionLabel(window);
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
