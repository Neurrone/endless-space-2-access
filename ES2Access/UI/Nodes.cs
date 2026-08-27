using System;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// The engine's own door onto the two node natures - and the one place a screen's claim to have
    /// NO evidence is checked against what it is holding.
    ///
    /// <see cref="DrawnNode"/> needs no door: its constructor refuses a null widget, so a screen that
    /// says "the game draws this" has already produced the proof. The claim that needs checking is the
    /// other one. <see cref="SyntheticNode"/> is honest for exactly two kinds of content - something
    /// synthesized from the game's facts, and mod-authored UI - and neither of those is holding a
    /// widget. A node whose SUBJECT is a widget is holding one: whatever else it is, the walk that
    /// declared it had the very thing the gate wants to ask, and calling it synthetic is how a row
    /// silently stops being checked. So that combination is reported, loudly and once, and while the
    /// dev server is up it throws - the render is blanked by the caller's own guard, which is a
    /// symptom impossible to miss and cheap to fix. A player's game only ever gets the log line.
    ///
    /// The CORE declares synthetic nodes too (<see cref="Core.UI.GraphSheet"/>), and does not come
    /// through here: it knows nothing of widgets, cannot run this test, and its rows are guaranteed
    /// by the walk that enumerated them.
    /// </summary>
    public static class Nodes
    {
        /// <summary>A control the game is drawing, with the widget that vouches for it. Present for
        /// symmetry with <see cref="Synthetic"/> - the constructor is the enforcement.</summary>
        public static DrawnNode Drawn(ControlId id, NodeVtable vtable, object drawnBy)
        {
            return new DrawnNode(id, vtable, drawnBy);
        }

        /// <summary>A control with nothing on the screen to ask about - synthesized from game facts,
        /// or drawn by this mod. Checked: a subject that is a widget means there WAS something to
        /// ask.</summary>
        public static SyntheticNode Synthetic(ControlId id, NodeVtable vtable)
        {
            SyntheticNode node = new SyntheticNode(id, vtable);
            AgeTransform widget = id == null ? null : DrawnBy.WidgetOf(id.Subject);
            if (widget != null)
            {
                Misdeclared(id, widget);
            }

            return node;
        }

        private static void Misdeclared(ControlId id, AgeTransform widget)
        {
            string screen = NodeGate.Building;
            string key = Convert.ToString(id.StructuralKey);
            if (NodeGate.Remember("misdeclared # " + screen, key))
            {
                Log.Error(
                    "NodeGate misdeclared: screen="
                        + screen
                        + " node="
                        + key
                        + " declared synthetic while its subject is the widget "
                        + DrawnBy.Path(widget)
                );
            }

            if (DevRunning())
            {
                throw new InvalidOperationException(
                    "Synthetic node " + key + " on " + screen + " has a widget subject"
                );
            }
        }

        // Read once: the loader's answer cannot change within a load, and the reflection behind it is
        // not free.
        private static bool? _dev;

        private static bool DevRunning()
        {
            if (!_dev.HasValue)
            {
                _dev = Dev.NotificationAudit.DevServerUp();
            }

            return _dev.Value;
        }
    }
}
