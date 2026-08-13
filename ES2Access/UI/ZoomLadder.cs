using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;

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
    /// The value is the rung and nothing else. What a rung MEANS is a matter for the page: on the scan
    /// view it is the lens's name, which that screen announces whenever it changes, and repeating it here
    /// would say it twice. While the game is flying between two view levels there is no rung to report -
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

        /// <summary>Declared only where the question applies at all - a battle or the system-discovery
        /// view has no rung.</summary>
        public void Build(GraphBuilder builder, object key)
        {
            if (GalaxyViewLevels.ZoomRung < 0)
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

            builder.AddItem(
                id,
                GraphNodes.Slider(() => ModStrings.Get(ModStrings.Zoom), Text, Step)
            );
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

            return new MessageBuilder().PushFraction(rung + 1, rungs).Build();
        }
    }
}
