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
    /// The wait between one part of the game and the next, made audible - and walkable.
    ///
    /// There is nothing here to operate - a load is something that happens to the player, not
    /// something they do - so every row is a plain readout and none of them is watched: what REPORTS
    /// the load is the per-frame speech below, which says each new status line and each quarter mark
    /// by itself. The rows are there so a player who missed a line, or who wants the tip again, can go
    /// and read it. That makes it the one screen whose speech comes from its per-frame update rather
    /// than from a control the cursor is sitting on.
    ///
    /// The rows are what the window draws, in the order it draws them, and the order is the game's:
    /// the multiplayer player list across the top, then the rolling list of status lines, then the
    /// progress bar under it, then the tip (measured 2026-09-02: players y=20, lines y=622-722,
    /// bar y=726, tip y=732). Inside the list the game draws its NEWEST line at the BOTTOM - the
    /// table's first child is its last row - so walking down the rows walks forward in time, which is
    /// the order the lines were spoken in.
    ///
    /// The bar carries no words of its own: the game draws it as a bare rectangle and localizes no
    /// name for it, so the mod names it and reads its value off the same progress record the speech
    /// below uses.
    ///
    /// It sits above the pages a load replaces, so the menu the player just left goes quiet rather
    /// than announcing itself under the loading screen; the confirmation box stays above it, because
    /// a question asked mid-load is still a question.
    ///
    /// Everything it says is queued, never interrupting: a load is a stream of short status lines and
    /// a player who is being read to should hear them in the order they happened rather than losing
    /// each one to the next.
    ///
    /// The status text SPOKEN is read from the engine's own progress record rather than from the
    /// labels on the window. The window keeps a rolling list of the last five lines and animates its
    /// bar toward the real value, so its labels lag; the record is what the game is actually doing
    /// right now. The ROWS are the other half of that: they are the labels, because a row is a place
    /// on the screen and has to say what is written there.
    ///
    /// Which has a visible edge. A new showing clears the list the window keeps but not the labels
    /// already on it, so for the first frames of a load the rows are the PREVIOUS load's lines -
    /// which is what the window is drawing, and stops the moment the game reports anything. It is the
    /// same "the record outlives the load that wrote it" caveat as above, seen from the picture's side.
    /// </summary>
    public sealed class LoadingScreen : Screen
    {
        /// <summary>How many quarters the progress is reported in, so a long load says something
        /// every so often without narrating every fraction of a percent.</summary>
        private const int Milestones = 4;

        private string _lastMessage;
        private int _lastMilestone;
        private bool _tipSpoken;

        public override string Key
        {
            get { return ModStrings.ScreenLoading; }
        }

        /// <summary>Above every page a load replaces, and below the confirmation box, which can be
        /// raised over anything.</summary>
        public override int Layer
        {
            get { return 60; }
        }

        /// <summary>Nothing on screen belongs to the page underneath while the game loads - the
        /// tutorial's bar and the chat alert are the pages the load is leaving or arriving at, not
        /// this one. What the loading window itself draws is declared below.</summary>
        public override bool AnswersOnly
        {
            get { return true; }
        }

        private static readonly object RowsStop = "loading:rows";

        private readonly List<Cell> _cells = new List<Cell>();

        /// <summary>
        /// One row per thing the window is drawing, laid out by where it is drawn: the shared linear
        /// emit sorts the cells down the screen, so the reading order is the picture's own and no
        /// order is written down here.
        ///
        /// A row is only ever declared for a piece the renderer is DRAWING. The status list is pooled
        /// - it keeps five children and fades the older ones - so the walk goes through the shared
        /// drawn-child test, and the rows are keyed by the child's place in that pool rather than by
        /// its words, which change under the cursor every time the game reports something new.
        /// </summary>
        public override void Build(GraphBuilder builder)
        {
            LoadingWindow window = Window();
            if (window == null)
            {
                return;
            }

            try
            {
                _cells.Clear();
                AddPlayers(window);
                AddStatusLines(window);
                AddProgress(window);
                Cells.AddReadout(_cells, AgeWidgets.Transform(window.TipLabel), "loading:tip");
                if (_cells.Count == 0)
                {
                    return;
                }

                builder.BeginStop(RowsStop);
                Cells.EmitLinear(builder, _cells);
            }
            catch (Exception e)
            {
                Log.Warn("loading: reading the window threw: " + e);
            }
        }

        /// <summary>The rolling list of status lines. The newest is the table's FIRST child and the one
        /// the game draws lowest, so nothing here orders them: the emit puts them back in the order
        /// they are drawn in.</summary>
        private void AddStatusLines(LoadingWindow window)
        {
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(window.ProgressItemTable);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                Cells.AddReadout(
                    _cells,
                    AgeWidgets.DrawnChild(children, i),
                    "loading:status/" + i
                );
            }
        }

        /// <summary>How far the load has got, as a row. The value comes from the progress RECORD and
        /// not from the bar, for the reason the class comment gives for the status text: the bar
        /// animates towards the real number and is behind it for as long as it is moving. It is not
        /// watched - a percentage that changes every frame would talk over everything else - and the
        /// quarter marks below are what report progress on their own.</summary>
        private void AddProgress(LoadingWindow window)
        {
            AgeTransform bar = window.ProgressValue;
            if (bar == null)
            {
                return;
            }

            Cells.Add(
                _cells,
                bar,
                ControlId.For(bar, "loading:progress"),
                GraphNodes.Readout(
                    () => ModStrings.Get(ModStrings.LoadingProgressBar),
                    Percentage,
                    null,
                    null,
                    false
                )
            );
        }

        /// <summary>One row per player, which the game draws only in a multiplayer load - it hides the
        /// whole group otherwise, and the drawn test above is what keeps the rows out of a single
        /// player's load rather than a mode flag read here.</summary>
        private void AddPlayers(LoadingWindow window)
        {
            IList<AgeTransform> children = AgeWidgets.DrawnChildren(window.PlayerLoadStatusTable);
            for (int i = 0; children != null && i < children.Count; i++)
            {
                Cells.AddReadout(
                    _cells,
                    AgeWidgets.DrawnChild(children, i),
                    "loading:player/" + i
                );
            }
        }

        private static string Percentage()
        {
            try
            {
                return ModStrings.Format(
                    ModStrings.LoadingProgress,
                    Mathf.Clamp((int)(Amplitude.Diagnostics.Progress.Current * 100f), 0, 100)
                );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the game calls this load - the caption it writes from the faction and the
        /// session it is loading into. Falls back on saying that a load is happening, for the moment
        /// before the caption is written.</summary>
        public override string ScreenName
        {
            get
            {
                LoadingWindow window = Window();
                string caption = window == null ? null : AgeText.Label(window.Caption);
                return string.IsNullOrEmpty(caption)
                    ? ModStrings.Get(ModStrings.ScreenLoading)
                    : caption;
            }
        }

        /// <summary>Ours while the window is up and has finished animating in. Waiting out the
        /// animation is what keeps a load from announcing itself twice as the window fades in.
        /// </summary>
        public override bool IsActive()
        {
            LoadingWindow window = Window();
            try
            {
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Every showing starts afresh - but the progress record outlives the load that
        /// wrote it, so a new load's first frames still show the last one's final line and its full
        /// bar. While nothing is actually progressing yet, whatever is in the record is history and
        /// is baselined silently; the moment the new load starts writing, its lines are new against
        /// that baseline and speak.</summary>
        public override void OnPush()
        {
            Rearm();
            try
            {
                if (!Amplitude.Diagnostics.Progress.IsProgressing)
                {
                    _lastMessage = AgeText.Clean(Amplitude.Diagnostics.Progress.Message);
                    _lastMilestone = Mathf.Clamp(
                        (int)(Amplitude.Diagnostics.Progress.Current * Milestones),
                        0,
                        Milestones
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("loading: baselining the progress record threw: " + e);
            }
        }

        public override void OnPop()
        {
            Rearm();
        }

        public override void OnUpdate()
        {
            try
            {
                Announce();
            }
            catch (Exception e)
            {
                Log.Warn("loading: reading the progress threw: " + e);
            }
        }

        private void Rearm()
        {
            _lastMessage = null;
            _lastMilestone = 0;
            _tipSpoken = false;
        }

        private void Announce()
        {
            string message = AgeText.Clean(Amplitude.Diagnostics.Progress.Message);
            if (!string.IsNullOrEmpty(message) && message != _lastMessage)
            {
                _lastMessage = message;
                Voice.Say(message, false);

                // The tip is worth hearing once, and it is worth hearing after the load has said it
                // is under way rather than into the silence before that.
                if (!_tipSpoken)
                {
                    _tipSpoken = true;
                    LoadingWindow window = Window();
                    Voice.Say(window == null ? null : AgeText.Label(window.TipLabel), false);
                }
            }

            // Each quarter mark, as it is passed. A phase that starts over drops the progress back
            // to zero, which re-arms the marks so the next phase reports itself too.
            int milestone = Mathf.Clamp(
                (int)(Amplitude.Diagnostics.Progress.Current * Milestones),
                0,
                Milestones
            );
            if (milestone > _lastMilestone)
            {
                Voice.Say(
                    ModStrings.Format(ModStrings.LoadingProgress, milestone * 100 / Milestones),
                    false
                );
            }

            _lastMilestone = milestone;
        }

        private static LoadingWindow Window()
        {
            return GameWindows.Of<LoadingWindow>();
        }
    }
}
