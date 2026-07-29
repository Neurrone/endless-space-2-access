using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// The list a combo box opens, as a screen of its own. Enter on a setting's drop list puts the
    /// player in here; up and down walk the entries, Enter picks one, Escape leaves the setting as it
    /// was.
    ///
    /// It is a screen rather than a mode of the options page because that is what it is to the player:
    /// a smaller thing on top of a bigger one, with its own name (the setting being chosen), its own
    /// contents, and its own way out. The options page underneath stays exactly where it was and gets
    /// the cursor back on the setting the player just answered.
    ///
    /// Which list is open is mod state, not a question asked of the game: the game's own drop list has
    /// no notion of being open by keyboard. The game's real popup is opened and closed alongside it so
    /// that the screen shows what the player is doing, and the entry the player is standing on is
    /// highlighted the way hovering it would - but the setting itself is not touched until Enter, so
    /// walking the list and then leaving changes nothing.
    ///
    /// The engine's own focus IS handed to the drop list while the list is open, because that is what
    /// makes Escape behave. The game's input manager consumes Escape outright whenever the focused
    /// control is keyboard-exclusive and accepts a standard cancel - clearing the focus and returning
    /// handled, without the options window ever seeing it - which is exactly why a mouse-opened popup
    /// closes on Escape and leaves the window standing. Without the focus, Escape would go straight to
    /// the window and take the whole options page down with it.
    ///
    /// The cost is that the mod's input layer stands down for a keyboard-exclusive control, which
    /// would leave the player in a list they could not move in. So the layer is told, through the hook
    /// it offers for exactly this, that this one control is the mod's - a drop list reads no keys of
    /// its own, so there is nothing to contend with.
    ///
    /// Both sides therefore see the same Escape, and the engine's side is the one that must act on it
    /// (see <see cref="Back"/>); every closing path here is written to be harmless a second time, so
    /// whichever order they arrive in, the popup ends up closed once and the window stays up.
    /// </summary>
    public sealed class DropListScreen : Screen
    {
        /// <summary>The setting whose list is open, or null. Static because opening it is a decision
        /// the options page makes and this screen's existence is the consequence.</summary>
        private static OptionDropListItem _open;

        /// <summary>The list this screen is currently showing. Held separately from
        /// <see cref="_open"/> so that picking an entry can close the screen while leaving the popup
        /// for <see cref="OnPop"/> to shut down.</summary>
        private OptionDropListItem _showing;

        /// <summary>The frame Escape was pressed on, or -1. The close happens after it; see
        /// <see cref="Back"/>.</summary>
        private int _closeAfterFrame = -1;

        /// <summary>The control the mod has handed the game's keyboard focus to, or null. Static
        /// because the input layer asks about it from outside any screen.</summary>
        private static AgeControl _focus;

        /// <summary>Open <paramref name="item"/>'s list. Takes effect on the next tick, when the
        /// screen manager notices this screen is now the one the player is on.</summary>
        public static void Open(OptionDropListItem item)
        {
            _open = item;
        }

        /// <summary>Whether <paramref name="control"/> is the widget this screen has the game's
        /// keyboard focus on - the input layer's question, answered from here because which widgets
        /// the mod drives is the screens' business.</summary>
        public static bool OwnsFocus(AgeControl control)
        {
            return control != null && ReferenceEquals(control, _focus);
        }

        /// <summary>Forget any open list - the mod is going away.</summary>
        public static void Reset()
        {
            _open = null;
            ReleaseFocus();
        }

        public override string Key
        {
            get { return "screen.drop-list"; }
        }

        /// <summary>Above the options page it belongs to, below the confirmation box.</summary>
        public override int Layer
        {
            get { return 20; }
        }

        /// <summary>The setting being chosen, in the game's own words - so opening the list reads
        /// "Resolution" and then the resolution currently set.</summary>
        public override string ScreenName
        {
            get { return Title(_showing ?? _open); }
        }

        /// <summary>Ours while a list is open, its drop list still exists, and the window it lives on
        /// is still up - the window closing under us takes the list with it. Once the popup has
        /// actually been opened, the game closing it counts too: Escape reaching the engine's own
        /// cancel handling takes the focus away, and the drop list closes its own popup on losing
        /// focus. Then this screen goes with it and the cursor is back on the setting.</summary>
        public override bool IsActive()
        {
            OptionDropListItem item = _open;
            try
            {
                if (item == null || item.DropList == null)
                {
                    return false;
                }

                OptionsModalWindow window = OptionsScreen.Window();
                if (window == null || !window.Shown)
                {
                    return false;
                }

                // Only once we have opened it: on the tick this screen first becomes active the popup
                // has not been opened yet - OnPush is what opens it.
                return _showing == null || StillOpen(item.DropList);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool StillOpen(AgeControlDropList list)
        {
            AgeManager age = AgeManager.Instance;
            return list.MenuActive && age != null && ReferenceEquals(age.FocusedControl, list);
        }

        /// <summary>
        /// Escape closes the list, not the window behind it - and the closing is left until the next
        /// tick, deliberately.
        ///
        /// The engine's own cancel handling is what keeps Escape from reaching the options window, and
        /// it only does that while the drop list still holds the keyboard. Closing the popup here and
        /// now would give the keyboard back mid-frame, and if the engine's input pass had not run yet
        /// it would then find nothing exclusive focused and pass the very Escape we were shielding
        /// against straight to the window - which would close the whole options page. Nothing in the
        /// game guarantees which of the two runs first.
        ///
        /// So the key is claimed and nothing is touched. By the next tick the engine has almost always
        /// closed the popup itself and this screen has already gone; if it has not, the request below
        /// closes it, a frame late and unnoticeably.
        /// </summary>
        public override bool Back()
        {
            _closeAfterFrame = Time.frameCount;
            return true;
        }

        public override void OnUpdate()
        {
            if (_closeAfterFrame >= 0 && Time.frameCount > _closeAfterFrame)
            {
                _closeAfterFrame = -1;
                _open = null;
            }
        }

        /// <summary>Show the game's real popup, so the screen shows the list the player is in.
        /// </summary>
        public override void OnPush()
        {
            _showing = _open;
            _closeAfterFrame = -1;
            AgeControlDropList list = ListOf(_showing);
            if (list == null)
            {
                return;
            }

            try
            {
                OptionsScreen.Call(OpenPopup, list);
                TakeFocus(list);
            }
            catch (Exception e)
            {
                Log.Warn("drop list: opening the popup threw: " + e);
            }
        }

        /// <summary>Put the game's keyboard focus where a mouse-opened popup would have put it, so
        /// that the engine's own cancel handling answers Escape instead of the window behind us.
        /// </summary>
        private static void TakeFocus(AgeControlDropList list)
        {
            AgeManager age = AgeManager.Instance;
            if (age == null)
            {
                return;
            }

            // Recorded before the hand-over: assigning focus runs the outgoing control's FocusLoss,
            // and nothing the mod is asked during that should see a focus it does not own yet.
            _focus = list;
            age.FocusedControl = list;
        }

        /// <summary>Give the keyboard back, but only if it is still ours: the game's Escape route
        /// clears the focus itself, and by the time we get here something else may legitimately have
        /// it.</summary>
        private static void ReleaseFocus()
        {
            AgeControl ours = _focus;
            _focus = null;
            if (ours == null)
            {
                return;
            }

            try
            {
                AgeManager age = AgeManager.Instance;
                if (age != null && ReferenceEquals(age.FocusedControl, ours))
                {
                    age.FocusedControl = null;
                }
            }
            catch (Exception e)
            {
                Log.Warn("drop list: handing the keyboard back threw: " + e);
            }
        }

        /// <summary>Close the popup and put the highlight back on the entry the setting is actually
        /// on. Leaving without picking anything therefore leaves the list looking exactly as it did,
        /// and picking one is already the entry we would restore.</summary>
        public override void OnPop()
        {
            OptionDropListItem item = _showing;
            _showing = null;
            _closeAfterFrame = -1;
            _open = null;

            AgeControlDropList list = ListOf(item);
            if (list == null)
            {
                ReleaseFocus();
                return;
            }

            try
            {
                // All three steps are written to be harmless when the game has already done them:
                // arriving here after the engine answered Escape, the popup is closed, the focus is
                // gone, and only the highlight still needs putting back.
                if (list.MenuActive)
                {
                    OptionsScreen.Call(ClosePopup, list, false);
                }

                if (list.PopupMenu != null)
                {
                    list.PopupMenu.SetSelection(list.SelectedItem);
                }
            }
            catch (Exception e)
            {
                Log.Warn("drop list: closing the popup threw: " + e);
            }

            ReleaseFocus();
        }

        public override void Build(GraphBuilder builder)
        {
            OptionDropListItem item = _showing ?? _open;
            AgeControlDropList list = ListOf(item);
            if (list == null)
            {
                return;
            }

            int count = Count(list);
            string key = "droplist:" + OptionName(item) + "/";
            for (int i = 0; i < count; i++)
            {
                int index = i;
                NodeVtable vtable = GraphNodes.Choice(
                    () => EntryText(list, index),
                    () => Chosen(list, index),
                    () => Choose(item, index),
                    () => EntryEnabled(list, index),
                    () => AgeText.Lines(EntryDetail(list, index))
                );
                // The game's own highlight follows the cursor, so someone watching sees the entry
                // being considered; what the setting is actually on does not move until Enter.
                vtable.OnFocusVisual = () => Highlight(list, index);

                AgeTransform entry = EntryTransform(list, index);
                builder.AddItem(
                    entry != null
                        ? ControlId.Referenced(entry, key + index)
                        : ControlId.Structural(key + index),
                    vtable
                );
            }
        }

        /// <summary>Take the entry the way clicking it does: the drop list's own selection first,
        /// which rewrites the closed control's label, then the item's selection handler, which stores
        /// the value and tells the window a setting changed. The screen closes itself on the next
        /// tick, which is what shuts the popup.</summary>
        private static void Choose(OptionDropListItem item, int index)
        {
            try
            {
                item.DropList.SelectedItem = index;
                OptionsScreen.Call(EntrySelected, item, OptionsScreen.NoSender);
            }
            catch (Exception e)
            {
                Log.Warn("drop list: choosing an entry threw: " + e);
            }

            _open = null;
        }

        private static void Highlight(AgeControlDropList list, int index)
        {
            try
            {
                if (list.PopupMenu != null)
                {
                    list.PopupMenu.SetSelection(index);
                }
            }
            catch (Exception e)
            {
                Log.Warn("drop list: highlighting an entry threw: " + e);
            }
        }

        /// <summary>An entry's text. The list holds localization keys rather than words - the labels
        /// the popup renders are localized as they are drawn, and there is nothing drawn until the
        /// popup is open - so the key is resolved here.</summary>
        private static string EntryText(AgeControlDropList list, int index)
        {
            try
            {
                return index < list.LabelTable.Count
                    ? AgeText.Clean(list.LabelTable[index])
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the entry's tooltip says about it, for the review buffer. Not spoken on
        /// focus: on a twenty-entry resolution list the description is the entry's own name again,
        /// and on the lists where it says something the player can read it at their own pace.
        /// </summary>
        private static string EntryDetail(AgeControlDropList list, int index)
        {
            try
            {
                return index < list.TooltipTable.Count
                    ? AgeText.Clean(list.TooltipTable[index])
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool Chosen(AgeControlDropList list, int index)
        {
            try
            {
                return list.SelectedItem == index;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Whether the game is offering this entry. A list can refuse individual entries -
        /// a resolution the display cannot do - and a refused one stays in the list, says so, and
        /// swallows Enter.</summary>
        private static bool EntryEnabled(AgeControlDropList list, int index)
        {
            AgeTransform entry = EntryTransform(list, index);
            try
            {
                return entry == null || entry.Enable;
            }
            catch (Exception)
            {
                return true;
            }
        }

        private static AgeTransform EntryTransform(AgeControlDropList list, int index)
        {
            try
            {
                if (list.PopupMenu == null || list.PopupMenu.Table == null)
                {
                    return null;
                }

                return index < list.PopupMenu.Table.Children.Count
                    ? list.PopupMenu.Table.Children[index]
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static int Count(AgeControlDropList list)
        {
            try
            {
                return list.LabelTable.Count;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static string Title(OptionDropListItem item)
        {
            try
            {
                return item == null ? null : AgeText.Label(item.TitleLabel);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string OptionName(OptionDropListItem item)
        {
            try
            {
                if (item == null)
                {
                    return "?";
                }

                return item.Option != null ? item.Option.PropertyName : item.name;
            }
            catch (Exception)
            {
                return "?";
            }
        }

        private static AgeControlDropList ListOf(OptionDropListItem item)
        {
            try
            {
                return item == null ? null : item.DropList;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // The game's own way in and out of a popup, and the handler a click on an entry reaches.
        // Resolved once: every entry of every list would otherwise pay for the lookup on every
        // navigation operation.
        private static readonly MethodInfo OpenPopup = OptionsScreen.Handler(
            typeof(AgeControlDropList),
            "OpenPopupMenu"
        );

        private static readonly MethodInfo ClosePopup = OptionsScreen.Handler(
            typeof(AgeControlDropList),
            "ClosePopupMenu"
        );

        private static readonly MethodInfo EntrySelected = OptionsScreen.Handler(
            typeof(OptionDropListItem),
            "OnEntrySelectedCb"
        );
    }
}
