using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// Factories for the control descriptions screens hand to the graph builder. A screen says what
    /// a control is and how to work it; everything about how that reads aloud lives here, so two
    /// screens with a button announce it identically.
    ///
    /// Every piece of text is a delegate, resolved at speak time, never a captured string: a graph is
    /// rebuilt from live game state on every operation, and a control that cached its label would go
    /// on announcing the state the game was in when the screen was first built.
    /// </summary>
    public static class GraphNodes
    {
        /// <summary>The control's name - always the first part, so the path diff can tell when a
        /// container's label merely repeats the control inside it.</summary>
        public static NodeAnnouncement LabelPart(Func<string> label)
        {
            return new NodeAnnouncement(label, kind: AnnouncementKinds.Label);
        }

        /// <summary>Speaks only while the control is unavailable, and watched live so a control that
        /// becomes available under the cursor says so. The game's own reason for the refusal is not
        /// repeated here.</summary>
        public static NodeAnnouncement DisabledPart(Func<bool> enabled)
        {
            return new NodeAnnouncement(
                () =>
                    enabled == null || enabled()
                        ? null
                        : ModStrings.Get(ModStrings.NavDisabled),
                live: true,
                kind: AnnouncementKinds.Enabled
            );
        }

        /// <summary>
        /// The sentence the game gives for refusing, as an extra spoken part on a blocked control.
        ///
        /// A game writes the reason into the same tooltip as the description, so a control whose
        /// tooltip is ANNOUNCED already says it and repeating it here would say it twice. It is the
        /// control whose tooltip is only INDICATED - the renderer-assembled kind - that would
        /// otherwise announce "unavailable" and leave the player to open the review buffer to find
        /// out why. So this part speaks only in that case, and only while the control is refusing.
        ///
        /// Additive, never a suppressor: the mode-derived part still contributes, and the section it
        /// comes from still fills the buffer.
        /// </summary>
        public static NodeAnnouncement RefusalPart(
            AgeTooltip tooltip,
            Func<bool> enabled,
            Func<string> name = null
        )
        {
            if (tooltip == null)
            {
                return null;
            }

            AgeTooltip it = tooltip;
            Func<string> label = name;
            return new NodeAnnouncement(
                () =>
                {
                    try
                    {
                        if (enabled != null && enabled())
                        {
                            return null;
                        }

                        if (ModeFor(it) == TooltipMode.Announce)
                        {
                            return null;
                        }

                        string refusal = Refusal(it);
                        return Repeats(refusal, label) ? null : refusal;
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                },
                live: true,
                kind: AnnouncementKinds.Tooltip
            );
        }

        /// <summary>
        /// Append the game's own refusal sentence to a control, where there is one to say.
        ///
        /// The node's own name is read back out of the parts already declared rather than passed in, so
        /// that no screen can forget it - and it is needed, because a "reason" that only repeats the
        /// control's name is not a reason. That happens whenever the game's tooltip for a disabled
        /// control is a bare DESCRIPTION on one line: <see cref="RefusalText.Compose"/> reads a lone
        /// line as the whole of what the game said (it has nothing to trim a description away from), and
        /// a read-only ship-design module - whose tooltip content is just the module's name - then reads
        /// "⟨name⟩, unavailable, ⟨name⟩".
        ///
        /// Call this instead of adding <see cref="RefusalPart"/> by hand.
        /// </summary>
        public static void AddRefusal(NodeVtable vtable, AgeTooltip tooltip, Func<bool> enabled)
        {
            if (vtable == null || vtable.Announcements == null)
            {
                return;
            }

            NodeAnnouncement refusal = RefusalPart(tooltip, enabled, NamePart(vtable));
            if (refusal != null)
            {
                vtable.Announcements.Add(refusal);
            }
        }

        /// <summary>
        /// The same, for a control the game refuses WITHOUT writing the reason anywhere on it.
        ///
        /// A game usually explains a blocked control on the control's own tooltip, and
        /// <see cref="AddRefusal(NodeVtable, AgeTooltip, Func{bool})"/> reads it from there. Some do not:
        /// the diplomacy ring's empire sectors carry no tooltip at all (measured - zero
        /// <c>AgeTooltip</c> components on the sector), and the sentence the game would say lives only in
        /// its own localization file. So the caller supplies the sentence - the GAME's own string, resolved
        /// through <c>Gui.Localize</c>, never a phrase this mod invented for it - and it is spoken under
        /// exactly the same conditions: only while the control is refusing, and never when it only repeats
        /// the control's name.
        /// </summary>
        public static void AddRefusal(
            NodeVtable vtable,
            Func<string> reason,
            Func<bool> enabled
        )
        {
            if (vtable == null || vtable.Announcements == null || reason == null)
            {
                return;
            }

            Func<string> name = NamePart(vtable);
            vtable.Announcements.Add(
                new NodeAnnouncement(
                    () =>
                    {
                        try
                        {
                            if (enabled != null && enabled())
                            {
                                return null;
                            }

                            string said = reason();
                            return Repeats(said, name) ? null : said;
                        }
                        catch (Exception)
                        {
                            return null;
                        }
                    },
                    live: true,
                    kind: AnnouncementKinds.Tooltip
                )
            );
        }

        /// <summary>The control's own name, out of the parts it has already declared.</summary>
        private static Func<string> NamePart(NodeVtable vtable)
        {
            IList<NodeAnnouncement> parts = vtable.Announcements;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] != null && parts[i].Kind == AnnouncementKinds.Label)
                {
                    return parts[i].Text;
                }
            }

            return null;
        }

        private static bool Repeats(string refusal, Func<string> name)
        {
            if (string.IsNullOrEmpty(refusal) || name == null)
            {
                return false;
            }

            string label = name();
            return !string.IsNullOrEmpty(label)
                && string.Equals(label.Trim(), refusal.Trim(), StringComparison.Ordinal);
        }

        /// <summary>The refusal alone, out of the three parts the game assembles a blocked control's
        /// tooltip from: its own description, the failure, and - only ever for a missing technology -
        /// the sentence telling a mouse where to click.</summary>
        private static string Refusal(AgeTooltip tooltip)
        {
            string written = RefusalText.Compose(
                AgeText.ContentLines(tooltip),
                MouseInstruction()
            );
            return written ?? TargetRefusal(tooltip);
        }

        /// <summary>
        /// The refusal of a control whose tooltip the RENDERER assembles: there are no words in
        /// <c>Content</c> to trim, and the sentence only exists once the tooltip window has drawn its
        /// failure panel - which is a hover delay away, and gone the moment the pointer leaves.
        ///
        /// The panel reads it off the wrapper the game hangs on the tooltip
        /// (<c>PanelFeatureFailureInfos.Bind</c> is exactly this expression), and that wrapper is
        /// filled in at bind time, so asking it gives the same sentence the player would see, at once
        /// and without the tooltip being drawn at all.
        /// </summary>
        private static string TargetRefusal(AgeTooltip tooltip)
        {
            try
            {
                IFailureInfosProvider failures =
                    tooltip == null ? null : tooltip.Target as IFailureInfosProvider;
                return failures == null
                    ? null
                    : AgeText.Clean(Gui.FormatFailureInfos(failures.FailureInfos));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The sections for a control whose tooltip the game may have appended a MOUSE INSTRUCTION to -
        /// "hold Control and click to find the technology you are missing", which is the third part of
        /// a blocked button's hint and the one part a keyboard player can do nothing with.
        ///
        /// It stays in the review buffer, because it is on the screen and someone may want it; it is
        /// kept out of what is SPOKEN, because a refusal that ends in an instruction to click is a
        /// refusal the player has to listen past every time they pass the control. Which is the same
        /// split the announced and reviewed halves always have here - one declaration, two surfaces -
        /// so the instruction is simply declared as its own reviewed-only section, after the words that
        /// speak.
        ///
        /// A tooltip the renderer assembles has no such part and is left alone: it is only indicated,
        /// and <see cref="RefusalPart"/> is what carries its refusal into speech.
        ///
        /// Every control in the mod gets this, because <see cref="Sections"/> is what calls it: a screen
        /// that builds its own vtable rather than going through the shared cell helpers used to keep the
        /// instruction in its spoken refusal, and the Academy's blocked Sell button is how that was found.
        /// </summary>
        public static IList<NodeSection> HintSections(AgeTooltip tooltip)
        {
            NodeSection whole = TooltipSection(tooltip);
            if (whole == null || whole.Mode != TooltipMode.Announce)
            {
                return whole == null ? null : new List<NodeSection> { whole };
            }

            Func<IList<string>> full = TooltipDetails(tooltip);
            if (full == null)
            {
                return new List<NodeSection> { whole };
            }

            // Both halves name the SAME tooltip: this is one hover surface split by loudness, and the
            // builder's one-tooltip rule counts surfaces so that the split stays legal.
            AgeTooltip it = tooltip;
            return new List<NodeSection>
            {
                NodeSection.Derived(
                    () => Lines(full(), false),
                    TooltipMode.Announce,
                    null,
                    it,
                    TooltipCosts.Of(tooltip)
                ),
                NodeSection.Buffer(() => Lines(full(), true)),
            };
        }

        /// <summary>The tooltip's lines split at the mouse instruction: everything else, or the
        /// instruction on its own.</summary>
        private static IList<string> Lines(IList<string> lines, bool instructionOnly)
        {
            string instruction = MouseInstruction();
            List<string> kept = new List<string>(lines == null ? 0 : lines.Count);
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                if (string.Equals(lines[i], instruction) == instructionOnly)
                {
                    kept.Add(lines[i]);
                }
            }

            return kept;
        }

        private static string MouseInstruction()
        {
            return AgeText.Clean(Gui.Localize("%MissingTechnologyClickDescription"));
        }

        /// <summary>
        /// The control's review-buffer content, read from its tooltip - and, via
        /// <see cref="TooltipPart"/>, the same lines a <see cref="TooltipMode.Announce"/> control
        /// speaks and the same test a <see cref="TooltipMode.Indicate"/> control uses to decide
        /// whether it has anything to indicate.
        ///
        /// A tooltip that carries its own words in <c>Content</c> - the game has already put the
        /// description there, and for a control that is refusing, the reason it is refusing - reads
        /// straight off it. One that names a CLASS has nothing in <c>Content</c> worth reading (see
        /// <see cref="DrawnTooltip"/>'s own remarks on what that field holds instead), so it is read
        /// back off the tooltip window's live drawing, which is what makes it exist at all. Resolved
        /// at review time in both cases, so a refusing button's reason - or a resource's stat block
        /// after another turn has changed it - is the one it would give now.
        /// </summary>
        public static Func<IList<string>> TooltipDetails(AgeTooltip tooltip)
        {
            if (tooltip == null)
            {
                return null;
            }

            return AgeWidgets.TooltipLines(tooltip);
        }

        /// <summary>Whether a tooltip's words live in its <c>Content</c> field - the one rule, asked of
        /// <see cref="AgeWidgets.Readable"/>, which is also what reads the lines back.</summary>
        private static bool ContentBacked(AgeTooltip tooltip)
        {
            return AgeWidgets.Readable(tooltip) != null;
        }

        /// <summary>The same, for a control that carries its tooltip on its transform rather than
        /// naming it in a field.</summary>
        public static Func<IList<string>> TooltipDetails(AgeTransform transform)
        {
            return transform == null ? null : TooltipDetails(transform.AgeTooltip);
        }

        /// <summary>
        /// A widget's tooltip as a declared SECTION - the single place it is written down, from which
        /// the engine derives both what the focus readout says about it and what the review buffer
        /// holds. The mode is the TOOLTIP'S OWN (<see cref="ModeFor"/>) and there is no way to ask for
        /// another: whether a tooltip is a sentence to hear or a stat block to walk is a fact about
        /// the tooltip, and every call site that used to answer it for itself either repeated a line
        /// the label had already said (now dropped by the readout's own dedupe) or threw the rest of
        /// the tooltip away.
        ///
        /// Null when there is no tooltip, so a caller can hand the result straight to
        /// <see cref="Sections"/>.
        /// </summary>
        public static NodeSection TooltipSection(AgeTooltip tooltip)
        {
            return TooltipSection(tooltip, TooltipDetails(tooltip));
        }

        /// <summary>The same for a tooltip whose words the caller reads ITSELF - a dossier the game
        /// keeps two widgets for, a stat block the mod recomposes - where the tooltip is still what
        /// says how loudly they reach the player.</summary>
        public static NodeSection TooltipSection(AgeTooltip tooltip, Func<IList<string>> lines)
        {
            if (lines == null)
            {
                return null;
            }

            // Every indicated section that comes off a GAME widget carries the engine's own can-draw
            // test, so the pointer is never aimed and the parity audit never held to a tooltip the
            // game would draw nothing for - and both start counting it the frame the game fills one
            // in. Declared here, once, because this is the single door every screen's tooltips come
            // through.
            // And its PRICE, from the same one door and for the same reason: which tooltip classes
            // draw a cost line is the game's own table (<see cref="TooltipCosts"/>), so every control
            // pointing at such a class says what it will take without a screen having gone and got
            // the number - and the ones that hand-built one before this existed had each got a
            // different subset of what the panel draws.
            AgeTooltip it = tooltip;
            return NodeSection.Derived(
                lines,
                ModeFor(tooltip),
                () => AgeWidgets.Draws(it),
                it,
                TooltipCosts.Of(tooltip)
            );
        }

        /// <summary>
        /// LEAVE THE PRICE TO THE ROW, for the few controls that draw their own turn count.
        ///
        /// The construction queue's lines, the research queue's rows and the empire banner's research
        /// line each already say how long the thing they are about has left, in words the game itself
        /// puts on the row - and each also points at a tooltip whose cost panel says the same thing
        /// again. Hearing it twice on the one row is worse than hearing it once anywhere.
        ///
        /// The opt-out is here rather than at the three call sites so that it is one named decision
        /// with one reason, and so that a fourth row claiming it has to say the reason out loud.
        /// Everything else with a cost panel speaks.
        /// </summary>
        public static void TurnsDrawnOnTheRow(NodeVtable vtable)
        {
            IList<NodeSection> sections = vtable == null ? null : vtable.Sections;
            for (int i = 0; sections != null && i < sections.Count; i++)
            {
                if (sections[i] != null)
                {
                    sections[i].Cost = null;
                }
            }
        }

        /// <summary>
        /// A tooltip that hangs on this control but is not the one a hover on it would raise - the
        /// icon that captions a row whose value carries its own dossier, a badge beside the thing the
        /// row is about. Reviewable, never announced.
        ///
        /// Not a mode a caller chose: a control announces the ONE tooltip it points at, and which of
        /// several that is, is a fact about the row that only the row knows. Everything else it
        /// carries is still written down, in drawn order, so the buffer holds all of it.
        /// </summary>
        public static NodeSection ReviewedTooltipSection(AgeTooltip tooltip)
        {
            Func<IList<string>> lines = TooltipDetails(tooltip);
            return lines == null ? null : NodeSection.Buffer(lines);
        }

        /// <summary>The same for a control that carries its tooltip on its transform.</summary>
        public static NodeSection TooltipSection(AgeTransform transform)
        {
            return transform == null ? null : TooltipSection(transform.AgeTooltip);
        }

        /// <summary>
        /// The declared sections of a control, in the order they read: what the control DRAWS
        /// beyond its readout first, then its tooltip. Null when there is neither, which is a complete
        /// declaration - the buffer still has the control's own name and state.
        ///
        /// <paramref name="details"/> is content the player can SEE, so it is reviewable and never
        /// spoken - repeating it into the readout would read the screen back at them. For words the
        /// game HAS and draws NOWHERE, <see cref="SpokenSections"/> is the door that says them. The
        /// TOOLTIP's own loudness is never asked at either: that is the tooltip's own class to answer
        /// (<see cref="ModeFor"/>).
        ///
        /// This builds the REVIEWABLE half alone, because it has no vtable to aim: a screen composing
        /// sections by hand owes the node the other half too, and
        /// <see cref="SectionsFor(NodeVtable, AgeTooltip, Func{IList{string}}, AgeTransform)"/> is the
        /// same call with the vtable, making both. Every factory in this file goes through that one.
        /// </summary>
        public static IList<NodeSection> Sections(Func<IList<string>> details, AgeTooltip tooltip)
        {
            return Build(NodeSection.Buffer(details), tooltip);
        }

        /// <summary>The same for a control whose <paramref name="details"/> are words the game HAS and
        /// draws NOWHERE - the ground report's outcome sentence, which the row went and got out of the
        /// model. Handing those over is the whole reason the row went for them, so they are said as
        /// the control is read - alongside the tooltip's own words where its kind says those speak too,
        /// never instead of them. Never a tooltip's own lines: those answer for themselves.</summary>
        public static IList<NodeSection> SpokenSections(
            Func<IList<string>> details,
            AgeTooltip tooltip
        )
        {
            return Build(NodeSection.Composed(details), tooltip);
        }

        private static IList<NodeSection> Build(NodeSection drawn, AgeTooltip tooltip)
        {
            IList<NodeSection> tip = HintSections(tooltip);
            if (drawn == null && tip == null)
            {
                return null;
            }

            List<NodeSection> list = new List<NodeSection>(3);
            if (drawn != null)
            {
                list.Add(drawn);
            }

            for (int i = 0; tip != null && i < tip.Count; i++)
            {
                list.Add(tip[i]);
            }

            return list;
        }

        /// <summary>
        /// A control's ONE tooltip, declared and AIMED in one call - the sections it reviews and
        /// announces, plus the pointer that makes the game draw it.
        ///
        /// <b>A tooltip is two promises through two doors, and this door makes them together.</b>
        /// Declaring a section says the words are REVIEWABLE; moving the pointer
        /// (<see cref="NodeVtable.OnFocusVisual"/>, wired by <see cref="Aim"/>) is what makes the game
        /// RAISE its own tooltip, because the game draws tooltips on hover and nothing else. Wiring one
        /// alone is how a screen came to declare four tooltips that read correctly and never appeared,
        /// and how the load/save window's Steam-Cloud box read its state and drew nothing (both
        /// owner-reported 2026-08-28). Every factory below goes through the same call, so the gap is
        /// not reachable from any door in this file.
        ///
        /// SEVERAL tooltips on one line do not come here: they go through the nesting sink
        /// (<c>TooltipChildren.Split</c>), which answers with the one the line points at and turns
        /// every other one into a child entry of its own. This door cannot take a list, because a
        /// tooltip declared on a line the pointer never visits is a promise nothing can keep.
        ///
        /// <paramref name="anchor"/> is required for - and refused outside - the one tooltip shape that
        /// has no widget of its own; see <see cref="Aim"/>.
        /// </summary>
        public static IList<NodeSection> SectionsFor(
            NodeVtable vtable,
            AgeTooltip tooltip,
            Func<IList<string>> details = null,
            AgeTransform anchor = null
        )
        {
            Aim(vtable, tooltip, anchor);
            return Sections(details, tooltip);
        }

        /// <summary>
        /// THE RAISING HALF, on its own, for a node whose sections are already built - and the one
        /// place in the mod that decides what a declared tooltip is aimed at.
        ///
        /// The aim is the tooltip's OWN transform, always: the game draws a tooltip for the widget it
        /// hangs on and for no other, so pointing at the row that contains it draws nothing (measured
        /// repeatedly - a card's tooltip is rarely on the card). There is nothing here for a caller to
        /// choose; what a caller may have to supply is the one FACT the door cannot read off the
        /// tooltip:
        ///
        /// <paramref name="anchor"/> - the widget a tooltip with NO transform of its own is drawn
        /// under. The game keeps some of these on a FIELD of a window (the planet card's improvement
        /// box), filled at bind time, and nothing in the widget tree leads back to them. It is REQUIRED
        /// there and REFUSED everywhere else, with a throw, because an anchor passed for a tooltip that
        /// owns a transform is a second opinion about where the game draws - which is the misuse the
        /// old per-site pointing calls made possible.
        ///
        /// A tooltip that owns no transform and is given no anchor keeps the declaration and gets no
        /// pointer. That is the honest answer rather than an aim at nothing, and it is REPORTED: a log
        /// line here, and the audit's <c>unraised</c> bucket on the node itself. It does not throw
        /// because <c>AgeTransform</c> is Awake-cached and answers null on a prefab, and taking a whole
        /// page down over one silent tooltip trades a small defect for a big one.
        ///
        /// Composes with everything else hung on <see cref="NodeVtable.OnFocusVisual"/> the way the
        /// pointing helpers always have - last call wins - so a screen with a visual of its own sets it
        /// after the door, and a screen that wants the door's aim writes nothing at all.
        /// </summary>
        public static void Aim(NodeVtable vtable, AgeTooltip tooltip, AgeTransform anchor = null)
        {
            if (vtable == null)
            {
                return;
            }

            if (tooltip == null)
            {
                if (anchor != null)
                {
                    throw new ArgumentException(
                        "An anchor with no tooltip to draw under it",
                        "anchor"
                    );
                }

                return;
            }

            AgeTransform own = AgeWidgets.TooltipOwner(tooltip);
            if (own != null)
            {
                if (anchor != null)
                {
                    throw new ArgumentException(
                        "This tooltip hangs on a widget of its own - that widget is where the game "
                            + "draws it, and the door finds it. An anchor here can only aim elsewhere.",
                        "anchor"
                    );
                }

                AgeWidgets.PointAt(vtable, own);
                return;
            }

            if (anchor == null)
            {
                Log.Warn(
                    "nodes: a tooltip with no widget of its own was declared with no anchor - it "
                        + "will review and never draw"
                );
                AgeTooltip it = tooltip;
                vtable.PointsAt = () => it;
                return;
            }

            AgeWidgets.PointUnder(vtable, anchor, tooltip);
        }

        /// <summary>
        /// The sections a shared reading built, with its tooltip swapped for the ONE the screen knows
        /// the node really shows.
        ///
        /// For the caller that re-aims a node a generic reader has already declared: a hero row's name
        /// column carries the row's own tooltip AND the hero's dossier on the portrait inside it, and
        /// the screen points at the dossier because that is the page the player came for. Whatever the
        /// generic reading declared then becomes a second hover surface the node no longer points at -
        /// a buffer promise nothing will fill - so it goes, and only what the control DRAWS is kept.
        ///
        /// Not a way to choose a tooltip's loudness: the replacement reads by its own kind like every
        /// other, through <see cref="HintSections"/>.
        /// </summary>
        public static IList<NodeSection> OnlyTooltip(
            IList<NodeSection> sections,
            AgeTooltip tooltip
        )
        {
            List<NodeSection> kept = new List<NodeSection>(sections == null ? 2 : sections.Count + 1);
            for (int i = 0; sections != null && i < sections.Count; i++)
            {
                if (sections[i] != null && !sections[i].FromTooltip)
                {
                    kept.Add(sections[i]);
                }
            }

            IList<NodeSection> tip = HintSections(tooltip);
            for (int i = 0; tip != null && i < tip.Count; i++)
            {
                kept.Add(tip[i]);
            }

            return kept.Count == 0 ? null : kept;
        }

        /// <summary>The same, for a screen that has already built its sections (a row with a heading
        /// tooltip and a value tooltip). Nulls are dropped, so every caller can pass what it has.</summary>
        public static IList<NodeSection> Sections(params NodeSection[] sections)
        {
            List<NodeSection> list = null;
            for (int i = 0; sections != null && i < sections.Length; i++)
            {
                if (sections[i] == null)
                {
                    continue;
                }

                if (list == null)
                {
                    list = new List<NodeSection>(sections.Length);
                }

                list.Add(sections[i]);
            }

            return list;
        }

        /// <summary>
        /// The deterministic choice of how a tooltip reaches the player, read off the tooltip itself
        /// rather than decided per screen or by how long it happens to run.
        ///
        /// A tooltip that names a CLASS is assembled at draw time by the tooltip window from live
        /// data - a resource's stat block, a trait's dossier, a screen icon's shortcut-and-status
        /// panel - the kind of thing a player wants to walk at their own pace rather than have read
        /// out whole every time focus passes over it: <see cref="TooltipMode.Indicate"/>. One that
        /// carries its own words in <c>Content</c> is the single sentence the game wrote to explain
        /// the control - or, for a refusing button, that sentence with the reason appended - short
        /// enough that saying it outright is exactly what the control's own author intended:
        /// <see cref="TooltipMode.Announce"/>. No tooltip at all is <see cref="TooltipMode.None"/>.
        ///
        /// One rule, so a screen never has to guess which of the two a tooltip it is handed turns out
        /// to be; <see cref="DrawnTooltip"/> and the review buffer still carry a Class tooltip's full
        /// text regardless of which mode this returns.
        /// </summary>
        public static TooltipMode ModeFor(AgeTooltip tooltip)
        {
            try
            {
                if (tooltip == null)
                {
                    return TooltipMode.None;
                }

                return ContentBacked(tooltip) ? TooltipMode.Announce : TooltipMode.Indicate;
            }
            catch (Exception)
            {
                return TooltipMode.None;
            }
        }

        /// <summary>Speaks only while the control is the chosen one among its peers - the showing tab,
        /// the entry a list is currently set to. Saying nothing when unselected is load-bearing: it is
        /// what lets focus entering a stop land on the choice already in force rather than at the top
        /// of the list.</summary>
        public static NodeAnnouncement SelectedPart(Func<bool> selected)
        {
            return new NodeAnnouncement(
                () =>
                    selected != null && selected()
                        ? ModStrings.Get(ModStrings.NavSelected)
                        : null,
                live: true,
                kind: AnnouncementKinds.Selected
            );
        }

        /// <summary>What the control currently holds. Watched live by default, so a value the game
        /// changes on its own - a setting another control has just constrained, a volume the game
        /// clamped - speaks under the cursor without the whole control being re-read.
        ///
        /// A watched part is re-resolved every frame the control is focused, so a value whose answer
        /// costs a walk of one of the game's repositories asks for <c>watch: false</c> and is resolved
        /// when the control is read instead. The player hears the same words either way; what they lose
        /// is the value announcing itself mid-focus, and what they gain is the scan not running at
        /// 60 Hz.</summary>
        public static NodeAnnouncement ValuePart(Func<string> value, bool watch = true)
        {
            return new NodeAnnouncement(value, live: watch, kind: AnnouncementKinds.Value);
        }

        /// <summary>A control the player activates. An unavailable one stays focusable and readable -
        /// knowing that Join Game exists but is out of reach is the point - and simply swallows the
        /// activation.</summary>
        public static NodeVtable Button(
            Func<string> label,
            Action activate,
            Func<bool> enabled = null,
            AgeTooltip tooltip = null,
            Func<IList<string>> details = null
        )
        {
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = Parts(label, enabled),
                Sections = Sections(details, tooltip),
                OnActivate = Guarded(activate, enabled),
            };
            Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>
        /// A line the player reads but does not work: a name and a number.
        ///
        /// No role word - there is no control here to name, and "Empire Dust, 150, 38 per turn" is
        /// the whole of what the banner says. Its tooltip reads by its own kind like every other: these
        /// are in practice always the renderer-assembled sort, and leaving it to the rule means a
        /// readout whose tooltip the game ever authored as plain content is read the way plain content
        /// should be.
        ///
        /// <paramref name="watchValue"/> is the one thing a caller sometimes has to switch off: a
        /// watched value re-announces itself under the cursor whenever it changes, which is right for a
        /// number the game revises and wrong for one that revises itself every second - a running
        /// timer, a log's newest line - where the player would be talked over continuously. Such a
        /// readout is still current when read; it just stops speaking on its own.
        /// </summary>
        public static NodeVtable Readout(
            Func<string> label,
            Func<string> value,
            Func<IList<string>> details,
            AgeTooltip tooltip,
            bool watchValue = true
        )
        {
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    LabelPart(label),
                    ValuePart(value, watchValue),
                },
                Sections = Sections(details, tooltip),
            };
            Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>A setting the player turns on and off. Its state is both announced live - so a
        /// box the game ticks on the player's behalf says so - and spoken immediately after a
        /// toggle, which is what makes holding the key down readable.
        ///
        /// <paramref name="value"/> is a number the box itself DRAWS beside its tick - what an outpost
        /// action costs, or how many turns the running one has left - and reads before the state, in
        /// the order the box is read on screen.
        ///
        /// A box that is REFUSING says nothing at all: see <see cref="ActedState"/>.</summary>
        public static NodeVtable Checkbox(
            Func<string> label,
            Func<bool> state,
            Action toggle,
            Func<bool> enabled = null,
            AgeTooltip tooltip = null,
            Func<IList<string>> details = null,
            Func<string> value = null
        )
        {
            Func<string> stateText = () =>
                ModStrings.Get(
                    state != null && state() ? ModStrings.NavChecked : ModStrings.NavUnchecked
                );

            List<NodeAnnouncement> parts = Parts(label, enabled);
            if (value != null)
            {
                parts.Add(ValuePart(value));
            }

            parts.Add(ValuePart(stateText));
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Checkbox,
                Announcements = parts,
                Sections = Sections(details, tooltip),
                StateText = ActedState(stateText, enabled),
                OnActivate = Guarded(toggle, enabled),
            };
            Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>
        /// One of a set where exactly one is in force - the game's own select-then-confirm model,
        /// where picking is not yet doing.
        ///
        /// Only the chosen one says so, which is the same silence a tab bar keeps and is what lets
        /// focus entering the group land on the choice already made rather than at the top. Activating
        /// says "selected" at once, interrupting, because unlike a checkbox there is no other state
        /// the keypress could have produced and the player needs to hear that it took.
        /// </summary>
        public static NodeVtable Radio(
            Func<string> label,
            Func<bool> selected,
            Action choose,
            Func<bool> enabled = null,
            Func<IList<string>> details = null,
            AgeTooltip tooltip = null
        )
        {
            Func<string> chosen = () =>
                selected != null && selected() ? ModStrings.Get(ModStrings.NavSelected) : null;

            List<NodeAnnouncement> parts = Parts(label, enabled);
            parts.Insert(1, SelectedPart(selected));
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.RadioButton,
                Announcements = parts,
                Sections = Sections(details, tooltip),
                StateText = ActedState(chosen, enabled),
                OnActivate = Guarded(choose, enabled),
            };
            Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>
        /// One row of a list the game lets the player pick SEVERAL things out of - a fleet line, a ship
        /// tile - where a plain click still replaces the whole selection with this one.
        ///
        /// It is a <see cref="ControlTypes.RadioButton"/> because that is what the unmodified key does,
        /// and it is not a checkbox for the same reason: Enter cannot untick. What makes it different
        /// from <see cref="Radio"/> is that membership is the thing being read, so BOTH states are
        /// spoken - a row that says nothing when it is out of the selection leaves the player counting
        /// silences. The chords that put one row in or out (<see cref="NodeVtable.OnSelectToggle"/>) and
        /// that extend the selection to here (<see cref="NodeVtable.OnSelectRange"/>) are the screen's
        /// to wire, because only the screen knows what the game does with them.
        ///
        /// <paramref name="member"/> is asked live and so has to be a cheap state read; the screen
        /// hands <paramref name="settled"/> separately when the widget's own flag lags the model by a
        /// frame - what a row says AFTER an action has to be what the game now believes, not what the
        /// panel has not yet redrawn.
        /// </summary>
        public static NodeVtable SelectionItem(
            Func<string> label,
            Func<bool> member,
            Func<bool> settled,
            Action choose,
            Func<bool> enabled = null,
            AgeTooltip tooltip = null,
            Func<IList<string>> details = null
        )
        {
            Func<bool> acted = settled ?? member;
            List<NodeAnnouncement> parts = Parts(label, enabled);
            parts.Insert(
                1,
                new NodeAnnouncement(
                    () => SelectionText.Membership(member != null && member()),
                    live: true,
                    kind: AnnouncementKinds.Selected
                )
            );
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.RadioButton,
                Announcements = parts,
                Sections = Sections(details, tooltip),
                StateText = ActedState(
                    () => SelectionText.Membership(acted != null && acted()),
                    enabled
                ),
                OnActivate = Guarded(choose, enabled),
            };
            Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>
        /// Free text the player types into the game's own editor. Hand-built vtables are how a row
        /// comes to have no tooltip: everything else here routes its tooltip through
        /// <see cref="Parts"/> and its buffer through <see cref="TooltipDetails"/>, so an edit field
        /// that built its own parts list quietly had neither. It is a factory like the rest now.
        ///
        /// <paramref name="value"/> reports null while the game holds the keyboard - the screen reader
        /// is already echoing the keys, and re-reading the whole field after every letter buries them.
        /// </summary>
        public static NodeVtable EditField(
            Func<string> label,
            Func<string> value,
            Action edit,
            Func<bool> enabled = null,
            AgeTooltip tooltip = null,
            Func<IList<string>> details = null
        )
        {
            List<NodeAnnouncement> parts = Parts(label, enabled);
            parts.Add(ValuePart(value));
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.EditField,
                Announcements = parts,
                Sections = Sections(details, tooltip),
                OnActivate = Guarded(edit, enabled),
            };
            Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>A value the player moves along a range with left and right, and by a coarse step
        /// with the same arrows held with Shift. <paramref name="valueText"/> is already in the form the player
        /// should hear it - percent, decibels, a count - because only the screen knows what the
        /// number means.</summary>
        public static NodeVtable Slider(
            Func<string> label,
            Func<string> valueText,
            Action<int, bool> adjust,
            Func<bool> enabled = null,
            AgeTooltip tooltip = null,
            Func<IList<string>> details = null
        )
        {
            List<NodeAnnouncement> parts = Parts(label, enabled);
            parts.Add(ValuePart(valueText));
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Slider,
                Announcements = parts,
                Sections = Sections(details, tooltip),
                StateText = ActedState(valueText, enabled),
                // Declared even when the slider is refusing, so left and right stay the slider's
                // keys rather than quietly turning back into navigation on a control that looks
                // exactly like the one beside it.
                OnAdjust = (sign, large) =>
                {
                    if (enabled != null && !enabled())
                    {
                        return;
                    }

                    if (adjust != null)
                    {
                        adjust(sign, large);
                    }
                },
            };
            Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>A setting chosen from a list the control opens. Activating it is the screen's
        /// business - what the list is and how it is navigated belongs to whoever declared it.</summary>
        public static NodeVtable ComboBox(
            Func<string> label,
            Func<string> valueText,
            Action open,
            Func<bool> enabled = null,
            AgeTooltip tooltip = null,
            Func<IList<string>> details = null
        )
        {
            List<NodeAnnouncement> parts = Parts(label, enabled);
            parts.Add(ValuePart(valueText));
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.ComboBox,
                Announcements = parts,
                Sections = Sections(details, tooltip),
                StateText = ActedState(valueText, enabled),
                OnActivate = Guarded(open, enabled),
            };
            Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>One page of a screen. Only the showing tab says it is selected, and saying
        /// nothing is how the rest stay quiet - which is also what lets focus entering the tab bar
        /// land on the page the player is actually looking at rather than on the first tab.
        ///
        /// How a tab is switched to is the screen's business: set <c>OnActivate</c> on the returned
        /// vtable if the game needs a click, leave it unset for a bar that changes page on focus.</summary>
        public static NodeVtable Tab(
            Func<string> label,
            Func<bool> selected,
            Func<bool> enabled = null,
            AgeTooltip tooltip = null,
            Func<IList<string>> details = null
        )
        {
            List<NodeAnnouncement> parts = Parts(label, enabled);
            parts.Add(SelectedPart(selected));
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Tab,
                Announcements = parts,
                Sections = Sections(details, tooltip),
            };
            Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>One entry of a list the player has opened to pick from. It carries no role word:
        /// the control that opened the list has just been read as the combo box it is, and repeating
        /// "list item" on every entry of a twenty-line resolution list only slows the reading down.
        /// The entry the list is currently set to says so, which is also how focus lands on it.</summary>
        public static NodeVtable Choice(
            Func<string> label,
            Func<bool> selected,
            Action choose,
            Func<bool> enabled = null,
            Func<IList<string>> details = null,
            AgeTooltip tooltip = null
        )
        {
            List<NodeAnnouncement> parts = Parts(label, enabled);
            parts.Insert(1, SelectedPart(selected));
            NodeVtable vtable = new NodeVtable
            {
                Announcements = parts,
                Sections = Sections(details, tooltip),
                OnActivate = Guarded(choose, enabled),
            };
            Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>One action in a menu the player has opened. Unlike a value list's entry it names
        /// its kind, because the player has been taken somewhere and needs to hear that; and unlike a
        /// button, an entry that is not on offer is never declared at all - a menu is built from what
        /// can be done now, so there is nothing here to be unavailable.</summary>
        public static NodeVtable MenuItem(
            Func<string> label,
            Action invoke,
            Func<bool> selected = null,
            Func<IList<string>> details = null
        )
        {
            return new NodeVtable
            {
                ControlType = ControlTypes.MenuItem,
                Announcements = new List<NodeAnnouncement>
                {
                    LabelPart(label),
                    SelectedPart(selected),
                },
                Sections = Sections(details, null),
                OnActivate = invoke,
            };
        }

        // The other half of the swallow: what the control reports right after the player acted on it,
        // which for a refused action is nothing at all. Re-reading the state after a keypress that
        // changed nothing is heard as the keypress having worked ("not checked" from a box that would
        // not untick), and a refusal word here would be the second "unavailable" in a row - the
        // player heard the first on focus.
        private static Func<string> ActedState(Func<string> state, Func<bool> enabled)
        {
            if (state == null || enabled == null)
            {
                return state;
            }

            return () => enabled() ? state() : null;
        }

        // The swallow every unavailable control shares: it stays focusable and readable, and the
        // action goes nowhere.
        private static Action Guarded(Action action, Func<bool> enabled)
        {
            return () =>
            {
                if (enabled != null && !enabled())
                {
                    return;
                }

                if (action != null)
                {
                    action();
                }
            };
        }

        /// <summary>A container the player expands and collapses. Declare it with the builder's
        /// BeginGroup, which stamps the expanded state and parents the children onto it.</summary>
        public static NodeVtable Group(
            Func<string> label,
            Func<bool> enabled = null,
            AgeTooltip tooltip = null,
            Func<IList<string>> details = null
        )
        {
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Group,
                Announcements = Parts(label, enabled),
                Sections = Sections(details, tooltip),
            };
            Aim(vtable, tooltip);
            return vtable;
        }

        // The readout every control here is built from: what it is called and whether it is refusing.
        // What it has to SAY is not here - that is declared once, as sections, and the announcer
        // derives the tooltip part from them.
        private static List<NodeAnnouncement> Parts(Func<string> label, Func<bool> enabled)
        {
            return new List<NodeAnnouncement> { LabelPart(label), DisabledPart(enabled) };
        }
    }
}
