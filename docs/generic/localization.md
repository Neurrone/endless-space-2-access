# Localization

Translatable speech from day one. Retrofitting localization onto frozen English strings is a
mod-wide rewrite (wotr-access treats it as an audit-level invariant; SoC built `.po`
infrastructure); starting with the seam costs nearly nothing.

## The four sources of spoken text

1. **Game-authored text** (menu titles, tooltips, entity names) — the game already localizes
   it; read it through the game's localization service and never re-translate it. Trap: only
   *rendered* labels are reliably localized. Backing data tables may hold raw localization
   keys even while every on-screen label shows translated text (ES2's droplist entry tables
   hold `%`-prefixed keys). Anything spoken from data rather than from a rendered label must
   pass through the game's localizer — and confirm that localizer passes plain,
   non-key strings through unchanged, since the same table can mix both. Where the engine's
   localizer returns its own key on a miss, filter unresolved keys once in the shared text
   reader — per-caller guards will be missed by the next reader.
   **And preserve it exactly.** Never insert the mod's separators or punctuation into game
   text: multi-line game text joins with a space, not the list separator — "disabled., Once"
   is a defect, and it appears the moment game *lines* are fed through a list-item builder
   meant for mod-composed enumerations. Surface the game's own words for a state and invent
   nothing where it shows nothing —
   [making-screens-accessible.md](making-screens-accessible.md) §0. Inline icon tokens in game
   text are named, not stripped: [icons-and-symbols.md](icons-and-symbols.md).
   **"The game's words" means the words, not the line-fitting.** When a drawn string is an
   ellipsized/truncated fit of a source string the game shows in full elsewhere (hover, a
   tooltip), speak the source string, never the fitted one — tile grids and narrow columns
   hit this in every game.
   **When hunting the corpus for a control's name, grep for the ACTION it performs, not the
   control**: wordless action buttons are routinely named by the game under the action's own
   key family, nowhere near the widget — a "no game-authored name exists" conclusion is
   often a wrong-noun search. **Grep for the STATE ENUM too, and read the legend's
   key-building expression**: a game with a state or category enum almost always composes
   its keys as prefix + enum member name, and the widget that draws the *legend* for the
   thing being modelled contains the exact expression (`"DeedState" + state`) — read the
   key recipe off the game's own display code instead of guessing at the corpus. **And search the game's DATA tables, not just the localization
   corpus**: a drawn value's name often lives with the value's definition (ES2: a palette XML
   naming every colour the renderer only ever paints) and never reaches the screen or the
   corpus — "the game's words" includes the words in its data.
2. **Mod-authored phrases** (role words like "button", status words, screen names, the
   startup line) — come from the mod's string table, never inline literals.
