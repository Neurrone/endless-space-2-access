using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// The tutorial popup - the box that tells a new player what to do next - made navigable.
    ///
    /// It is a panel rather than a window, and it comes and goes on the game's schedule rather than
    /// the player's: finishing the step it asked for advances it to new words in the same box. So
    /// what is written on it is watched, and a page the player turned to and a page the game turned
    /// for them announce the same way.
    ///
    /// Minimising it is not this screen getting smaller, it is this screen ending. The box collapses
    /// to its title bar - the game crops the panel to the strip above its content, leaving the title,
    /// the close button and the arrow that collapsed it drawn at the top right of the screen - and
    /// everything this screen is for is behind that crop. So a minimised tutorial is not ours: the
    /// galaxy underneath takes the keyboard back, and the bar that is still on screen is declared
    /// THERE, by <see cref="BuildCollapsedBar"/>, among the things drawn down the right-hand edge.
    /// Nothing in the game's own notification strip stands for a minimised tutorial, so the bar is
    /// modelled as the bar: its title, its close button, and the arrow that brings it back.
    ///
    /// It is walked the way it is drawn: the strip across the top of the box - closing it, collapsing
    /// it - then the page itself, then the strip along the bottom that turns the pages, marks which
    /// page this is and points at the thing being talked about. Which strip a control belongs to is
    /// read off the rectangle the game drew it at, so a control the tutorial only shows sometimes
    /// lands where the player sees it.
    ///
    /// The page's text is a control in its own right and the one focus starts on: what the tutorial is
    /// asking for is the reason the box is there. Every other control speaks its own tooltip on focus
    /// and carries it as review-buffer content - the game wrote one sentence on each of them saying
    /// what it does - while the text carries the whole page, so a long objective can be re-read from
    /// where the words are.
    ///
    /// It sits below the notification popups, which are shown over it, and above nothing: it
    /// annotates the game's own view rather than replacing it.
    ///
    /// Pages are turned through the selector the popup itself turns them with, rather than by
    /// pressing its arrows, because the selector is the thing that actually holds the page number and
    /// tells the popup to redraw.
    ///
    /// Closing raises the game's question about switching the tutorial off, and that question is the
    /// same confirmation box every other question uses, so nothing here has to know about it.
    /// </summary>
    public sealed class TutorialScreen : Screen
    {
        /// <summary>How long to wait for the game to put the tutorial back on the popup after whatever
        /// was drawn over it has gone. It takes one frame; a couple more cost nothing and are not
        /// something to be exact about.</summary>
        private const int LingerFrames = 3;

        private int _linger;
        private int _page = -1;
        private string _title;
        private string _description;

        public override string Key
        {
            get { return "screen.tutorial"; }
        }

        /// <summary>Under the notification popups that are drawn over it, above the game's own
        /// view.</summary>
        public override int Layer
        {
            get { return 30; }
        }

        /// <summary>What the page is called. Spoken on arrival, ahead of the page text focus lands
        /// on, so the two together read as the box reads and neither says the other's half twice.
        /// </summary>
        public override string ScreenName
        {
            get
            {
                string title = Title(Panel());
                return string.IsNullOrEmpty(title)
                    ? ModStrings.Get(ModStrings.ScreenTutorial)
                    : title;
            }
        }

        /// <summary>
        /// Ours while the popup is showing a tutorial, and for as long as the game is only borrowing
        /// it. The window is always there while a game is running; the panel inside it is what appears
        /// and disappears.
        ///
        /// The popup going away is not the same as the tutorial being over. While anything is drawn
        /// over it - a notification popup, a modal - the game takes the tutorial off the popup
        /// altogether and puts it back afterwards, one frame later. A screen that stood down for that
        /// would hand the player to the galaxy underneath and announce it, twice, around a
        /// notification they were reading. So the tutorial stays ours while something is drawn over it
        /// - all of which is drawn over this screen too, so nothing is claimed that is not covered -
        /// and for the few frames the game takes to hand the popup back.
        ///
        /// Collapsing it IS the tutorial being over, for as long as it stays collapsed: the box is
        /// cropped to its title bar, everything this screen declares is behind the crop, and a screen
        /// that held on would sit on top of the galaxy owning the keyboard with nothing on it the
        /// player can see. The bar itself stays reachable from the galaxy (<see
        /// cref="BuildCollapsedBar"/>), which is where it is drawn.
        /// </summary>
        public override bool IsActive()
        {
            try
            {
                if (Showing() || (_linger > 0 && Covered()))
                {
                    _linger = LingerFrames;
                    return true;
                }

                if (_linger > 0)
                {
                    _linger--;
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool Showing()
        {
            return Open(Panel());
        }

        /// <summary>The popup showing a tutorial at full size. Collapsed is not showing: the game
        /// crops the panel rather than hiding anything, so every widget below the title bar is still
        /// marked visible and holding this page's words while none of it can be seen.</summary>
        private static bool Open(TutorialPopupPanel panel)
        {
            TutorialWindow window = Window();
            if (window == null || !window.Shown || panel == null)
            {
                return false;
            }

            return panel.IsBound && panel.Shown && !Minimized(panel);
        }

        /// <summary>Whether the box is collapsed to its title bar. The panel keeps that to itself, but
        /// it holds the arrow's tick box in step with it, which is also what the player sees.</summary>
        private static bool Minimized(TutorialPopupPanel panel)
        {
            try
            {
                return panel.MinimizeToggle != null && panel.MinimizeToggle.State;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Whether something the game draws over the tutorial is up - which is the reason it
        /// takes the tutorial off the popup, and every one of them is drawn over this screen as
        /// well.</summary>
        private static bool Covered()
        {
            GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
            return gui != null && (gui.IsAnyNotificationVisible || gui.IsAnyModalVisible);
        }

        /// <summary>Escape belongs to the game here - the popup is drawn over the galaxy, and the key
        /// means whatever the view under it says it means.</summary>
        public override bool Back()
        {
            return false;
        }

        /// <summary>Arrival says the page, so the watch starts from what was just said.</summary>
        public override void OnPush()
        {
            Remember();
        }

        public override void OnPop()
        {
            _linger = 0;
            _page = -1;
            _title = null;
            _description = null;
        }

        /// <summary>The tutorial advances itself as the player does what it asked, so a new page can
        /// arrive without anyone having turned to it.</summary>
        public override void OnUpdate()
        {
            try
            {
                TutorialPopupPanel panel = Panel();
                if (panel == null || !panel.IsBound)
                {
                    // The game has taken the tutorial off the popup while something is drawn over it.
                    // There is nothing to compare until it puts it back, and what it puts back is
                    // measured against the page the player last heard.
                    return;
                }

                if (
                    panel.PageIndex == _page
                    && Title(panel) == _title
                    && Description(panel) == _description
                )
                {
                    return;
                }

                Remember();
                Voice.Say(Announcement(), false);
            }
            catch (Exception e)
            {
                Log.Warn("tutorial: watching the shown page threw: " + e);
            }
        }

        public override void Build(GraphBuilder builder)
        {
            TutorialPopupPanel panel = Panel();
            if (panel == null || !panel.IsBound)
            {
                return;
            }

            List<Control> controls = new List<Control>();

            // The popup hides the next-page arrow on the last page rather than greying it out, so
            // the last page simply does not have one; the same goes for the arrow that points at what
            // the step is talking about, on a step that points at nothing.
            Collect(
                controls,
                panel.PreviousPageButton,
                "previous-page",
                ModStrings.TutorialPreviousPage,
                () => Previous(panel)
            );
            Collect(controls, panel.PageSelector);
            Collect(
                controls,
                panel.NextPageButton,
                "next-page",
                ModStrings.TutorialNextPage,
                () => Next(panel)
            );
            Collect(
                controls,
                panel.ShowLocationButton,
                "show-location",
                ModStrings.TutorialShowLocation,
                null
            );
            Collect(controls, panel.MinimizeToggle, "minimize", ModStrings.TutorialMinimize);
            Collect(controls, panel.CloseButton, "close", ModStrings.TutorialClose, null);

            AgePrimitiveLabel label = panel.DescriptionLabel;
            AgeTransform words = label == null ? null : label.AgeTransform;

            List<Control> above = new List<Control>();
            List<Control> below = new List<Control>();
            foreach (Control control in controls)
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

            Strip(builder, above, "tutorial:");
            if (words != null)
            {
                // Declared outside the rows: the page is a block of text, not one item of a list, so
                // it takes no place in a count. The builder wires the strips above and below it to it.
                ControlId id = WordsId(label);
                builder.AddNode(id, Page());
                builder.SetStart(id);
            }

            Strip(builder, below, "tutorial:");
        }

        /// <summary>
        /// The bar a collapsed tutorial leaves on screen, declared wherever the player is once this
        /// screen has stood down - which is the galaxy underneath. It is modelled as it is drawn: the
        /// close button, the title saying which tutorial is waiting, and the arrow that brings it back,
        /// in the order the bar reads. Answers whether there was anything to declare.
        /// </summary>
        public static bool BuildCollapsedBar(GraphBuilder builder)
        {
            TutorialPopupPanel panel = Panel();
            TutorialWindow window = Window();
            if (
                panel == null
                || !panel.IsBound
                || !panel.Shown
                || window == null
                || !window.Shown
                || !Minimized(panel)
            )
            {
                return false;
            }

            List<Control> bar = new List<Control>();
            Collect(bar, panel.CloseButton, "close", ModStrings.TutorialClose, null);
            Collect(bar, panel.MinimizeToggle, "minimize", ModStrings.TutorialMinimize);
            Collect(bar, panel.TitleLabel);
            if (bar.Count == 0)
            {
                return false;
            }

            bar.Sort(ReadingOrder);
            Strip(builder, bar, "hud:tutorial/");
            return true;
        }

        /// <summary>One strip of controls: left and right walk it, and up and down reach the page and
        /// the other strip because they are separate rows.</summary>
        private static void Strip(GraphBuilder builder, List<Control> controls, string prefix)
        {
            if (controls.Count == 0)
            {
                return;
            }

            builder.StartRow();
            foreach (Control control in controls)
            {
                Control it = control;
                NodeVtable vtable = it.Vtable;
                vtable.OnFocusVisual = () =>
                    PointerFocus.MoveTo(it.Button, it.Widget.AgeTooltip, it.Widget);
                vtable.OnBlurVisual = ReleasePointer;
                builder.AddItem(ControlId.Referenced(it.Widget, prefix + it.Key), vtable);
            }

            builder.EndRow();
        }

        /// <summary>What the page says, and nothing else: where it sits among the pages is drawn as
        /// the row of dots below it and read there, so saying it here as well would be the mod adding
        /// words the box does not have.</summary>
        private static NodeVtable Page()
        {
            return new NodeVtable
            {
                // No role word: the page is not a control the player works, it is what the tutorial
                // is telling them to do.
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => Words(Panel())),
                },
                DetailLines = Content,

                // Nothing is hovered while the player is on the page: there is no control under the
                // cursor to light up, and no tooltip of a neighbouring one to leave hanging over the
                // box.
                OnFocusVisual = ReleasePointer,
            };
        }

        private static ControlId WordsId(AgePrimitiveLabel label)
        {
            return ControlId.Referenced(label, "tutorial:page");
        }

        /// <summary>One thing the popup draws in a strip: the rectangle it is drawn at - which is what
        /// decides where it is walked - the button under it when there is one to light up, and how it
        /// reads and works.</summary>
        private struct Control
        {
            public string Key;
            public AgeTransform Widget;
            public AgeControlButton Button;
            public NodeVtable Vtable;
        }

        private static void Collect(
            List<Control> controls,
            AgeControlButton button,
            string key,
            string nameKey,
            Action activate
        )
        {
            if (!Visible(button))
            {
                return;
            }

            AgeControlButton it = button;
            Action press = activate ?? (() => Press(it));
            controls.Add(
                new Control
                {
                    Key = key,
                    Widget = it.AgeTransform,
                    Button = it,
                    Vtable = GraphNodes.Button(
                        () => ModStrings.Get(nameKey),
                        press,
                        () => Enabled(it.AgeTransform),
                        it.AgeTransform.AgeTooltip,
                        GraphNodes.ModeFor(it.AgeTransform.AgeTooltip)
                    ),
                }
            );
        }

        private static void Collect(
            List<Control> controls,
            AgeControlToggle toggle,
            string key,
            string nameKey
        )
        {
            if (!Visible(toggle))
            {
                return;
            }

            AgeControlToggle it = toggle;
            controls.Add(
                new Control
                {
                    Key = key,
                    Widget = it.AgeTransform,
                    Vtable = GraphNodes.Checkbox(
                        () => ModStrings.Get(nameKey),
                        () => it.State,
                        () => Flip(it),
                        () => Enabled(it.AgeTransform),
                        it.AgeTransform.AgeTooltip,
                        GraphNodes.ModeFor(it.AgeTransform.AgeTooltip)
                    ),
                }
            );
        }

        /// <summary>A label the popup draws and the player cannot work - the title on the bar a
        /// collapsed tutorial leaves behind, which is the whole of what says which tutorial it is.
        /// </summary>
        private static void Collect(List<Control> controls, AgePrimitiveLabel label)
        {
            if (label == null || !Visible(label.AgeTransform))
            {
                return;
            }

            AgePrimitiveLabel it = label;
            controls.Add(
                new Control
                {
                    Key = "title",
                    Widget = it.AgeTransform,
                    Vtable = new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => AgeText.Label(it)),
                        },
                    },
                }
            );
        }

        /// <summary>
        /// The dots the popup marks its pages with, one per page, the current one filled in. The game
        /// writes nothing on them, so each is named for the page it stands for; they are the position
        /// indicator the box actually draws, which is why the page itself says no position of its own.
        ///
        /// They are radio buttons, and pressing one jumps to its page - so pressing one does exactly
        /// that, through the group the dots belong to, which is what tells the popup to redraw.
        /// </summary>
        private static void Collect(List<Control> controls, StepSelector selector)
        {
            try
            {
                if (selector == null || !selector.IsSetUp || !Visible(selector.AgeTransform))
                {
                    return;
                }

                List<AgeTransform> marks = selector.MarksTable.Children;
                for (int i = 0; i < marks.Count; i++)
                {
                    AgeTransform mark = marks[i];
                    AgeControlToggle dot =
                        mark == null ? null : mark.GetComponent<AgeControlToggle>();
                    if (dot == null || !Visible(mark))
                    {
                        continue;
                    }

                    AgeControlToggle it = dot;
                    int page = i + 1;
                    NodeVtable vtable = new NodeVtable
                    {
                        // No role word: a dot is not a control the player came here to work, it is
                        // where the box says they are among the pages.
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(
                                () => ModStrings.Format(ModStrings.TutorialPageMark, page)
                            ),
                            GraphNodes.SelectedPart(() => it.State),
                        },
                        OnActivate = () => Pick(it),
                    };
                    controls.Add(
                        new Control
                        {
                            Key = "page-mark/" + page,
                            Widget = mark,
                            Vtable = vtable,
                        }
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("tutorial: reading the page marks threw: " + e);
            }
        }

        private static readonly Comparison<Control> ReadingOrder = delegate(Control a, Control b)
        {
            return AgeLayout.ReadingOrder(a.Widget, b.Widget);
        };

        private static void Previous(TutorialPopupPanel panel)
        {
            try
            {
                panel.PageSelector.Previous();
            }
            catch (Exception e)
            {
                Log.Warn("tutorial: turning back a page threw: " + e);
            }
        }

        private static void Next(TutorialPopupPanel panel)
        {
            try
            {
                panel.PageSelector.Next();
            }
            catch (Exception e)
            {
                Log.Warn("tutorial: turning a page threw: " + e);
            }
        }

        /// <summary>Press a control the way the engine presses it: every AGE button carries the
        /// object and the method name its own mouse handler sends to, so replaying that pair runs the
        /// popup's own handler with no click that could land on whatever the mouse is over.</summary>
        private static void Press(AgeControlButton button)
        {
            try
            {
                Send(button.OnActivateObject, button.OnActivateMethod, button.gameObject);
            }
            catch (Exception e)
            {
                Log.Warn("tutorial: pressing a control threw: " + e);
            }
        }

        /// <summary>Collapse or expand the popup exactly as a click does: the toggle's own state
        /// first, then the handler it is wired to.</summary>
        private static void Flip(AgeControlToggle toggle)
        {
            try
            {
                toggle.State = !toggle.State;
                Send(toggle.OnSwitchObject, toggle.OnSwitchMethod, toggle.gameObject);
            }
            catch (Exception e)
            {
                Log.Warn("tutorial: collapsing the popup threw: " + e);
            }
        }

        /// <summary>Jump to the page a dot stands for exactly as clicking that dot does: the dot's own
        /// state first, then the handler the group of dots is wired to, which is what settles which
        /// dot is filled in and tells the popup which page to draw.</summary>
        private static void Pick(AgeControlToggle mark)
        {
            try
            {
                mark.State = true;
                Send(mark.OnSwitchObject, mark.OnSwitchMethod, mark.gameObject);
            }
            catch (Exception e)
            {
                Log.Warn("tutorial: jumping to a page threw: " + e);
            }
        }

        private static void Send(GameObject target, string method, GameObject sender)
        {
            if (target != null && !string.IsNullOrEmpty(method))
            {
                target.SendMessage(method, sender, SendMessageOptions.DontRequireReceiver);
            }
        }

        private void Remember()
        {
            TutorialPopupPanel panel = Panel();
            _page = panel == null ? -1 : panel.PageIndex;
            _title = Title(panel);
            _description = Description(panel);
        }

        /// <summary>The page as one spoken line: its title, then what it says.</summary>
        private static string Announcement()
        {
            TutorialPopupPanel panel = Panel();
            return new MessageBuilder().ListItem(Title(panel)).ListItem(Words(panel)).Build();
        }

        /// <summary>
        /// What the page says, as one spoken line - the objective and the way to meet it.
        ///
        /// The game wraps the objective over as many lines as the box is wide, so its line breaks are
        /// where the words ran out and not punctuation. They are joined with a space, which is the
        /// sentence the game wrote; a comma between them would put a pause in the middle of one and
        /// read a full stop as "disabled., Once you".
        /// </summary>
        private static string Words(TutorialPopupPanel panel)
        {
            MessageBuilder message = new MessageBuilder();
            foreach (string line in AgeText.Lines(Description(panel)))
            {
                message.Fragment(line);
            }

            return message.Build();
        }

        /// <summary>The page as the review buffer holds it: its title, then its text a line at a
        /// time - an objective and the way to meet it are written as exactly those lines.</summary>
        private static IList<string> Content()
        {
            List<string> lines = new List<string>();
            TutorialPopupPanel panel = Panel();
            if (panel == null)
            {
                return lines;
            }

            try
            {
                string title = Title(panel);
                if (!string.IsNullOrEmpty(title))
                {
                    lines.Add(title);
                }

                foreach (string line in AgeText.Lines(Description(panel)))
                {
                    lines.Add(line);
                }
            }
            catch (Exception e)
            {
                Log.Warn("tutorial: reading the page threw: " + e);
            }

            return lines;
        }

        private static string Title(TutorialPopupPanel panel)
        {
            return panel == null ? null : AgeText.Label(panel.TitleLabel);
        }

        private static string Description(TutorialPopupPanel panel)
        {
            return panel == null ? null : AgeText.Label(panel.DescriptionLabel);
        }

        private static readonly Action ReleasePointer = PointerFocus.Release;

        private static bool Visible(AgeControl control)
        {
            try
            {
                return control != null && Visible(control.AgeTransform);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool Visible(AgeTransform widget)
        {
            try
            {
                return widget != null && widget.Visible;
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

        private static TutorialPopupPanel Panel()
        {
            TutorialWindow window = Window();
            return window == null ? null : window.TutorialPopupPanel;
        }

        private static TutorialWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<TutorialWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
