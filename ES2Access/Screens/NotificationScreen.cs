using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Speech;
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
                string title = Title(window);
                string description = Description(window);
                if (title == _title && description == _description)
                {
                    return;
                }

                _title = title;
                _description = description;

                Voice.Say(
                    new MessageBuilder().ListItem(title).ListItem(Words(window)).Build(),
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

            AgePrimitiveLabel label = Value(window, NotificationDescription) as AgePrimitiveLabel;
            AgeTransform words = label == null ? null : label.AgeTransform;

            // The strip above the words and the strip below them. A popup whose words cannot be found
            // has no strips to speak of, so everything goes in one row rather than being sorted into
            // bands that mean nothing.
            List<Control> above = new List<Control>();
            List<Control> below = new List<Control>();
            foreach (Control control in Controls(window))
            {
                if (words != null && AgeLayout.Band(control.Widget, words) > 0)
                {
                    below.Add(control);
                }
                else
                {
                    above.Add(control);
                }
            }

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
            if (words != null)
            {
                // Declared outside the rows: the notification's text is a block of words, not one item
                // of a list, so it takes no place in a count. The builder wires the strips above and
                // below it to it.
                ControlId id = WordsId(label);
                builder.AddNode(
                    id,
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
                builder.SetStart(id);
            }

            builder.SetRegion(BottomRegion);
            Strip(builder, below);
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
            NodeVtable vtable =
                it.Toggle == null
                    ? GraphNodes.Button(
                        () => Caption(it),
                        () => Press(it),
                        () => Enabled(it.Widget),
                        it.Widget.AgeTooltip
                    )
                    : GraphNodes.Checkbox(
                        () => Caption(it),
                        () => State(it.Toggle),
                        () => Press(it),
                        () => Enabled(it.Widget),
                        it.Widget.AgeTooltip
                    );

            vtable.OnFocusVisual = () =>
                PointerFocus.MoveTo(it.Button, it.Widget.AgeTooltip, it.Widget);
            vtable.OnBlurVisual = ReleasePointer;
            builder.AddItem(ControlId.Referenced(it.Widget, "notification:" + it.Key), vtable);
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

        private static string CaptionOf(AgeTransform widget)
        {
            try
            {
                foreach (AgePrimitiveLabel label in widget.GetChildren<AgePrimitiveLabel>(false))
                {
                    string text = AgeText.Label(label);
                    if (!string.IsNullOrEmpty(text))
                    {
                        return text;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("notification: reading a control's caption threw: " + e);
            }

            return null;
        }

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

        private static string Text(NotificationWindow window, PropertyInfo label, bool title)
        {
            if (window == null)
            {
                return null;
            }

            try
            {
                string text = AgeText.Label(Value(window, label) as AgePrimitiveLabel);
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }

                GuiNotification notification = window.GuiNotification;
                if (notification == null)
                {
                    return null;
                }

                return AgeText.Clean(
                    title ? notification.GetTitle() : notification.GetDescription()
                );
            }
            catch (Exception e)
            {
                Log.Warn("notification: reading the text threw: " + e);
                return null;
            }
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
