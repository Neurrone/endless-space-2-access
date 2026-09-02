using System;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    public sealed partial class GraphNavigator
    {
        // Which control the game is currently being made to look hovered on, and the node whose
        // hooks will undo it. Kept by id, not by object: the graph is rebuilt every frame, so the
        // node standing for a control is a different instance each time.
        private ControlId _visualKey;
        private GraphNode _visualNode;

        // What that commit aimed at (NodeVtable.PointsAt's answer at the time). The graph is rebuilt
        // every frame and a node's aim is resolved when ASKED, so this is what a later frame's answer
        // is compared against - see SyncVisual.
        private object _visualAim;

        // Where the cursor stood at the last visual commit, and whether the commit being made now is
        // the cursor having been PLACED. Unlike _visualKey this survives ClearVisual, which is a
        // re-commit on the control the cursor is already on.
        private ControlId _visualFrom;
        private bool _cursorMovedHere;

        // A screen has PUT the cursor somewhere and the landing has just been taken (EnsureFocus,
        // FocusStop). Consumed by the next visual commit, which it makes a placement even where the
        // cursor was already standing on that control - the one case an id comparison cannot see, and
        // the case a handover makes when it hands the player back to the row they never left.
        private bool _placed;

        /// <summary>
        /// Point the game's own pointer feedback at the focused control - the hover highlight, a menu
        /// opening under it, its tooltip - so that someone watching the screen can follow where the
        /// keyboard is. Nothing here speaks; the hooks are the screen's, and a screen that does not
        /// set them simply looks untouched.
        ///
        /// Alongside the announcement, in the same place and on the same comparison: whatever moved
        /// focus, the game's appearance follows it exactly once.
        ///
        /// Scrolling the focused control into view is done here rather than by the screens, and needs
        /// nothing declared: a control that named the game object it came from can be found on screen,
        /// and whether anything above it scrolls is a question about the game's own hierarchy. So it
        /// costs a screen nothing and is never forgotten.
        ///
        /// And re-committed, on the SAME control, whenever what the control aims at has changed
        /// (<see cref="NodeVtable.PointsAt"/>). A commit happens once per focus change, but the thing
        /// a node points at is a question the game keeps answering differently under a standing
        /// cursor: pooled widgets get handed to another row, one tooltip on a window gets re-bound to
        /// whatever the camera is looking at. The pointer stayed where it was first put, so the game
        /// went on drawing somebody else's dossier for the control the player was standing on - and
        /// nothing was ever going to correct it. Comparing the answer against the one that was
        /// committed is what turns that into a re-commit, per site, with nothing for a screen to
        /// remember; a node whose answer is stable takes exactly the path it always did.
        ///
        /// A PLACEMENT re-commits too, even onto the control the cursor already occupied
        /// (<see cref="_placed"/>): a screen that hands the player back to where they are standing is
        /// saying "the cursor is placed here", and everything hung on a placement -
        /// <see cref="CursorMovedHere"/>, and through it the galaxy page's camera - has to hear it. It
        /// stays silent: the announcement compares the SPOKEN key, which the standing cursor already
        /// matches.
        /// </summary>
        private void SyncVisual(GraphNode node)
        {
            bool placed = _placed;
            _placed = false;
            if (!placed && _visualKey != null && _visualKey.Equals(node.Id))
            {
                if (ReferenceEquals(Aim(node), _visualAim))
                {
                    return;
                }
            }

            ClearVisual();
            _visualKey = node.Id;
            _visualNode = node;
            _visualAim = Aim(node);
            _cursorMovedHere = _visualFrom != null && (placed || !_visualFrom.Equals(node.Id));
            _visualFrom = node.Id;
            ScrollIntoView.Reveal(node.Vtable.ScrollAnchor, node.Id.Subject);
            // The screen's own half first, so a rule that moves the WORLD (the galaxy page's camera)
            // has run before the node aims the pointer at whatever the new distance draws.
            if (_screen != null)
            {
                try
                {
                    _screen.OnFocusVisual(node);
                }
                catch (Exception e)
                {
                    Log.Warn("navigator: a screen's OnFocusVisual threw: " + e);
                }
            }

            Safe(node.Vtable.OnFocusVisual, "OnFocusVisual");
            _cursorMovedHere = false;
        }

        /// <summary>
        /// Whether the commit now running is the cursor having been PLACED here - asked from inside an
        /// <c>OnFocusVisual</c> hook, and false anywhere else.
        ///
        /// A focus visual is committed for three different reasons and only one of them is the cursor
        /// being put somewhere: the cursor was placed - the player moved it, or a screen seated it
        /// (<see cref="FocusNode"/>, <see cref="FocusStop"/>) - or the screen was re-attached and the
        /// cursor it remembered re-seated, or the visual was dropped and re-taken on the SAME control
        /// because what the game draws for it changed (<c>GalaxyHudScreen.FollowCamera</c>). A hook
        /// that only points the game's pointer wants all three. A hook that MOVES THE WORLD - a camera
        /// pan to whatever the cursor is on - wants only the first, or re-entering a page flies the
        /// camera back to the system the player was reading before, over wherever the game has since
        /// taken it.
        ///
        /// A screen's seat counts because it IS the cursor being put somewhere: the player is left
        /// reading whatever it landed on, and a page whose camera followed the player's own arrow but
        /// not the handover that seated them would be two rules (owner ruling 2026-08-26 - the fleet
        /// panel's Escape). That includes a seat onto the control the cursor already occupied, which is
        /// why a placement re-commits (<see cref="SyncVisual"/>) rather than being compared away.
        /// </summary>
        public bool CursorMovedHere
        {
            get { return _cursorMovedHere; }
        }

        /// <summary>Leave the game looking as though nothing were hovered - focus has gone somewhere
        /// we do not describe, or the mod is going away.</summary>
        public void ClearVisual()
        {
            if (_visualNode != null)
            {
                Safe(_visualNode.Vtable.OnBlurVisual, "OnBlurVisual");
            }

            _visualKey = null;
            _visualNode = null;
            _visualAim = null;
        }

        /// <summary>What a node aims at right now, or null where it aims at nothing and where asking
        /// threw - an aim that cannot be resolved is not a reason to keep re-committing.</summary>
        private static object Aim(GraphNode node)
        {
            try
            {
                Func<object> points = node.Vtable.PointsAt;
                return points == null ? null : points();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Safe(Action action, string what)
        {
            if (action == null)
            {
                return;
            }

            try
            {
                action();
            }
            catch (Exception e)
            {
                Log.Warn("nav: " + what + " threw: " + e);
            }
        }
    }
}
