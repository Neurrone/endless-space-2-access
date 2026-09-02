using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.UI.Input;
using UnityEngine;

namespace ES2Access.UI
{
    /// <summary>
    /// How close the game is looking, as something the player can move.
    ///
    /// The game's own answer for a keyboard is two keys HELD down (PageUp and PageDown, polled while
    /// pressed - a tap moves nothing) and nothing at all once the game is inside a system, which left
    /// the whole zoom-dependent surface out of reach: on the scan view every lens but the one the camera
    /// happened to be on, and on the map itself a system's page and a planet's. So the zoom is offered as
    /// an adjustable of its own, on the arrows the mod already spends on a value, and the ladder runs all
    /// the way from the whole galaxy to one planet (<see cref="GalaxyViewLevels.StepZoom"/>).
    ///
    /// The value is the rung and the band of the map's own layer table it falls in
    /// (<see cref="GalaxyViewLevels.ZoomBand"/>) - what the camera draws at that distance, which the
    /// number alone does not say. What a rung means to the PAGE is a separate matter and stays the
    /// page's: on the scan view it selects the lens, whose name that screen announces whenever the
    /// layer under it changes - name repeated or not - and repeating it here would say it twice. The
    /// two vocabularies do not collide: the game's nine layer descriptors carry six lens names of
    /// their own ("Trade", "Economy") and none of them is a band word. In scan mode the band word is
    /// dropped altogether - the lens title has already said what the rung bought - and that is the
    /// band's own answer rather than a rule of this class (<see cref="GalaxyViewLevels.ZoomBand"/>),
    /// so the ladder and the watcher lose it together.
    /// While the game is flying between two view levels there is no rung to report -
    /// the answer is a step behind - so the value says nothing rather than answering "nothing happened" to
    /// a press that did something, and speaks itself as soon as it is true again.
    ///
    /// One ladder per page rather than a static: the wait below is per-page state and a page's
    /// <c>OnPop</c> gives it back (<see cref="Forget"/>), which is what keeps a hot reload clean.
    ///
    /// The ladder's top three rungs cross between pages, and a step across them is answered by a page
    /// that does not exist yet - so that one step is carried by state belonging to no page
    /// (<see cref="Claim"/>): the arriving page's ladder takes the seat, and the player who stepped off
    /// a ladder arrives on one. Nothing else about those pages changes - every other way in still
    /// lands where it always did.
    /// </summary>
    public sealed class ZoomLadder
    {
        /// <summary>How long the value waits for a view level the game has been asked for - about half a
        /// second, which is longer than the game takes to begin a transition and short enough that a
        /// refused request is not left mute.</summary>
        private const int SettleFrames = 30;

        /// <summary>How long the seat a page-changing step leaves for the next page stays open - about
        /// five seconds, the same budget a landing itself gets (<see cref="ES2Access.Core.UI.Graph.FocusRequest.DefaultFrames"/>),
        /// because it is the same wait: the game flying between two view levels and drawing the page it
        /// arrives on. Short enough that a crossing the game never completed cannot seat some later,
        /// unrelated page the player opens by hand.</summary>
        private const int HandoffFrames = 300;

        /// <summary>The rung the last press was made from, and what is left of its wait.</summary>
        private int _from = -1;

        private int _wait;

        /// <summary>The ladder whose press is still crossing between two pages, and the frame the seat
        /// it left stops being held. Static because the two ends of such a step are two different
        /// ladders on two different screens, and for eight or so frames in between there is no screen
        /// at all (measured 2026-09-02): the press is made on the page being left and has to be
        /// answered by the page arriving, so what carries it can belong to neither.
        ///
        /// A deadline read off the game's own frame counter rather than a countdown, for the same
        /// reason: nothing would be counting. The frames in the middle of such a step belong to no
        /// page, and a per-page update cannot expire a seat over a window in which no page is being
        /// updated - which is exactly the window an abandoned step would have to survive.</summary>
        private static ZoomLadder _handedFrom;

        /// <summary>The id the arriving page's ladder has been sent to. What says the crossing is over:
        /// the cursor reaching it is the arrival, and until then the seat goes on being asked for.</summary>
        private static ControlId _seat;

        private static int _handingUntil;

        /// <summary>Every id a ladder has been declared under. The rung is also announced from
        /// wherever the player is standing (<see cref="ZoomWatch"/>), and the one place that must not
        /// happen is on the ladder itself, whose own value has just read the new rung out - so the
        /// watcher asks whether the control under the cursor is one of these.</summary>
        private static readonly List<ControlId> Ladders = new List<ControlId>();

