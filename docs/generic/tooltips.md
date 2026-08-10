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
   data, not a navigation model; nothing in a tooltip is clickable.)
5. **Does the game append state into tooltip text** — "why disabled" hints? (ES2: yes,
   `FormatButtonHint` appends the reason into `Content`; always read tooltips live.)
6. **Where do tooltips render** — anchored to the widget, or at the cursor? Cursor-anchored
   tooltips shown for keyboard focus land wherever the idle mouse is parked; re-anchor them
   to the focused element while displaying (see the focus-visuals section of
   [ui-navigation.md](ui-navigation.md)).

## Then: pick the surfacing strategy

Three proven shapes:

1. **Announce inline** — the tooltip text joins the focus announcement as a trailing part
   (its own announcement *kind*, so per-type filtering comes free once announcement settings
   exist; placed after the state words, before the position). For **short** tooltips — the
   one-sentence description the game's author wrote to be read whole, including any appended
   disabled reason. Resolve at speak time so those reasons arrive automatically.
2. **Indicate + buffer** — the announcement says only "has tooltip"; the full content sits
   in the review buffer ([buffers.md](buffers.md)) for line-by-line reading at the player's
   own pace. For **long** tooltips — stat panels, dossiers, anything render-composed. In
   **both** modes the full content also populates the buffer, so review behaves identically
   everywhere.
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

- **Indication must never gate on rendered content existing.** A render-composed tooltip's
  words do not exist until the game draws them — a hover-delay *after* focus arrives — so
  "say 'has tooltip' only if the lines are non-empty" is silent every single time. For a
  renderer-assembled tooltip, having content is definitional: indicate unconditionally.
- **Reading a render-composed tooltip back**: where provider interfaces are readable
  headlessly, prefer them — and the REFUSAL is the part that nearly always is: the failure
  feature is typically one call over a provider on the tooltip's target, populated at bind
  time, so a blocked control's reason can speak instantly with no hover, and the drawn
  failure panel is the free oracle proving your expression matches it. Where they are not, make focus trigger the game's own tooltip
  display (see focus visuals in [ui-navigation.md](ui-navigation.md)) and read the drawn
  window's labels — what is drawn is exactly what should be spoken.
  **Find the assembly unit first and scope the reading to it.** A tooltip system that
  composes at render time composes from an ordered list of typed sub-panels (ES2: panel
  features under one table). Reading by geometry *across* those units is what divorces
  values from their captions and fuses adjacent blocks into nonsense lines. Read within
  each unit, and give typed readers to the few structures geometry gets wrong — a
  repeated-item grid pairs the Nth title with the Nth value by sibling index (its drawn
  layout puts all titles above all values, so no row-banding can pair them); a textless
  gauge reads as its drawn proportion. Two safety nets make a hundred-class surface
  tractable: unknown unit types read through the generic scoped reader, and a dev probe
  names which reader answered each unit — coverage gaps surface in tooling, not in speech.
  Remaining traps, all hit in practice: check *which* tooltip the window is currently bound
  to before reading; the
  window pools its child widgets, so a shrunk tooltip still carries the previous one's
  labels (use the engine's own effective-visibility test); join labels drawn on one visual
  line into one spoken line, in x-order, with the label's own alignment as the tiebreak when
  caption and value occupy the same rect; and refill the review buffer when the drawn
  tooltip lands (an invalidation hook), guarded by a lines-equality check so a refill with
  identical content never resets the player's reading position.
- **Point at the widget that owns the tooltip, not its row.** A row's tooltip often hangs
  off a child (the title label), and pointing at the container draws nothing — while the
  tree dump still looks plausible and the readout still says "has tooltip". Verify tooltip
  rendering with the drawn-tooltip probe, never with the tree dump.
- **A row can carry more than one tooltip** (the heading's explanation and the value's
  description): announce the value's — the last-drawn — by the short/long rule, **and
  indicate whenever any tooltip in the row is long**; put every tooltip in the row into
  the row's buffer **in drawn order** (the heading's explanation first, then the value's
  dossier), so review follows the screen. "Last-drawn speaks" is the caption-then-value
  rule, not a universal: where the row is a card's own tooltip plus a badge's, the
  important one is the card's — the screen names which tooltip speaks.
- **The mode is a per-frame answer — never store it.** A widget can swap its tooltip class
  with its state (ES2's stage-deed marker: a plain-text placeholder while locked, a
  class-composed dossier once its stage is researched), so one control's short/long mode
  flips over its lifetime. Asking the marker at every declaration is what makes the shared
  helper correct; a cached `TooltipMode` ships a bug that only appears after a state change.
- **One implementation of the short/long test, shared with the lines reader.** Two copies
  of "are this tooltip's words on the widget" that disagree produce a tooltip announced
  from one source and reviewed from another — and nothing in the spoken output reveals
  it. The mode test and the content reader must ask the same helper.
- **A hand-added announcement part of the tooltip kind is ADDITIVE, never a suppressor.**
  A screen may speak its own live line under the tooltip kind (a refusal reason on a
  blocked entry); the engine-derived part (the announced words or "has tooltip") still
  contributes, and both read in the control type's kind order — "unavailable, ⟨reason⟩,
  has tooltip". Suppressing derivation because a screen added a part would silently
  un-indicate the buffer that section still fills.
- **Captions for bare numbers come from the game's registries.** When a drawn value's only
  name is a static icon, ask the game's element/property registry for its localized title
  before inventing a mod word. Hazard: the registry can point at a translation key that no
  longer exists — a title lookup needs the engine's naming-convention fallback and must
  degrade to silence, never to a raw key.

A dedicated speak-tooltip key is unnecessary under 1+2 (that was the ES2 call: no Space/F1
tooltip key at all), and only justified under 3.

Nested tooltip-on-tooltip data (question 4) slots into strategy 2 as extra buffer lines — a
"verbose" enhancement, never a navigation model, when the nesting is hover-replacement
rather than a link graph.

Icons written inline in tooltip text are their own subject:
[icons-and-symbols.md](icons-and-symbols.md).

## Source files

`TooltipParts.cs` in [`src/graph-ui/`](src/graph-ui/) — the mode enum and announcement-part
factory (engine-side, game-agnostic). Per-game wiring exemplars: `GraphNodes.cs` (the
mode-from-marker helper and the factories), `AgeText.cs` (localize + markup-strip + line
split), `DrawnTooltip.cs` (reading the rendered tooltip window: bind-identity check, pooled
widgets, row joining), `PointerFocus.cs` (making focus render the tooltip; the drawn-tooltip
change hook).
