using System;
using System.Collections.Generic;
using Amplitude;
using Amplitude.Unity.Framework;
using Amplitude.Unity.Gui;
using ES2Access.Core.UI.Graph;
using ES2Access.Loader.Dev;
using ES2Access.Screens;
using Screen = ES2Access.Screens.Screen;
using ES2Access.UI;
using Newtonsoft.Json;
using UnityEngine;
using Breach = ES2Access.Dev.NotificationAudit.Breach;
using Declared = ES2Access.Dev.NotificationAudit.Declared;

namespace ES2Access.Dev
{
    /// <summary>
    /// The tooltip half of the notification family's self-check, asked of ANY screen: what the game
    /// would draw on hover against what the mod declares and points at.
    ///
    /// It exists because the notification audit found the same defect four times on four prefabs and
    /// no other screen had anything like it. Every tooltip defect this mod has shipped was one of
    /// three shapes, all of them invisible in a transcript, in a tree dump and in speech:
    ///
    /// <list type="number">
    /// <item><b>Promised and empty</b> - a node undertakes to have a dossier in its review buffer and
    /// nothing near it would draw one, so the player walks into an empty buffer.</item>
    /// <item><b>Mis-aimed</b> - the node declares a dossier and sends the pointer at a DIFFERENT
    /// tooltip, one the game draws nothing for; the words exist and are unreachable.</item>
    /// <item><b>Uncovered</b> - the game would draw a tooltip somewhere no node stands, or the node
    /// standing there says nothing the tooltip says.</item>
    /// </list>
    ///
    /// The two sides are re-derived from different evidence - the painted side is a walk of the
    /// screen's own widget tree and knows nothing of the mod's code - which is what makes agreement
    /// worth anything. Except for the AIM: that is asked of the node's own declaration
    /// (<see cref="NodeVtable.PointsAt"/>) and never re-derived, because the deepest tooltip inside a
    /// card is routinely decoration and a second opinion reports defects on screens that are right.
    ///
    /// Three widenings over the notification version, each of which was a blind spot there:
    /// tooltips on NON-control widgets are reported in their own lower-severity bucket rather than
    /// skipped (that is exactly the caption-on-an-icon class this mod now reads); a second pass runs
    /// with the transparency gate OFF and reports what it finds as <c>hidden</c> (a subtree the game
    /// reveals on hover carries real tooltips the painted walk cannot see); and a tooltip naming a
    /// class the game has no <c>GuiTooltipDescription</c> for is reported separately as what it is -
    /// a defect in the GAME, which will draw nothing there whatever the mod does.
    ///
    /// THE BUCKETS, which is what a reader of <c>/eval DevProbe.TooltipParity()</c> is looking at.
    /// Seven of them are the findings that count (<see cref="Result.Findings"/>): <c>promised</c> - a
    /// node claims a dossier nothing near it would draw; <c>misaimed</c> - it points at one that
    /// draws nothing, judged by <see cref="NodeVtable.PointsAt"/>; <c>unraised</c> - it points at one
    /// and never moves the pointer there, so the words review and the game draws nothing;
    /// <c>unaimed</c> - it declares a tooltip's words and aims at nothing at all, which is the shape
    /// that fell through every other bucket and let a screen answer <c>clean</c> with a live defect on
    /// it; <c>uncovered</c> - the game would
    /// draw a tooltip on a CONTROL that no node stands on; <c>unread</c> - a node covers it but
    /// carries none of its words; <c>misclassed</c> - the tooltip reaches the player the wrong way
    /// round for its kind, a content-backed one announcing nothing or a class-backed one announcing
    /// its words. The other four are context rather than defects:
    /// <c>decoration</c> - the same gap on a widget the player cannot work, a weaker claim because
    /// some of it is decoration nobody needs; <c>hidden</c> - seen only by the alpha-gate-off pass;
    /// <c>undescribed</c> - the GAME has no description for that tooltip class; and <c>unknown</c> -
    /// the node is read off something that is not an AGE widget at all (a place on the map), so
    /// there is no tree to ask. An unknown-aim node is JUDGED ON ITS DECLARED AIM where it has one
    /// and is NEVER counted a defect: every galaxy node used to land in <c>promised</c> for this
    /// reason alone, over dossiers that demonstrably draw.
    ///
    /// A node aimed at one of the mod's own tooltip carriers (<see cref="ES2Access.UI.ScratchTooltips"/>)
    /// is exempt from <c>promised</c>: a carrier is deliberately outside the widget tree, so no walk of
    /// the node's own widget can ever find it, and judging it there reported every carrier in the mod
    /// as an empty promise over a dossier that demonstrably draws (measured 2026-09-02 on the hero
    /// overview's skill children). The other buckets still judge it, because the carrier itself answers
    /// <see cref="AgeWidgets.Draws"/> like any other tooltip.
    ///
    /// The painted half needs <see cref="Screen.RootTransform"/>; with none, the check is
    /// declaration-side only.
    ///
    /// <b>A COLLAPSED branch reads as <c>unread</c></b> - the same blind spot
    /// <see cref="CoverageAudit"/> records for its own buckets. A group's child nodes are not
    /// declared while it is collapsed, so every dossier hanging under one looks like a tooltip no
    /// node carries. Expand the group and re-run before believing an <c>unread</c> finding: measured
    /// repeatedly on the hero card and on the ship-overview fact lines, where expanding cleared the
    /// finding and collapsing brought it straight back.
    ///
    /// <b>A NODE WITH NO TOOLTIP AT ALL IS OUTSIDE EVERY BUCKET</b>, and that is the blind spot this
    /// check answered <c>clean</c> through on 2026-08-29. All seven findings are about a node that
    /// PROMISES or CARRIES tooltip words - <c>unaimed</c> included, which is "declares a tooltip's
    /// words and aims at nothing". A node that declares no tooltip promises nothing, so nothing here
    /// looks at it; yet it still has a pointer to move, and one that moves nowhere leaves the game
    /// drawing whatever the player's real mouse is resting on. That is a live defect no bucket can
    /// show, and it is why the star-system page read <c>clean</c> with a focused control speaking one
    /// thing and the screen showing another's dossier. The mechanism is fixed at the source
    /// (<c>PointerFocus.HoverTarget</c> now aims a tooltip-less control at its own widget), so the
    /// shape should not recur - but the CHECK still cannot see it, and a run that says <c>clean</c>
    /// is saying nothing about any tooltip-less node on the screen. Catching it needs the live probe,
    /// not this audit: focus the node and read <c>DevProbe.Tooltip()</c>, which must answer the
    /// control's own tooltip or none - never another control's.
    ///
    /// Main-thread only, dev-only, and never on the player's path: nothing here speaks, focuses,
    /// moves the pointer or changes what the game is showing.
    /// </summary>
    internal static class TooltipAudit
    {
        /// <summary>How deep under the screen's root the painted walk looks, and how many widgets it
        /// will visit before answering anyway. A whole screen is a bigger tree than a popup.</summary>
        private const int MaxDepth = 24;

