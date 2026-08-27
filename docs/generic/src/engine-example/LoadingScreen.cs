using System;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// The wait between one part of the game and the next, made audible.
    ///
    /// There is nothing here to operate - a load is something that happens to the player, not
    /// something they do - so the screen declares no controls at all and exists only to say what the
    /// game is doing and how far along it is. That makes it the one screen whose speech comes from
    /// its per-frame update rather than from a control the cursor is sitting on.
    ///
    /// It sits above the pages a load replaces, so the menu the player just left goes quiet rather
    /// than announcing itself under the loading screen; the confirmation box stays above it, because
    /// a question asked mid-load is still a question.
    ///
    /// Everything it says is queued, never interrupting: a load is a stream of short status lines and
    /// a player who is being read to should hear them in the order they happened rather than losing
    /// each one to the next.
    ///
    /// The status text is read from the engine's own progress record rather than from the labels on
    /// the window. The window keeps a rolling list of the last five lines and animates its bar toward
    /// the real value, so its labels lag; the record is what the game is actually doing right now.
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
            get { return "screen.loading"; }
        }

        /// <summary>Above every page a load replaces, and below the confirmation box, which can be
        /// raised over anything.</summary>
        public override int Layer
        {
            get { return 60; }
        }

        /// <summary>Nothing on screen is the player's while the game loads - there is only the progress
        /// to hear and the wait, and whatever page the load is on its way to has not opened yet.
        /// </summary>
        public override bool AnswersOnly
        {
            get { return true; }
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
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<LoadingWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
