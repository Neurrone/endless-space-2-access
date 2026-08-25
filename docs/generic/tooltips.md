# Tooltips

How tooltip content reaches a screen reader user. The right design depends on what the
game's tooltip system can actually express — research that first; the strategy follows.

## First: research what the game's tooltips can do

Answer these from the decompile before designing anything (ES2 answers in parentheses as the
worked example — method and full findings in the game-specific `es2-gui-framework.md`):

1. **When does tooltip content exist?** At widget-bind time in a readable field, or only
   composed at render time? (ES2: a `Content` string at bind time — readable without ever
   hovering — plus, for rich tooltips, `Target`/`Context` objects whose provider interfaces
   are also populated at bind time and readable headlessly.)
2. **Is the content string the whole tooltip?** (ES2: two tiers. "Simple" tooltips — one
   string, complete. "Rich" tooltips — the string is empty or a bare title, and the real
   substance is generated at display time by ~117 panel-feature classes reading provider
   interfaces; a content-string reader would miss essentially everything.)
3. **Can tooltips link to other tooltips** — a glossary/encyclopedia graph? (ES2: no. No
   hyperlink machinery at all; bracket markup is icon substitution only.)
4. **Can elements inside a tooltip have their own tooltips, or be clicked?** (ES2: a few
   widgets inside rich tooltips carry nested tooltips shown by hover *replacement* — extra
   data, not a navigation model — though a feature may still hold a control: ES2 rents a
   named ship from inside a tooltip.)
5. **Does the game append state into tooltip text** — "why disabled" hints? (ES2: yes,
   `FormatButtonHint` appends the reason into `Content`; always read tooltips live.)
6. **Where do tooltips render** — anchored to the widget, or at the cursor? Cursor-anchored
   tooltips shown for keyboard focus land wherever the idle mouse is parked; re-anchor them
   to the focused element while displaying (see the focus-visuals section of
   [ui-navigation.md](ui-navigation.md)).

## Then: pick the surfacing strategy

The choice in this section is the owner's taste call, reversible in a sentence; the rules
after it (the aim, the can-draw gate, the buffer fill) are correctness and are not. Keep
the two apart when writing them down — an earlier revision of this document interleaved
them, and one reversed taste call left five paragraphs stale at once.

Three proven shapes:

1. **Announce inline** — the tooltip text joins the focus announcement as a trailing part
   (its own announcement *kind*, so per-type filtering comes free once announcement settings
   exist; placed after the state words, before the position). For **short** tooltips — the
   one-sentence description the game's author wrote to be read whole, including any appended
   disabled reason. Resolve at speak time so those reasons arrive automatically.
2. **Buffer-only (Indicate mode)** — the announcement says nothing about it; the full
   content sits in the review buffer ([buffers.md](buffers.md)) for line-by-line reading at
   the player's own pace, and the convention — stated once in the mod's documentation, not
   per control — is that the player always checks the buffer. For **long** tooltips — stat
   panels, dossiers, anything render-composed. (This mod originally spoke a "has tooltip"
   indicator here; the owner later removed it, because on content-dense screens it was true
   of most controls and a near-constant cue carries no information.) In **both** modes the
   full content also populates the buffer, so review behaves identically everywhere. The
   mode stays distinct from plain drawn content even with no spoken output: it is what
   tells the pointer-aim and the parity audit that a hover-drawn tooltip is expected here —
   a surfacing mode earns its existence from every consumer, not only from what it speaks.
3. **A navigable tooltip reader** (wotr-access) — a modal child screen the user arrows
   through, with drill-in. This exists **only** because that game's tooltips form a link
   graph (glossary terms leading to further tooltips); the reader is how you follow links.
   If your research at step 3 above found no links, do not build this — the buffer is
   strictly simpler and reads the same content.

**Short-vs-long is decided by a deterministic game-side marker, found once — not by a length
heuristic in code and not per-declaration by hand.** (An earlier revision of this doc made it
a per-declaration human call; that shipped a screen's worth of inconsistent modes and was
replaced.) Every game that has both kinds distinguishes them somewhere, because its own
renderer must: find the property that separates "renders the content string as-is" from
"assembles a panel at display time" (ES2/AGE: the tooltip's class field — empty or the
plain-text class means the content string *is* the tooltip → announce; any other class means
renderer-assembled → indicate). One shared helper reads the marker and picks the mode;
screens never choose. The human judgment moves up a level — approving the *rule* — and
every future screen inherits it.

Rules that came out of shipping this, all hit in practice:

- **Never gate the mode's machinery on rendered content existing.** A render-composed
  tooltip's words do not exist until the game draws them — a hover-delay *after* focus
  arrives — so any per-readout "only if the lines are non-empty" test answers "empty" every
  single time. For a renderer-assembled tooltip, having content is definitional: treat it
  as expected unconditionally — but only where the engine's own can-draw test accepts it
  (its two-field content/target check); prefabs routinely hang class-backed tooltips with
  no target on decoration, and treating one as expected promises a review buffer that is
  always empty.