        private const int MaxWidgets = 12000;

        internal sealed class Result
        {
            public string Screen;
            public string ScreenName;
            public string Root;
            public int Nodes;

            /// <summary>The declaration side, kept so a caller checking a SECOND thing about the same
            /// screen (see <see cref="CoverageAudit"/>) does not rebuild it: composing every node's
            /// announcement and buffer is the expensive half of this check.</summary>
            public List<Declared> DeclaredNodes = new List<Declared>();

            public int PaintedTooltips;
            public int HiddenTooltips;

            /// <summary>Declares a dossier to review with nothing near it that would draw.</summary>
            public readonly List<Breach> Promised = new List<Breach>();

            /// <summary>Declares a dossier and points at a tooltip that draws nothing.</summary>
            public readonly List<Breach> Misaimed = new List<Breach>();

            /// <summary>
            /// Points at a tooltip and never moves the pointer to it - the node names which dossier it
            /// shows, and focusing it raises nothing, so the words are reviewable and the picture never
            /// appears.
            ///
            /// A tooltip is two promises through two doors (<see cref="GraphNodes.SectionsFor"/>):
            /// <see cref="NodeVtable.PointsAt"/> declares WHICH, and
            /// <see cref="NodeVtable.OnFocusVisual"/> is what makes the game draw it. Every other
            /// bucket here reads the declaration side; this one is the only one that asks whether the
            /// raising half was wired, which is why it is the only one that could have caught the
            /// hero page's four silent lines (owner-reported 2026-08-28).
            ///
            /// Read straight off the vtable, so it needs no drawing and no pointer moved to answer.
            /// </summary>
            public readonly List<Breach> Unraised = new List<Breach>();

