# Audit: nodes whose spoken text can be a bare number

Read-only audit (decompile + mod source + the game's shipped `Public/Gui` and
`Public/Localization` XML). No live probing — every "unmeasured" note below is a prefab
question the fix stage must answer with the running game.

Absolute paths: mod source under
`C:\Users\Dickson\Desktop\projects\endless-space-2-access\ES2Access\`, decompile under
`...\decompiled\Assembly-CSharp\`, game data under
`C:\Program Files (x86)\Steam\steamapps\common\Endless Space 2\Public\`.

---

## 1. The caption mechanisms the mod ALREADY has (reuse, don't invent)

The fix stage should pick from this list per site. Every one of them is already shipped and
already documented in a doc comment.

| # | Mechanism | Where it lives | When it applies |
|---|---|---|---|
| M1 | **Explicit `%…Title` key** → `Gui.Localize(key)`, composed "Caption value" | `Screens\MilitaryScreen.cs:823` (`AddStat`), `Screens\HeroInspectionScreen.cs:530-545` + `:1573` (`AddStat`), `UI\HeroCards.cs:47-54,409-416`, `Screens\FleetPanel.cs:648-670` (`AddCell`), `Screens\BattleNotifications.cs` (`Value(..., TitleKey, ...)`) | The stat has a known localization key and the caption is drawn nowhere |
| M2 | **GuiElement registry title** → `Gui.GetTitle(elementName)` with naming-convention fallback then silence | `UI\TooltipFeatures.cs:629-652` (`StatTitle`), `UI\ShipDesignRows.cs:517-535,1029-1032` | The value belongs to a named element/category/descriptor |
| M3 | **Simulation-property title** → `Gui.GetLocalizedTitle(property)`, guarded against the pink "(missing GuiElement)" placeholder and raw keys | `Screens\GlobalHud.cs:1150-1182` (`Naming`/`PropertyTitle`), `Screens\PlanetOverviewScreen.cs:378-420`, `Screens\GalaxyHudScreen.cs:1358-1400`, `Screens\ScanViewScreen.cs:660-690` | FIDSI / empire-property readouts |
| M4 | **Tooltip wrapper title** → `AgeWidgets.TooltipTitle` (`tooltip.Target as GuiWrapper` → `.Title`) | `UI\AgeWidgets.cs:239-250`, used ~20 places | The game hangs a `GuiWrapper` on the tooltip (resources, parties, populations, modules) |
| M5 | **Declare the GROUP, not the label** — `TextOf(parent)` picks up the prefab's caption sibling | `UI\ShipDesignRows.cs:1273-1283` (`Group`), `Screens\PirateDiplomacyScreen.cs:239-249`, `Screens\PlanetOverviewScreen.cs:440-442`, `Screens\GovernmentScreen.cs:337-339` | A caption label is drawn beside the value |
| M6 | **Find the prefab caption by name** → `AgeWidgets.ChildNamed(root, "…Title", n)` | `Screens\SenateScreen.cs:694-700`, `Screens\GovernmentScreen.cs:138-143`, `Screens\ImprovementsModalScreen.cs:158-175` (`HeadingBeside`) | The caption exists but the panel exposes no field for it |
| M7 | **Inline `[token]` icon → icon name** (`AgeText.SubstituteIcons` + `IconTable`) | `UI\AgeText.cs:252-307`, `Core\Speech\IconTable.cs` | The game itself glued a token onto the number — this self-names and needs no work |
| M8 | **Table column headings** `%<Table><Column>Title`, spoken as the crossed edge | `UI\TableSheet.cs:890-915` (`Caption`/`HeaderName`) | Anything inside a `GuiTable` |
| M9 | **Nth-caption-to-Nth-value by sibling index** for a repeated grid whose titles are all in one band and values in another | `UI\TooltipFeatures.cs:518-563` (`CountsBySize`) | A column strip (ranges, hull sizes) |
| M10 | **Stock/net pair template** `ModStrings.GalaxyStockAndNet` | `Screens\GlobalHud.cs:1899-1906` (`StockAndNet`) | Two adjacent numbers that mean different things |

---

## 2. Findings

Severity: **A** = confirmed bare number today; **B** = confirmed bare number but the caption
may be drawn in the prefab (measure first); **C** = named, but named worse than the game
names it.

### A1 — the ship designer's six "simple stats" (THE exemplar: "1500")

| | |
|---|---|
| Mod read | `UI\ShipDesignRows.cs:132-149` — `Build` declares four bands (`BuildInfo`, `BuildModules`, `BuildStats`, `BuildSlots`). `BuildStats` (`:675-707`) walks **`panel.StatisticsTable` only**. `ShipDesignBasePanel.SimpleStatsGroup` — Health, Manpower, Offensive/Defensive power, Movement, Command points — is **never named anywhere in `ShipDesignRows.cs` or `Screens\ShipDesignScreen.cs`** |
| What it can speak | `"1500"` plus the tooltip (whose words are `%ShipStatHealthDescription`, a sentence, not a name) — or nothing at all if the group is outside the walked band |
| Game side | `decompiled\Assembly-CSharp\ShipDesignBasePanel.cs:21-33` (the six label fields, all under `SimpleStatsGroup`), `:110-123`: `HealthLabel.Text = GuiShipDesign.ShipStatMaximumHealthProperty` (= `FloatExtensions.ToString(MaximumHealth)`, `GuiShipDesign.cs:416`) and `HealthLabel.GetComponentInParent<AgeTooltip>().Content = Gui.Localize("%ShipStatHealthDescription")` — the tooltip is on the **parent group**, the value on the label, and no caption is written at all |
| Where the caption lives | The game's string table: `%ShipStatHealthTitle` = "Health", `%ShipStatManpowerTitle`, `%ShipStatOffensiveMilitaryPowerTitle`, `%ShipStatDefensiveMilitaryPowerTitle`, `%ShipStatMovementTitle`, `%ShipStatCommandPointsTitle` (all present in `Public\Localization\english\ES2_Localization_Locales.xml`) |
| Recommended source | **M1**, exactly as the other two hosts of the same base panel already do it: `Screens\MilitaryScreen.cs:774-779` and `Screens\HeroInspectionScreen.cs:527-545`. Both carry the same doc comment — "The game draws each as a number beside a bare symbol and names it nowhere on screen" — i.e. this was already measured twice |
| Recommended form | `"Health: 1500"` (`MessageBuilder` caption + value; no double-naming risk — nothing on screen says "Health") |
| Fix-stage note | `AddStat` is currently **duplicated** verbatim in `MilitaryScreen.cs:823` and `HeroInspectionScreen.cs:1573`. The six-stat naming belongs in `ShipDesignRows` (the file that already exists for "the panel, whichever host draws it"), with the other two hosts calling it — one caption map, three consumers. Also measure whether `SimpleStatsGroup` is inside `StatisticsTable` in the *edition* prefab: if it is, the shape walk is already emitting these as bare numbers (the reported symptom); if it is not, they are missing entirely. The fix is the same either way, the verification differs |

### A2 — the range-accuracy / DPS block in the ship designer's statistics table

| | |
|---|---|
| Mod read | `UI\ShipDesignRows.cs:741-756` — the three DPS labels share one parent, so the walk is special-cased to emit **one readout per column child** (`Cells.AddReadout(cells, columns[i], …)`). The comment on those lines states the problem and stops short of fixing it: *"three identical `(0)`s at that, with the sentence saying which range each belongs to left on its own label"* |
| What it can speak | `"(0)"`, three times in a row. The accuracies above them (`LongRangeEfficiencyLabel` etc.) are `"40%"`, `"65%"`, `"80%"` in a separate group |
| Game side | `ShipDesignEditionPanel.cs:1079-1088` — `LongRangeEfficiencyLabel.Text = rangeEfficiencies["Long"].ToString(0, percentage: true)`; `LongRangeDPSLabel.Text = "(" + FloatExtensions.ToString(DPSAtLongRange) + ")"`. Nothing else is written into either |
| Where the caption lives | `%ShipStatRangeLongTitle` = "Long", `…MediumTitle`, `…ShortTitle`, and the band heading `%ShipStatRangeEfficienciesTitle` = "Overall Range Accuracy" — all four exist in the localization and appear **nowhere in the decompile**, so they are prefab-authored labels, drawn as a header band above the columns. The per-figure explanations are `%ShipStatRangeLongDescription` / `%ShipStatRangeLongDPSDescription` |
| Recommended source | **M9 + M1**: pair the Nth column caption with the Nth accuracy and the Nth DPS by sibling index (the pattern `TooltipFeatures.CountsBySize` already implements for hull sizes), taking the caption from the drawn header label where it is a sibling, else from `%ShipStatRange<Range>Title` |
| Recommended form | `"Long: 40%, (0)"` per column, or two nodes per column — the screen's call. **Do not** prepend a caption to the value while the header band is also declared as its own line, or the block says "Long Medium Short" and then "Long: 40%" |

### A3 — the pirate diplomacy window's next-fleet figures (five nodes)

| | |
|---|---|
| Mod read | `Screens\PirateDiplomacyScreen.cs:222-224` (health, offense, defense) and `:326-327` (command points, movement) → `Line()` at `:239-249`, which declares the label's **parent group** with `Cells.Readout(group, AgeWidgets.Raw(group), …)`; `Cells.Readout` (`UI\Cells.cs:125-143`) speaks `AgeWidgets.TextOf(widget)` |
| What it can speak | `"142"`, `"87"`, `"63"`, `"4"`, `"6"` — the group's only other child is an `AgePrimitiveImage`, which contributes no text (pictures are named only inside `DrawnTooltip`, never in `TextOf`) |
| Game side | `decompiled\Assembly-CSharp\PirateDiplomacyModalWindow.cs:408-410` and `:431-432` — all five are bare `FloatExtensions.ToString(...)`. The window sets exactly three tooltips in code (`:360`, `:474-478`), none of them on these groups, so any explanation on the row is prefab-authored — **and whether it sits on the group the mod declares or on the icon inside it is unmeasured** (the doc comment on `Line` asserts the group, unverified) |
| Where the caption lives | Nowhere on screen; the same six ship stats as A1, so the same keys: `%ShipStatHealthTitle`, `%ShipStatOffensiveMilitaryPowerTitle`, `%ShipStatDefensiveMilitaryPowerTitle`, `%ShipStatCommandPointsTitle`, `%ShipStatMovementTitle` |
| Recommended source | **M1** (the same helper A1 introduces). Keep pointing the node at whichever widget really owns the tooltip — verify with the drawn-tooltip probe, per `tooltips.md`'s "point at the widget that owns the tooltip, not its row" |
| Recommended form | `"Health: 142"`. Contrast the current reading, which is the exact failure `making-screens-accessible.md` §4 names |

### A4 — the election window's total electors

| | |
|---|---|
| Mod read | `Screens\ElectionScreen.cs:540-545` — `AddReadout(_cells, Widget(panel.TotalElectorsValue), "election:total-electors", Raw(panel.TotalElectorsValue))`, i.e. the **value label itself**, not its group |
| What it can speak | `"37"` |
| Game side | `decompiled\Assembly-CSharp\ElectionLocalPanel.cs:50` (the field) and `:237` — `TotalElectorsValue.Text = cumulatedRepresentativesCount.ToString()`. Nothing else touches it |
| Where the caption lives | `%TotalElectorsTitle` = "Total" and `%TotalElectorsDescription` = "Total number of votes that would be cast if the election is held now" — both present in the localization, **neither referenced anywhere in the decompile** → prefab-authored, so a caption label and a tooltip are drawn next to the number |
| Recommended source | **M5** first (declare `Widget(panel.TotalElectorsValue).Parent` and let `TextOf` pick up the drawn "Total"), falling back to **M1** with `%TotalElectorsTitle` if the caption is not a sibling. Measure which |
| Recommended form | `"Total 37"` from the drawn caption. Note the drawn word is thin on its own; the row's tooltip carries the real gloss and already reaches the buffer |

### B1 — the rest of the ship designer's statistics table

`UI\ShipDesignRows.cs:692` hands `panel.StatisticsTable` to `SidePanels.Content` with the
`Stats` escape hatch. Every figure in it is written bare:

`ShipDesignEditionPanel.cs:1146-1188` — `KineticPowerLabel`, `MissilePowerLabel`,
`LaserPowerLabel`, `BeamPowerLabel`, `PlatingHealthBonusLabel`, `ShieldCapacityLabel`,
`FighterCountLabel`, `BomberCountLabel`, `AccuracyLevelLabel`, `EvasionLevelLabel` are
`FloatExtensions.ToString(...)`; `PlatingAbsorptionLabel` and `ShieldAbsorptionLabel` are
`.ToString(0, percentage: true)`. Every tooltip the panel sets on them is a
`%…Description` **sentence** (`:1147,1149,1151,1153,1181,1184`).

**But** the localization holds prefab-only Title keys that can only be the drawn captions of
this very table — `%CategoryWeaponKineticTitle` = "Kinetic" (and Missile/Laser/Beam, on
GuiElements `CategoryModuleWeaponKinetic…` at
`Public\Gui\GuiElements[Categories].xml:1009-1031`), `%AccuracyTitle` = "Accuracy",
`%EvasionTitle` = "Evasion", `%HullPlatingAbsorptionTitle`, `%ShieldAbsorptionTitle`,
`%ShipStatMilitaryPowerBalanceTitle` = "Projectile-Energy Balance", `%ShipStatBonusesTitle`
— none of them appear in the decompile. So most of these rows probably DO draw a caption
sibling, which `SidePanels`' shape walk merges into the row's line already
(`SidePanels.cs:250-280` emits the group when its children are all primitives).

**Action for the fix stage: measure the table before touching it.** Where a caption is
drawn, adding one would double-name ("Kinetic: Kinetic 40"). The rows with **no Title key at
all in the game's strings** — and therefore the real candidates — are:
`ShieldCapacityLabel`, `PlatingHealthBonusLabel`, `FighterCountLabel`, `BomberCountLabel`
(only `%ShieldCapacityDescription`, `%PlatingHealthBonusDescription`,
`%ShipStatFightersCountDescription`, `%ShipStatBombersCountDescription` exist). For those
the honest options are the drawn caption if there is one, else the description's first line
as a last-resort name (the `Cells.Control` / `CardActions.FirstLine` pattern,
`UI\Cells.cs:66-71`) — never a mod paraphrase.

### B2 — the two balance gauges in the ship designer

`UI\ShipDesignRows.cs:796-842` declares each gauge with `GraphNodes.Readout(() => null, () =>
GaugeText(it), …)` — an explicitly **nameless** node whose value is `"62%, 38%"`. That is
deliberate and documented (`:789-795`: the two halves' meaning is the sentence on the
gauge's own tooltip). The caption does exist in the game's strings, prefab-authored:
`%ShipStatMilitaryPowerBalanceTitle` = "Projectile-Energy Balance". If that label is drawn
above the gauges the walk already reads it as its own line and nothing is needed; if it is
not, **M1** with that key is the fix. Measure.

### C1 — the economy screen's resource strip speaks two unnamed numbers

`Screens\EconomyScreen.cs:670-690` names the row correctly (**M4**, wrapper title) and then
adds `ValuePart(AgeText.Label(it.StockLabel))` and `ValuePart(AgeText.Label(it.NetLabel))` —
two adjacent bare numbers with nothing saying which is the holding and which is the per-turn
change. `Screens\GlobalHud.cs:1899-1906` already has `StockAndNet` / `ModStrings.GalaxyStockAndNet`
for exactly this pair and uses it at `:506` and `:690`. Same fix for
`Screens\EconomyScreen.cs:1035` (salable items) and `Screens\RecipeCreationScreen.cs:282`.
Low severity, cheap, and it makes three screens read alike.

### C2 — the colony panel's security line uses a mod word

`Screens\SystemManagementScreen.cs:1123-1131` captions `panel.SecurityValue` (`"240/240"`,
`ColonyInfoSidePanel.cs:551`) with `ModStrings.Get(ModStrings.SystemSecurity)` — a
mod-authored caption where the convention (and `localization.md`) is the game's own word.
Worth one lookup during the fix stage for a `%…Title` on the defence property (**M3** on
`SimulationProperties.StarSystem.Defense…`); if the registry has no title, the mod word
stays and this row is fine.

### C3 — marginal, listed for completeness

- `Screens\SenateScreen.cs:762-773` (`BoostText`) — `PopulationCensusArc.PopulationBoostLabel`
  is `RemainingTurns + "[turn]"` (`PopulationCensusArc.cs:114`) → **M7** self-names it as
  "3 Turn"; the other branch (`:124`) is a resource symbol string, which resolves to the
  resource's Title. No action.
- `Screens\GlobalHud.cs:771` — `AddValue(cells, "motherships", …, property: null, …)`: the
  mothership count is named from its tooltip's first line because the property has no
  GuiElement (already measured, per the `PropertyTitle` doc comment). Documented fallback,
  no action.

---

## 3. Sites checked and found CORRECT (do not churn these)

Named because the fix stage should not "fix" them, and because each is a worked example of a
mechanism above.

- **Scan view, system lens FIDSI** — `Screens\ScanViewScreen.cs:497-510` reads only
  `label.ValueLabel` and looks broken, but is not: `ScanViewSystemOverviewFidsiLabel.BindValue`
  (`:72-91`) writes `value + " " + ColorizeText(element.SymbolString, …)`, and those symbol
  strings are `[prestige]`, `[science]`, `[dust]`, `[industry]`, `[foodColored]`,
  `[population]` (`Public\Gui\GuiElements[GameVariables].xml:990-997,624-631,460-467,266-273,1225-1232,2071`),
  every one of them present in `Core\Speech\IconTable.cs` → **M7** yields "+12 Influence".
- Scan view planet lens outputs — `Screens\ScanViewScreen.cs:660-690` (**M3**).
- Planet overview FIDSI — `Screens\PlanetOverviewScreen.cs:378-420` (**M3**).
- Galaxy HUD orbital card FIDSI — `Screens\GalaxyHudScreen.cs:1358-1400` (**M3**).
- Empire banner resources / relics / lifeforce — `Screens\GlobalHud.cs:490-519,975-1020` (**M3** + **M10**).
- Hero cards — `UI\HeroCards.cs:225-291` (**M1**, the `%HeroCard…Title` set).
- Ship / fleet / power tooltip features — `UI\TooltipFeatures.cs:460-582` (**M2**).
- Ship designer costs and stocks — `UI\ShipDesignRows.cs:351-408` (**M4**, with the
  "the tooltip IS the name, so don't say it twice" guard at `:400-404`).
- Ship designer module-category toggles — `UI\ShipDesignRows.cs:517-535` (**M2**).
- Military screen and hero inspection overview boxes — `Screens\MilitaryScreen.cs:774-779`,
  `Screens\HeroInspectionScreen.cs:527-545` (**M1**).
- Fleet list rows — `Screens\FleetPanel.cs:632-670` (**M1**).
- Battle report damage totals — `Screens\AdvancedBattleReportScreen.cs:406-425` (caption key).
- Ground battle contenders and troop cells — `Screens\BattleNotifications.cs:415-470` (**M1** + **M4**).
- Election representatives — `Screens\ElectionScreen.cs:628-668` (**M4**).
- Senate census total — `Screens\SenateScreen.cs:694-716` (**M6**).
- Government running totals — `Screens\GovernmentScreen.cs:332-360` (**M4**).
- Improvements upkeep, laws upkeep, negotiation costs, custom-faction point pools, war
  exhaustion, fighter distribution — all **self-naming in the game**: the label is written
  with `Gui.Localize("%…Title", value)` (`ImprovementsManagementModalWindow.cs:80`,
  `ActiveLawsPanel.cs:144`, `NegotiationModalWindow.cs:1191`,
  `CustomFactionTraitsSelectionPanel.cs:399,413`, `BattleReportNotificationWindow.cs:325`,
  `AdvancedEncounterPlayModalWindow.cs:562`). Nothing to add.
- Election money/influence, colony upkeep, hero upkeep, election prestige — self-naming via
  appended icon tokens (`ElectionBeforePanel.cs:168,170`, `ColonyInfoSidePanel.cs:565`,
  `ImprovementsManagementModalWindow.cs:90`, `ShipDesignEditionPanel.cs:735`) → **M7**.
- Anything inside a `GuiTable` — `UI\TableSheet.cs` (**M8**).

---

## 4. The docs question

### What the generic docs say today, nearest first

`docs\generic\tooltips.md:121-125` — the closest thing to a rule, and it is about the
*source*, not the obligation:

> - **Captions for bare numbers come from the game's registries.** When a drawn value's only
>   name is a static icon, ask the game's element/property registry for its localized title
>   before inventing a mod word. Hazard: the registry can point at a translation key that no
>   longer exists — a title lookup needs the engine's naming-convention fallback and must
>   degrade to silence, never to a raw key.

`docs\generic\widgets.md:125-128` — the Charts/value-only line the brief asked for:

> A drawn graph — bars as clipped rectangles, gauges as fill ratios — often carries no text
> at all. Read the encoded values off the drawn geometry (fill percentages, clip heights),
> announce the non-trivial series in one line, put every series in the review buffer, and
> take the series names from the model's own ordered list: the bars themselves name nothing.

`docs\generic\making-screens-accessible.md:167-171` — the rule as a **verification** bar,
with this exact number in it:

> the pair's spoken half is judged **as a listener hears it**, not as a match: a number
> without its caption in the same line, or a tooltip feature answered by the fallback
> reader, FAILS the check even though it matches the pixels perfectly — "1500/1500" beside an
> unnamed icon satisfies spoken-equals-drawn and tells the player nothing. Matching is
> necessary; comprehensible is the bar.

Also relevant: `making-screens-accessible.md:47-49` ("the words are the game's words … never
a mod paraphrase"), `:28-30` (a caption over exactly one control folds into that control's
readout), `reverse-engineering.md:52-56` (the per-class XML/data registry is
"a chokepoint-adjacent closed set worth checking before concluding 'the game never names
this'"), and `tooltips.md:76-84` (reading by geometry across assembly units "divorces values
from their captions"; the Nth-title-to-Nth-value grid rule).

### Verdict: derivable, with one genuine and cheap gap

The rule **"a widget whose drawn text is a bare value must be named from the caption the game
keeps elsewhere"** is already derivable: §4 forbids shipping it, tooltips.md:121 says where to
get the caption, and reverse-engineering.md says to check the registry before giving up. The
audit's own evidence supports this — the mod applies the rule correctly in ten different
places, which per the generic-docs bar is evidence the rules work, not evidence of a gap.

Two halves are genuinely absent:

1. **It is stated only as a verification failure and a source hint, never as a modeling
   obligation** — nothing in §0 (where the model is decided) or in widgets.md (where value
   widgets are specified) tells the implementer to *ask, at declaration time, whether this
   node can speak a bare number*. Both A1 and A3 shipped from screens whose authors clearly
   knew the rule (their sibling helpers apply it) and simply did not ask the question for the
   band in front of them.
2. **"The tooltip is not the name" is nowhere.** tooltips.md strategy 1 says a short tooltip
   joins the announcement as a trailing part; that is what makes A1/A3/A4 *sound* almost
   informative ("1500, The total health of this ship") and is exactly why they survived
   review. Nothing distinguishes a tooltip that *explains* a value from a caption that
   *names* it — and the ES2 evidence is sharp: every one of these values has a
   `%…Description` sentence and a separate `%…Title` word, and the mod needs the Title.

**Cheapest form — tighten the existing tooltips.md:121 bullet rather than add a rule.**
Proposed replacement for that bullet's first sentence, with one added clause and one added
sentence (net +2 lines):

> - **Captions for bare numbers come from the game's registries — the tooltip is not the
>   name.** A node whose drawn text is only a value (a number, a percentage, a fraction) must
>   be named at declaration time, from the caption the game keeps elsewhere: a drawn caption
>   sibling, the element/property registry's localized title, or the game's own
>   `%…Title` string for that statistic — never the tooltip's explanatory sentence, which is
>   a gloss the game wrote *in addition to* the word, and never a mod paraphrase. Ask the
>   registry before inventing one. Hazard: the registry can point at a translation key that
>   no longer exists — a title lookup needs the engine's naming-convention fallback and must
>   degrade to silence, never to a raw key. A tooltip's first line is a last-resort name only
>   where the game has no title at all, and then the tooltip must not also be announced.

**Placement:** stays in tooltips.md, because that is where a reader arrives when they think
"this value's meaning is in its tooltip" — which is precisely the wrong turn this bug is.
A one-clause cross-reference from `widgets.md`'s Charts paragraph ("the same naming rule
applies to a *textual* value with no caption — tooltips.md") is the only other candidate, and
is probably rejectable as duplication.

**What I would NOT add:** anything to `making-screens-accessible.md` §0 or §4 — §4 already
states the bar with this exact example, and a second statement of it would be the third copy.
