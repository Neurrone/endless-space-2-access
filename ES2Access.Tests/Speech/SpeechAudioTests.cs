using ES2Access.Core.Speech.Mac;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// The trim that turns a rendered utterance into the audio the streamed queue plays: the
    /// silence AVSpeech wraps around speech is what made its own queue stutter, so what the trim
    /// keeps and what it drops is the rule the macOS speech queue stands on.
    /// </summary>
    public class SpeechAudioTests
    {
        [Fact]
        public void SilenceAloneTrimsToNothing()
        {
            float[] samples = new float[1000];
            Assert.Empty(SpeechAudio.Trim(samples, samples.Length, 22050));
        }

        [Fact]
        public void LeadingAndTrailingSilenceGoButAnEdgeStays()
        {
            // 100 ms of silence, 10 ms of speech, 100 ms of silence at 1 kHz for easy counting.
            float[] samples = new float[210];
            for (int i = 100; i < 110; i++)
            {
                samples[i] = 0.5f;
            }

            float[] trimmed = SpeechAudio.Trim(samples, samples.Length, 1000);

            // 5 ms kept either side at 1 kHz is 5 samples.
            Assert.Equal(20, trimmed.Length);
            Assert.Equal(0f, trimmed[0]);
            Assert.Equal(0.5f, trimmed[5]);
            Assert.Equal(0.5f, trimmed[14]);
            Assert.Equal(0f, trimmed[19]);
        }

        [Fact]
        public void OnlyTheRenderedCountIsConsidered()
        {
            float[] samples = new float[100];
            samples[90] = 0.5f;
            Assert.Empty(SpeechAudio.Trim(samples, 50, 1000));
        }

        [Fact]
        public void QuietInteriorIsKept()
        {
            float[] samples = new float[300];
            samples[100] = 0.5f;
            samples[200] = 0.5f;
            float[] trimmed = SpeechAudio.Trim(samples, samples.Length, 1000);
            Assert.Equal(111, trimmed.Length);
        }
    }
}
