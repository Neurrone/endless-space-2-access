# Icons and symbols — speaking the pictures in the text

Games write icons *into* their strings — "+10 [food] per [population]" renders a number, a
picture, a word. A screen reader mod that strips markup loses the noun and speaks "+10 per":
grammatically fine, informationally wrong, and invisible to every existence check. This doc
is the pattern for naming them, and the cautionary tale of three heuristics that failed
before enumeration won.

## Find the registry, prove it closed

Inline icons resolve somewhere: a symbol table the text renderer consults when it expands the
markup token into a glyph or sprite. Find that registry's **writer** in the decompiled code
(ES2/AGE: one method loads five XML assets into one dictionary; `SymbolString` fields
elsewhere are consumers, not writers). If the registry has a single writer loading static
data, the vocabulary is a **closed set**: dump it live, diff it against the source data, and
you can say "382 tokens, complete" instead of guessing. Do this *before* designing any naming
scheme — the ten-minute enumerability check is the difference between a table and a
heuristic.

Sprite images drawn beside text (stat columns, panel icons) are the second universe, and it
usually *cannot* be closed — any bitmap can be drawn anywhere. Restrict it honestly (ES2:
textures the game's own data pairs with a registered token) and give the leftovers a
detector, not a guess (below). The detector earns its keep even after enumeration wins: a
symbol nothing in the game's data carries — one painted straight into a panel — escapes
any data-derived table, and the unknown-pictures audit is the only thing that finds it.

## Name them in the mod's translation file

Every enumerated icon gets an entry in the mod's own string table (`icon.*` keys — the same
table as every other mod phrase, so names are reviewable, editable, and translated with
everything else). Seed the names from the game's own localized titles where the concept is
unambiguous; where the game's data is ambiguous, choose the shortest accurate noun and flag
it for the owner's review. Variants collapse to one concept through an explicit alias map —
colored/size/style variants of the dust icon are all `icon.dust` — by listed aliases, never
by string heuristics guessing at suffixes.

Resolution at speak time is then a pure table lookup. **No unmapped icons by design**: an
icon not in the table (a game patch, a DLC) logs a warn-once and drops from speech — the
warning is the tripwire that says the table needs a row, and a dev-server probe lists what
the tripwire caught this session.

### What failed first (so it isn't rebuilt)

1. **Majority vote** over every game database entry sharing the icon: statistically wrong for
   exactly the icons that appear most (19 entries share the dust icon; 3 agree on "Dust" —
   the winner was an unrelated feature's title).
2. **Refined votes** (containment-weighted, shortest-title): elected generic words
   ("Planet", "War") for everything.
3. **Word-shape fallbacks** (speak the bare token if it looks like a word): leaked asset ids
   (`turnColored`, `terransCenter`) into sentences — an asset id costs the whole sentence,
   a dropped icon costs one word.

## Substitution mechanics (the traps)

- **Substitute before the engine's markup pass destroys the token.** Engines resolve markup
  when a label is *set* and cache the result — the rendered string holds an unspeakable
  private-use glyph or nothing at all. Read the raw assigned text when it still holds a
  token, substitute the name, then let the engine's cleaner run.
- **The engine referees what is an icon — ask its registry directly.** Literal brackets exist
  in real text (a save titled "[Beginner] game"): a token absent from the symbol registry is
  literal text and passes through untouched, brackets and all. Do NOT infer by probing the
  cleaner ("did `[x]` come back changed?") — cleaners also strip color markup, so a
  non-icon bracket containing color runs comes back changed without being an icon (a shipped
  bug). Never assume every bracket is markup.
- **Seam spacing**: games write numbers hard against icons ("3[population]") because a glyph
  needs no gap; a substituted *name* does. Insert a space only where both effective
  neighbors are word characters — and "effective" means looking through color-markup runs
  that will be stripped later, or the seam re-appears after cleaning.
- **Dedupe against adjacent words**: authors often write the icon *and* the word ("[overcol]
  Over-colonization penalty"). When most of the icon-name's words already appear beside it on
  the line (compare on letters/digits only — punctuation differs), skip the substitution
  rather than stutter.
- **Sprite columns**: a row of bare numbers beside image widgets is named by the images'
  *asset names* (ES2's stat line: assets literally named `FIDSIFood`, `FIDSIIndustry`… — the
  asset name is the ground truth for what each column means), resolved through the same
  table.

## Verifying

Icon fixes are text-pipeline fixes: push the game's entire localization corpus through the
cleaner and assert the unknown-token list is empty (ES2: 25 821 strings — it also caught a
referee bug no targeted test found). Then read the audited tooltips against their rendered
form per the evidence-pair method in
[making-screens-accessible.md](making-screens-accessible.md).

## Source notes

The table itself is game data and lives with the game mod, not here (ES2: `IconTable.cs`, a
BCL-only token→key map with the alias list, unit-tested against the locale file). The lookup
shell is small: token → alias → `icon.*` key → string table, plus the warn-once. The
substitution mechanics live in the adapter's text pipeline (`src/graph-ui/AgeText.cs`, the
`SubstituteIcons`/`EngineExpands` region) and the letters-and-digits normalizer in
`src/graph-ui/TextUtil.cs` (engine, copy verbatim).