- **Reading a render-composed tooltip back**: where provider interfaces are readable
  headlessly, prefer them — and the REFUSAL is the part that nearly always is: the failure
  feature is typically one call over a provider on the tooltip's target, populated at bind
  time, so a blocked control's reason can speak instantly with no hover, and the drawn
  failure panel is the free oracle proving your expression matches it. Where they are not, make focus trigger the game's own tooltip
  display (see focus visuals in [ui-navigation.md](ui-navigation.md)) and read the drawn
  window's labels — what is drawn is exactly what should be spoken.
  A hover tooltip system is edge-triggered: it asks once per hover change and may park a
  failed request forever. A mod that holds a hover steady for keyboard focus must own the
  retry — re-issue the engine's own re-ask signal when nothing has drawn past the delay,
  budgeted per stall with the budget re-arming only on a successful draw — and must never
  aim at a tooltip the engine's own can-I-draw test would refuse, re-checked every frame
  so a widget the game fills late starts drawing without a refocus. When verifying, probe
  the REQUEST's state, not only the drawn window — "not drawn yet" and "never will be"
  look identical in speech, dump, and buffer.
  **Find the assembly unit first and scope the reading to it.** A tooltip system that
  composes at render time composes from an ordered list of typed sub-panels (ES2: panel
  features under one table). Reading by geometry *across* those units is what divorces
  values from their captions and fuses adjacent blocks into nonsense lines. Read within
  each unit, and give typed readers to the few structures geometry gets wrong — a
  repeated-item grid pairs the Nth title with the Nth value by sibling index (its drawn
  layout puts all titles above all values, so no row-banding can pair them), and collapsing
  one of its items into a single spoken line is gated on that item being DRAWN as one line —
  a repeated-prefab signal means "same shape, N times", never "small", so an item the prefab
  draws over several rows reads as those rows; a textless gauge reads as its drawn
  proportion. Two safety nets make a hundred-class surface
  tractable: unknown unit types read through the generic scoped reader, and a dev probe
  names which reader answered each unit, with the FALLBACK reader registering every class it
  answered for (judging a feature from source over-reports — prefab captions are invisible
  there) — coverage gaps surface in tooling, not in speech.
  Remaining traps, all hit in practice: substitute icon names on the string the game
  AUTHORED, never the one the widget rendered — an engine's localizer/markup pass can
  consume inline icon tokens (turning them into glyphs or nothing) before any reader sees
  them, and the loss is silent because every remaining word still reads; check *which*
  tooltip the window is currently bound to before reading; the
  window pools its child widgets, so a shrunk tooltip still carries the previous one's
  labels (use the engine's own effective-visibility test); join labels drawn on one visual
  line into one spoken line, in x-order, with the label's own alignment as the tiebreak when
  caption and value occupy the same rect; and refill the review buffer when the drawn
  tooltip lands (an invalidation hook), guarded by a lines-equality check so a refill with
  identical content never resets the player's reading position.
- **A cheap lines-reading of an unmodelled panel is complete only where its tooltips are
  content-backed.** The descend-into-containers reading
  ([making-screens-accessible.md](making-screens-accessible.md) §3) can only report words
  that already exist, so count the class-backed tooltips on a surface BEFORE promising it
  reads: a panel whose whole substance is renderer-assembled reads as its own name and
  nothing else, and silently, because the reading itself succeeds.
- **Point at the widget that owns the tooltip, not its row.** A row's tooltip often hangs
  off a child (the title label), and pointing at the container draws nothing — while the
  tree dump still looks plausible and the node still declares the tooltip. The reverse bites
  too: a caption whose WORD is on the label and whose SENTENCE is on the group around it
  needs the group, or the sentence gets no node at all — and where that group is the block
  itself, take the word from the label and the sentence from the group, never both from one
  (descending for the word swallows the block into its own name). Verify tooltip
  rendering with the drawn-tooltip probe, never with the tree dump: an expected long
  tooltip is accepted only once the review buffer, with the node focused, actually holds
  its words — the declaration itself stays unconditional (above), which is exactly why a
  mis-aimed pointer strands content the player can never reach and nothing in the speech
  says so.
- **A row can carry more than one tooltip** (the heading's explanation and the value's
  description): announce the value's — the last-drawn — by the short/long rule; a long one
  anywhere in the row goes to the buffer; put every tooltip in the row into
  the row's buffer **in drawn order** (the heading's explanation first, then the value's
  dossier), so review follows the screen. That is the rule for a caption-plus-value pair;
  a row carrying several INDEPENDENT explanations makes them nodes instead — one per
  tooltip-bearing widget, never merged into one buffer. Before picking which one to POINT AT, drop the
  tooltips the engine could never draw anything for (no class, no content, no target —
  and note "never draws" and "draws" are different tests: a class-ONLY tooltip survives
  this filter yet still renders nothing, so never judge an aim with the collector's test):
  prefabs hang empty ones on decoration, and a last-one-wins aim lands on them while the
  real tooltip beside them is never shown — invisible in speech, dump and buffer alike. "Last-drawn speaks" is the caption-then-value
  rule, not a universal: where the row is a card's own tooltip plus a badge's, the
  important one is the card's — the screen names which tooltip speaks. And a tooltip whose
  words are the row's own always-drawn text is not a second thing to say or buffer — the
  game reused the printed paragraph as its hover copy; skip it; when only its FIRST LINE
  repeats the label and more follows, buffer it whole instead — the label is not the
  tooltip.
- **The mode is a per-frame answer — never store it — and neither is the CONTENT: a
  tooltip the game rewrites per value is part of what a change to that value has to
  re-speak.** A widget can swap its tooltip class
  with its state (ES2's stage-deed marker: a plain-text placeholder while locked, a
  class-composed dossier once its stage is researched), so one control's short/long mode
  flips over its lifetime. Asking the marker at every declaration is what makes the shared
  helper correct; a cached `TooltipMode` ships a bug that only appears after a state change.
- **One thing, two tooltip objects, swapped by view state.** Where the game draws the same
  thing through different widgets per view (a zoom tier, a list-versus-detail switch), each
  widget carries its own tooltip: resolve which one to read at READ time from whichever is
  drawn now, or the remembered one reads empty the frame the view changes. And when a view
  tier draws NOTHING for data the game still holds, a render-keyed tooltip engine (pointer +
  class + target/context fields, no ownership check) can be driven from a carrier widget the
  mod owns, stamped with the same fields — words byte-identical to the drawn case. Its mirror in a
  camera-driven view is that the anchor can leave the screen while focus stays on the node,
  so a camera-dependent focus pointer must re-commit on every camera change, not once when
  focus arrives.
- **One implementation of the short/long test, shared with the lines reader.** Two copies
  of "are this tooltip's words on the widget" that disagree produce a tooltip announced
  from one source and reviewed from another — and nothing in the spoken output reveals
  it. The mode test and the content reader must ask the same helper — and so must the
  answer to WHICH tooltip a node has (own / parent / sibling / list entry): four collectors
  feeding one mode test still ship four answers. The AIM obeys the
  same law: a parity check asks the READING which tooltip it points at — it never
  re-derives one from the widget tree, because the deepest prefab tooltip is often
  decoration and the check then reports a defect on a screen whose pointer is correct.
- **A hand-added announcement part of the tooltip kind is ADDITIVE, never a suppressor.**
  A screen may speak its own live line under the tooltip kind (a refusal reason on a
  blocked entry); the engine-derived part (a short tooltip's announced words) still
  contributes, and both read in the control type's kind order — "unavailable, ⟨reason⟩,
  ⟨tooltip sentence⟩". Suppressing derivation because a screen added a part would silently
  drop words only the derivation speaks.
- **Captions for bare numbers come from the game's registries — the tooltip is not the name.**
  A widget whose drawn text is a bare value is named from the caption the game keeps
  elsewhere (a `%…Title` key, the element/property title, a sibling caption), asked before
  inventing a mod word; the tooltip's sentence stays a sentence. Hazard: the registry can
  point at a translation key that no longer exists — a title lookup needs the engine's
  naming-convention fallback and must degrade to silence, never to a raw key. Measure first
  with hidden widgets included: some prefabs already draw the caption, and adding one there
  double-names the row — a prefab-only Title key does NOT prove the caption is missing, only
  a tree dump taken with visible-only OFF settles it.

A dedicated speak-tooltip key is unnecessary under 1+2 (that was the ES2 call: no Space/F1
tooltip key at all), and only justified under 3.

Nested tooltip-on-tooltip data (question 4) slots into strategy 2 as extra buffer lines when
the nesting is hover-replacement rather than a link graph — with two corrections measured
since: hover-REPLACEMENT nesting releases the inner panel's data the moment the replacement
draws, so the only readings are the parent's drawn lines or the provider behind the inner
widget (never the replaced panel); and the inner things become NODES (a "Tooltips" region
under their owner) when the owner rules them navigable — extra buffer lines are the default,
not the ceiling.

Icons written inline in tooltip text are their own subject:
[icons-and-symbols.md](icons-and-symbols.md).

## Source files

`TooltipParts.cs` in [`src/graph-ui/`](src/graph-ui/) — the mode enum and announcement-part
factory (engine-side, game-agnostic). Per-game wiring exemplars: `GraphNodes.cs` (the
mode-from-marker helper and the factories), `AgeText.cs` (localize + markup-strip + line
split), `DrawnTooltip.cs` (reading the rendered tooltip window: bind-identity check, pooled
widgets, row joining), `PointerFocus.cs` (making focus render the tooltip; the drawn-tooltip
change hook).