            /// <summary>
            /// Declares a tooltip's words and aims at NOTHING - neither
            /// <see cref="NodeVtable.PointsAt"/> nor a focus visual that would move the pointer.
            ///
            /// The sibling of <see cref="Unraised"/>, and the one that could see what it could not: a
            /// node that never aimed has no aim for the aim buckets to judge and no promise for the
            /// raising bucket to check, so it fell through every finding here and read as a node with
            /// no tooltip at all. That is exactly how the load/save window's Steam-Cloud box shipped
            /// with a tooltip nothing drew while this check answered <c>clean:true, nodes:134</c>
            /// (owner-reported 2026-08-28).
            ///
            /// Unreachable through the door: every tooltip-taking factory and
            /// <see cref="GraphNodes.SectionsFor"/> aim as they declare (<see cref="GraphNodes.Aim"/>).
            /// What is left for this bucket to guard is the hand-built vtable - a screen composing
            /// sections itself - and the one case the door reports rather than fixes, a tooltip with no
            /// widget of its own declared with no anchor.
            ///
            /// The comparison is <see cref="TooltipAimRule.Unraisable"/>, proved off the engine.
            /// </summary>
            public readonly List<Breach> Unaimed = new List<Breach>();

            /// <summary>The game would draw a tooltip on a CONTROL here and no node covers it.
            /// </summary>
            public readonly List<Breach> Uncovered = new List<Breach>();

            /// <summary>The same on a widget the player cannot work - an icon captioning a value, a
            /// block around a row. Its own bucket because it is a weaker claim: some of these are
            /// decoration the game hangs a sentence on and nobody needs.</summary>
            public readonly List<Breach> Decoration = new List<Breach>();

            /// <summary>The tooltip's own words are not in anything the node covering it carries.
            /// </summary>
            public readonly List<Breach> Unread = new List<Breach>();

            /// <summary>Found only with the transparency gate off: a carrier inside a subtree the
            /// game reveals on hover. Reported apart because the painted side cannot see it and
            /// neither can a screenshot.</summary>
            public readonly List<Breach> Hidden = new List<Breach>();

            /// <summary>A tooltip naming a class the game has no description for. NOT a mod finding -
            /// the game draws nothing for it however it is pointed at.</summary>
            public readonly List<Breach> Undescribed = new List<Breach>();

            /// <summary>The check cannot answer for this node: what it is read off is not an AGE
            /// widget at all - a place on the map, a thing the renderer draws - so there is no widget
            /// tree to ask "would anything here draw". It is not a defect and must not be counted as
            /// one: every galaxy node landed in <see cref="Promised"/> for exactly this reason, and
            /// the map's dossiers demonstrably draw. A node like this that DOES declare an aim is
            /// still judged on the aim, and only one declaring neither ends up here.</summary>
            public readonly List<Breach> Unknown = new List<Breach>();

            /// <summary>
            /// THE CLASS BOUNDARY. A tooltip's KIND decides how it reaches the player and nothing else
            /// does (owner ruling 2026-08-28): one the game carries WORDS for is announced whole when
            /// focus lands; one the game ASSEMBLES on hover is buffer-only. The door derives both from
            /// the tooltip itself, so no screen can ask for the other - and this bucket is the reading
            /// back, on real nodes with real tooltips, of whether it came out that way.
            ///
            /// Two shapes, both reported here:
            /// <list type="bullet">
            /// <item>a node pointing at a CONTENT-backed tooltip whose readout has no tooltip part at
            /// all, with words of that tooltip the readout never says. The dedupe is allowed for: a
            /// line the LABEL speaks is in the readout, so the control named after its own tooltip's
            /// first line is not a finding, and one whose whole one-line tooltip is its name is not
            /// either.</item>
            /// <item>a node whose readout contains a line of a CLASS-backed tooltip - its own aim, or
            /// any buffer-only section it declares. Those words belong in the review buffer alone.</item>
            /// </list>
            ///
            /// What it cannot see, by the nature of the two kinds. A class-backed tooltip HAS no words
            /// until the tooltip window draws them (<c>DrawnTooltip</c>), so the leak half only ever
            /// answers for the node whose tooltip the game is drawing right now - focus the node,
            /// <c>TooltipDelay(0)</c>, then run. The content-backed half needs nothing drawn and
            /// answers for every node on the screen.
            /// </summary>
            public readonly List<Breach> Misclassed = new List<Breach>();

            public int Findings
            {
                get
                {
                    return Promised.Count
                        + Misaimed.Count
                        + Uncovered.Count
                        + Unread.Count
                        + Unraised.Count
                        + Unaimed.Count
                        + Misclassed.Count;
                }
            }
        }

