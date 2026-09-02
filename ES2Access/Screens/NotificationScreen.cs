using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Input;

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
    /// The popup is TWO Tab stops: what it says and draws, and then the rails it is worked with - the
    /// browsing arrows, the pop-up-again box, Minimize and Done, plus whatever buttons this popup put
    /// in its own bottom bar. Which of the three drawn bands a control belongs to is read off the
    /// CONTAINER the game drew it in rather than from a list of names, so a control a popup adds of
    /// its own - Accept and Refuse along the bottom bar, Empire Information out in the content - is
    /// walked where the player sees it without anything here knowing it exists; the two rails then
    /// make up the second stop between them.
    ///
    /// Not every popup fills that description in. A window that draws its own content - the research
    /// report, with a card per technology and a line per thing it unlocked - leaves the shared
    /// description label parked under a container it has hidden, still holding the raw template the
    /// game would have filled ("Research has been completed: {0}"). A sentence with a hole in it is
    /// not what the popup says, so a description whose label the player cannot see, or which still
    /// carries an unfilled slot, is treated as absent: not spoken, not a control, not in a buffer. So
    /// is one the window does not HOLD - a handful of popups wire the shared label to an object left
    /// out of their layout altogether, with the skeleton's own key still written on it, and that key
    /// localizes to a congratulation on the popup that announces a deed FAILED.
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
    /// than because it wrote a caption on them: a card the popup draws as a picture with no words at all
    /// still has to be choosable, and dropping it would leave a keyboard player unable to choose at all.
    /// What is written on the card, all of it, is its name. Picking is not doing - the popup wants the choice
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
    /// band of the content stop for it while it is open, because that is what the panel is to the
    /// player: more of what this popup is showing, there only while it is on screen. Its lines are
    /// that region's and nobody else's: they
    /// are drawn level with the content, so a popup that draws its own content leaves them out of it.
    ///
    /// Walking to the next notification keeps the same screen up with different words in it, so the
    /// change is watched for and announced rather than being left silent.
    ///
    /// Escape belongs to the game: the window is an input handler and turns it into Minimize.
    /// </summary>
    public sealed partial class NotificationScreen : Screen
    {
        /// <summary>How far up a parent chain to look before deciding it is not a chain.</summary>
        private const int MaxAncestors = 64;

        /// <summary>The two component walks a build makes over the popup, each made once per root per
        /// frame. Popups are POOLED and rebound to the next notification, so neither is kept beyond the
        /// frame.</summary>
        private static readonly FrameSweep<AgeControlScrollView> ScrollViews =
            new FrameSweep<AgeControlScrollView>("notification");

        private static readonly FrameSweep<AgeControl> WindowControls =
            new FrameSweep<AgeControl>("notification");

        /// <summary>How deep inside one cell of a drawn table to look for what it is showing. Three,
        /// measured against the construction report: a cell is a group holding a picture and a label,
        /// and the deepest word in one is two levels down.</summary>
        private const int MaxCellDepth = 3;

        /// <summary>The key the body's table emits its cells under.</summary>
        private const string SheetKey = "notification:table:";

        // The popup's two Tab stops. What it SAYS and what it DRAWS are one place - the reason it
        // interrupted - and the rails it is worked with are another: the same five controls on every
        // popup, which the player reaches for after reading rather than while reading (owner ruling
        // 2026-08-19). Tab is the step between the two, and the regions below stay the faster way
        // across each.
        private static readonly object ContentStop = "notification:content";
        private static readonly object ControlsStop = "notification:controls";

        // The four bands Alt+Up/Down jump between, top to bottom as the popup draws them: the first
        // two belong to the content stop and the last two to the controls stop.
        // Internal because the family's self-check sorts nodes by these (NotificationAudit): a band
        // renamed here and re-spelled there would leave the check reporting every popup clean.
        internal const string TopRegion = "notification:top";
        internal const string InfoRegion = "notification:empire-info";
        private const string BodyRegion = "notification:body";
        internal const string BottomRegion = "notification:bottom";

        private GuiManager _gui;
        private NotificationWindow[] _windows;
        private NotificationWindow _showing;
        private bool _up;
        private string _title;
        private string _description;

        public override string Key
        {
            get { return ModStrings.ScreenNotification; }
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

        /// <summary>
        /// GO TO WHERE THIS HAPPENED, from anywhere on the popup - the key the show-location button's
        /// own name carries ("Show location (Ctrl+L)").
        ///
        /// The popup itself is the one surface where the affordance belongs to the PAGE rather than to
        /// a row: the button is drawn in the bottom bar and the player is usually reading the body. So
        /// it is answered here rather than on a node, and it presses the game's own button - the popup
        /// IS showing, so the toggle at the end of that handler does what it is meant to do and puts
        /// the popup aside as the mouse would.
        ///
        /// Gated on the button being DRAWN, not merely bound: forty-one of the sixty-nine prefabs bind
        /// one their layout never holds (ES2 facts), and the same paint test that keeps those out of
        /// the walk keeps them out of the key.
        /// </summary>
        public override bool GoToLocation()
        {
            try
            {
                NotificationWindow window = Current();
                AgeControlButton button =
                    window == null ? null : Button(window, ShowLocationButton);
                if (
                    button == null
                    || !Painted(button.AgeTransform, Root(window))
                    || !AgeWidgets.Operable(button.AgeTransform)
                )
                {
                    return false;
                }

                AgeWidgets.Press(button);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn("notification: the go-to-location key threw: " + e);
                return false;
            }
        }

        /// <summary>The same fact asked before the press, for the key's claim.</summary>
        public override bool OffersGoToLocation
        {
            get
            {
                try
                {
                    NotificationWindow window = Current();
                    AgeControlButton button =
                        window == null ? null : Button(window, ShowLocationButton);
                    return button != null
                        && Painted(button.AgeTransform, Root(window))
                        && AgeWidgets.Operable(button.AgeTransform);
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>Where this screen is drawn: whichever popup is up right now, which is the same
        /// window its own four-invariant audit walks.</summary>
        public override AgeTransform RootTransform
        {
            get { return RootOf(Current()); }
        }

        /// <summary>Escape belongs to the game: the popup is an input handler and its own exit route
        /// is what turns the key into Minimize.</summary>
        public override bool Back()
        {
            return false;
        }

        /// <summary>
        /// Dev-only: a popup's words have SETTLED - it has arrived, or the player has browsed to
        /// another one and the labels now hold its text. Null in a player's game and whenever the
        /// dev server is off, which is what makes this cost a null check on an event that happens
        /// once per popup rather than anything per frame.
        ///
        /// The seam is here rather than in a patch on the game because the screen already knows the
        /// answer: it waits out the animation for its own announcement (<see cref="Ready"/>), and
        /// that is the same moment a check of what the popup draws becomes meaningful.
        ///
        /// The watcher answers whether it is FINISHED with this popup. Ready is not painted - the
        /// game calls a popup ready while its content is still fading up, and a watcher that measures
        /// what is drawn sees nothing there - so answering false asks to be shown the same popup again
        /// on the next ready frame, up to <see cref="MaxSettleWaits"/> of them. The watcher's own
        /// patience is what gives up in words; this cap only stops a broken one asking forever.
        /// </summary>
        internal static Func<NotificationWindow, bool> Shown;

        /// <summary>How many ready frames to let pass before the popup counts as settled. Measured:
        /// on the frame the game calls the popup ready its content is laid out but not FINISHED - the
        /// quest popup's body counted one item more than it does a moment later, which moved every
        /// row's position - so a watcher reading the first ready frame is reading a layout the player
        /// never sees.</summary>
        private const int SettleFrames = 2;

        /// <summary>How many ready frames the watcher may keep asking for before the screen stops
        /// offering the popup: about two seconds of them, which is several times the longest arrival
        /// animation measured.</summary>
        private const int MaxSettleWaits = 120;

        private NotificationWindow _settling;
        private int _settleFrames;
        private int _settleWaits;

        /// <summary>The page keys browse the notifications the way the popup's own previous/next
        /// buttons do - the same direction the BUTTONS mean, which is the opposite of what the game
        /// wired its own Up/Down to on this window. Silent at either end, where the game switches the
        /// button off.</summary>
        public override bool PagePrev()
        {
            NotificationWindow window = Current();
            return Page(AgeWidgets.Transform(Button(window, PreviousNotificationButton)));
        }

        public override bool PageNext()
        {
            NotificationWindow window = Current();
            return Page(AgeWidgets.Transform(Button(window, NextNotificationButton)));
        }

        /// <summary>Arrival says the notification, so the watch starts from what was just said.
        /// </summary>
        public override void OnPush()
        {
            NotificationWindow window = Current();
            Remember(window);
            Settling(window);
        }

        /// <summary>Start the countdown to telling <see cref="Shown"/> about this popup. Nothing
        /// watching means no countdown, so an unwatched game does not so much as compare a
        /// field.</summary>
        private void Settling(NotificationWindow window)
        {
            if (Shown == null || window == null)
            {
                return;
            }

            _settling = window;
            _settleFrames = SettleFrames;
            _settleWaits = 0;
        }

        private void Settled()
        {
            if (_settling == null || --_settleFrames > 0)
            {
                return;
            }

            NotificationWindow window = _settling;
            Func<NotificationWindow, bool> watching = Shown;
            if (watching == null)
            {
                _settling = null;
                return;
            }

            bool done;
            try
            {
                done = watching(window);
            }
            catch (Exception e)
            {
                Log.Warn("notification: the settled-popup watcher threw: " + e);
                done = true;
            }

            if (!done && ++_settleWaits <= MaxSettleWaits)
            {
                _settleFrames = 1;
                return;
            }

            _settling = null;
        }

        public override void OnPop()
        {
            _up = false;
            _title = null;
            _description = null;
            _settling = null;
            _settleWaits = 0;
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

                Settled();

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

                // Browsing to the next notification is a new popup as far as anything checking one
                // is concerned, and this is the frame its words became real.
                Settling(window);
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
                label != null && Painted(label.AgeTransform, Root(window))
                    ? label.AgeTransform
                    : null;

            // A popup whose content is a MODEL rather than text writes its own body, and then it owns
            // every control it added as well - so only the shared skeleton is collected here.
            Action<NotificationBody> body = BodyOf(window);
            List<Control> controls = Controls(window, body == null);
            List<Control> above = new List<Control>();
            List<Control> inside = new List<Control>();
            List<Control> below = new List<Control>();
            Sort(window, controls, above, inside, below);

            above.Sort(ReadingOrder);
            below.Sort(ReadingOrder);

            // Two stops - what the popup says and draws, then the rails it is worked with (owner
            // ruling 2026-08-19). What the popup says is why it interrupted, so it is what the walk
            // opens with; browsing to another notification and dismissing this one are the same two
            // strips on every popup, and putting them behind Tab means a long report is read to its
            // end without the controls in the way and reached in one key from anywhere in it. Within
            // each stop Alt+Up/Down jump straight between the regions. The empire-info region is
            // declared ahead of the content because that is where the panel actually opens - beside
            // the portrait, above the description - and it is simply absent on a build where
            // BuildEmpireInfo finds nothing to say.
            builder.BeginStop(ContentStop);
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
                // Through the nesting sink: the words block is drawn inside wrappers that carry
                // explanations of their own, and only the OUTERMOST is the one this node points at.
                // Every other one used to be a reviewed section on this node - a second hover target
                // read as part of the first - and is now an entry of its own, which is the standing
                // ruling about two hover targets. A popup with one explanation (which is every one
                // measured so far) declares exactly what it declared before.
                TooltipChildren.Carried carried =
                    TooltipChildren.Split(WordsTooltips(label, Words(window)));
                AgeTooltip explains = carried.Own;
                AgeTransform hover = explains == null ? null : AgeWidgets.TooltipOwner(explains);
                NodeVtable saying = new NodeVtable
                {
                    // No role word: the text is not a control the player works, it is what they
                    // were interrupted to read.
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => Words(Current())),
                    },
                    Sections = GraphNodes.Sections(() => Content(Current()), explains),

                    // Where the words explain nothing, nothing is hovered: there is no control
                    // under the cursor to light up, and no tooltip of a neighbouring one to leave
                    // hanging over the popup. Aimed here rather than by the door because the
                    // explanation hangs on a WRAPPER of the words and the pointer belongs on the
                    // holder the popup drew it in (the widget the tooltip is hung on).
                    OnFocusVisual =
                        hover == null
                            ? AgeWidgets.ReleasePointer
                            : () => PointerFocus.MoveTo(hover, explains),
                    OnBlurVisual = AgeWidgets.ReleasePointer,
                    PointsAt = () => hover == null ? null : explains,
                };
                DrawnNode said = Nodes.Drawn(lead, saying, label);
                if (carried.Children == null)
                {
                    builder.AddNode(said);
                }
                else
                {
                    // The one shape that has to change: a block with entries under it is a group, and
                    // a group is a row. The plain case above stays a raw node so that the words go on
                    // taking no place in a count.
                    builder.BeginGroup(said);
                    if (builder.IsExpanded(lead))
                    {
                        TooltipChildren.Emit(builder, WordsKey, carried.Children, builder.Region);
                    }

                    builder.EndGroup();
                }
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

            builder.BeginStop(ControlsStop);
            builder.SetRegion(TopRegion);
            Strip(builder, above);

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

        /// <summary>The control's name: the caption the game wrote on it, else the name this mod has
        /// for the role it plays - the game draws the browsing arrows and the pop-up-again box as
        /// icons and never names them.</summary>
        private static string Caption(Control control)
        {
            return control.ChordAction == null
                ? Named(control)
                : ChordNames.Label(Named(control), control.ChordAction, 0);
        }

        private static string Named(Control control)
        {
            string caption = control.Card != null ? ChoiceName(control) : CaptionOf(control.Widget);
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

        /// <summary>
        /// What the game wrote on a control, all of it: a card drawn as a heading and a name beside it
        /// - "Just Completed", "Xenobiology" - is one button saying both, and reading only the first of
        /// them names the shelf instead of the thing on it. Read across the way they are drawn rather
        /// than in the order the widget tree happens to list them.
        ///
        /// The whole subtree, not the control's direct children: a line the game builds as a block per
        /// column - the cancelled-relics line draws the system's name inside a <c>StarSystemInfo</c>
        /// group and the reason beside it - loses the whole of one column to a one-level reading, and
        /// nothing in the spoken line says a word is missing. A choice CARD reads the same subtree but
        /// keeps the pieces apart (<see cref="CaptionLines"/>): a title over a paragraph is not a
        /// caption spread over a row.
        ///
        /// This is NOT the question <see cref="Captioned"/> asks. "What does this control say" reads the
        /// subtree; "did the popup write a caption ON this control" must not, or every wired container
        /// holding text - the invisible sheet over a lore paragraph - becomes a control of its own.
        /// </summary>
        private static string CaptionOf(AgeTransform widget)
        {
            MessageBuilder caption = new MessageBuilder();
            List<string> written = CaptionLines(widget);
            for (int i = 0; i < written.Count; i++)
            {
                caption.ListItem(written[i]);
            }

            return caption.Build();
        }

        /// <summary>
        /// The same reading, kept apart: what the control says, one entry per label the game laid out
        /// in it, in the order they are read across it.
        ///
        /// <see cref="CaptionOf"/> joins them into the one phrase a control is NAMED by. A card whose
        /// substance is written on it - a choice's title over its consequences - is a different
        /// question: the title names it and the rest is content to walk, and both come off this one
        /// reading so the name can never be a piece the buffer leaves out.
        /// </summary>
        private static List<string> CaptionLines(AgeTransform widget)
        {
            List<string> written = new List<string>();
            try
            {
                List<AgePrimitiveLabel> labels = new List<AgePrimitiveLabel>();
                Labels(widget, labels, 0);
                labels.Sort(AcrossTheControl);

                foreach (AgePrimitiveLabel label in labels)
                {
                    string text = AgeText.Label(label);
                    if (!string.IsNullOrEmpty(text))
                    {
                        written.Add(text);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("notification: reading a control's caption threw: " + e);
            }

            return written;
        }

        /// <summary>The words a choice card is read from: the ones the game wrote on the SWITCH where it
        /// put them there, else the ones on the card around it - the same preference the shared naming
        /// has always had, asked once so the name and the buffer can never come from different
        /// readings.</summary>
        private static List<string> ChoiceCaptions(AgeTransform widget, AgeTransform card)
        {
            List<string> written = CaptionLines(widget);
            return written.Count > 0 || card == null || ReferenceEquals(card, widget)
                ? written
                : CaptionLines(card);
        }

        /// <summary>
        /// What a choice card is CALLED: the first thing written on it.
        ///
        /// A card is a title over its consequences - "Pillage", then six lines of what pillaging costs -
        /// and naming it with all of them makes every walk past it read the whole card, makes the
        /// "selected" word arrive a paragraph late, and gives the buffer one line to review. The title
        /// names it, the rest is content (<see cref="ChoiceDetail"/>); the buffer holds all of it either
        /// way, so nothing the card says is lost. A card with one label is unchanged: its one label is
        /// its title.
        /// </summary>
        private static string ChoiceName(Control control)
        {
            List<string> written = ChoiceCaptions(control.Widget, control.Card);
            return written.Count == 0 ? null : written[0];
        }

        /// <summary>Everything a choice card says, a line at a time: each label it draws, split where the
        /// game wrapped it - the consequences are written as one label of six lines, and a buffer holding
        /// them as one line is the blob again under another name.</summary>
        private static IList<string> ChoiceDetail(AgeTransform widget, AgeTransform card)
        {
            List<string> lines = new List<string>();
            List<string> written = ChoiceCaptions(widget, card);
            for (int i = 0; i < written.Count; i++)
            {
                IList<string> split = AgeText.Lines(written[i]);
                for (int j = 0; j < split.Count; j++)
                {
                    lines.Add(split[j]);
                }
            }

            return lines;
        }

        /// <summary>
        /// Whether the popup wrote a caption ON this control - the labels it laid out directly inside
        /// it, and no deeper.
        ///
        /// This is the test that tells a control the player works from the invisible click-catchers
        /// every notification is built out of: the sheet behind it that minimises it, the bar it is
        /// dragged by, the text area that finishes the typing animation. Every one of those WRAPS
        /// content - the quest popup's lore group is a wired button around the scroll view the
        /// paragraph is in - so a subtree reading answers "captioned" for all of them and the
        /// paragraph stops being a row of the body and becomes a button saying it.
        /// </summary>
        private static string Captioned(AgeTransform widget)
        {
            try
            {
                MessageBuilder caption = new MessageBuilder();
                List<AgePrimitiveLabel> labels = new List<AgePrimitiveLabel>();
                foreach (AgePrimitiveLabel label in widget.GetChildren<AgePrimitiveLabel>(false))
                {
                    labels.Add(label);
                }

                labels.Sort(AcrossTheControl);
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

        private static void Labels(AgeTransform widget, List<AgePrimitiveLabel> into, int depth)
        {
            // Content: which labels are collected at all.
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
                AgeTransform child = AgeWidgets.DrawnChild(children, i);
                if (child != null)
                {
                    Labels(child, into, depth + 1);
                }
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
                    AgeWidgets.Toggle(control.Toggle);
                    return;
                }

                AgeWidgets.Press(control.Button);
            }
            catch (Exception e)
            {
                Log.Warn("notification: pressing " + control.Key + " threw: " + e);
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

        /// <summary>The notification as the review buffer holds it: its title, then - where there is
        /// more than one of them, the readout having said the single line already - its description a
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

            // A description of ONE line is already the row's spoken readout, word for word - the buffer
            // opens with it - and listing it again puts the paragraph either side of the title. More
            // than one and they are the report's own lines, which the readout joined into prose and the
            // buffer is the only place they can be walked.
            IList<string> described = AgeText.Lines(Description(window));
            for (int i = 0; described.Count > 1 && i < described.Count; i++)
            {
                lines.Add(described[i]);
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
                if (AgeWidgets.Visible(groups[i]))
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
        /// The description is held to conditions the title is not. It has to be somewhere the
        /// player can SEE - a window that draws its own content parks the shared label under a
        /// container it has hidden, and what is written on a hidden label is the leftovers of a
        /// skeleton, not the popup's words - and it has to be FILLED IN: a notification that never
        /// overrode its description leaves the template with the hole still in it, both on the label
        /// and in what the notification answers, and either way "Research has been completed: {0}"
        /// tells the player nothing they were interrupted for. Titles are formatted properly by every
        /// popup there is, so neither condition is asked of them.
        ///
        /// A popup that holds no description label at all (<see cref="DescriptionLabel"/>) has no
        /// description, and the question stops there - it does not fall through to what the
        /// notification would have written on one. The fallback is for a label the popup DRAWS and has
        /// not filled; a popup with nowhere to draw a description never showed the player that sentence
        /// under any circumstances, and reading it out is inventing a line the game left out.
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
                if (!title && drawn == null)
                {
                    return null;
                }

                string text =
                    title || (drawn != null && AgeWidgets.Visible(drawn.AgeTransform))
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
        private static readonly PropertyInfo TitleGroup = Member("TitleGroup");
        private static readonly PropertyInfo NotificationTitle = Member("NotificationTitle");
        private static readonly PropertyInfo NotificationDescription = Member(
            "NotificationDescription"
        );

        private static PropertyInfo Member(string name)
        {
            return GameHandlers.Property(typeof(NotificationWindow), name);
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

        /// <summary>
        /// Whether the popup is really drawing this, or has folded it away.
        ///
        /// A report popup collapses its detail panel by FADING it: the panel keeps <c>Visible</c> true,
        /// keeps its rectangle and keeps every word inside it at alpha 1, so
        /// <see cref="AgeWidgets.Visible"/> says
        /// yes to a whole "Damage Report" the screen shows nothing of. Measured on
        /// <c>IonWaveReportNotificationWindow</c> with the report collapsed: <c>ReportPanel</c> visible,
        /// alpha 0, all five of its children visible at alpha 1, every ancestor above it at alpha 1.
        /// The step from a parent to a child is the engine's own drawn test
        /// (<see cref="AgeWidgets.Paints"/>) - the same one the parity probe walks with, so what the
        /// popup reads and what the probe measures agree by construction.
        ///
        /// The window's OWN alpha is never asked, which is what <paramref name="root"/> is for: a popup
        /// fades ITSELF in on arrival (measured: the window transform animates 0 to 1 while every child
        /// stays at alpha 1), and asking it would empty the body for the length of that animation.
        ///
        /// The walk must END at <paramref name="root"/>, not merely run out of parents. Every prefab
        /// binds the base window's rails by NAME, and forty-one of the sixty-nine bind a Show Location
        /// button their own layout never holds - an orphan with no parent at all, parked at the screen's
        /// origin, which the game then marks visible because the notification does have a location
        /// (ES2 facts). Visible, alpha 1, drawn nowhere: a chain test that stops at the first null parent
        /// calls that painted, and the player gets a stop that does nothing.
        /// </summary>
        private static bool Painted(AgeTransform widget, AgeTransform root)
        {
            try
            {
                if (widget == null || !AgeWidgets.Visible(widget))
                {
                    return false;
                }

                AgeTransform at = widget;
                for (
                    int depth = 0;
                    at != null && !ReferenceEquals(at, root) && depth < MaxAncestors;
                    depth++
                )
                {
                    // Painted with an explicit stop at root, so the WINDOW's own arrival fade is not read as
                    // a blank popup - which is more than any one-widget test can express.
                    if (!AgeWidgets.Paints(at))
                    {
                        return false;
                    }

                    at = at.Parent;
                }

                return ReferenceEquals(at, root);
            }
            catch (Exception)
            {
                return false;
            }
        }

    }
}
