using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.UI.Input;

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
    /// </summary>
    public sealed class ZoomLadder
    {
        /// <summary>How long the value waits for a view level the game has been asked for - about half a
        /// second, which is longer than the game takes to begin a transition and short enough that a
        /// refused request is not left mute.</summary>
        private const int SettleFrames = 30;

        /// <summary>The rung the last press was made from, and what is left of its wait.</summary>
        private int _from = -1;

        private int _wait;

        private bool _known;

        /// <summary>Every id a ladder has been declared under. The rung is also announced from
        /// wherever the player is standing (<see cref="ZoomWatch"/>), and the one place that must not
        /// happen is on the ladder itself, whose own value has just read the new rung out - so the
        /// watcher asks whether the control under the cursor is one of these.</summary>
        private static readonly List<ControlId> Ladders = new List<ControlId>();

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
            if (!_known)
            {
                _known = true;
                if (!IsLadder(id))
                {
                    Ladders.Add(id);
                }
            }

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

        /// <summary>Given back when the page goes.</summary>
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
            if (!GalaxyViewLevels.StepZoom(sign, coarse) || GalaxyViewLevels.ZoomRung != before)
            {
                return;
            }

            _from = before;
            _wait = SettleFrames;
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