        private sealed class Tip
        {
            public AgeTooltip Tooltip;
            public AgeTransform Owner;
            public bool Interactive;
        }

        /// <summary>The check, run against whichever of our screens is focused, as JSON.</summary>
        public static string Json()
        {
            try
            {
                Screen screen = TheScreen();
                if (screen == null)
                {
                    return DevJson.Error("no screen of ours is focused");
                }

                return Write(Check(screen, null, true));
            }
            catch (Exception e)
            {
                return DevJson.Error(e.Message);
            }
        }

        private static Screen TheScreen()
        {
            ScreenManager screens = ModEntry.Screens;
            return screens == null ? null : screens.Current;
        }

        /// <summary>
        /// The check itself, for a caller that wants the buckets rather than the JSON.
        ///
        /// <paramref name="roots"/> null means "ask the screen where it is drawn"
        /// (<see cref="Screen.RootTransform"/>), which is what <see cref="Json"/> does and what leaves
        /// half the check unrun on a screen that has no window of its own. A caller handing the trees
        /// in explicitly - the whole live GUI, for a screen whose content is the world - gets the
        /// painted half on a screen that could never have it, and must then pass
        /// <paramref name="ownOnly"/> false: the live tree holds the heads-up display too, and
        /// narrowing to the screen's own stops would throw away exactly the nodes that cover it.
        ///
        /// <paramref name="ownOnly"/> is answered structurally, off the STOP each node was declared
        /// into (<see cref="NotificationAudit.DeclaredNodes"/>), not off how its key is spelled: a
        /// screen that keys its frame one way and its body another - every notification popup - kept
        /// only its frame under the spelling test.
        /// </summary>
        internal static Result Check(Screen screen, IList<AgeTransform> roots, bool ownOnly)
        {
            Result result = new Result();
            result.Screen = screen.Key;
            result.ScreenName = Named(screen);

            List<Breach> unlocatable = new List<Breach>();
            List<Declared> declared = NotificationAudit.DeclaredNodes(screen, unlocatable, ownOnly);
            result.Nodes = declared.Count;
            result.DeclaredNodes = declared;

            CheckDeclarations(declared, result);

            List<AgeTransform> walked = new List<AgeTransform>();
            if (roots == null)
            {
                AgeTransform own = Root(screen);
                if (own != null)
                {
                    walked.Add(own);
                }
            }
            else
            {
                for (int i = 0; i < roots.Count; i++)
                {
                    if (roots[i] != null)
                    {
                        walked.Add(roots[i]);
                    }
                }
            }

            result.Root = Names(walked);
            if (walked.Count == 0)
            {
                // Half a check is worth having and worth SAYING: without a root there is no painted
                // side, so the coverage answers are absent rather than clean.
                return result;
            }

            List<Tip> painted = new List<Tip>();
            List<Tip> all = new List<Tip>();
            for (int i = 0; i < walked.Count; i++)
            {
                Walk(walked[i], painted, true, 0, new int[1]);
                Walk(walked[i], all, false, 0, new int[1]);
            }

            result.PaintedTooltips = painted.Count;
            CheckCoverage(painted, declared, result, false);

            List<Tip> hidden = Missing(all, painted);
            result.HiddenTooltips = hidden.Count;
            CheckCoverage(hidden, declared, result, true);
            return result;
        }

        private static string Names(List<AgeTransform> roots)
        {
            if (roots.Count == 0)
            {
                return null;
            }

            System.Text.StringBuilder text = new System.Text.StringBuilder();
            for (int i = 0; i < roots.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(", ");
                }

                text.Append(roots[i].name);
            }

            return text.ToString();
        }

        // ---- the declaration side ----