3. **Connective structure** (list separators, "N of M", "x N") — also from the string table,
   because they are language-dependent in both wording and word order. A separator a MATCHER
   reads (the type-ahead's name/metadata comma) is engine, not string table — or a locale that
   re-punctuates breaks the rule.
4. **Mod-authored content** — prose describing what the game SHOWS but never says: audio
   description for cutscenes, animated stingers, wordless sequences. It is mod-authored like
   (2), and the temptation is therefore to put it in the string table. Don't. It is bulk prose
   attached to specific ASSETS, not phrases attached to UI, and it swamps the template a
   translator works from — one game's cutscenes ran to 4,400 words against a 900-key table.
   Ship it as a **per-asset table beside the string table** (`descriptions/<language>.json`
   next to `locale/<language>.json`), following the game's language through the same watcher.
   Four rules that are the opposite of the string table's, and each one bit before it was
   written down:
   - **Key by the game's own asset name, not the human one.** A describer writes "Vodyani
     Intro"; the game asks for `Vampirilis_Intro`. Do that translation ONCE in a build step,
     so the runtime carries no mapping table that can drift from the game's files.
   - **Have the build step FAIL on a name the install does not have.** The mapping is the
     only place this can be caught; a wrong name at runtime is silence that looks like a
     missing feature.
   - **Commit the built table, not what built it.** A describer's output is bulk generator
     artefact — one game's ran to 208 files against a 40 KB table — and nothing but the build
     step ever reads one, so ignore it the way the media it describes is ignored. Then carry in
     the BUILT table anything the runtime does not need but a later editor does — where the
     video's own dialogue resumes, so a rewritten cue can be checked against the room it has —
     or that constraint leaves the repository along with the input.
   - **English is a FILE here, not a compiled-in fallback.** There is nothing to hard-code, so
     the degradation ladder is per-language-file → English file → silence, and silence is a
     legitimate rung. Descriptions are additive; a language without them loses nothing it had.

## The string table (`ModStrings` pattern)

- Flat key → format-template lookup. English defaults are **compiled in** as the safety net;
  a translation file overlays them.
- Per-key degradation: unknown key → English → the key itself, with a warn-once log. A
  broken translation (bad `{n}` placeholders → `FormatException`) falls back to the English
  template for that key. A bad locale file can never crash or silence the mod.
- One file per language, named after the **game's own language identifiers** (e.g. Steam
  names: `english.json`, `french.json`, `schinese.json`) so the mapping is trivial; follow
  the game's language service, and reload live when the player changes language. The English
  file is the translator template: copy, translate values, keep keys and placeholders.
