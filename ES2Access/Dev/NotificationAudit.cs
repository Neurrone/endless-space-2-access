using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Loader.Dev;
using ES2Access.Screens;
using ES2Access.UI;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using Breach = ES2Access.Dev.AuditModel.Breach;
using Declared = ES2Access.Dev.AuditModel.Declared;

namespace ES2Access.Dev
{
    /// <summary>
    /// The notification family's self-check: what the popup PAINTS against what the mod DECLARES,
    /// for whichever popup is on screen right now.
    ///
    /// Sixty-nine window types share one reader, and every defect the family has produced was the
    /// same shape - one prefab violating a premise the shared reading never checked. A label drawn
    /// where nothing looked for it, a sentence spoken that no widget draws, a control swept into the
    /// wrong band, a tooltip promised on a control that has none or hung on a piece nothing reads.
    /// None of them is visible in a transcript, all of them are visible in a comparison, and the
    /// comparison is mechanical - so it lives here rather than in a stage's notes, and a popup nobody
    /// has ever sighted checks itself the moment it opens.
    ///
    /// Five invariants, each answering with the widgets and strings that broke it:
    ///
    /// <list type="number">
    /// <item><b>Completeness</b> - every painted string is somewhere in what the popup says or
    /// carries.</item>
    /// <item><b>Honesty</b> - every spoken word traces back to painted text, to a tooltip the game
    /// would draw, or to the mod's own vocabulary. A word from none of the three was invented.</item>
    /// <item><b>Placement</b> - the strips hold the rails and what the game drew beside them and
    /// nothing else, every declared node hangs under the popup, and the body reads down the page.
    /// </item>
    /// <item><b>Tooltip parity</b> - a node that declares tooltip content has a tooltip that would
    /// draw, and a tooltip that would draw is reachable from the node that covers its widget.</item>
    /// <item><b>Pairing</b> - no spoken line is a bare figure. Completeness counts words, so a card
    /// read label by label passes it while saying "Level" and "2" as two lines.</item>
    /// </list>
    ///
    /// The painted side is measured from the window's own tree and knows nothing of the screen's
    /// code, which is what makes the comparison worth anything: both halves are re-derived, from
    /// different evidence, and agreement is the claim being tested.
    ///
    /// Main-thread only, dev-only, and never on the player's path: nothing here speaks, focuses or
    /// changes what the game is showing.
    /// </summary>
    internal static class NotificationAudit
    {
        /// <summary>How deep under the popup's root the painted walk looks. The deepest word in the
        /// family is a reward label eight levels down inside two nested scroll views; twenty leaves
        /// room for a prefab nobody has met yet without letting a wiring loop run away.</summary>
        private const int MaxDepth = 20;

        /// <summary>A ceiling on the painted walk, so a probe on a pathological tree answers rather
        /// than hangs.</summary>
        private const int MaxWidgets = 4000;

        /// <summary>How far up and down from a node's own widget a tooltip still counts as that
        /// node's: the game hangs an explanation on the block it drew a label in (up) and on the
        /// picture inside a cell (down), and both are read by the node that covers them.</summary>
        private const int MaxRelated = 8;

        // ---- the answer ----

        private sealed class Result
        {
            public string Window;
            public string Title;
            public int PaintedTexts;
            public int PaintedControls;
            public int PaintedTooltips;
            public int Nodes;
            public readonly List<Breach> Completeness = new List<Breach>();
            public readonly List<Breach> Honesty = new List<Breach>();
            public readonly List<Breach> Placement = new List<Breach>();
            public readonly List<Breach> Tooltips = new List<Breach>();
            public readonly List<Breach> Pairing = new List<Breach>();

            /// <summary>Nodes whose widget could not be found - not a breach, but the placement and
            /// tooltip answers are blind to them, so they are reported rather than dropped.</summary>
            public readonly List<Breach> Unlocatable = new List<Breach>();

            public int Breaches
            {
                get
                {
                    return Completeness.Count
                        + Honesty.Count
                        + Placement.Count
                        + Tooltips.Count
                        + Pairing.Count;
                }
            }
        }

        // ---- the two sides ----

        private sealed class Painted
        {
            public readonly List<PaintedText> Texts = new List<PaintedText>();
            public readonly List<PaintedTip> Tips = new List<PaintedTip>();
            public int Controls;

            /// <summary>Every string the player can see and every string a tooltip on this popup
            /// would draw, reduced (<see cref="Reduce"/>) and whole as well as line by line - the
            /// accounts a spoken line is allowed to be assembled out of.</summary>
            public readonly List<string> Phrases = new List<string>();
        }

        private sealed class PaintedText
        {
            public AgeTransform Widget;
            public string Value;

            /// <summary>What the label says when the box it is drawn in has ellipsized it - a spoken
            /// line that says the whole thing is right, not a mismatch.</summary>
            public string Full;
        }

        private sealed class PaintedTip
        {
            public AgeTooltip Tooltip;
            public AgeTransform Owner;
            public bool Interactive;
        }

        // ---- entry points ----

        /// <summary>The check, run against whichever popup is showing, as JSON.</summary>
        public static string Json()
        {
            try
            {
                NotificationWindow window = Shown();
                if (window == null)
                {
                    return DevJson.Error("no notification popup is showing");
                }

                Result result = Check(window);
                return Write(result);
            }
            catch (Exception e)
            {
                return DevJson.Error(e.Message);
            }
        }