        /// <summary>
        /// THE RAISING HALF. A node that names which tooltip it shows and never moves the pointer to
        /// it is one whose words review correctly and whose picture never appears - the defect no
        /// other bucket here can see, because every other one reads the declaration side and this is
        /// the only question about the other door.
        ///
        /// Asked of the vtable alone: <see cref="NodeVtable.PointsAt"/> answering a tooltip while
        /// <see cref="NodeVtable.OnFocusVisual"/> is unset. That needs nothing drawn and no pointer
        /// moved, so it answers on an unfocused screen and on a node the player has never visited.
        ///
        /// A node that aims at NOTHING is not a finding: plenty of nodes carry no tooltip at all, and
        /// the tooltip-less half of the screen is not this check's business.
        /// </summary>
        private static void Unraised(Declared node, Result result)
        {
            try
            {
                NodeVtable vtable = node.Node == null ? null : node.Node.Vtable;
                if (
                    vtable == null
                    || vtable.OnFocusVisual != null
                    || AgeWidgets.AimOf(vtable) == null
                )
                {
                    return;
                }

                result.Unraised.Add(
                    NotificationAudit.Made(
                        node.Widget,
                        node.Key,
                        "points at a tooltip and never moves the pointer to it - it can be reviewed but the game will not draw it",
                        null
                    )
                );
            }
            catch (Exception)
            {
                // A vtable whose aim closure throws is the aim check's business, not this one's.
            }
        }

        /// <summary>
        /// THE OTHER HALF OF THE RAISING QUESTION: a node that wrote a tooltip's words down and aims at
        /// nothing at all. <see cref="Result.Unaimed"/> says what it is and why nothing else here could
        /// see it.
        ///
        /// Asked of the vtable alone, like <see cref="Unraised"/>, and of the SECTIONS rather than of a
        /// tooltip found by re-walking the tree: the question is what the node undertook to hold, and
        /// only its own declaration answers that.
        /// </summary>
        private static void Unaimed(Declared node, Result result)
        {
            try
            {
                NodeVtable vtable = node.Node == null ? null : node.Node.Vtable;
                if (
                    vtable == null
                    || !TooltipAimRule.Unraisable(
                        vtable.Sections,
                        AgeWidgets.AimOf(vtable) != null,
                        vtable.OnFocusVisual != null
                    )
                )
                {
                    return;
                }

                result.Unaimed.Add(
                    NotificationAudit.Made(
                        node.Widget,
                        node.Key,
                        "declares a tooltip's words and aims at nothing - nothing moves the pointer there, so the buffer promises what the game will never draw",
                        null
                    )
                );
            }
            catch (Exception)
            {
                // A vtable whose aim closure throws is the aim check's business, not this one's.
            }
        }

        /// <summary>
        /// THE CLASS BOUNDARY, read back off a real node: <see cref="Result.Misclassed"/> says what it
        /// is and what it cannot see.
        ///
        /// Both halves are asked of the node's AIM - the tooltip it declares it shows
        /// (<see cref="NodeVtable.PointsAt"/>), the same authority every other bucket here judges by -
        /// and of the sections it declares, never of a tooltip found by re-walking the tree: the
        /// deepest tooltip inside a card is routinely decoration, and a second opinion about which
        /// tooltip is the node's reports defects on screens that are right.
        ///
        /// The content-backed half fires only where the readout has NO tooltip part at all
        /// (<see cref="TooltipParts.Part"/> answering null over the node's own sections). That is the
        /// shape the ruling outlawed - a tooltip the game wrote a sentence for, declared buffer-only -
        /// and it keeps a node whose part exists but reads its words back some other way (a dossier the
        /// mod recomposes, a stat block it re-words) out of the bucket, where a line-by-line comparison
        /// would file every one of them.
        /// </summary>
        private static void Misclassed(Declared node, Result result)
        {
            try
            {
                NodeVtable vtable = node.Node == null ? null : node.Node.Vtable;
                string readout = node.Announcement;
                if (vtable == null || string.IsNullOrEmpty(readout))
                {
                    return;
                }

                IList<NodeSection> sections = vtable.Sections;
                AgeTooltip aim = AgeWidgets.AimOf(vtable);
                AgeTooltip written = AgeWidgets.Readable(aim);
                if (written != null && TooltipParts.Part(sections) == null)
                {
                    string missing = TooltipKindRule.Unspoken(
                        AgeText.Lines(AgeText.Tooltip(written)),
                        readout
                    );
                    if (missing != null)
                    {
                        result.Misclassed.Add(
                            NotificationAudit.Made(
                                node.Widget,
                                node.Key,
                                "points at a tooltip the game wrote words for and announces none of it - a content-backed tooltip is announced whole",
                                NotificationAudit.Excerpt(missing)
                            )
                        );
                        return;
                    }
                }

                string leaked = Leaked(node, sections, aim, written, readout);
                if (leaked != null)
                {
                    result.Misclassed.Add(
                        NotificationAudit.Made(
                            node.Widget,
                            node.Key,
                            "says the words of a tooltip the game assembles on hover - a class-backed tooltip is reviewed, never announced",
                            NotificationAudit.Excerpt(leaked)
                        )
                    );
                }
            }
            catch (Exception)
            {
                // A closure that throws while being read is the declaration side's own business: it is
                // already reported as unlocatable, and a check that rethrew would take the whole run
                // down over one bad node.
            }
        }