- JSON is sufficient and toolless; move to gettext `.po` (SoC's stack, with a validator CLI)
  only if outside translators ask for tooling.

## Template rules

- **Complete phrases only.** Each key is a full translatable unit with `{n}` placeholders —
  `"{0} of {1}"`, not `"of"` glued between numbers. Placeholders let translators reorder:
  Japanese renders the same fraction as `"{1}中{0}"`.
- **Never resolve-then-compose.** Composing already-resolved fragments into a larger English
  sentence freezes English grammar into the output. Compose *structurally* (via the builder)
  or promote the whole sentence to its own key.
- **Coarse-units rule.** No string system can inflect a game-supplied noun inside a mod
  template (gender, case, classifiers). When a composition would require grammatical
  agreement, that composition becomes a single key, not a glue job. Accept that speech
  tolerates slightly telegraphic style; translators adapt within their template.
- **Numbers speak the displayed counter.** A strategy game's internal turn counter is
  usually one off from the displayed one; any mod-composed schedule ("turn N") must use
  the displayed counter, while durations stay relative.

## The message builder

A fluent accumulator (`Fragment` space-joined, `ListItem` comma-joined, `PushFraction`,
`PushQuantity`; single-use; `Build()` owned by the speech sink — the `Speak` chokepoint in
[speech.md](speech.md)) — ported from Factorio Access via Tangledeep. Localized by pulling its connectives from the string table at append
time: fragment separator (may be empty — CJK), list separator (`", "`, `"、"`, `"،"`), and
the fraction/quantity templates. With that change the builder is language-neutral and English
output stays byte-identical.

Composition subtlety: the first `ListItem` after leading content joins with the fragment
separator (a leading comma would be wrong); use `ListItemForcedComma` when a "label, role"
reading should always get the separator.

## Validation

Build-time tests walk every locale file, so a broken translation fails the build instead of
producing silent wrong speech. The minimal validator ([`LocaleFileTests.cs`](src/localization/LocaleFileTests.cs))
asserts keys are a subset of the compiled-in English keys and each value carries exactly the
`{n}` placeholders of its English template. The full lint set
([`LocaleLint.cs`](src/localization/LocaleLint.cs), applied by
[`TranslationLintTests.cs`](src/localization/TranslationLintTests.cs) and
[`DescriptionFileTests.cs`](src/localization/DescriptionFileTests.cs)) adds, each as a pure
function over strings so it is provable against synthetic bad input:

- **Encoding**: strict UTF-8 decode, no byte-order mark, no U+FFFD, no C1 controls, no
  double-encoding bigrams (`Ã©`, `Ð`+continuation) — the mojibake a copy through the wrong
  code page leaves behind.
- **Script**: a Cyrillic, Hangul or Han language must actually contain its script — an entry
  whose English has three or more words needs at least one native letter, and most entries
  overall must; keyboard key names legitimately stay Latin.
- **Completeness both ways**: english.json equals the compiled-in default key set exactly, and
  every other language answers for every key. A half-finished translation cannot ship, because
  a missing key falls back to an English sentence mid-phrase rather than to an obvious hole.
- **Untranslated**: an entry identical to English where English has three or more words.
- **Staleness**: a per-language snapshot of the English each entry was translated from
  (`locale/sources/<language>.json`, in a subfolder so a non-recursive copy step never ships
  it). Nothing at runtime can notice that English was reworded under a translation; the
  snapshot diff can. Whoever translates a key re-records it (`mark-translated.ps1`).
- **Descriptions**: same movie set and cue count as English, identical cue times, non-empty
  text, and their own snapshot.

Plurals: start with a two-form key pair chosen by count, each key a complete sentence, and
carry the extra forms a three-form language needs in its locale file under
`<many key>.few` (2–4 in Polish and Russian) and `<many key>.one` (the counts a language's
singular also serves, 21 and 31 in Russian, needed when the pair's ONE sentence is "this
turn" rather than "{0} turn"). The rules are CLDR cardinals keyed by the game's language
name ([`PluralRules.cs`](src/localization/PluralRules.cs)); the lint requires the forms by
scanning the sources for every pair that goes through the plural helper
([`PluralPairs.cs`](src/localization/PluralPairs.cs)). The corollary is that every counted
phrase must go through that helper: an inline `count == 1 ? one : many` test silently
denies the third form.

Producing the translations: the game's own localization files are a glossary. Extract, for
every short English game string the mod's own strings mention, the official translation per
language, and hand each translation batch the terms its keys name — a mod that says
"workforce" where the game says "manpower" has two vocabularies for one stat. The
auto-match yields false friends (a key-name "Space", a verb sense of "Dismiss"), so the
brief says to ignore those. Batch inputs carry the English, the source comment above the
constant, and the pair forms required ([`export-batches.ps1`](src/localization/tools/export-batches.ps1));
the parts are merged, checked and snapshotted in one step
([`merge-parts.ps1`](src/localization/tools/merge-parts.ps1)); and one review per language
afterwards unifies the mod's own coinages (bookmark, buffer, inspect mode, tile) that
independent batches render differently.

## Source files

[`src/localization/ModStrings.cs`](src/localization/ModStrings.cs),
[`MessageBuilder.cs`](src/localization/MessageBuilder.cs),
[`PluralRules.cs`](src/localization/PluralRules.cs),
[`ModLocale.cs`](src/engine-example/ModLocale.cs) (adapt the game-language lookup),
[`english.json`](src/localization/english.json),
[`LocaleFileTests.cs`](src/localization/LocaleFileTests.cs) (the minimal validator),
[`LocaleLint.cs`](src/localization/LocaleLint.cs),
[`TranslationLintTests.cs`](src/localization/TranslationLintTests.cs),
[`DescriptionFileTests.cs`](src/localization/DescriptionFileTests.cs),
[`TranslationFiles.cs`](src/localization/TranslationFiles.cs) (the language list and
script map the lints read; the paths are this mod's),
[`PluralPairs.cs`](src/localization/PluralPairs.cs) (its hand-traced sites are this mod's),
[`MessageBuilderTests.cs`](src/localization/MessageBuilderTests.cs) (including the
Japanese-style table cases), and the translation workflow scripts under
[`src/localization/tools/`](src/localization/tools/) (`export-batches.ps1`,
`merge-parts.ps1`, `mark-translated.ps1`, `translation-lib.ps1`; the folder names are this
mod's).
