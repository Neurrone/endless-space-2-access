using System;
using System.Collections.Generic;
using ES2Access.UI;
using ES2Access.UI.Input;
using UnityEngine;

namespace ES2Access.Dev
{
    /// <summary>The probes about input: what the mod is claiming from the game, what a chord is
    /// doing, and the text-edit and sound state a keystroke leaves behind.</summary>
    public static partial class DevProbe
    {
        /// <summary>
        /// What the input layer is claiming from the game right now - the other end of the tripwire
        /// <see cref="Patches"/> watches. The patches say the game is ASKING; this says what it is
        /// being told, which is the only way to see a key leaking through a screen handover.
        ///
        /// <c>latched</c> is the consumed-key latch: a key the mod acted on stays the mod's until the
        /// player lets go of it, because the game's scan runs after our frame and the screen that
        /// consumed the key may be gone by then (see <see cref="ModInput.ClaimsKey"/>). An entry with
        /// <c>held: false</c> is one the next <c>Tick</c> will drop - normal for an injected action,
        /// which pressed no key; an entry that stays with <c>held: false</c> across calls is a stuck
        /// claim, and the game is deaf to that key until it clears.
        ///
        /// <c>layerLive</c> is <see cref="ModInput.LayerIsLive"/>'s verdict, split into the two halves
        /// that can answer no - no screen of ours (<c>screen: null</c>) or the game holding the
        /// keyboard for a text field. A key that reads claimed while <c>layerLive</c> is false is
        /// claimed by the latch alone, which is exactly the handover window the bug class lives in.
        /// </summary>
        public static string Claims()
        {
            return Claims(null);
        }

        /// <summary>
        /// <see cref="Claims()"/> plus <see cref="ModInput.ClaimsKey"/>'s answer - side-effect-free, the
        /// same call the game's key scans make - for each comma-separated <c>KeyCode</c> name in
        /// <paramref name="keys"/> (<c>"Escape,Return,Tab"</c>).
        /// </summary>
        public static string Claims(string keys)
        {
            return Guarded(json =>
            {
                ModInput input = ModEntry.Input;
                if (input == null)
                {
                    throw new InvalidOperationException("the input layer is not up");
                }

                GraphNavigator navigator = ModEntry.Navigator;
                Screens.Screen screen = navigator == null ? null : navigator.Screen;
                json.WritePropertyName("screen");
                json.WriteValue(screen == null ? null : screen.Key);
                json.WritePropertyName("screenFocused");
                json.WriteValue(input.ScreenIsFocused());
                json.WritePropertyName("keyboardElsewhere");
                json.WriteValue(input.KeyboardIsElsewhere());
                json.WritePropertyName("layerLive");
                json.WriteValue(input.LayerIsLive());
                json.WritePropertyName("backClaimed");
                json.WriteValue(input.BackClaimed);
                json.WritePropertyName("claimsBack");
                json.WriteValue(input.ClaimsBack());

                json.WritePropertyName("latched");
                json.WriteStartArray();
                IList<KeyCode> latched = input.ConsumedKeys;
                for (int i = 0; i < latched.Count; i++)
                {
                    json.WriteStartObject();
                    json.WritePropertyName("key");
                    json.WriteValue(latched[i].ToString());
                    json.WritePropertyName("held");
                    json.WriteValue(UnityEngine.Input.GetKey(latched[i]));
                    json.WriteEndObject();
                }

                json.WriteEndArray();

                // The chords that stay the game's whatever the key set says - ask about one with
                // Chord(), which is the only probe that can tell them apart.
                json.WritePropertyName("leftToGame");
                json.WriteStartArray();
                IList<ES2Access.UI.Input.KeyboardBinding> chords = input.ChordsLeftToGame;
                for (int i = 0; i < chords.Count; i++)
                {
                    json.WriteValue(chords[i].DisplayName);
                }

                json.WriteEndArray();

                if (string.IsNullOrEmpty(keys))
                {
                    return;
                }

                json.WritePropertyName("asked");
                json.WriteStartArray();
                foreach (string name in keys.Split(','))
                {
                    string wanted = name.Trim();
                    if (wanted.Length == 0)
                    {
                        continue;
                    }

                    json.WriteStartObject();
                    json.WritePropertyName("key");
                    json.WriteValue(wanted);
                    try
                    {
                        KeyCode key = (KeyCode)Enum.Parse(typeof(KeyCode), wanted, true);
                        json.WritePropertyName("claims");
                        json.WriteValue(input.ClaimsKey(key));
                        json.WritePropertyName("held");
                        json.WriteValue(UnityEngine.Input.GetKey(key));
                    }
                    catch (Exception)
                    {
                        json.WritePropertyName("error");
                        json.WriteValue("no KeyCode is named '" + wanted + "'");
                    }

                    json.WriteEndObject();
                }

                json.WriteEndArray();
            });
        }