        /// <summary>
        /// Run the check on every popup the player is shown, and complain in the log when one does
        /// not line up.
        ///
        /// This is the point of the whole file: a defect that only shows on the one prefab nobody
        /// tested is caught while somebody is playing, without a stage having to think of that popup
        /// first. It is armed only when the dev server is up, costs nothing per frame - the screen
        /// calls it when a popup's words settle, which is once per popup - and is dropped on
        /// teardown.
        /// </summary>
        public static void Arm()
        {
            _vocabulary = null;
            _unpaintedWaits = 0;
            if (!DevServerUp())
            {
                return;
            }

            NotificationScreen.Shown = OnShown;
        }

        public static void Disarm()
        {
            NotificationScreen.Shown = null;
            _vocabulary = null;
        }

        /// <summary>How many ready frames the check will wait for a popup to paint SOMETHING before
        /// running anyway and saying so. Measured: the arrival animation of a forced popup runs about
        /// forty frames, and no popup of the sixty-four in the family paints fewer than four strings
        /// once it has settled (the smallest measured count), so a popup still drawing nothing after a
        /// second of ready frames is a finding rather than an animation.</summary>
        private const int MaxUnpaintedWaits = 60;

        /// <summary>What the SHARED half of every popup is keyed under - the rails, the words, the
        /// regions the strips are sorted into. Not a scope test: each variant keys its own body by a
        /// prefix of its own ("ground-report/", "battle-setup/", …), so asking this of a node answers
        /// "is this the frame or the content", never "is this ours" (which
        /// <see cref="DeclaredNodes"/> answers structurally).</summary>
        private const string Prefix = "notification:";

        private static int _unpaintedWaits;

        /// <summary>
        /// Check the popup, once it is actually drawing.
        ///
        /// Ready is not painted: the screen calls this two ready frames after a popup's words settle,
        /// and on those frames the popup can still be fading its content up with not one string drawn.
        /// Every line the mod spoke is then unaccounted for and the whole readout reads as "says what
        /// nothing draws" - measured live on LuxuryDiscovered, TechnologyStageUnlocked and
        /// PopulationChange, each of which checks clean a moment later. So a check that finds NOTHING
        /// painted is not a finding, it is an early frame: answer false and be asked again.
        /// </summary>
        private static bool OnShown(NotificationWindow window)
        {
            try
            {
                Result result = Check(window);
                if (result.PaintedTexts == 0 && ++_unpaintedWaits <= MaxUnpaintedWaits)
                {
                    return false;
                }

                if (result.PaintedTexts == 0)
                {
                    Core.Util.Log.Warn(
                        "notification parity: "
                            + result.Window
                            + " was still drawing no text at all after "
                            + MaxUnpaintedWaits
                            + " ready frames - checking it anyway"
                    );
                }

                _unpaintedWaits = 0;
                if (result.Breaches == 0)
                {
                    return true;
                }

                Report(result, "completeness", result.Completeness);
                Report(result, "honesty", result.Honesty);
                Report(result, "placement", result.Placement);
                Report(result, "tooltip parity", result.Tooltips);
                Report(result, "pairing", result.Pairing);
            }
            catch (Exception e)
            {
                Core.Util.Log.Warn("notification parity: the check itself threw: " + e);
            }

            return true;
        }

        /// <summary>One line per invariant broken, naming the popup and the first offender - enough
        /// to know a defect is there and which probe to run, never the whole answer.</summary>
        private static void Report(Result result, string invariant, List<Breach> breaches)
        {
            if (breaches.Count == 0)
            {
                return;
            }

            Breach first = breaches[0];
            Core.Util.Log.Warn(
                "notification parity: "
                    + result.Window
                    + " breaks "
                    + invariant
                    + " ("
                    + breaches.Count
                    + "): "
                    + first.Where
                    + " - "
                    + first.What
                    + (string.IsNullOrEmpty(first.Detail) ? "" : " [" + first.Detail + "]")
                    + " (DevProbe.NotificationParity() for all of it)"
            );
        }

        // ---- the check ----

        private static Result Check(NotificationWindow window)
        {
            Result result = new Result();
            result.Window = window.GetType().Name;

            AgeTransform root = window.gameObject.GetComponent<AgeTransform>();
            if (root == null)
            {
                throw new Exception("the popup has no AgeTransform to walk");
            }

            Painted painted = new Painted();
            Walk(root, painted, 0, new int[1]);
            AddDrawnTooltipWords(painted);
            result.PaintedTexts = painted.Texts.Count;
            result.PaintedControls = painted.Controls;
            result.PaintedTooltips = painted.Tips.Count;

            NotificationScreen screen = TheScreen();
            List<Declared> declared = AuditModel.DeclaredNodes(screen, result.Unlocatable, true);
            result.Nodes = declared.Count;

            // Arriving on the popup says its title before any node speaks, so the title is declared
            // even though no node carries it.
            List<string> spokenAnywhere = new List<string>();
            string name = ScreenName(screen);
            result.Title = name;
            if (!string.IsNullOrEmpty(name))
            {
                spokenAnywhere.Add(name);
            }

            for (int i = 0; i < declared.Count; i++)
            {
                for (int j = 0; j < declared[i].Spoken.Count; j++)
                {
                    spokenAnywhere.Add(declared[i].Spoken[j]);
                }
            }

            CheckCompleteness(painted, spokenAnywhere, result);
            CheckHonesty(painted, declared, name, result);
            CheckPlacement(root, declared, result);
            CheckTooltips(painted, declared, result);
            CheckPairing(declared, result);
            return result;
        }

