using System;
using System.Globalization;
using ES2Access.Core.Util;

namespace ES2Access.Core.Speech.Mac
{
    /// <summary>
    /// The macOS system voice, spoken by the mod itself. <see cref="MacSpeechStream"/> renders
    /// each message through AVSpeech and plays the audio back-to-back through an engine of the
    /// mod's own, because AVSpeech's own queue leaves well over 100 ms of silence between
    /// utterances and VoiceOver's announcement API has no queue at all - and this mod's speech
    /// leans on a queue (notifications, chat, the save spinner and buffer review all speak
    /// without interrupting). AVSpeech is only spoken through directly if that stream cannot be
    /// set up. Voice and rate are the OS Spoken Content settings (<see cref="MacSpokenContent"/>),
    /// read once at start; the rate is AVSpeech's own 0 to 1 scale, the one Spoken Content
    /// stores. Only constructed on macOS; every call happens on the game's main thread, inside
    /// an autorelease pool of its own so nothing depends on how the host drains its pool.
    /// </summary>
    public sealed class MacSystemVoice
    {
        private const int BoundaryImmediate = 0; // AVSpeechBoundaryImmediate

        private IntPtr _synth; // owned (+1): AVSpeechSynthesizer, for the fallback; also marks the voice as started
        private string _voiceIdentifier; // the Spoken Content voice; null for AVSpeech's default
        private float _rate = 0.5f; // AVSpeech's [0, 1] scale
        private MacSpeechStream _stream;

        private static readonly IntPtr SelSetVolume = ObjC.Sel("setVolume:");
        private static readonly IntPtr SelSpeak = ObjC.Sel("speakUtterance:");
        private static readonly IntPtr SelStopSpeaking = ObjC.Sel("stopSpeakingAtBoundary:");

        /// <summary>What was set up, for the startup log: the voice, the rate, and which queue.</summary>
        public string Description
        {
            get
            {
                return (_voiceIdentifier ?? "AVSpeech default voice")
                    + " at rate "
                    + _rate.ToString("F2", CultureInfo.InvariantCulture)
                    + (_stream != null ? " (streamed)" : " (AVSpeech queue)");
            }
        }

        /// <summary>Stand the voice up. False, with the cause logged, if the system voice cannot
        /// be reached at all; the stream failing on its own only costs the queue's smoothness.</summary>
        public bool Start()
        {
            IntPtr pool = ObjC.AutoreleasePoolPush();
            try
            {
                ObjC.LoadSpeechFrameworks();
                _synth = ObjC.Alloc("AVSpeechSynthesizer", "init");
                _voiceIdentifier = MacSpokenContent.DefaultVoiceIdentifier();
                float stored = _voiceIdentifier == null ? -1f : MacSpokenContent.DefaultVoiceRate(_voiceIdentifier);
                _rate = stored >= 0f ? stored : 0.5f;
                CreateStream();
                return true;
            }
            catch (Exception e)
            {
                Log.Error("speech: the macOS system voice could not be started: " + e);
                Stop();
                return false;
            }
            finally
            {
                ObjC.AutoreleasePoolPop(pool);
            }
        }

        /// <summary>Set up the streaming queue. If that fails, messages go to AVSpeech's own queue instead.</summary>
        private void CreateStream()
        {
            DropStream();
            try
            {
                _stream = new MacSpeechStream();
                _stream.SetVolume(1f);
            }
            catch (Exception e)
            {
                Log.Error("speech: streamed queue unavailable, speaking through AVSpeech directly: " + e);
                DropStream();
            }
        }

        private void DropStream()
        {
            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }
        }

        public void Speak(string text, bool interrupt)
        {
            if (_synth == IntPtr.Zero)
            {
                return;
            }

            IntPtr pool = ObjC.AutoreleasePoolPush();
            try
            {
                if (interrupt)
                {
                    Silence();
                }

                if (_stream == null)
                {
                    Utter(text);
                }
                else if (!_stream.Enqueue(text, _voiceIdentifier, _rate))
                {
                    Log.Error("speech: the streamed queue can no longer render; speaking through AVSpeech directly from now on");
                    DropStream();
                    Utter(text);
                }
            }
            catch (Exception e)
            {
                Log.Error("speech: macOS speak failed: " + e.Message);
            }
            finally
            {
                ObjC.AutoreleasePoolPop(pool);
            }
        }

        public void Silence()
        {
            if (_synth == IntPtr.Zero)
            {
                return;
            }

            try
            {
                if (_stream != null)
                {
                    _stream.Clear();
                }
                else
                {
                    ObjC.Send(_synth, SelStopSpeaking, new IntPtr(BoundaryImmediate));
                }
            }
            catch (Exception e)
            {
                Log.Error("speech: macOS silence failed: " + e.Message);
            }
        }

        /// <summary>Once per frame: drive the streamed queue.</summary>
        public void Update()
        {
            if (_stream == null)
            {
                return;
            }

            IntPtr pool = ObjC.AutoreleasePoolPush();
            try
            {
                _stream.Update();
            }
            catch (Exception e)
            {
                Log.Error("speech: macOS speech update failed: " + e.Message);
            }
            finally
            {
                ObjC.AutoreleasePoolPop(pool);
            }
        }

        public void Stop()
        {
            DropStream();
            try
            {
                if (_synth != IntPtr.Zero)
                {
                    ObjC.Send(_synth, SelStopSpeaking, new IntPtr(BoundaryImmediate));
                    ObjC.Release(_synth);
                }
            }
            catch (Exception e)
            {
                Log.Info("speech: macOS synthesizer release failed: " + e.Message);
            }

            _synth = IntPtr.Zero;
        }

        /// <summary>Speak through AVSpeech's own queue: the fallback when the stream could not be set up.</summary>
        private void Utter(string text)
        {
            if (text == null || text.Trim().Length == 0)
            {
                return;
            }

            IntPtr voice = _voiceIdentifier == null ? IntPtr.Zero : MacSpeechStream.LookupVoice(_voiceIdentifier);
            IntPtr utterance = MacSpeechStream.MakeUtterance(text, voice, _rate);
            if (utterance == IntPtr.Zero)
            {
                Log.Error("speech: AVSpeechUtterance returned nil");
                return;
            }

            ObjC.SendFloat(utterance, SelSetVolume, 1f);
            ObjC.Send(_synth, SelSpeak, utterance);
        }
    }
}
