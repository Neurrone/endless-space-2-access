using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// A short list of the things that can be done to one control, opened over the screen the control
    /// is on.
    ///
    /// It exists because Right is spoken for. On a page with a tree in it - a system opening into its
    /// planets, a queue opening into its lines - Right means "go in", and a control that also wanted
    /// Right to mean "and here are its four actions" would be asking the player to know which of the
    /// two a given control is. So a control with more than one thing to do answers Enter with a menu,
    /// and every such control on every screen answers it the same way.
    ///
    /// The options are a SNAPSHOT, taken when the menu opens from whatever was possible then. Nothing
    /// in here is ever unavailable: an action the game is refusing is simply not in the list, which is
    /// what makes a three-line menu a three-line menu instead of a nine-line one with six refusals.
    /// The snapshot is safe because the menu is thrown away when it closes - it never outlives the
    /// moment it was built for.
    ///
    /// Opening always goes through <see cref="Open"/>, and an empty list is answered there, once, for
    /// every caller. (The wotr-access screen this is ported from left that check to each call site and
    /// it was copy-pasted five times.)
    /// </summary>
    public sealed class ChoiceSubmenuScreen : Screen
    {
        private readonly string _title;
        private readonly List<string> _options;
        private readonly List<Func<IList<string>>> _details;
        private readonly int _current;
        private readonly Action<int> _select;

        private ChoiceSubmenuScreen(
            string title,
            List<string> options,
            List<Func<IList<string>>> details,
            int current,
            Action<int> select
        )
        {
            _title = title;
            _options = options;
            _details = details;
            _current = current;
            _select = select;
        }

        /// <summary>
        /// Open a menu of <paramref name="options"/> over the screen the player is on, calling
        /// <paramref name="select"/> with the index of whichever one they pick.
        /// <paramref name="current"/> is the entry already in force, which is where focus lands and
        /// which says it is selected; -1 for a menu of actions, where none of them is.
        ///
        /// With nothing to offer, the menu is not opened and the player is told so - one control that
        /// answers Enter with silence is indistinguishable from a broken key.
        /// </summary>
        public static bool Open(
            string title,
            List<string> options,
            int current,
            Action<int> select,
            List<Func<IList<string>>> details = null
        )
        {
            if (options == null || options.Count == 0)
            {
                Voice.Say(ModStrings.Get(ModStrings.MenuNoActions), true);
                return false;
            }

            ScreenManager screens = ModEntry.Screens;
            Screen parent = screens == null ? null : screens.Current;
            if (parent == null)
            {
                return false;
            }

            parent.PushChild(
                new ChoiceSubmenuScreen(title, options, details, current, select)
            );
            return true;
        }

        public override string Key
        {
            get { return "screen.choice-submenu"; }
        }

        /// <summary>What the menu is for - the control it was opened from, in that control's own
        /// words - so opening reads the heading and then the entry focus landed on.</summary>
        public override string ScreenName
        {
            get { return _title; }
        }

        /// <summary>Never polled: this screen exists because a control opened it, and it is only ever
        /// a child of the screen that control is on.</summary>
        public override bool IsActive()
        {
            return false;
        }

        /// <summary>Escape leaves without doing anything. Claimed rather than passed on, so it closes
        /// the menu and NOT the page behind it: this menu is the mod's own idea and the game knows
        /// nothing about it, so an Escape that reached the game as well would close the menu and open
        /// the pause screen in the same keystroke.</summary>
        public override bool Back()
        {
            CloseSelf();
            return true;
        }

        public override bool ConsumesBack
        {
            get { return true; }
        }

        public override void Build(GraphBuilder builder)
        {
            for (int i = 0; i < _options.Count; i++)
            {
                int index = i;
                string label = _options[i];
                NodeVtable vtable = GraphNodes.MenuItem(
                    () => label,
                    () => Choose(index),
                    _current < 0 ? (Func<bool>)null : () => index == _current,
                    _details != null && index < _details.Count ? _details[index] : null
                );
                builder.AddItem(ControlId.Structural("menu:" + index), vtable);
            }

            // The entry already in force is where the player is put down, the same rule a value list
            // follows; a menu of actions has none, and starts at the top.
            if (_current >= 0 && _current < _options.Count)
            {
                builder.SetStart(ControlId.Structural("menu:" + _current));
            }
        }

        /// <summary>Do it, then close - in that order. Closing first would leave the action running
        /// against a screen the manager has already moved the cursor off, and an action that opens
        /// something of its own (a rename box, a confirmation) would find itself parented to nothing.
        /// </summary>
        private void Choose(int index)
        {
            if (_select != null)
            {
                _select(index);
            }

            CloseSelf();
        }
    }
}
