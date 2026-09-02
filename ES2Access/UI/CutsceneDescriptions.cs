using System;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using HarmonyLib;

// The engine has its own Amplitude.Unity.Framework.Path.
using Path = System.IO.Path;

namespace ES2Access.UI
{
    /// <summary>
    /// What a cutscene LOOKS like, spoken as the video plays it.
    ///
    /// The game's videos carry their dialogue in the picture's own audio track, so a player hears
    /// the words either way; what is lost without sight is everything the camera shows. The cues
    /// are written into the gaps between the spoken lines and shipped per language
    /// (<see cref="ES2Access.Localization.ModDescriptions"/>), so this only has to say them at the
    /// right moment.
    ///
    /// The right moment is the game's OWN clock. <c>CutsceneModalWindow</c> keeps a private
    /// PlayTime that its PlayMovie coroutine advances by Time.deltaTime, and that same field is
    /// what it compares the subtitle Start and End times against (<c>CutsceneModalWindow</c>
    /// :245-280). Reading it puts the descriptions on the clock the gaps were measured in, which
    /// matters more than being right about the video: under dropped frames PlayTime falls behind
    /// the picture, and a cue timed off anything else would drift INTO the dialogue that PlayTime
    /// is still holding back. It stops with the game, for the same reason.
    ///
    /// Two hooks, because the window answers two different questions:
    ///
    /// - ShowWindow says WHICH video, and is the only place the outro variant exists at all. The
    ///   game passes its subtitlesSpecifier straight into path-building and never stores it
    ///   (<c>CutsceneModalWindow.InitializeSubtitles</c>), so an outro's ending cannot be
    ///   recovered from the window afterwards. Reading it here also works with the game's own
    ///   subtitles switched off, where the window loads no subtitle track to infer it from.
    /// - OnPlayStarted says WHEN, and is called on the frame PlayTime is zeroed. Without it the
    ///   pump would read the previous video's PlayTime during the seconds this one spends
    ///   loading, and empty the whole track into the player's ear at once.
    ///
    /// Neither hook speaks: they record what is playing, and the cutscene screen's per-frame
    /// update says the cues, per the repo's rule that all speech leaves from the pump.
    /// </summary>
    public static class CutsceneDescriptions
    {
        private static readonly ModPatch Patches = new ModPatch(
            "cutscenedescriptions",
            "cutscene descriptions"
        );

        private static MethodInfo _playTime;

        private static string _movie;
        private static string _variant;
        private static DescriptionTrack _track;
        private static bool _playing;

        /// <summary>Whether descriptions are wanted at all, from the mod's own setting.
        ///
        /// A change to it takes effect on the next LAUNCH, and a reload will not do: BepInEx reads
        /// its config file when the plugin binds the setting and does not watch it afterwards, so
        /// POST /reload re-reads the same in-memory value it had before (measured 2026-08-22 - the
        /// file said true and the reloaded mod still came up false). Set it from the REPL to test
        /// both answers inside one session.</summary>
        public static bool Enabled = true;

        /// <summary>The video being described, its variant, and how many cues it has - what the
        /// REPL needs to answer "is this scene described, and by which track".</summary>
        public static string Movie
        {
            get { return _movie; }
        }

        public static string Variant
        {
            get { return _variant; }
        }

        public static int CueCount
        {
            get { return _track == null ? 0 : _track.Count; }
        }

        public static bool Playing
        {
            get { return _playing; }
        }

        public static void Install()
        {
            bool armed = Patches.Install(
                patch =>
                {
                    _playTime = AccessTools.PropertyGetter(typeof(CutsceneModalWindow), "PlayTime");
                    if (_playTime == null)
                    {
                        throw new MissingMemberException(
                            typeof(CutsceneModalWindow).FullName,
                            "PlayTime"
                        );
                    }

                    patch.Prefix(
                        AccessTools.Method(typeof(CutsceneModalWindow), "ShowWindow"),
                        typeof(CutsceneDescriptions),
                        "Showing"
                    );
                    patch.Postfix(
                        AccessTools.Method(typeof(CutsceneModalWindow), "OnPlayStarted"),
                        typeof(CutsceneDescriptions),
                        "Started"
                    );
                }
            );

            if (!armed)
            {
                _playTime = null;
            }
        }

        public static void Remove()
        {
            Patches.Remove();
            _playTime = null;
            Rearm();
        }

        /// <summary>Forget the scene, so a video that ended, was skipped, or was interrupted by a
        /// reload leaves nothing to say over whatever comes next.</summary>
        public static void Rearm()
        {
            _movie = null;
            _variant = null;
            _track = null;
            _playing = false;
        }

        /// <summary>
        /// Say whatever the video has reached. Called once per frame from the cutscene screen,
        /// which is on-screen exactly while a video is up.
        ///
        /// Drains in a loop rather than speaking one cue per frame: a video can open with two cues
        /// close together, and a frame that arrived late would otherwise push the rest of the
        /// track a frame further behind for the whole scene.
        /// </summary>
        public static void Tick(CutsceneModalWindow window)
        {
            if (!_playing || _track == null || window == null || _playTime == null)
            {
                return;
            }

            float playTime;
            try
            {
                playTime = (float)_playTime.Invoke(window, null);
            }
            catch (Exception e)
            {
                // One report, then stand down: a throw here would repeat every frame of the video.
                Log.Warn("cutscene descriptions: the play clock could not be read: " + e.Message);
                _playing = false;
                return;
            }

            string line;
            while (_track.TryNext(playTime, out line))
            {
                Voice.Say(line, false);
            }
        }

        /// <summary>Which video is about to play, and which ending it is telling. Harmony matches
        /// the two by name against ShowWindow's own parameters.</summary>
        private static void Showing(string moviePath, string subtitlesSpecifier)
        {
            try
            {
                Rearm();
                if (!Enabled || string.IsNullOrEmpty(moviePath))
                {
                    return;
                }

                _movie = Path.GetFileNameWithoutExtension(moviePath);
                _variant = subtitlesSpecifier;
            }
            catch (Exception e)
            {
                Log.Warn("cutscene descriptions: the scene could not be identified: " + e.Message);
                Rearm();
            }
        }

        /// <summary>The video has started and its clock is at zero, so the track can be armed. A
        /// video nobody wrote for arms nothing and the scene plays as it always did.</summary>
        private static void Started()
        {
            try
            {
                if (!Enabled || _movie == null)
                {
                    return;
                }

                _track = VideoDescriptions.Track(_movie, _variant);
                _playing = _track != null;
                if (_track == null)
                {
                    Log.Info("cutscene descriptions: nothing written for '" + _movie + "'");
                }
            }
            catch (Exception e)
            {
                Log.Warn("cutscene descriptions: the track could not be armed: " + e.Message);
                Rearm();
            }
        }
    }
}