        /// <summary>The first line of a class-backed tooltip this node's readout is saying anyway: its
        /// own aim where the game assembles that one, and every buffer-only section it declares -
        /// which is where a badge's dossier would show up, the row having pointed elsewhere. Answers
        /// only while the tooltip window is drawing those words; before that they do not exist to
        /// compare.</summary>
        private static string Leaked(
            Declared node,
            IList<NodeSection> sections,
            AgeTooltip aim,
            AgeTooltip written,
            string readout
        )
        {
            List<string> otherwise = Otherwise(node.Node);
            if (written == null && AgeWidgets.Draws(aim))
            {
                string leak = TooltipKindRule.Leaked(
                    AgeWidgets.TooltipLines(aim)(),
                    readout,
                    otherwise
                );
                if (leak != null)
                {
                    return leak;
                }
            }

            for (int i = 0; sections != null && i < sections.Count; i++)
            {
                NodeSection section = sections[i];
                if (
                    section == null
                    || section.Lines == null
                    || section.Mode != TooltipMode.Indicate
                )
                {
                    continue;
                }

                string leak = TooltipKindRule.Leaked(section.Lines(), readout, otherwise);
                if (leak != null)
                {
                    return leak;
                }
            }

            return null;
        }

        /// <summary>What the node's readout says APART from its tooltip part - the label, the role
        /// word, the state. A tooltip line equal to one of these is that part's own words, not a
        /// tooltip getting into the readout.</summary>
        private static List<string> Otherwise(GraphNode node)
        {
            List<string> texts = new List<string>();
            List<NodeAnnouncement> parts = GraphAnnouncer.EffectiveAnnouncements(node);
            for (int i = 0; i < parts.Count; i++)
            {
                NodeAnnouncement part = parts[i];
                if (part == null || part.Text == null || part.Kind == AnnouncementKinds.Tooltip)
                {
                    continue;
                }

                try
                {
                    texts.Add(part.Text());
                }
                catch (Exception)
                {
                    // An unreadable part says nothing, which is exactly how it is treated here.
                }
            }

            return texts;
        }

        private static void CheckDeclarations(List<Declared> declared, Result result)
        {
            for (int i = 0; i < declared.Count; i++)
            {
                Declared node = declared[i];
                Unraised(node, result);
                Unaimed(node, result);
                Misclassed(node, result);
                if (!NotificationAudit.Promises(node))
                {
                    continue;
                }

                AgeTooltip aimed = NotificationAudit.AimOf(node);
                if (node.Widget == null)
                {
                    // Nothing to walk: this node is read off a place on the map rather than off a
                    // widget. Its own declared AIM is the only thing that can be judged, and where it
                    // has none the honest answer is "cannot tell" - not "promises nothing".
                    if (aimed == null)
                    {
                        result.Unknown.Add(
                            NotificationAudit.Made(
                                null,
                                node.Key,
                                "declares a tooltip on something that is not a widget - cannot tell",
                                null
                            )
                        );
                        continue;
                    }
                }
                else if (!NotificationAudit.AnyDrawing(node.Widget) && !ScratchTooltips.Owns(aimed))
                {
                    // A carrier is never in the widget tree - that is the whole of how it works
                    // (<see cref="ScratchTooltips"/>: parented under a GameObject with no
                    // AgeTransform so nothing lays it out or draws it) - so the walk of the node's
                    // own widget cannot find it however well it draws. A node aimed at one has a
                    // dossier that demonstrably appears; judging it by what hangs on the widget
                    // beneath would report every carrier the mod owns as an empty promise.
                    result.Promised.Add(
                        NotificationAudit.Made(
                            node.Widget,
                            node.Key,
                            "declares a tooltip to review with nothing that draws",
                            null
                        )
                    );
                    continue;
                }

                if (aimed != null && !AgeWidgets.Draws(aimed))
                {
                    result.Misaimed.Add(
                        NotificationAudit.Made(
                            node.Widget,
                            node.Key,
                            "declares a tooltip to review and points at one that draws nothing",
                            Describes(aimed) ? null : "class '" + Class(aimed) + "' has no description"
                        )
                    );
                }
            }
        }

        // ---- the painted side ----

