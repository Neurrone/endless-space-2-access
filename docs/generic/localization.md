# Localization

Translatable speech from day one. Retrofitting localization onto frozen English strings is a
mod-wide rewrite (wotr-access treats it as an audit-level invariant; SoC built `.po`
infrastructure); starting with the seam costs nearly nothing.

## The three sources of spoken text

1. **Game-authored text** (menu titles, tooltips, entity names) — the game already localizes
   it; read it through the game's localization service and never re-translate it. Trap: only
   *rendered* labels are reliably localized. Backing data tables may hold raw localization
   keys even while every on-screen label shows translated text (ES2's droplist entry tables
   hold `%`-prefixed keys). Anything spoken from data rather than from a rendered label must
   pass through the game's localizer — and confirm that localizer passes plain,
   non-key strings through unchanged, since the same table can mix both.
   **And preserve it exactly.** Never insert the mod's separators or punctuation into game
   text: multi-line game text joins with a space, not the list separator — "disabled., Once"
   is a defect, and it appears the moment game *lines* are fed through a list-item builder
   meant for mod-composed enumerations. Where the game shows something for a state (failure
   tooltips, captions, placeholders), surface those words, never a mod paraphrase; where the
   game shows nothing, invent nothing (no placeholder nodes for empty states, no spoken
   position text where the game draws dots). Inline icon tokens in game text are named, not
   stripped: [icons-and-symbols.md](icons-and-symbols.md).
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
   because they are language-dependent in both wording and word order.

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

A build-time test walks every locale file and asserts: keys are a subset of the compiled-in
English keys, and each value contains exactly the `{n}` placeholders of its English template.
Broken community translations fail the build instead of producing silent wrong speech.

Plurals: defer until the first plural-sensitive phrase, then adopt a gettext-style
plural-forms mechanism (SoC's `ModPluralString`/`JoinList` is the worked example). Note it;
don't build it speculatively.

## Source files

[`src/localization/ModStrings.cs`](src/localization/ModStrings.cs),
[`MessageBuilder.cs`](src/localization/MessageBuilder.cs),
[`ModLocale.cs`](src/localization/ModLocale.cs) (adapt the game-language lookup),
[`english.json`](src/localization/english.json),
[`LocaleFileTests.cs`](src/localization/LocaleFileTests.cs) (the validator),
[`MessageBuilderTests.cs`](src/localization/MessageBuilderTests.cs) (including the
Japanese-style table cases).