        /// <summary>Let go of every declared ladder and any crossing still in the air - mod
        /// teardown, which the per-page <see cref="Forget"/> deliberately does not do (a page
        /// popping must leave the seat for the page arriving).</summary>
        public static void Reset()
        {
            Ladders.Clear();
            _handedFrom = null;
            _seat = null;
            _handingUntil = 0;
        }

        /// <summary>Whether a control is a zoom ladder - i.e. whether the player standing on it is
        /// already being told the rung.</summary>
        public static bool IsLadder(ControlId id)
        {
            for (int i = 0; id != null && i < Ladders.Count; i++)
            {
                if (Ladders[i].Equals(id))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a step that changes the page is still on its way - the press has been made and the
        /// cursor is not standing on the arriving page's ladder yet. Over that window the player counts
        /// as being on the ladder, including the frames when there is no screen at all to be standing
        /// on: the ladder they are travelling to is about to read the new rung out, so the watcher must
        /// not read it out first (<see cref="ZoomWatch"/>).
        ///
        /// It ends on ARRIVAL rather than when the seat is taken. The two are frames apart - the seat
        /// is asked for as the arriving page first builds and the cursor lands once the page has
        /// settled - and the rung the game flies to settles in between, which is exactly when the
        /// watcher asks (measured 2026-09-02: the planet page said "Zoom level 15 of 15" and then its
        /// own slider said it again).
        ///
        /// Latches shut, so that a crossing which is over cannot mute a rung the player changes by some
        /// other means a moment later.
        /// </summary>
        public static bool Crossing()
        {
            if (_handedFrom == null)
            {
                return false;
            }

            GraphNavigator navigator = ModEntry.Navigator;
            ControlId focused = navigator == null ? null : navigator.FocusedKey;
            if (
                (_seat != null && focused != null && focused.Equals(_seat))
                || Time.frameCount >= _handingUntil
            )
            {
                _handedFrom = null;
                _seat = null;
                return false;
            }

            return true;
        }

        /// <summary>Whether there is a rung to declare at all - a battle or the system-discovery view has
        /// none. Asked by the cluster the ladder is declared INTO before it begins its stop, because a
        /// stop with nothing in it is a Tab press that lands nowhere.</summary>
        public static bool Rungs
        {
            get { return GalaxyViewLevels.ZoomRung >= 0; }
        }

        /// <summary>Declared only where the question applies at all - a battle or the system-discovery
        /// view has no rung.</summary>
        public void Build(GraphBuilder builder, object key)
        {
            if (!Rungs)
            {
                return;
            }

            ControlId id = ControlId.Structural(key);
            if (!IsLadder(id))
            {
                Ladders.Add(id);
            }

            Claim(id);

            NodeVtable vtable = GraphNodes.Slider(
                () => ModStrings.Get(ModStrings.Zoom),
                Text,
                Step
            );
            // The one control on this page whose gestures nothing else suggests: the game's own zoom is
            // two keys HELD, so a player who has never met an adjustable here has no reason to try the
            // arrows. The sentence names both of them at once - a ladder worked from one end is half a
            // control - and says what the gesture BUYS on the page it is standing on, which is how much
            // detail the map draws and which lens the scan view is read through.
            //
            // It names the COARSE pair and not the plain arrows (owner ruling 2026-09-01, after
            // playtest): a single rung usually changes nothing the player can hear - the band words and
            // the lens names only move at a boundary - so a hint naming the fine step was pointing at a
            // key that mostly does nothing. Shift and an arrow jumps the whole band, which is what these
            // two sentences promise. Rendered from the live bindings like every other hint, so
            // re-binding either chord re-words it.
            NodeHints.Add(
                vtable,
                GalaxyViewLevels.Scanning
                    ? ModStrings.HintChangeLens
                    : ModStrings.HintChangeDetailLevel,
                UiActions.CoarseDecrease,
                0,
                null,
                UiActions.CoarseIncrease
            );
            // Synthetic: mod-authored - the zoom ladder is the mod's own control over the camera,
            // and the game draws nothing for it.
            builder.AddItem(Nodes.Synthetic(id, vtable));
        }

        /// <summary>Counts the wait down, and ends it the moment the rung moves - so the value speaks
        /// itself as soon as it is true, and a request the game refused goes quiet again instead of
        /// staying silent for good. Called from the page's own per-frame update.</summary>
        public void Update()
        {
            if (_wait <= 0)
            {
                return;
            }

            _wait--;
            if (GalaxyViewLevels.ZoomRung != _from)
            {
                _wait = 0;
            }
        }

        /// <summary>Given back when the page goes - the page's own wait, and deliberately not the
        /// crossing (<see cref="Crossing"/>): a step out of this page pops it while the seat it left is
        /// still on its way to the next one, so a page giving back its state must not take the seat
        /// with it. The crossing needs no teardown of its own - it expires on the game's own frame
        /// counter, and a reload takes it with the assembly.</summary>
        public void Forget()
        {
            _wait = 0;
            _from = -1;
        }

        /// <summary>One rung, and then the wait for a rung the game has not moved to yet. A press that
        /// asks for a VIEW LEVEL is deferred - the game starts flying a frame or two later - so the rung
        /// read straight afterwards is still the one the player has just left, and saying it answers
        /// "nothing happened" to a press that did something.</summary>
        private void Step(int sign, bool coarse)
        {
            int before = GalaxyViewLevels.ZoomRung;
            bool changesPage;
            if (!GalaxyViewLevels.StepZoom(sign, coarse, out changesPage))
            {
                return;
            }

            // The top three rungs take the whole page away with them, and the page that arrives has a
            // ladder of its own. Where the player was standing is a fact about the press, not about
            // either page, so the seat is left here for whichever ladder builds next: a step made ON
            // the ladder lands on the ladder, and no other route into these pages is touched.
            if (changesPage)
            {
                _handedFrom = this;
                _seat = null;
                _handingUntil = Time.frameCount + HandoffFrames;
            }

            if (GalaxyViewLevels.ZoomRung != before)
            {
                return;
            }

            _from = before;
            _wait = SettleFrames;
        }

        /// <summary>
        /// Take the seat a step made from ANOTHER page's ladder left open, so the player who stepped
        /// off one ladder arrives standing on the next one - whatever this page's own cursor memory
        /// says and whatever it would otherwise open on. Asked as the ladder is declared, which is
        /// early enough in the frame that the landing is already in flight when the cursor would
        /// otherwise be seated and read out, so the page announces itself and then the rung, once.
        ///
        /// Asked again on every build until the cursor is actually standing there, because a landing
        /// aimed at this ladder can be DROPPED mid-flight: the rung the ladder is declared on goes away
        /// while the game is between two view levels (<see cref="Rungs"/>), and a landing whose target
        /// the render no longer leads to is given up at once. Re-asking costs nothing once the cursor
        /// has arrived - the crossing has ended by then (<see cref="Crossing"/>) - and it is what makes
        /// a flight whose page flickers land the same as one that does not (measured 2026-09-02: the
        /// scan view's own rung 14, arrived at from the galaxy, seated the lens button instead).
        ///
        /// The ladder that MADE the step never takes its own seat: it goes on building for the frames
        /// the game takes to leave, and the seat is not for it. And only the build the player is
        /// actually navigating may claim - the dev server builds other screens' renders to READ them,
        /// and a read must not move the cursor.
        /// </summary>
        private void Claim(ControlId id)
        {
            if (
                _handedFrom == null
                || ReferenceEquals(_handedFrom, this)
                || Time.frameCount >= _handingUntil
            )
            {
                return;
            }

            GraphNavigator navigator = ModEntry.Navigator;
            Screens.Screen screen = navigator == null ? null : navigator.Screen;
            if (screen == null || screen.Key != NodeGate.Building)
            {
                return;
            }

            _seat = id;
            navigator.FocusNode(id);
        }

        private string Text()
        {
            int rung = GalaxyViewLevels.ZoomRung;
            int rungs = GalaxyViewLevels.ZoomRungs;
            if (
                rung < 0
                || rungs <= 0
                || GalaxyViewLevels.ChangingLevel
                || (_wait > 0 && rung == _from)
            )
            {
                return null;
            }

            // The rung, then what the map draws there, exactly as the watcher says it
            // (<see cref="ZoomWatch"/>) - the two are the same reading and must not differ by which
            // of them the player happened to hear it from.
            return new MessageBuilder()
                .PushFraction(rung + 1, rungs)
                .ListItemForcedComma(GalaxyViewLevels.ZoomBand)
                .Build();
        }
    }
}