        private static void CheckCoverage(
            List<Tip> painted,
            List<Declared> declared,
            Result result,
            bool hidden
        )
        {
            for (int i = 0; i < painted.Count; i++)
            {
                Tip tip = painted[i];
                if (!Describes(tip.Tooltip))
                {
                    result.Undescribed.Add(
                        NotificationAudit.Made(
                            tip.Owner,
                            NotificationAudit.Name(tip.Owner),
                            "the game has no description for tooltip class '"
                                + Class(tip.Tooltip)
                                + "'",
                            NotificationAudit.Excerpt(AgeText.Tooltip(tip.Tooltip))
                        )
                    );
                    continue;
                }

                List<Declared> covering = NotificationAudit.Covering(
                    declared,
                    tip.Owner,
                    tip.Tooltip
                );
                if (covering.Count == 0)
                {
                    if (DeliberatelyUndeclared(tip.Owner))
                    {
                        continue;
                    }

                    Breach breach = NotificationAudit.Made(
                        tip.Owner,
                        NotificationAudit.Name(tip.Owner),
                        "the game would draw a tooltip here and no node covers it",
                        NotificationAudit.Excerpt(AgeText.Tooltip(tip.Tooltip))
                    );
                    (
                        hidden ? result.Hidden
                        : tip.Interactive ? result.Uncovered
                        : result.Decoration
                    ).Add(breach);
                    continue;
                }

                if (hidden)
                {
                    // A hidden carrier that IS covered says nothing: whether its words are carried
                    // depends on the game having revealed it, which it has not.
                    continue;
                }

                // Words to look for only where the words are ON the tooltip. A renderer-assembled one
                // keeps a data key in the same field, and that key is what the renderer looks its
                // dossier up by - never anything the game draws or the mod says. Asked through the
                // same helper the reader picks its mode with.
                string content = AgeText.Tooltip(AgeWidgets.Readable(tip.Tooltip));
                if (string.IsNullOrEmpty(content))
                {
                    continue;
                }

                if (!NotificationAudit.CarriedBy(covering, content))
                {
                    result.Unread.Add(
                        NotificationAudit.Made(
                            tip.Owner,
                            NotificationAudit.Name(tip.Owner) + " (" + covering[0].Key + ")",
                            "the tooltip's words are not in what that node carries",
                            NotificationAudit.Excerpt(content)
                        )
                    );
                }
            }
        }

        /// <summary>
        /// A tooltip the mod has DECIDED not to declare, which must not be reported as a gap.
        ///
        /// One family so far: the five FIDSI figures on the galaxy map's orbital planet cards - both
        /// strips, the <c>FidsiEnumerator</c>'s duplets for a colony and the <c>FidsiScoreTable</c>'s
        /// pips for a world nobody has settled. Their dossiers explain what FOOD, INDUSTRY, DUST,
        /// SCIENCE and INFLUENCE ARE: the same five paragraphs on every world in the galaxy, and the
        /// star system's management card already declares them (<c>SystemManagementScreen.PlanetDossiers</c>).
        /// Owner ruling 2026-08-24 - they were declared on the map for one batch and taken off again,
        /// and this comment is the record of why, because the audit would otherwise report five
        /// findings per visible planet on the map forever and the next reader would "fix" them back.
        /// The figures themselves are still drawn and still read: this is about the hover pages
        /// behind them, nothing else.
        ///
        /// Judged only where NO node covers the tooltip, so it can never hide a real defect - a
        /// mis-aimed or promised-and-empty node on the same card is reported exactly as before.
        /// </summary>
        private static bool DeliberatelyUndeclared(AgeTransform widget)
        {
            try
            {
                PlanetLabel_SystemOrbital card =
                    widget == null ? null : widget.GetComponentInParent<PlanetLabel_SystemOrbital>();
                if (card == null)
                {
                    return false;
                }

                AgeTransform duplets =
                    card.FidsiEnumerator == null ? null : card.FidsiEnumerator.FidsiGroup;
                return (duplets != null && NotificationAudit.Under(widget, duplets))
                    || (card.FidsiScoreTable != null
                        && NotificationAudit.Under(widget, card.FidsiScoreTable));
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Every tooltip the screen would draw, from its own tree.
        ///
        /// <paramref name="drawnOnly"/> is the transparency gate the engine's own draw makes - a
        /// pooled row retired by alpha keeps its words and its rectangle, so a walk that ignored it
        /// would report the dead rows of every table. Turning it OFF is the second pass: the
        /// difference between the two is the set of carriers inside subtrees the game reveals on
        /// hover, which no screenshot and no ordinary walk can see.
        /// </summary>
        private static void Walk(
            AgeTransform widget,
            List<Tip> into,
            bool drawnOnly,
            int depth,
            int[] budget
        )
        {
            if (widget == null || depth > MaxDepth || budget[0]++ > MaxWidgets)
            {
                return;
            }

            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            if (AgeWidgets.Draws(tooltip))
            {
                Tip tip = new Tip();
                tip.Tooltip = tooltip;
                tip.Owner = widget;
                tip.Interactive = AgeWidgets.Control(widget) != null;
                into.Add(tip);
            }

            List<AgeTransform> children = widget.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child == null || !child.Visible)
                {
                    continue;
                }

                if (drawnOnly && !AgeWidgets.Paints(child))
                {
                    continue;
                }

                Walk(child, into, drawnOnly, depth + 1, budget);
            }
        }

