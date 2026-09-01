using System;
using System.Collections.Generic;

namespace ES2Access.Core.Speech
{
    /// <summary>One thing to say about a video, and how many seconds into it to say it.</summary>
    public struct DescriptionCue
    {
        public readonly float At;
        public readonly string Text;

        public DescriptionCue(float at, string text)
        {
            At = at;
            Text = text;
        }
    }

    /// <summary>
    /// The cues written for one video, and how far through them a single playback has got.
    ///
    /// The cursor only ever moves forward, so a cue is spoken once per playback no matter how many
    /// times the pump asks. Playbacks get a track each (<see cref="VideoDescriptions.Track"/>)
    /// rather than sharing one, because the same colonization scene plays again the next time a
    /// colony ship lands and has to start over.
    ///
    /// Nothing here drops a late cue. The timings were written for a screen reader reading at 600
    /// words per minute and the mod cannot see the rate the player actually set, nor whether the
    /// last line has finished; a reader slower than that hears every cue in the order it was
    /// written, running progressively behind the picture (owner decision 2026-08-22). Dropping
    /// would trade words the player never hears for a synchronisation the mod cannot measure.
    /// </summary>
    public sealed class DescriptionTrack
    {
        private readonly DescriptionCue[] _cues;
        private int _next;

        public DescriptionTrack(DescriptionCue[] cues)
        {
            _cues = cues ?? new DescriptionCue[0];
        }

        public int Count
        {
            get { return _cues.Length; }
        }

        /// <summary>True once every cue has been handed out.</summary>
        public bool Finished
        {
            get { return _next >= _cues.Length; }
        }

        public void Rewind()
        {
            _next = 0;
        }

        /// <summary>
        /// The next cue that has come due at <paramref name="playTime"/> seconds in, or false when
        /// the track is waiting or spent. Call it in a loop: a frame that arrives late, or a video
        /// that opens with several cues close together, has more than one due at once.
        /// </summary>
        public bool TryNext(float playTime, out string text)
        {
            if (_next < _cues.Length && _cues[_next].At <= playTime)
            {
                text = _cues[_next].Text;
                _next++;
                return true;
            }

            text = null;
            return false;
        }
    }

    /// <summary>
    /// Every video the mod has been given descriptions for, keyed the way the game names its own
    /// sidecar files: the movie's basename, plus the metaplot outcome for the outros, which are
    /// the only videos whose narration branches ("Vampirilis_Intro", "Swamp",
    /// "Terrans_Outro_UE.LostBack").
    ///
    /// Keying on the game's internal name rather than the player-facing one is what keeps the
    /// runtime free of a mapping table: the lookup is the basename of the file the game just
    /// asked to play. The translation from the names a describer writes under happens once, in
    /// build-descriptions.ps1, where a name that matches no video in the install fails the build.
    ///
    /// A build with no table, or a language with no descriptions, simply answers null for every
    /// video and the cutscene plays as it did before.
    /// </summary>
    public static class VideoDescriptions
    {
        private static Dictionary<string, DescriptionCue[]> _tracks;

        /// <summary>How many videos the loaded table covers, for the startup log and the dev
        /// server's status.</summary>
        public static int Count
        {
            get { return _tracks == null ? 0 : _tracks.Count; }
        }

        public static void Install(Dictionary<string, DescriptionCue[]> tracks)
        {
            _tracks = tracks;
        }

        public static void Reset()
        {
            _tracks = null;
        }

        /// <summary>
        /// A fresh track for the named video, or null where nothing was written for it.
        ///
        /// <paramref name="variant"/> is the game's own subtitles specifier, which is the metaplot
        /// state for an outro and empty for everything else. An outro with no track for its variant
        /// falls back to the video's own name, so a table that describes a video once still speaks
        /// on both endings rather than going silent on one.
        /// </summary>
        public static DescriptionTrack Track(string movie, string variant)
        {
            if (_tracks == null || string.IsNullOrEmpty(movie))
            {
                return null;
            }

            DescriptionCue[] cues;
            if (!string.IsNullOrEmpty(variant) && _tracks.TryGetValue(movie + "." + variant, out cues))
            {
                return new DescriptionTrack(cues);
            }

            return _tracks.TryGetValue(movie, out cues) ? new DescriptionTrack(cues) : null;
        }
    }
}
