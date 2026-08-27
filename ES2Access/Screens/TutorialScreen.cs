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
    /// everything this screen is for is behind that crop. So a minimised tutorial is not ours: the page
    /// underneath takes the keyboard back, and the bar that is still on screen is declared THERE, by
    /// <see cref="BuildCollapsedBar"/> - on the eleven pages that share the HUD's right-hand edge among
    /// the things drawn down it, and on every other page (<see cref="Screen.BuildShared"/>) wherever the
    /// game is still drawing the bar, which over a modal it is.
    /// Nothing in the game's own notification strip stands for a minimised tutorial, so the bar is
    /// modelled as the bar: its title, its close button, and the arrow that brings it back.
    ///
    /// It is walked as the list of pages it is: one row per page in the first stop, then the arrow that
    /// points at what the step is talking about, then the arrow that collapses the box, then the button
    /// that closes it - a stop each, one control per row. Standing on a page's row is what turns the box
    /// to that page, so up and down read the tutorial and the box follows visibly; the dots and the
    /// page arrows the box draws are not declared, because the list does their whole job and the
    /// engine's place-in-list stamp says which page this is.
    ///
    /// Focus starts on the page the box is already showing: what the tutorial is asking for is the
    /// reason the box is there. Every other control speaks its own tooltip on focus and carries it as
    /// review-buffer content - the game wrote one sentence on each of them saying what it does - while
    /// the page carries the whole of what it says, so a long objective can be re-read from where the
    /// words are.
    ///
    /// It sits above everything else of ours bar the error box and the confirmation box, because the game
    /// draws most tutorial popups over its own windows and a tutorial nobody can reach is worse than no
    /// tutorial at all. Minimising it is what gives the keyboard back.
    ///
    /// Pages are turned through the dot the popup itself turns them with, rather than by pressing its
    /// arrows, because the dots' group is the thing that actually holds the page number and tells the
    /// popup to redraw.
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

        /// <summary>
        /// Above every screen of ours except the two boxes that have to be answerable, because the game
        /// itself draws most tutorial popups above its own windows: of the game's tutorial definitions,
        /// 49 declare <c>AboveModalWindows</c> and 16 <c>AboveNotifications</c>
        /// (<c>Public\Tutorials\TutorialDefinitions*.xml</c>), and the popup this screen reads is the
        /// one the game has chosen to put on top. A tutorial buried under the window it was raised over
        /// is unreadable and unclosable, which is the worst thing this mod can do to a page.
        ///
        /// What makes so high a place livable is that a COLLAPSED tutorial stands down (<see
        /// cref="IsActive"/>): minimising the box hands the keyboard straight back to whatever is
        /// underneath. The error box at 99 and the message box at 100 stay above, because a popup that
        /// can be minimised is not the same as a question that must be answered - and the question this
        /// screen's own Close button raises is one of them, so it is still the page the player is on.
        /// </summary>
        public override int Layer
        {
            get { return 98; }
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
        /// Ours exactly while the popup is drawing a tutorial at full size, which is the only state in
        /// which there is anything here to read. The window is always there while a game is running;
        /// the panel inside it is what appears and disappears.
        ///
        /// Whether the popup survives a window opening over it is the tutorial's own decision, not
        /// something to be guessed at: each definition declares a popup layer, and the game hides the
        /// panel for the whole time a window it is not allowed above is up
        /// (<c>TutorialPopupPanel.UpdateLayerAndVisibilityAccordingToOtherWindows</c>). So an
        /// <c>UnderScreens</c> tutorial goes away for any screen, modal or notification, an
        /// <c>AboveModalWindows</c> one stays drawn over all of them, and following the panel follows
        /// the game.
        ///
        /// Which is why a hidden popup is never held onto. This screen sits above everything else of
        /// ours, so a screen that kept the keyboard while the panel was hidden would leave the player on
        /// a page they cannot see with the window that hid it unreachable underneath - the same defect
        /// as the burial this layer exists to fix, upside down. The cost is the announcement of whatever
        /// is underneath when the popup goes and comes back, which is the truth of what happened.
        ///
        /// The linger only bridges the frames between a covering window closing and the game putting the
        /// panel back, so it is kept - not spent - while something is covering it, and it bridges only
        /// while the popup is still bound to a tutorial: once the game has unbound it there is nothing
        /// coming back. It asks the collapsed question again as well, because the box can be COLLAPSED
        /// while a window is covering it - minimising a popup a modal is drawn over is the ordinary way
        /// to get at the modal - and a bridge that only asked whether a tutorial was still bound would
        /// hand the closing modal's frames to a box the player has already put away, which announces its
        /// title and its page over the page they went back to.
        ///
        /// Collapsing it IS the tutorial being over, for as long as it stays collapsed: the box is
        /// cropped to its title bar, everything this screen declares is behind the crop, and a screen
        /// that held on would own the keyboard with nothing on it the player can see. That is what makes
        /// layer 98 livable - minimising hands everything underneath the keyboard back. The bar itself
        /// stays reachable on whatever page that is (<see cref="BuildCollapsedBar"/>), for as long as the
        /// game keeps drawing it there.
        /// </summary>
        public override bool IsActive()
        {
            try
            {
                if (Showing())
                {
                    _linger = LingerFrames;
                    return true;
                }

                if (Covered())
                {
                    return false;
                }

                if (_linger > 0)
                {
                    _linger--;
                    TutorialPopupPanel panel = Panel();
                    return panel != null && panel.IsBound && !Minimized(panel);
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

        /// <summary>Whether a window the game weighs the tutorial popup against is up - which is why the
        /// panel may be hidden, and asked only once it is: a popup allowed above the window is still
        /// showing, and answers before this is reached. The three the game weighs are the three it
        /// passes to the panel (<c>UpdateLayerAndVisibilityAccordingToOtherWindows</c>): a full screen,
        /// a modal, a notification.</summary>
        private static bool Covered()
        {
            GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
            return gui != null
                && (
                    gui.IsAnyScreenVisible
                    || gui.IsAnyNotificationVisible
                    || gui.IsAnyModalVisible
                );
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

        /// <summary>The linger is deliberately not cleared: standing down for a window that covered the
        /// popup is what leaves it armed, and it is the frames after that window closes that it is for.
        /// </summary>
        public override void OnPop()
        {
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

        /// <summary>
        /// The popup as the list of pages it is, and then the two things that put it away.
        ///
        /// The box turns pages with a row of dots, arrows either side and one page of words in the
        /// middle - a shape that costs the keyboard three controls to read one sentence. Here the PAGES
        /// are the list: one row each, and standing on a row is what turns the box to it (owner ruling,
        /// 2026-08-18). Up and down therefore walk the tutorial, the box follows visibly, and the arrows
        /// and dots are gone because the list has taken over their whole job. Where the page number was
        /// said by the dots it is now the engine's own place-in-list stamp.
        ///
        /// The page switch happens where the row's WORDS are read rather than in a focus hook, and that
        /// is not a detail: the navigator composes a landing's speech the moment the cursor moves, long
        /// before any focus visual runs, so a switch driven from the hook would read the page the player
        /// just left. It is guarded on the row being the focused one, so a dump or a type-ahead pass
        /// that resolves every row's label turns no pages.
        /// </summary>
        public override void Build(GraphBuilder builder)
        {
            TutorialPopupPanel panel = Panel();
            if (panel == null || !panel.IsBound)
            {
                return;
            }

            BuildPages(builder, panel);

            // The arrow that points at what the step is talking about, on the steps that point at
            // something. Its own stop, drawn below the page as it is.
            List<Control> controls = new List<Control>();
            Collect(
                controls,
                panel.ShowLocationButton,
                "show-location",
                ModStrings.TutorialShowLocation,
                null
            );
            Rows(builder, controls, LocationStop, "tutorial:");

            controls.Clear();
            Collect(controls, panel.MinimizeToggle, "minimize", ModStrings.TutorialMinimize);
            Rows(builder, controls, MinimizeStop, "tutorial:");

            controls.Clear();
            Collect(controls, panel.CloseButton, "close", ModStrings.TutorialClose, null);
            Rows(builder, controls, CloseStop, "tutorial:");
        }

        /// <summary>One row per page the tutorial has, the row of the page on show being where focus
        /// lands. Every row reads the words the box is drawing, because standing on a row is what makes
        /// the box draw that page.</summary>
        private void BuildPages(GraphBuilder builder, TutorialPopupPanel panel)
        {
            AgePrimitiveLabel label = panel.DescriptionLabel;
            if (label == null || !Visible(label.AgeTransform))
            {
                return;
            }

            int pages = Pages(panel);
            int current = Current(panel);
            builder.BeginStop(PagesStop);
            for (int i = 0; i < pages; i++)
            {
                ControlId id = ControlId.Structural(PageKey + i);
                builder.AddItem(Nodes.Synthetic(id, Page(i)));
                if (i == current)
                {
                    builder.SetStart(id);
                }
            }
        }

        /// <summary>How many pages the box has. The selector only sets itself up for more than one
        /// (<c>StepSelector.Setup</c>), so a box with no dots is a box with a single page.</summary>
        private static int Pages(TutorialPopupPanel panel)
        {
            StepSelector selector = panel.PageSelector;
            return selector != null && selector.IsSetUp ? selector.StepNb : 1;
        }

        /// <summary>Which page the box is drawing, counted among the pages this tutorial made
        /// available rather than among the ones it defines - which is what the dots count too.</summary>
        private static int Current(TutorialPopupPanel panel)
        {
            StepSelector selector = panel.PageSelector;
            return selector != null && selector.IsSetUp ? selector.CurrentSelection : 0;
        }

        /// <summary>
        /// The bar a collapsed tutorial leaves on screen, declared wherever the player is once this
        /// screen has stood down - which is whatever page the game handed the keyboard back to.
        ///
        /// The bar is named rather than described: the stop is called "Tutorial" and holds its three
        /// things one per row - the title saying which tutorial is waiting, the arrow that brings it
        /// back, and the button that closes it (owner ruling, 2026-08-18). That order is deliberately
        /// not the drawn one: the drawn bar puts Close first, and what the player wants first is which
        /// tutorial this is.
        ///
        /// The gate is the game's own drawing, which is why callers need no rule of their own: the panel
        /// is bound, shown and cropped to its title bar. A tutorial the game has HIDDEN for the window
        /// that opened over it (an <c>UnderScreens</c> page) is not shown, and then this declares nothing
        /// - which is what the answer is for.
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
            Collect(bar, panel.TitleLabel);
            Collect(bar, panel.MinimizeToggle, "minimize", ModStrings.TutorialMinimize);
            Collect(bar, panel.CloseButton, "close", ModStrings.TutorialClose, null);
            if (bar.Count == 0)
            {
                return false;
            }

            builder.PushContext(ModStrings.Get(ModStrings.TutorialBar));
            Rows(builder, bar, null, "hud:tutorial/");
            builder.PopContext();
            return true;
        }

        /// <summary>One node per row, in the order they were handed over: the bar's members are peers of
        /// one kind, so up and down walk them and nothing has to be guessed about which way the game
        /// packed them. <paramref name="stop"/> begins a stop first where the caller has one to begin,
        /// and nothing is declared at all for an empty set - an empty stop is a Tab press that lands
        /// nowhere.</summary>
        private static void Rows(
            GraphBuilder builder,
            List<Control> controls,
            object stop,
            string prefix
        )
        {
            if (controls.Count == 0)
            {
                return;
            }

            if (stop != null)
            {
                builder.BeginStop(stop);
            }

            foreach (Control control in controls)
            {
                Control it = control;
                NodeVtable vtable = it.Vtable;
                vtable.OnFocusVisual = () =>
                    PointerFocus.MoveTo(it.Button, it.Widget.AgeTooltip, it.Widget);
                vtable.OnBlurVisual = ReleasePointer;
                builder.AddItem(Nodes.Drawn(ControlId.For(it.Widget, prefix + it.Key), vtable, it.Widget));
            }
        }

        /// <summary>
        /// One page of the tutorial: the words the box is drawing, and - asked first - the box being
        /// turned to this page in the first place.
        ///
        /// Reading the label is where the turn happens because it is the only thing that runs between
        /// the cursor arriving and the landing being spoken. It is guarded twice over: the box is
        /// already on the page for every read but the first, and a read on a row that is not the
        /// focused one (a graph dump, a type-ahead pass over the stop) turns nothing.
        /// </summary>
        private NodeVtable Page(int page)
        {
            int it = page;
            return new NodeVtable
            {
                // No role word: the page is not a control the player works, it is what the tutorial
                // is telling them to do.
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() =>
                    {
                        Show(it);
                        return Words(Panel());
                    }),
                },
                Sections = GraphNodes.Sections(Content, null),

                // Nothing is hovered while the player is on the page: there is no control under the
                // cursor to light up, and no tooltip of a neighbouring one to leave hanging over the
                // box.
                OnFocusVisual = ReleasePointer,
            };
        }

        /// <summary>Turn the box to <paramref name="page"/> the way clicking its dot does, if it is not
        /// there already and if that page's row is the one the cursor is standing on. What the box then
        /// says is remembered, so the watcher that reads pages the GAME turned stays quiet about a page
        /// the player turned to and is about to hear.</summary>
        private void Show(int page)
        {
            try
            {
                TutorialPopupPanel panel = Panel();
                StepSelector selector = panel == null ? null : panel.PageSelector;
                if (
                    selector == null
                    || !selector.IsSetUp
                    || selector.CurrentSelection == page
                    || FocusedPage() != page
                )
                {
                    return;
                }

                List<AgeTransform> marks = selector.MarksTable.Children;
                AgeTransform mark = page >= 0 && page < marks.Count ? marks[page] : null;
                AgeControlToggle dot =
                    mark == null ? null : mark.GetComponent<AgeControlToggle>();
                if (dot == null)
                {
                    return;
                }

                Pick(dot);
                Remember();
            }
            catch (Exception e)
            {
                Log.Warn("tutorial: turning to the page under the cursor threw: " + e);
            }
        }

        /// <summary>Which page's row the cursor is on, or -1 for anywhere else.</summary>
        private static int FocusedPage()
        {
            ControlId key = ModEntry.Navigator == null ? null : ModEntry.Navigator.FocusedKey;
            string structural = key == null ? null : key.StructuralKey as string;
            if (structural == null || !structural.StartsWith(PageKey, StringComparison.Ordinal))
            {
                return -1;
            }

            int page;
            return int.TryParse(structural.Substring(PageKey.Length), out page) ? page : -1;
        }

        private const string PageKey = "tutorial:page/";

        private static readonly object PagesStop = "tutorial:pages";
        private static readonly object LocationStop = "tutorial:show-location";
        private static readonly object MinimizeStop = "tutorial:minimize";
        private static readonly object CloseStop = "tutorial:close";

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
                        it.AgeTransform.AgeTooltip
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
                        it.AgeTransform.AgeTooltip
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

        /// <summary>
        /// What the review buffer holds BEYOND the page itself: the title of the box.
        ///
        /// The page's words are already the buffer's first line - the engine opens every buffer with the
        /// control's own readout, and this control's readout is the page - so declaring them here as
        /// well is the same paragraph twice, which is what it was. The title is not said by anything the
        /// player can come back to: it is the screen's name, spoken once on arrival, so the buffer is
        /// the only place it can be re-read from.
        /// </summary>
        private static IList<string> Content()
        {
            List<string> lines = new List<string>();
            try
            {
                string title = Title(Panel());
                if (!string.IsNullOrEmpty(title))
                {
                    lines.Add(title);
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