        private static List<Tip> Missing(List<Tip> all, List<Tip> painted)
        {
            List<Tip> missing = new List<Tip>();
            for (int i = 0; i < all.Count; i++)
            {
                bool seen = false;
                for (int j = 0; j < painted.Count && !seen; j++)
                {
                    seen = ReferenceEquals(all[i].Owner, painted[j].Owner);
                }

                if (!seen)
                {
                    missing.Add(all[i]);
                }
            }

            return missing;
        }

        // ---- the game's own side ----

        private static string Class(AgeTooltip tooltip)
        {
            try
            {
                string cls = tooltip == null ? null : tooltip.Class;
                return string.IsNullOrEmpty(cls) ? "Simple" : cls;
            }
            catch (Exception)
            {
                return "Simple";
            }
        }

        /// <summary>Whether the GAME has anything to draw this tooltip's class with
        /// (<c>GuiTooltipController.ReadTooltipInformation</c> looks the class up in the
        /// <c>GuiTooltipDescription</c> database and gives up when it is not there). A tooltip that
        /// fails this is a defect in the game's own data - one prefab writes a SENTENCE into the class
        /// field - and no amount of pointing at it will make it draw, so it is reported as its own
        /// thing and never as a mod finding.</summary>
        private static bool Describes(AgeTooltip tooltip)
        {
            try
            {
                IDatabase<GuiTooltipDescription> database =
                    Databases.GetDatabase<GuiTooltipDescription>();
                return database == null
                    || database.GetValue(new StaticString(Class(tooltip))) != null;
            }
            catch (Exception)
            {
                return true;
            }
        }

        private static AgeTransform Root(Screen screen)
        {
            try
            {
                return screen.RootTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Named(Screen screen)
        {
            try
            {
                return screen.ScreenName;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Write(Result result)
        {
            return DevJson.Write(json =>
            {
                json.WriteStartObject();
                json.WritePropertyName("screen");
                json.WriteValue(result.Screen);
                json.WritePropertyName("title");
                json.WriteValue(result.ScreenName);
                json.WritePropertyName("root");
                json.WriteValue(result.Root);
                json.WritePropertyName("clean");
                json.WriteValue(result.Findings == 0);
                json.WritePropertyName("nodes");
                json.WriteValue(result.Nodes);
                json.WritePropertyName("paintedTooltips");
                json.WriteValue(result.PaintedTooltips);
                json.WritePropertyName("hiddenTooltips");
                json.WriteValue(result.HiddenTooltips);
                NotificationAudit.WriteBreaches(json, "promised", result.Promised);
                NotificationAudit.WriteBreaches(json, "misaimed", result.Misaimed);
                NotificationAudit.WriteBreaches(json, "unraised", result.Unraised);
                NotificationAudit.WriteBreaches(json, "unaimed", result.Unaimed);
                NotificationAudit.WriteBreaches(json, "uncovered", result.Uncovered);
                NotificationAudit.WriteBreaches(json, "decoration", result.Decoration);
                NotificationAudit.WriteBreaches(json, "unread", result.Unread);
                NotificationAudit.WriteBreaches(json, "misclassed", result.Misclassed);
                NotificationAudit.WriteBreaches(json, "hidden", result.Hidden);
                NotificationAudit.WriteBreaches(json, "undescribed", result.Undescribed);
                NotificationAudit.WriteBreaches(json, "unknown", result.Unknown);
                json.WriteEndObject();
            });
        }
    }
}