        // ---- 1. completeness ----

        /// <summary>
        /// Every painted string is somewhere in what the popup says.
        ///
        /// Matched on letters and digits alone (see <see cref="Reduce"/>) and per painted PIECE: a
        /// mod-composed line joins several of the game's strings with separators of its own - a
        /// quadrant, a technology and a cost read as one sentence - and asking whether the sentence
        /// equals any one piece would fail on all of them, while asking whether it CONTAINS each
        /// piece is exactly the claim being made.
        ///
        /// Two exceptions, both the family's own rules rather than concessions. A label the game
        /// ellipsized to fit its box is matched on the whole string as well, because speaking the
        /// full words is right and the drawn "Colony Ba." is the artifact. And a string the popup
        /// never filled in - a template with its hole still in it - is not the popup's words at all
        /// (<c>NotificationScreen</c> drops those on purpose), so it is not owed a reading.
        /// </summary>
        private static void CheckCompleteness(
            Painted painted,
            List<string> spoken,
            Result result
        )
        {
            List<string> reduced = new List<string>();
            for (int i = 0; i < spoken.Count; i++)
            {
                reduced.Add(AuditModel.Reduce(spoken[i]));
            }

            for (int i = 0; i < painted.Texts.Count; i++)
            {
                PaintedText text = painted.Texts[i];
                IList<string> lines = AgeText.Lines(text.Value);
                for (int line = 0; line < lines.Count; line++)
                {
                    string piece = lines[line];
                    if (Hollow(piece))
                    {
                        continue;
                    }

                    if (AuditModel.Contains(reduced, piece) || AuditModel.Contains(reduced, Slice(text.Full, line)))
                    {
                        continue;
                    }

                    result.Completeness.Add(
                        AuditModel.Made(
                            text.Widget,
                            AuditModel.Name(text.Widget),
                            "painted but nothing says it",
                            AuditModel.Excerpt(piece)
                        )
                    );
                }
            }
        }

        private static string Slice(string full, int line)
        {
            if (string.IsNullOrEmpty(full))
            {
                return null;
            }

            IList<string> lines = AgeText.Lines(full);
            return line < lines.Count ? lines[line] : full;
        }

        // ---- 2. honesty ----

        /// <summary>
        /// Every spoken line is accounted for, piece by piece: painted text, a tooltip the game would
        /// draw (or is drawing), or one of the mod's own phrases.
        ///
        /// PHRASES, not words. A spoken line is a composition - a caption, a role word, a state, a
        /// position - so the question is whether any PART of it was invented, and word-by-word
        /// accounting cannot answer it: the mod's own strings contain several hundred ordinary
        /// English words between them, and "Choose an objective" is spelled entirely out of words
        /// some ModStrings template happens to use. Measured, at word granularity, on a label faded
        /// out from under a node that kept reading it: no complaint. So each account - painted string,
        /// tooltip line, mod template with its holes taken out - is struck out of the line where it
        /// appears, and what is left with letters still in it is what nothing can explain.
        ///
        /// Numbers are not phrases: a value comes from the game and the same one is drawn under a
        /// dozen formats ("104", "104.0", "+104"), so leftover digits are not a complaint.
        /// </summary>
        private static void CheckHonesty(
            Painted painted,
            List<Declared> declared,
            string title,
            Result result
        )
        {
            List<string> accounts = new List<string>(painted.Phrases);
            accounts.AddRange(Vocabulary());
            // Longest first: striking "not checked" out before "checked" leaves nothing behind that a
            // shorter phrase could half-cover.
            accounts.Sort(LongestFirst);

            for (int i = 0; i < declared.Count; i++)
            {
                Declared node = declared[i];
                for (int j = 0; j < node.Spoken.Count; j++)
                {
                    string line = node.Spoken[j];
                    string left = Unaccounted(line, accounts);
                    if (left == null)
                    {
                        continue;
                    }

                    result.Honesty.Add(
                        AuditModel.Made(node.Widget, node.Key, "says what nothing draws: " + left, AuditModel.Excerpt(line))
                    );
                }
            }

            string strayTitle = Unaccounted(title, accounts);
            if (strayTitle != null)
            {
                result.Honesty.Add(
                    AuditModel.Made(
                        null,
                        "(the popup's name)",
                        "says what nothing draws: " + strayTitle,
                        AuditModel.Excerpt(title)
                    )
                );
            }
        }

        private static readonly Comparison<string> LongestFirst = delegate(string a, string b)
        {
            return b.Length.CompareTo(a.Length);
        };

