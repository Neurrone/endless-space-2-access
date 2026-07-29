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

Three proven shapes. **Which elements get which treatment is a UX decision the human
developer makes per declaration — not a length heuristic in code.** What reads as "short
enough to announce" is a judgment call (a three-line quick-start blurb was fine by ear; a
technology stat panel is not), so the API takes an explicit mode and the human decides.

1. **Announce inline** — the tooltip text joins the focus announcement as a trailing part
   (its own announcement *kind*, so per-type filtering comes free once announcement settings
   exist; placed after the state words, before the position). For short descriptions where
   hearing it on focus beats asking for it. Resolve at speak time — appended disabled
   reasons then arrive automatically.
2. **Indicate + buffer** — the announcement says only "has tooltip"; the full content sits
   in the review buffer ([buffers.md](buffers.md)) for line-by-line reading. For long/rich
   tooltips. For render-composed rich tooltips, build the lines by querying the same
   provider interfaces the game's tooltip renderer reads (headless — no window shown, no
   hover), one line per semantic row. In **both** modes the full content also populates the
   buffer, so review behaves identically everywhere.
3. **A navigable tooltip reader** (wotr-access) — a modal child screen the user arrows
   through, with drill-in. This exists **only** because that game's tooltips form a link
   graph (glossary terms leading to further tooltips); the reader is how you follow links.
   If your research at step 3 above found no links, do not build this — the buffer is
   strictly simpler and reads the same content.

A dedicated speak-tooltip key is unnecessary under 1+2 (that was the ES2 call: no Space/F1
tooltip key at all), and only justified under 3.

Nested tooltip-on-tooltip data (question 4) slots into strategy 2 as extra buffer lines — a
"verbose" enhancement, never a navigation model, when the nesting is hover-replacement
rather than a link graph.

## Source files

`TooltipParts.cs` in [`src/graph-ui/`](src/graph-ui/) — the mode enum and announcement-part
factory (engine-side, game-agnostic). Per-game wiring exemplars: `GraphNodes.cs` (factories
taking the mode + the tooltip source), `AgeText.cs` (localize + markup-strip + line split).