        /// <summary>
        /// What the game's own key scans are told about one CHORD - <c>"Ctrl+Tab"</c>, <c>"Tab"</c>,
        /// <c>"Shift+Tab"</c>, parsed by the game's own <c>KeyCombination.FromString</c>.
        ///
        /// <see cref="Claims(string)"/> cannot answer this: it asks per <c>KeyCode</c>, exactly as
        /// <see cref="ModInput.ClaimsKey"/> is asked, so it says "claimed" for Tab and has no way to
        /// speak about Ctrl+Tab. This calls the prefix's own decision
        /// (<see cref="GameKeyStandDown.Claimed"/>) on a combination built here, which is what proves a
        /// game binding is reachable without holding three keys down while an HTTP request arrives.
        ///
        /// <c>suppressed: true</c> means the game is told the chord is not pressed, so whatever it has
        /// bound to it never runs. A chord handed back (<see cref="ModInput.LeaveToGame"/>) reads false
        /// while its bare key reads true.
        /// </summary>
        public static string Chord(string chord)
        {
            return Guarded(json =>
            {
                if (string.IsNullOrEmpty(chord))
                {
                    throw new InvalidOperationException("no chord was asked about");
                }

                Amplitude.Unity.Input.KeyCombination combination =
                    Amplitude.Unity.Input.KeyCombination.FromString(chord, "+");
                json.WritePropertyName("chord");
                json.WriteValue(chord);
                json.WritePropertyName("parsed");
                json.WriteValue(combination.ToString("+"));
                json.WritePropertyName("keys");
                json.WriteStartArray();
                for (int i = 0; i < combination.KeyCodes.Count; i++)
                {
                    json.WriteStartObject();
                    json.WritePropertyName("key");
                    json.WriteValue(combination.KeyCodes[i].ToString());
                    json.WritePropertyName("held");
                    json.WriteValue(UnityEngine.Input.GetKey(combination.KeyCodes[i]));
                    json.WriteEndObject();
                }

                json.WriteEndArray();
                json.WritePropertyName("suppressed");
                json.WriteValue(ES2Access.UI.Input.GameKeyStandDown.Claimed(combination));
            });
        }

        /// <summary>
        /// End the text edit that is running right now, as either of its two endings, and report what
        /// the box held on the way out.
        ///
        /// A COMMIT is a physical Return and a cancel is a physical Escape, and neither can be
        /// injected - the mod's own <c>/input</c> queue carries actions, not keystrokes, and the
        /// engine reads both of these keys straight off Unity. So the only way to drive the two
        /// endings from a test is to answer the question the focus setter asks
        /// (<c>TextFieldEditor.CommitTheNextRelease</c>) and then let go of the keyboard for real:
        /// everything downstream - the restore, the words, the refusal path - runs exactly as it does
        /// for the player.
        /// </summary>
        public static string EndEdit(bool commit)
        {
            return Guarded(json =>
            {
                json.WritePropertyName("wasEditing");
                json.WriteValue(ES2Access.Screens.TextFieldEditor.Editing);
                ES2Access.Screens.TextFieldEditor.CommitTheNextRelease = commit;
                AgeManager age = AgeManager.Instance;
                if (age != null)
                {
                    age.FocusedControl = null;
                }

                ES2Access.Screens.TextFieldEditor.CommitTheNextRelease = false;
                json.WritePropertyName("commit");
                json.WriteValue(commit);
            });
        }

        /// <summary>The same lever, ARMED and left armed, for the endings the mod does not cause: the
        /// game's own validate callback releases the keyboard itself (and hides the surface around it),
        /// so a successful commit is driven by arming this and then invoking that callback - which is
        /// the real sequence, with only the physical Return replaced.</summary>
        public static string ArmCommit()
        {
            return Guarded(json =>
            {
                ES2Access.Screens.TextFieldEditor.CommitTheNextRelease = true;
                json.WritePropertyName("armed");
                json.WriteValue(true);
            });
        }

        /// <summary>What the carry has asked the game to PLAY, which is the one thing about a drag a
        /// test cannot hear: how many sound events this load of the mod has posted and which one it
        /// posted last (<see cref="ES2Access.UI.CarrySounds"/>). A pick-up and a carry's ending are
        /// two different ids, so a pair of reads either side of a gesture says which of them
        /// happened.</summary>
        public static string Sounds()
        {
            return Guarded(json =>
            {
                json.WritePropertyName("posted");
                json.WriteValue(ES2Access.UI.CarrySounds.Posted);
                json.WritePropertyName("last");
                json.WriteValue(ES2Access.UI.CarrySounds.Last);
            });
        }

    }
}