        /// <summary>What is left of a spoken line once everything that can account for it has been
        /// struck out, or null when nothing with letters in it is left. Struck out with a marker
        /// rather than deleted, so two unrelated halves cannot be pushed together into a third
        /// account that was never there.</summary>
        private static string Unaccounted(string line, List<string> accounts)
        {
            string reduced = AuditModel.Reduce(line);
            if (reduced.Length == 0)
            {
                return null;
            }

            char[] left = reduced.ToCharArray();
            for (int i = 0; i < accounts.Count; i++)
            {
                string account = accounts[i];
                if (account.Length == 0)
                {
                    continue;
                }

                for (
                    int at = IndexOf(left, account, 0);
                    at >= 0;
                    at = IndexOf(left, account, at + 1)
                )
                {
                    for (int c = at; c < at + account.Length; c++)
                    {
                        left[c] = ' ';
                    }
                }
            }

            System.Text.StringBuilder rest = new System.Text.StringBuilder();
            bool letters = false;
            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] == ' ')
                {
                    if (rest.Length > 0 && rest[rest.Length - 1] != ' ')
                    {
                        rest.Append(' ');
                    }

                    continue;
                }

                letters = letters || char.IsLetter(left[i]);
                rest.Append(left[i]);
            }

            return letters ? rest.ToString().Trim() : null;
        }

        private static int IndexOf(char[] text, string want, int from)
        {
            for (int i = from; i + want.Length <= text.Length; i++)
            {
                int c = 0;
                while (c < want.Length && text[i + c] == want[c])
                {
                    c++;
                }

                if (c == want.Length)
                {
                    return i;
                }
            }

            return -1;
        }

        // ---- 3. placement ----

        /// <summary>
        /// Where the nodes were put, against where the game drew them.
        ///
        /// Three questions. Every declared node hangs under the popup - a node whose widget belongs
        /// to something else is reading another window's content. The strips hold the rails and
        /// whatever the game drew inside the same containers and nothing else, which is the rule the
        /// screen sorts by (<c>NotificationScreen.Sort</c>): asked here of the CONTAINERS again, from
        /// the declared nodes, so a sort that starts measuring rectangles instead is caught. And the
        /// body reads down the page, so a body item drawn above the one before it is a walk that
        /// jumps around the popup.
        ///
        /// A clipped widget is measured at the box it is shown in (<see cref="AgeWidgets.Clipped"/>):
        /// a paragraph laid out taller than its viewport keeps a rectangle that runs off the popup
        /// and would put every item after it out of order.
        /// </summary>
        private static void CheckPlacement(
            AgeTransform root,
            List<Declared> declared,
            Result result
        )
        {
            List<AgeTransform> bars = new List<AgeTransform>();
            for (int i = 0; i < declared.Count; i++)
            {
                if (IsRail(declared[i].Key) && declared[i].Widget != null)
                {
                    AgeTransform holder = declared[i].Widget.Parent;
                    if (holder != null && bars.IndexOf(holder) < 0)
                    {
                        bars.Add(holder);
                    }
                }
            }

            Declared previous = null;
            for (int i = 0; i < declared.Count; i++)
            {
                Declared node = declared[i];
                if (node.Widget == null)
                {
                    continue;
                }

                if (!AgeWidgets.Under(node.Widget, root))
                {
                    result.Placement.Add(
                        AuditModel.Made(node.Widget, node.Key, "declared but drawn outside the popup", null)
                    );
                    continue;
                }

                bool strip = IsStrip(node.Region);
                bool inBar = WithinAny(node.Widget, bars);
                if (strip && !IsRail(node.Key) && !inBar)
                {
                    result.Placement.Add(
                        AuditModel.Made(
                            node.Widget,
                            node.Key,
                            "in the " + node.Region + " strip, but the game drew it outside the bar",
                            null
                        )
                    );
                }

                if (!strip && IsBody(node.Region) && inBar)
                {
                    result.Placement.Add(
                        AuditModel.Made(
                            node.Widget,
                            node.Key,
                            "read as content, but the game drew it inside a strip's bar",
                            null
                        )
                    );
                }

                if (!IsBody(node.Region))
                {
                    continue;
                }

                // What the popup SAYS leads what it DRAWS (<c>NotificationScreen.Build</c>), so the
                // words take no place in the down-the-page order: a popup that writes a sentence over
                // its chart, or beneath it, reads the sentence first either way. Measured over the
                // family, nine popups draw their first body item above the words and every one of them
                // is that convention - the flag was the rule the design deliberately breaks.
                if (node.Key == Words)
                {
                    continue;
                }

                if (
                    previous != null
                    && AgeLayout.TopThenLeft(
                        AgeWidgets.Clipped(previous.Widget),
                        AgeWidgets.Clipped(node.Widget)
                    ) > 0
                )
                {
                    result.Placement.Add(
                        AuditModel.Made(
                            node.Widget,
                            node.Key,
                            "read after " + previous.Key + ", but drawn above or left of it",
                            null
                        )
                    );
                }

                previous = node;
            }
        }

        /// <summary>The node the popup's own words are read as, which leads the body by design - the
        /// screen's own key for it, never a second spelling.</summary>
        private const string Words = NotificationScreen.WordsKey;

        /// <summary>The six controls the base popup window owns, under the node keys they are declared
        /// with - built from the screen's own two lists rather than re-spelled here, because a rail this
        /// file failed to recognise is a rail counted as the popup's own content.</summary>
        private static readonly List<string> Rails = RailKeys();

        private static List<string> RailKeys()
        {
            List<string> keys = new List<string>();
            for (int i = 0; i < NotificationScreen.TopKeys.Length; i++)
            {
                keys.Add(Prefix + NotificationScreen.TopKeys[i]);
            }

            for (int i = 0; i < NotificationScreen.BottomKeys.Length; i++)
            {
                keys.Add(Prefix + NotificationScreen.BottomKeys[i]);
            }

            return keys;
        }

        private static bool IsRail(string key)
        {
            return Rails.IndexOf(key) >= 0;
        }

        private static bool IsStrip(string region)
        {
            return region == NotificationScreen.TopRegion
                || region == NotificationScreen.BottomRegion;
        }

        private static bool IsBody(string region)
        {
            return region != null && !IsStrip(region) && region != NotificationScreen.InfoRegion;
        }

        // ---- 4. tooltip parity ----

        /// <summary>
        /// The three directions a tooltip claim can be wrong.
        ///
        /// A control that DECLARES a reviewable tooltip must have one the game would draw - the
        /// promise is that there is something to read, and a tooltip with neither words nor a target
        /// draws nothing - and the tooltip it POINTS AT must be that one, since a control carrying two
        /// of them shows only what the pointer is sent to.
        ///
        /// Nothing in the readout says any of this any more (the "has tooltip" indication is gone: the
        /// player checks the review buffer on every control, so a per-control claim about it said
        /// nothing). The promise did not go with it - it became implicit, and a promise nobody hears is
        /// a promise nobody can check by ear, so the check reads the DECLARATION instead of the spoken
        /// line. Same nodes, same breaches, no wording in the middle.
        /// And a tooltip the game WOULD draw on a control the player can reach must be reachable
        /// from the node that covers it: the luxury popup hung the resource's dossier on the block
        /// around the words rather than on the words, and the reading that only ever asked the widget
        /// itself lost it silently.
        ///
        /// A tooltip with no words of its own is judged on coverage alone: the renderer assembles it
        /// from its target when it draws, so there is no content to look for in a buffer until the
        /// player is on it (see the blind spots in the stage report). "No words of its own" is the
        /// reader's own test, not an empty content field - a renderer-assembled tooltip's content field
        /// holds the key its dossier is looked up by.
        /// </summary>
        private static void CheckTooltips(
            Painted painted,
            List<Declared> declared,
            Result result
        )
        {
            for (int i = 0; i < declared.Count; i++)
            {
                Declared node = declared[i];
                if (!AuditModel.Promises(node))
                {
                    continue;
                }

                AgeTransform evidence = AuditModel.Evidence(node);
                if (evidence == null || !AnyDrawing(evidence))
                {
                    result.Tooltips.Add(
                        AuditModel.Made(
                            evidence,
                            node.Key,
                            "declares a tooltip to review with nothing that draws",
                            null
                        )
                    );
                    continue;
                }

                // A tooltip EXISTING near the widget is not the promise: the promise is that the one the
                // pointer goes to draws, and a line carrying two - the law's dossier and an empty one on
                // the picture inside it - used to aim at the empty one and draw nothing while saying
                // this. Asked of the node's own declared aim rather than re-derived here, so the check
                // and the reading cannot disagree about which tooltip a node points at.
                AgeTooltip aimed = AuditModel.AimOf(node);
                if (aimed != null && !AgeWidgets.Draws(aimed))
                {
                    result.Tooltips.Add(
                        AuditModel.Made(
                            evidence,
                            node.Key,
                            "declares a tooltip to review and points at one that draws nothing",
                            null
                        )
                    );
                }
            }

            for (int i = 0; i < painted.Tips.Count; i++)
            {
                PaintedTip tip = painted.Tips[i];
                if (!tip.Interactive)
                {
                    continue;
                }

                List<Declared> covering = AuditModel.Covering(declared, tip.Owner, tip.Tooltip);
                if (covering.Count == 0)
                {
                    result.Tooltips.Add(
                        AuditModel.Made(
                            tip.Owner,
                            AuditModel.Name(tip.Owner),
                            "the game would draw a tooltip here and no node covers it",
                            AuditModel.Excerpt(AgeText.Tooltip(tip.Tooltip))
                        )
                    );
                    continue;
                }

                // Words to look for only where the words are ON the tooltip. A renderer-assembled one
                // keeps a data key in the same field - the law card's "LawP01L00", the survey's
                // "Politics01" - and that key is what the renderer looks its dossier UP by, never
                // anything the game draws or the mod says (crop-verified: the drawn panels head
                // "Dust Windfall" and "Industrialists"). Asked through the same helper the reader picks
                // its mode with, so the check and the reading can never disagree about which tooltips
                // carry their own words.
                string content = AgeText.Tooltip(AgeWidgets.Readable(tip.Tooltip));
                if (string.IsNullOrEmpty(content))
                {
                    continue;
                }

                if (!AuditModel.CarriedBy(covering, content))
                {
                    result.Tooltips.Add(
                        AuditModel.Made(
                            tip.Owner,
                            AuditModel.Name(tip.Owner) + " (" + covering[0].Key + ")",
                            "the tooltip's words are not in what that node carries",
                            AuditModel.Excerpt(content)
                        )
                    );
                }
            }
        }

        /// <summary>Whether anything on this widget, inside it or around it would draw a tooltip -
        /// the game hangs an explanation on the block it drew a label in as readily as on the label.
        /// </summary>
        internal static bool AnyDrawing(AgeTransform widget)
        {
            List<AgeTooltip> found = new List<AgeTooltip>();
            AgeWidgets.Tooltips(widget, found);
            for (int i = 0; i < found.Count; i++)
            {
                if (AgeWidgets.Draws(found[i]))
                {
                    return true;
                }
            }

            AgeTransform at = widget.Parent;
            for (int depth = 0; at != null && depth < MaxRelated; depth++)
            {
                if (AgeWidgets.Draws(at.AgeTooltip))
                {
                    return true;
                }

                at = at.Parent;
            }

            return false;
        }

        // ---- 5. pairing ----

        /// <summary>
        /// Nothing the popup says is a bare figure.
        ///
        /// The other four invariants are set questions - is this word somewhere, does this word come
        /// from somewhere - and a reading that keeps every word but throws the pairing away passes all
        /// of them: a hero card read label by label says "Level" and "2" as two lines and four bare
        /// "0/6" figures, and completeness finds every one of those words present. What went missing is
        /// not a word, it is which caption a number belongs to, so it needs its own question.
        ///
        /// The test is the honesty rule's own reason turned around. Numbers are not phrases there,
        /// because a value comes from the game under a dozen formats and nothing can account for
        /// digits; here that is exactly what makes a digits-only line a finding, since a figure with
        /// nothing but separators around it names nothing at all. A caption sitting beside its value
        /// - "Wit, 0/6" - has letters and is clean, whichever way the reader composed it.
        /// </summary>
        private static void CheckPairing(List<Declared> declared, Result result)
        {
            for (int i = 0; i < declared.Count; i++)
            {
                Declared node = declared[i];
                for (int j = 0; j < node.Spoken.Count; j++)
                {
                    if (!FigureOnly(node.Spoken[j]))
                    {
                        continue;
                    }

                    result.Pairing.Add(
                        AuditModel.Made(
                            node.Widget,
                            node.Key,
                            "says a figure with no caption",
                            AuditModel.Excerpt(node.Spoken[j])
                        )
                    );
                }
            }
        }

        /// <summary>Whether a line is a figure and nothing else: digits, and whatever separators a
        /// fraction or a sign is written with, but not one letter. Empty of letters AND digits is not
        /// a figure - it is nothing, and nothing is the buffer's own business.</summary>
        internal static bool FigureOnly(string line)
        {
            string reduced = AuditModel.Reduce(line);
            if (reduced.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < reduced.Length; i++)
            {
                if (char.IsLetter(reduced[i]))
                {
                    return false;
                }
            }

            return true;
        }

        // ---- the painted side ----

        /// <summary>
        /// Everything the popup is drawing, from its own tree.
        ///
        /// The gate is the family's own painted-ness rule: a widget the player cannot see is one
        /// whose chain is hidden OR whose chain has been faded to nothing, and a pooled row retired
        /// by alpha keeps its old words, its old rectangle and its Visible flag. Alpha is only
        /// inherited where the parent says so (<c>StrictVisibility</c>), which is the same test the
        /// engine's own draw makes.
        /// </summary>
        private static void Walk(AgeTransform widget, Painted painted, int depth, int[] budget)
        {
            if (widget == null || depth > MaxDepth || budget[0]++ > MaxWidgets)
            {
                return;
            }

            AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
            if (label != null)
            {
                string text = AgeText.Label(label);
                if (!string.IsNullOrEmpty(text))
                {
                    PaintedText painting = new PaintedText();
                    painting.Widget = widget;
                    painting.Value = text;
                    painting.Full = AgeText.FullLabel(label);
                    painted.Texts.Add(painting);
                    AddPhrase(painted.Phrases, text);
                    AddPhrase(painted.Phrases, painting.Full);
                }
            }

            AgeControl control = AgeWidgets.Control(widget);
            if (control != null)
            {
                painted.Controls++;
            }

            AgeTooltip tooltip = widget.AgeTooltip;
            if (AgeWidgets.Draws(tooltip))
            {
                PaintedTip tip = new PaintedTip();
                tip.Tooltip = tooltip;
                tip.Owner = widget;
                tip.Interactive = control != null;
                painted.Tips.Add(tip);
                AddPhrase(painted.Phrases, AgeText.Tooltip(tooltip));
                AddPhrase(painted.Phrases, AgeWidgets.TooltipTitle(tooltip));
            }

            List<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = AgeWidgets.DrawnChild(children, i);
                if (child != null)
                {
                    Walk(child, painted, depth + 1, budget);
                }
            }
        }

        /// <summary>The tooltip the game is drawing right now, if it is drawing one: a tooltip built
        /// by the renderer has no words on the widget at all, and its buffer line is honest text the
        /// popup's own tree cannot account for.</summary>
        private static void AddDrawnTooltipWords(Painted painted)
        {
            try
            {
                GuiTooltipWindow window = GameWindows.Shown<GuiTooltipWindow>();
                if (window == null || window.PanelFeaturesTable == null)
                {
                    return;
                }

                IList<string> lines = AgeWidgets.DrawnLines(window.PanelFeaturesTable);
                for (int i = 0; i < lines.Count; i++)
                {
                    AddPhrase(painted.Phrases, lines[i]);
                }
            }
            catch (Exception e)
            {
                Core.Util.Log.Warn("notification parity: reading the drawn tooltip threw: " + e);
            }
        }

        // ---- the declared side ----

        private static NotificationScreen TheScreen()
        {
            ScreenManager screens = ModEntry.Screens;
            if (screens == null)
            {
                return null;
            }

            IList<Screens.Screen> registered = screens.Registered;
            for (int i = 0; i < registered.Count; i++)
            {
                NotificationScreen screen = registered[i] as NotificationScreen;
                if (screen != null)
                {
                    return screen;
                }
            }

            return null;
        }

        private static string ScreenName(NotificationScreen screen)
        {
            try
            {
                return screen == null ? null : screen.ScreenName;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static NotificationWindow Shown()
        {
            GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
            if (gui == null || !gui.IsAnyNotificationVisible)
            {
                return null;
            }

            NotificationWindow[] windows = gui.gameObject.GetComponentsInChildren<NotificationWindow>(
                true
            );
            for (int i = 0; i < windows.Length; i++)
            {
                if (windows[i] != null && windows[i].Shown)
                {
                    return windows[i];
                }
            }

            return null;
        }

        // ---- phrases ----

        /// <summary>
        /// The mod's own phrases, read out of <see cref="ModStrings"/> rather than listed here: every
        /// key it compiles in, resolved through the LIVE translation, cut at its holes into the
        /// literal pieces a composed line would actually contain. So the role words, the states, the
        /// position phrase, the separators and every screen name account for themselves in whatever
        /// language the player is running, and a stage that adds a new spoken word to the mod does
        /// not have to remember this file.
        ///
        /// The class holds a few PREFIX constants beside the keys ("color."), which are not keys and
        /// would each cost a warning; they are told apart by shape, since a key always has a dotted
        /// tail and a prefix always ends in the dot.
        ///
        /// The GAME's own captions are read the same way (<see cref="GameCaptions"/>): a word the game
        /// wrote for a figure it draws as a bare icon was not invented by the mod either.
        /// </summary>
        private static List<string> Vocabulary()
        {
            if (_vocabulary != null)
            {
                return _vocabulary;
            }

            List<string> phrases = new List<string>();
            FieldInfo[] fields = typeof(ModStrings).GetFields(
                BindingFlags.Public | BindingFlags.Static
            );
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!field.IsLiteral || field.FieldType != typeof(string))
                {
                    continue;
                }

                string key = field.GetRawConstantValue() as string;
                if (string.IsNullOrEmpty(key) || key.EndsWith(".") || key.IndexOf('.') < 0)
                {
                    continue;
                }

                string[] pieces = ModStrings.Get(key).Split('{', '}');
                for (int piece = 0; piece < pieces.Length; piece++)
                {
                    AddPhrase(phrases, pieces[piece]);
                }
            }

            GameCaptions(phrases);
            GameKeyNames(phrases);
            _vocabulary = phrases;
            return phrases;
        }

        /// <summary>
        /// The game's own captions the mod borrows, resolved through the live string table.
        ///
        /// Where the game draws a figure with an icon and no words - a hero card's experience, a minor
        /// faction's relation - the mod captions it with the game's OWN word for that figure rather
        /// than a paraphrase, and that word is drawn nowhere on the popup, so the painted side cannot
        /// account for it. It is still not invented: the reader names the string-table key in its own
        /// source, which is what makes the borrowing checkable at all. So every key the mod compiles
        /// in - a const string starting with the game's own '%' - accounts for the words it resolves
        /// to, and a caption the mod made up still has nothing behind it.
        /// </summary>
        private static void GameCaptions(List<string> phrases)
        {
            try
            {
                Type[] types = typeof(NotificationAudit).Assembly.GetTypes();
                for (int i = 0; i < types.Length; i++)
                {
                    FieldInfo[] fields = types[i].GetFields(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
                    );
                    for (int f = 0; f < fields.Length; f++)
                    {
                        FieldInfo field = fields[f];
                        if (!field.IsLiteral || field.FieldType != typeof(string))
                        {
                            continue;
                        }

                        string key = field.GetRawConstantValue() as string;
                        if (string.IsNullOrEmpty(key) || key[0] != '%')
                        {
                            continue;
                        }

                        // Per key: one that the live string table cannot answer must not cost the
                        // accounts every other key would have contributed, or the honesty check
                        // spends the session reporting words the game does draw.
                        try
                        {
                            string text = AgeText.Clean(Gui.Localize(key));
                            if (!string.IsNullOrEmpty(text) && text[0] != '%')
                            {
                                AddPhrase(phrases, text);
                            }
                        }
                        catch (Exception) { }
                    }
                }
            }
            catch (Exception e)
            {
                Core.Util.Log.Warn("notification parity: reading the game's captions threw: " + e);
            }
        }

        /// <summary>
        /// The game's own names for the keys, for the chord hints.
        ///
        /// A chord hint names its key with the game's key-name table
        /// (<c>ChordNames.KeyName</c>, "%KeyCode&lt;name&gt;") - the same borrowing as
        /// <see cref="GameCaptions"/>, but the key is built at runtime from the KeyCode, so the
        /// const scan cannot see it. Without these accounts every icon-only control with a chord in
        /// its name reads as "says what nothing draws: rightarrow".
        ///
        /// Only the names long enough to be phrases: the table also answers "A" and "F3", and a
        /// two-character account strikes across word boundaries - "f3" out of "1 of 3" leaves "1o"
        /// as an invented word - the same reason a single reduced letter is never an account
        /// (<see cref="AddPhrase"/>). A short-named key turning up in a popup's chord hint will
        /// report itself here, which is when it earns a targeted account.
        ///
        /// And no name with a DIGIT in it, whatever its length, for the same reason one letter longer:
        /// "F10" is three characters and strikes clean through "1 of 10", which is what every position
        /// phrase on a card with ten dossiers under it says. A digit-bearing name is the one shape that
        /// can bridge a figure and the word beside it, and the figures are exactly what the honesty
        /// rule already refuses to account for.
        /// </summary>
        private static void GameKeyNames(List<string> phrases)
        {
            try
            {
                Array keys = Enum.GetValues(typeof(UnityEngine.KeyCode));
                for (int i = 0; i < keys.Length; i++)
                {
                    string key = "%KeyCode" + keys.GetValue(i);
                    try
                    {
                        string text = AgeText.Clean(Gui.Localize(key));
                        string reduced = AuditModel.Reduce(text);
                        if (text != key && reduced.Length >= 3 && !HasDigit(reduced))
                        {
                            AddPhrase(phrases, text);
                        }
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception e)
            {
                Core.Util.Log.Warn("notification parity: reading the game's key names threw: " + e);
            }
        }

        private static bool HasDigit(string reduced)
        {
            for (int i = 0; i < reduced.Length; i++)
            {
                if (char.IsDigit(reduced[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<string> _vocabulary;

        /// <summary>Record a string as something a spoken line may be assembled out of - the whole of
        /// it, and each of its lines, since a tooltip is carried into the buffer a line at a time. A
        /// single reduced letter is not an account: it would strike a letter out of the middle of a
        /// word nothing draws.</summary>
        private static void AddPhrase(List<string> into, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            string whole = AuditModel.Reduce(text);
            if (whole.Length >= 2 && into.IndexOf(whole) < 0)
            {
                into.Add(whole);
            }

            IList<string> lines = AgeText.Lines(text);
            if (lines.Count < 2)
            {
                return;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                string line = AuditModel.Reduce(lines[i]);
                if (line.Length >= 2 && into.IndexOf(line) < 0)
                {
                    into.Add(line);
                }
            }
        }

        /// <summary>Whether a painted string says nothing a reader could owe: no letters and no
        /// digits, or a template the popup never filled in - "Research has been completed: {0}" is
        /// the skeleton showing through, and the family drops those on purpose.</summary>
        private static bool Hollow(string text)
        {
            if (AuditModel.Reduce(text).Length == 0)
            {
                return true;
            }

            int open = text.IndexOf('{');
            return open >= 0 && text.IndexOf('}', open) > open;
        }

        // ---- odds and ends ----

        private static bool WithinAny(AgeTransform widget, List<AgeTransform> ancestors)
        {
            for (int i = 0; i < ancestors.Count; i++)
            {
                if (AgeWidgets.Under(widget, ancestors[i]))
                {
                    return true;
                }
            }

            return false;
        }

        // ---- the answer as JSON ----

        private static string Write(Result result)
        {
            return DevJson.Write(json =>
            {
                json.WriteStartObject();
                json.WritePropertyName("window");
                json.WriteValue(result.Window);
                json.WritePropertyName("title");
                json.WriteValue(result.Title);
                json.WritePropertyName("clean");
                json.WriteValue(result.Breaches == 0);
                json.WritePropertyName("painted");
                json.WriteStartObject();
                json.WritePropertyName("texts");
                json.WriteValue(result.PaintedTexts);
                json.WritePropertyName("controls");
                json.WriteValue(result.PaintedControls);
                json.WritePropertyName("tooltips");
                json.WriteValue(result.PaintedTooltips);
                json.WriteEndObject();
                json.WritePropertyName("nodes");
                json.WriteValue(result.Nodes);
                AuditModel.WriteBreaches(json, "completeness", result.Completeness);
                AuditModel.WriteBreaches(json, "honesty", result.Honesty);
                AuditModel.WriteBreaches(json, "placement", result.Placement);
                AuditModel.WriteBreaches(json, "tooltips", result.Tooltips);
                AuditModel.WriteBreaches(json, "pairing", result.Pairing);
                AuditModel.WriteBreaches(json, "unlocatable", result.Unlocatable);
                json.WriteEndObject();
            });
        }

        // ---- the gate ----

        /// <summary>
        /// Whether the dev server is actually up, which is what the auto-check is for: a player has
        /// no use for a log line about a prefab, and the check is not free.
        ///
        /// The loader owns that answer and publishes it (<c>ModHost.DevServerUp</c>): the config
        /// setting and the environment override both land inside <c>DevServer.Start</c>, and what the
        /// host answers is where they end up. Read once, on arming; no host reads as OFF, because a dev
        /// feature that cannot prove it is wanted should not run.
        /// </summary>
        internal static bool DevServerUp()
        {
            ES2Access.Loader.ModHost host = ModEntry.Host;
            return host != null && host.DevServerUp;
        }
    }
}
