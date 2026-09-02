# The empire-wide screens

Research, quests, the senate family, the economy, the military and design screens, heroes and
the tables they share.

## The technology wheel

**Working the technology wheel.** Open/close it from `/eval` with
`Gui.GuiService.GetWindow<GameOverlayWindow>().ControlBanner.ToggleScreen("TechnologyScreen")`
(F4 does the same); the first open in a session raises the "Tech Savvy" tutorial popup. The
permitted round trip is queue-then-cancel — probe with
`Gui.PlayerEmpire.GetAgency<DepartmentOfScience>().ResearchQueue.Length` and
`.PendingConstructions[i].ConstructibleElement.Name` before and after — but queueing fires
`EventTutorial_TechnologySelected`, so do it LAST (cancelling restores the QUEUE, not that event's
own effect on the tutorial — `POST /loadsave` is the only restore for it).
**The queue round trip's route is FIVE injections, and the first Enter does not queue** (measured on
`[Beginner] test` turn 21, whose research queue is empty): `ui.prev` to the `research:suggested`
stop, `ui.down` onto a cheap technology, then Enter — which **JUMPS to that dot** rather than
queueing, `research:suggested`'s activate being `Jump` — and it is the SECOND Enter, on the dot
itself, that queues and says *"Queued …"* on top of the dot's own state word flipping to "In
progress". "Survival Suits" and "Ubiquitous Surveillance" are the fixture's two available Military I
dots (195 Science each); "Military II" expands to four "Not available" dots, the ready-made research
REFUSAL controls. Restore by cancelling what was added — `ResearchQueue.Length` back to 0, measured
2026-08-19, no `POST /loadsave` needed.
**The queue region must grow a row per queued technology** (`research:queue/<tech>`, "position 1,
N turns remaining", after `research:queue-title`) the moment one is queued, and read
`research:queue-empty` only while the game draws its empty-queue label. The label is a wired prefab
field, so `empty != null` is always true — the branch is chosen by `AgeWidgets.Visible(empty)`
(fixed 2026-08-29: the existence test made every queued technology unreadable while the empty
wording still read fine, the wired-prefab branch-chooser class).
**Cancelling a technology that has PROGRESS** (measured 2026-08-22, and it works on both routes):
queue a cheap dot, `ui.endTurn` TWICE (this save has an idle system, so the first press only raises
the game's own prompt), then reopen the wheel and press Enter either on the wheel dot or on
`research:queue/<TechnologyDefinition…>` in the `research:status` stop — reached by Tab to that
stop, `ui.home`, `ui.down` twice, NOT by `ui.end` (which lands in the `research:region/key` band).
Both answer *"Cancelled ⟨title⟩"* with `ResearchQueue.Length` dropping to 0, and the dot's own
state word then flips to "Available". `TechnologyItem2.Dragging` reads false throughout. Cancelling
REFUNDS the science, so the dot reads its full cost again. Ending turns raises notification popups
that steal focus — hide them with a `FindObjectsOfType<NotificationWindow>()` loop over `Shown`
before reading the wheel, or the next injection goes to the popup. Restore with `POST /loadsave`
(the turns cannot be given back).
**Proving the queue row's REFUSAL branch** (the never-be-silent guarantee — the state is not
reachable in play): queue a dot, focus its queue row, then race a background
`ResearchQueue.Remove(PendingConstructions[0])` `/eval` against a `POST /input ui.activate` ~0.3 s
later. The row is still drawn while the queue no longer holds the technology, so Enter answers with
the technology's state (*"Available"*) instead of the "Cancelled" it can no longer honestly say.
**Searching the wheel by what a technology UNLOCKS** (2026-08-22): Tab onto the `research:tree`
stop first — `POST /type` searches the FOCUSED stop, and from `research:status` the same letters
find nothing. Then `POST /type "surv"` → 2 results, Survival Suits first (title match);
`"Impervious"` → 1 result, Survival Suits (its unlock "Impervious Bunkers"); `"Miners Union"` →
Galactic Commodities Exchange; `"Zelevas Incarnate"` → Emperor's Shadow. `ui.down`/`ui.up` step,
`ui.back` says "Search cleared". The per-keystroke cost is
`ES2Access.Screens.ResearchScreen.SearchTextBuilds`: it must reach 107 (one per drawn dot on this
fixture) and then STOP rising, however many letters follow — it climbs by another 107 only after a
turn change or a `POST /loadsave`, which is the cache expiring. To read a technology's whole search
string, invoke the private static `ResearchScreen.BuildSearchText(GuiTechnology2)` by reflection
over `TechnologyQuadrantsContainer` → `TechnologyQuadrantItem.TechnologyStagesContainer` →
`BaseTechnologyStageItem.TechnologyItems` (the stage's dots are NOT its `Children`).
**Driving the game's own "go and look at this technology"** (the Ctrl+click hint jump, which no
injection can reproduce because the game reads the physically held Control): call what the game
calls — `var w = Gui.GuiService.GetWindow<TechnologyScreen>(); w.FocusTechnology(
Gui.GuiWrapperProviderService.GetGuiTechnology2("TechnologyDefinition<Quadrant><n>"));
Gui.GuiService.ShowWindow(w);` — from the galaxy AND with the wheel already open on some other dot.
The mod's cursor must land on that technology (`ResearchLocate`); a plain reopen afterwards must
still restore the remembered position. In **`unlocked`** the tech screen binds "A MATTER OF
INFLUENCE" on EVERY open (not just the first) and the popup arrives expanded and takes the keyboard,
so each of these steps ends with the minimize replay before the landing can be read — the locate is
HELD, not lost, while the popup is up. Closing the window unbinds the tutorial again.
**The transition round trip** (the route that lost the cursor to the tutorial bar): from the star
system page, park the cursor on a real node, `w.FocusTechnology(…); Gui.GuiService.ShowWindow(w);`,
then close with `ControlBanner.ToggleScreen("TechnologyScreen")`. It must come back on the node it
left. Watch the frames with a `POST /wait` on `ES2Access.Dev.DevProbe.Trace("tag")` and
`GET /log?since=0&grep=trace` — the frame to look at is the one where `screen.star-system` is active
with a node count in single digits.
**Where the wheel's first open lands** (re-check after any `IsActive` change): `POST /reload` for a
fresh navigator state, then open — measured landing is `research:tree`, "Technology tree, Military,
group, … 1 of 4". A 115-frame `POST /wait` across an open found NO frame where the window is
`Shown && IsReady` while its transform is disabled, so this screen has no readiness race to gate on.
**Link arcs in `unlocked` (turn ~15)**: 22 of 162 drawn — cost-reduction pairs (Xenobiology →
Machine Bacteria / Eukaryotic Sap, and Eukaryotic Sap on to Wave Function Control / Graviton
Research, which is the one dot that reads an arc from BOTH ends) and four exclusions (Tensor
Algorithms ↔ Advanced Game Theory, Mineral Manipulation ↔ Optimized Logistics, Advanced Fusion Power
↔ Hyperium Magnetics, Orichalcix Alignment ↔ Programmable Quadrinix). **Dependency arcs
("Unlocks"/"Unlocked by") are drawn in neither fixture** — offline-tested only.
**An expanded STAGE declares two regions of its own (2026-08-27).**
`research:stage-unlock/<StageDefinition>/actions` holds the ring's deed and its technology dots;
`…/tooltips` holds one node per improvement or module the stage unlocks. Route from the tree stop's
landing: `ui.right`, `ui.down`, `ui.right` (which lands inside **Military II** on
`[Beginner] access test` turn 32), then **Alt+Down** (`ui.regionNext`) from any technology jumps
straight to the second region — spoken *"Tooltips, Upgraded Coupled C5AI, 1 of 8"*. Military II's
eight are Upgraded Coupled C5AI, G-War Camps, Basic Reactive Plating, Basic Uniform Shielding, Basic
Pinch Beam, Basic Ultradense Slugs, Basic Sync Laser, Basic Fusion Torpedoes (Military I draws seven);
the ids are `research:stage-unlock/TechnologyStageDefinitionMilitary2/tooltip/<i>`, keyed off the
stage DEFINITION so they survive a rebuild. Each node's `DevProbe.Tooltip()` must read `shown:true`
with the unlocked thing's own class (`EmpireImprovement`, `ShipModule`) and its buffer the full
dossier — the MODULE case is the one that proves the point, carrying a `Cost: 39 Industry` line the
stage's own `TechnologyStage` tooltip has not got. **The caveat that catches a tester out**: those
icons are never chain-visible at any wheel zoom but the outermost (the container's `Visible` follows
the stage name group — `docs/research.md`), so the mod filters on the ICON's own `Visible` and aims
resolved tooltips at undrawn icons, and `DevProbe.TooltipParity()` reports every COLLAPSED stage's
icons as `uncovered` BY CONSTRUCTION. Read a parity run here as a shape check, not a delta: no
`promised`, no `misaimed`, no `undescribed`, and the 141 `uncovered` are the locked-stage
`UnlockProgress0xx` markers (52), `DeedItem2` (12) and the 23 collapsed stages' icons. Regression to
keep in the same walk: a technology DOT's own unlock children are untouched
(`ui.regionPrev`, `ui.right` → *"Tooltips, Electromagnetic Shield, 1 of 2"*,
`research:technology/TechnologyDefinitionMilitary3/tooltip/0…1`).
**Blocked in the beginner fixture (last checked turn 2)**: dependency links (only the Juggernaut
chain has them and the fixture draws none), Disabled technologies and their failure reasons,
buyout, a queue long enough to scroll, and a deed that has been WON or LOST — all 12 drawn deeds
are in progress, so "Locked" and "Available" read live (the latter with its whole `DeedDescription`
in the buffer) while Completed, Failed and "won by ⟨empire⟩" are unit-tested offline only. Add to
that list a stage whose new `actions` region also holds a DEED node: Military II draws no readable
deed, so that region has only ever been walked with technology dots in it.

## The pinned quest

**Round-tripping the pinned quest** (how both halves of the `hud:quest` passive announcement get
proved in one run): stash the quest first — `Quest __pinned = Gui.PlayerEmpire.GetAgency
<DepartmentOfInternalAffairs>().QuestJournal.ActiveQuest;` — then unpin through the mod's own node
(`/input ui.down` onto "Unpin quest", then `ui.activate`) and read `/speech` for "No quest is pinned"
plus a `/gui/graph` with no `hud:quest` stop; put it back with `…QuestJournal.ActiveQuest = __pinned;`,
which is the same assignment the journal's own pin toggle makes (`NarrativeScreen.cs:443`) and
answers with "Pinned quest: …". Opening the journal from the panel node is safe and reversible:
`ControlBanner.ToggleScreen("NarrativeScreen")` closes it again, and the stop comes back with the
cursor still on it. **Unverified in either fixture**: "Show location" (the turn-3 quest has no
marker, so the game hides the button), the numeric "(x/y)" progress branch, and a quest waiting on
an objective choice (which draws no progress word at all).
**Proving the quest half of the galaxy locate without a marker.** `unlocked` has six current quests
and all six report `GetMarkers(step).Count == 0`, and `GuiManager.ShowQuestLocation` makes NO
position request when there are none — so nothing lands. Make the pair of calls the game would make,
in ONE `/eval` statement so they share a frame: `RequestGalaxyOverviewViewLevel(<a system's
position>)` then `ShowQuestLocation(quest, quest.GetCurrentStep())`. `GalaxyLocate.RememberQuest`
attaches the quest to the still-fresh position request exactly as a real marker would, and the
landing speaks *"⟨quest⟩, objective shown on the map"* before the place's own readout. Enumerate the
quests with `Services.GetService<IQuestRepositoryService>().GetCurrentQuests(Gui.PlayerEmpire.Index)`
(cast each item to `Quest`; `Quest` has `State` and `GetCurrentStep()`, not `QuestState`/
`QuestStepInProgress`).

## The quest journal

**Working the quest journal.** Open/close from `/eval` with
`Gui.GuiService.GetWindow<GameOverlayWindow>().ControlBanner.ToggleScreen("NarrativeScreen")`
(F7 and Enter on the pinned-quest panel node do the same; the mod screen is polled, so all
three land identically). Switching the filter is reversible and is the game's own radio group —
`ES2Access.UI.AgeWidgets.Toggle(w.QuestSelectionTogglesTable.Children[i].AgeControl as
AgeControlToggle)`, with `w.QuestFilteringRadioGroup.CurrentSelection` the before/after probe
(fixture: 0 = Current). **The turn-3 fixture draws exactly ONE card under every filter** — the
journal holds 40 in-progress and 13 completed quests and all but one are `QuestDefinition.Hidden`
or narrative events (`NarrativeScreen.cs:279`), so multi-card list navigation and the strip's paging
follow have no fixture at all. The Failed filter draws none, which IS the testable empty case: the
`quests:list` and `quests:detail` stops both disappear. **The pin is a child node of the card**, not a
gesture on it: `ui.right` opens the card, `ui.down` lands on "Pin Quest", Enter toggles, and
`QuestJournal.ActiveQuest` is the probe; unpinning speaks "not checked" from the toggle and "No quest
is pinned" from the HUD's watcher, even with the journal covering the panel. The alternate click
(Ctrl+Shift+Enter) on a card is
now silent by design (the game has no modified click there). **Also unverified at turn 3**: the
Show-location marker (the quest has none, so the game hides the button), the minor-faction button,
the podium a cooperative quest gets instead of a reward table, and the "Pending objective choice…"
placeholder.

## The senate family

**The senate family** (senate, government, laws, population). Open it from `/eval` with
`ControlBanner.OnControlBannerToggle`; reach the modals through the mod's own nodes. The government
modal also opens directly — `Gui.GuiService.ShowWindow<SenateScreen>()` then
`Gui.GuiService.ShowWindow<GovernmentModalWindow>()`, closed with `HideWindow` — which is how its
Validate button's hint state is probed (`Gui.IsHintActive(w.ValidateButton)`; ES2 facts says why it
reads false here). **NEVER press
Validate, Pass, Abolish, a boost, or Assimilate.** The selection resets on every show, so nothing
carries between visits. Expect a ~1 s `unavailable` on the page under a just-closed modal — that is
the game's fade, not a defect; re-read. **Save-blocked**: the gene hunter, assimilation, relics, a
real election, an enabled Abolish, a drawn history graph, an empty senator slot, and the outpost
panel.
Since the one-per-row rollout (2026-08-18) every band in the senate family is one node per row — a
`left ->`/`right ->` edge under a `senate:`/`government:`/`laws:`/`population:` stop is a
regression. Ten caption ids became level labels (measured live; the list is not preserved in the
repo); the words
arrive prefixed onto each block's first node. Regions to expect: `laws:detail/{law,effects,action}`;
`population:detail/affinity`, `population:thresholds`, one per captioned block,
`population:detail/assimilate`; `population:politics/{intro,traits,reactions}`.

**The senate's census badges and the forced-law badge.** Each `senate:census/arc/N` is now
an expandable group whose "Tooltips" region holds four nodes — the party the population leans
towards, what that gives the empire, the law it unlocks, and what the boost badge means. The three
dots are 8x8 pictures under `LabelsContainer/SubInfosTable/CollectionTable`; the fourth is
`PopulationBoostLabel`. `senate:law-slot/0` opens onto its forced badge's sentence. **Coverage reads
these as `unread` while the branch is COLLAPSED** (the probe's declaration side is the render as it
stands), so expand before believing a count.

## Population overview

**Population overview** (senate → census, or a star system's population row):

- `population:heading` still reads "Population Overview" and keeps its tooltip.
- `population:detail` — the first region is now named **"Imperials"** (the people's own name) with
  the lore paragraph as its only row; there is no "Imperials" ROW any more.
- The collection track: first node **"Collection status, 3"** (the caption plus the count, the
  caption's `%CollectionUnlockGroupDescription` in its buffer), then one node per threshold reading
  **"10 population, not reached"** / **"50 population, reached"**, with that bonus's effect lines
  reviewable but not spoken. Cross-check the state against the drawing: `.\crop-shot.ps1` the track,
  and a bright circle must be a "reached" node.
- **"Collection Effects"** must read the game's **"No Effects"** where the block is empty, and must
  NOT read the ghost row ("Militarist" at alpha 0 — the defect this gate fixes). Prove it with an
  `/eval` walk printing `Alpha` beside `Visible` for `CollectionEffectsTable.Children`, and a crop
  of the same rect.
- `population:politics` — the reactions region is now named **"Reaction to Political Events"** (said
  once on arrival, again on the Alt+Down into the ring), the six sector rows are unchanged, and a
  **"Tooltips"** region follows them with one node per party dossier off the legend
  (`PoliticsLabelsTable`). Each must DRAW its dossier on focus (`DevProbe.Tooltip()`).

## The empire page

**The empire page.** The interactive cells are columns 1/2/4/11/13. Nothing closes an opened band
except leaving the page. The tab switch and the panel instances are both probeable from `/eval`;
`SidePanels`' `PanelTitle` branch first got exercised here. `ui.end` does not move inside a
`GraphSheet` row (it answers consumed and speaks nothing) — walk columns with `ui.right`.

**The planets panel under the table** (Enter on a row's Population cell opens it; the stop is
`empire:detail/planets`). Since 2026-08-29 a card's population is a row per SLOT in bands, exactly
like the star system page's cards and off the same shared arithmetic
(`PopulationMoves.Slots`/`Carried`) — it was a row per AFFINITY with the count said, which answered who
lived there and nothing about how much room there was. The regression evidence is a pair of dumps:
before, one row per affinity ("Imperials, x 4"); after, "Slot 1 of 8, Imperials" … "Empty slot 8 of 8"
in "Population"/"Overpopulation" regions, with the drag hints counting down inside each run and
stating the count even at one ("x 1"). The DROP targets are those slot rows and not the card — an
empty non-locked slot takes the plain add, an occupied one the swap, and the card's group header
carries no `DropKind`, so mid-carry it must show no "drop target" while its slots do.

**Shipping population to another system** is the Population CELL of another system's row, and only
rows the game would ship to advertise: not this system's own, not an Outpost (`State != Colony`),
only while the source system's spaceport can create a ship, **and only while that spaceport has FREE
ROOM** — the people board it before any ship leaves, so a FULL port ships nobody however much room
the destination has. That last clause asks the same numbers the drop's clamp uses
(`PopulationMoves.IntoPort`), which is the point: it was missing until 2026-08-29 and the owner met
the divergence at once — every colony row said "drop target" and every Enter answered "cannot go
there". **The regression check is one dump with a FULL source port: zero "drop target" and zero drop
hints on the whole page.** Free one slot (drop a unit onto a planet) and the same dump must bring
them back, with the ship then succeeding. Carry a run off a card, dump `/gui/graph?buffers=1`, and
read which `...c2` cells carry "drop target" — that list IS the check.
Note the press is still not gated by that word: `CarryActions.Activate` deliberately asks only
"right kind of drop target" and lets the target's own refusal speak (`docs/interaction.md`), so
Enter on a row that does NOT advertise still answers the mod's generic "⟨thing⟩ cannot go there"
rather than falling through to the cell's own click. Prove a fixed gate by the ABSENCE of the word
and the hint, never by pressing.
The drop reports what will really board, not what was carried, because the order clamps against the
source spaceport's free room: carrying four into a capacity-three port answers "Sent Imperials x 3 to
Ita by spaceport" and the port probe reads 3. It also sets the spaceport's destination as the mouse
does, so probe `Spaceport.CurrentDestination` before and after if the fixture's destination matters.

**Opening the EMPIRE screen raises a tutorial page in a tutorial save** ("Snapshot Of An Empire"),
which takes the mod's focus (`screen: screen.tutorial`) and has to be re-minimized afterwards. The
HUD button for it reads "This functionality is disabled during this part of the Tutorial" in that
save, so the empire screen is fixture-blocked there for anything but a forced `ShowWindow`.
**`[Beginner] test` (turn 21) is one of those saves** — measured 2026-08-29:
`ControlBanner.ToggleScreen("EmpireScreen")` (which is what F1 does) leaves the stack unchanged and
the HUD button reads "unavailable" with that same sentence. The two saves that DO open the page from
the player's own gesture are **`[Beginner] access test`** (turn 32, FOUR colonized systems — Dusay,
Primus, Ita, Sabel — the one to use for anything that needs a row swap) and **`unlocked`** (turn 1,
one system, Xiu).

**The panels a cell slides out, and their announcements.** The four openers and what each says
(measured 2026-08-29, phrases re-measured 2026-08-30, on `[Beginner] access test`, all through
`POST /input`): the STATUS cell (col 1) and the POPULATION cell (col 2) both open the planet-cards
panel but in the game's two DIFFERENT card modes (`PlanetCard.DisplayMode`, picked by which cell
was clicked in `StarSystemsManagementPanel.OnLineSelection` :311-318) — status is Actions mode with
the colonize/specialization/terraform buttons on each card, *"Planet actions panel open for
⟨system⟩"*; population is Population mode with the slot rings and NO action buttons at all
(`PlanetActionsTable.Visible` false), *"Population panel open for ⟨system⟩"*;
the CONSTRUCTION cell (col 11) opens the game's constructibles and queue panels together and is ONE
announcement — *"Construction panel open for ⟨system⟩"*; the HANGAR cell (col 13) — *"Hangar panel open
for ⟨system⟩"*. Walk the columns with `ui.right` from the row node (`ui.home` goes to the HEADER row,
not to column 0, and Enter there sorts the table). The HERO cell is col 4, and Enter on it opens a
modal — close it with `Gui.GuiService.HideWindow(Gui.GuiService.GetWindow<HeroSelectionModalWindow>(false))`,
because `ui.back` does not.
Swaps say only the NEW opening: stepping down the rows with the Status column held gives *"Planet
actions panel open for Ita"*, *"… Primus"*, *"… Sabel"* one line each, and Construction → Hangar on one
row gives just the hangar line. A status↔population swap on the SAME row also announces (measured
2026-08-30: *"Population panel open for Dusay"* then *"Planet actions panel open for Dusay"*) — it was
silent until then because one Detail member covered both card modes, which was the defect that hid
the specialization button: a player who opened the panel from the population cell had no way to hear
that the status cell's panel is a different one carrying the planet action buttons. The **close** line
needs a gesture that leaves the page standing: pressing the
already-selected **Systems tab** re-shows the table panel, whose `GuiTable.BeginShow` nulls the
selection and hides everything under it — *"Hangar panel closed"* / *"Construction panel closed"* /
*"Planet actions panel closed"* / *"Population panel closed"*. Leaving the page, and opening the hero modal, both pop the mod screen first,
so neither says a close line; arriving says nothing either (the watch baselines on push).

**The specialization list from a planet card** (`screen.planet-constructibles`, layer 20, over the
empire page at 15). Route: STATUS cell only (Actions mode is the one that draws the card buttons;
the population cell's mode has none) → Tab to `empire:detail/planets` → `ui.right` to
expand a card → Enter on its "Click to select a specialization Improvement…" button. **No fixture
save has an ENABLED one as saved** (a session advanced past the right research does get one —
measured live 2026-08-30, turn 37 off `[Beginner] access test`: Raia's button enabled, the list
offering "SPIN Project" for real): `PlanetCard.RefreshBuildInfrastructureButton` draws the button
only for a colonized planet of a Colony-state system and enables it only when
`DepartmentOfIndustry.GetAvailableConstructibles(planet, PlanetImprovementDefinition, Discard)` is
non-empty, and every planet in every fixture answers "No relevant or available construction is
available" (Terraform needs `TerraformationGameplayUnlocked`; Reduce Anomaly needs a reduction the
empire has researched — Primus I has the anomaly and not the tech). Force it with
`card.BuildInfrastructureButton.Enable = true` and press through the mod's own `ui.activate`: the
screen takes over and names itself *"Select a specialization"*. The list is then EMPTY — the panel's
own refresh reads the same `Discard` query the button does — so this route proves the OWNER LOOKUP
and the close path, never the rows.
The negative control for the lookup, one eval, taken with the panel up:
`PlanetLabelsWindow_SystemOrbital.ConstructiblePanel` and `PlanetLabelsWindow_SystemManagement.ConstructiblePanel`
both read `Shown=false, Planet=null` while `EmpireScreen.StarSystemsManagementPanel.StarSystemPlanetCardsPanel.ConstructiblePanel`
reads `Shown=true, Planet=Raia, Client=PlanetCardsPanel` — which is exactly what a two-owner lookup
answers null on. Escape closes it through that Client and hands focus back to the empire page's own
node; the panel watcher stays silent across the whole round trip (the cards panel never left).
The same forced press on the system-management route is the regression check: its cards are
`PlanetLabel_SystemManagement` (not `PlanetCard`), reached under
`GetWindow<PlanetLabelsWindow_SystemManagement>()`, and the node is `system:planet/<id>/action/0` in
the `…/actions` region — two `ui.regionNext` jumps from the population region, not `ui.end`.

## The economy page's side panels

**The Inflation panel's explanation hangs on the panel ROOT** — `AgeTooltip` with
content `%DustInflationDescription` and no class, on the `InflationSidePanel` transform itself, with
no tooltip anywhere inside it. `SidePanels.Readouts` starts its walk at `ContentGroup` and its
heading path only fires for a panel that DRAWS a heading, which this one does not, so the sentence
was declared by nothing and `DevProbe.Tooltip()` answered `shown:false` on the panel's only line
until `SidePanels.Explains` was added (2026-08-30). Census that justified putting it in the shared
reader: of **all 31 side panels the game instantiates, exactly one** carries a root tooltip, and it
carries no other. The gate is "the panel's first line has no explanation of its own", so a panel
whose heading already carries one is untouched — verified on the academy screen, whose two panels
both keep their own title tooltips.

**The tooltip audits cannot see any of this.** `TooltipParity`/`Coverage` walk `Screen.RootTransform`
— the EconomyScreen window — and the side panels live in a different window (`SidePanelsWindow`), so
they were never in the painted set: parity read `clean` while the sentence was unreachable. Do not
take a clean audit as coverage of a screen's side panels; the check there is a focused
`DevProbe.Tooltip()` per panel line.

**"Dust Dust Inflation" is NOT an icon duplicating text** (measured 2026-08-30, contradicting the
first hypothesis). The two labels are `InflationDescription.Text = "%DustInflationTitle"` — a
localization key with no icon token, which localizes to the game's own two words "Dust Inflation" —
and `InflationValue.Text = "x1.11#E6C361#[dust]#REVERT#"`, whose dust icon is TRAILING and so is
kept by `LabelWithoutLeadingIcon` on purpose (a mid-sentence icon stands in for a noun). Applying
that helper changes neither string. The real defect is READING ORDER: `AgeWidgets.TextOf` walks
`Children` in collection order, which is Value then Description, while the game draws Description at
x=36 and Value at x=204 — so the mod says "x1.11 Dust" before "Dust Inflation" and the two Dusts
land next to each other. Reading in drawn order gives "Dust Inflation x1.11 Dust". NOT FIXED: the
sort would have to go into `TextOf`'s shared `Collect` (every caller in the mod) or into
`SidePanels`' leaf read (all eight `Readouts` callers), and neither blast radius was measured — it
is an open question for the owner, not a bug in the words.

## The economy page and the recipe modal

**The economy page and the recipe modal.** Which rows draw at all follows from the save being
screen-unlocked, not tech-unlocked (the per-screen gate table was measured live and is not preserved
in the repo) — which also means the **Marketplace tab is refused**
(missing Galactic Commodities Exchange), so the buy table has NO fixture in `unlocked` and the
resources grids are the only economy tables that can be walked. The recipe modal is reachable with zero slots via
`w.GuiRecipeSlot = new GuiRecipeSlot(0, false); Gui.GuiService.ShowWindow(w)`; close with
`HideWindow(w)`, and a RE-SHOW is the restore — `OnBeginShow` clears `currentRecipeIngredients`
and `RecipeModified` (measured 2026-08-19). **NEVER press Confirm** — it is enabled even with an
empty recipe and posts `OrderCreateRecipe` — and note Reset does NOT clear `RecipeModified`.
All four family grids are tables sharing one `ResourceGrid` reading since 2026-08-19 (luxuries 8
columns, strategics 6; "empty" holes, blank lines undeclared). The column captions read the economy
screen's short titles ("Industry"), not the family descriptions. **Since 2026-08-21 the families are
also a header ROW** — `<stop>/family/<i>`, one node per drawn icon, above the first line and inside
the sheet's `reg:0`; Tab still lands on the first data row and Up from a cell reaches its own
column's heading. Each heading carries the family sentence ("This family of resource improves Food
…") and no cell does any more, so a before/after of that sentence's count in
`/gui/graph?buffers=1` is the check: 132 buffer lines → 14, one per family, on each page.
Type-ahead here matches CELLS by their own words (the rows have no name): `POST /type "transv"`
from any column lands on `economy:luxuries/r0c5` and says "Approval, Transvine, 22, +2 per turn,
1 of 2" — a landing is never stepped sideways off, and "trad" lands on the family heading.
**Whether the strategics grids draw is `CanUseStrategicForRecipe` (above) plus a
`EconomyPanel.RefreshNow()`** — setting the property alone leaves `StrategicsGroup.Visible` false
until the panel refreshes.
**A 0×0 game window makes every grid degenerate to ONE column.** `AgeTransform.GetGlobalPosition()`
answers all-zero rects when `UnityEngine.Screen.width` is 0 (a locked or disconnected session), so
`ResourceGrid.ColumnOf` maps every item to column 0 and the luxury lattice reads as 12 rows of one
resource plus seven "empty"s instead of 3×8. Probe `UnityEngine.Screen.width` before believing a
layout measurement; the navigation model (8 headers, 8 columns, the column-paired seam) is still
fully exercised in that state, only the cell-to-column assignment is wrong. The modal's project stop draws ONE slot in `unlocked` (multi-slot strip
unmeasured). The two STRATEGIC grids draw only for an empire with Material Expertise — no save
here has it, but `SimulationProperties.Empire.CanUseStrategicForRecipe` DOES stick under
`SetPropertyBaseValue` + `Refresh(false)`: set it, the game draws the real grid itself, set it
back (verified round trip 2026-08-19).

## The marketplace tab

**No saved fixture researches it.** All three saves refuse the tab (missing Galactic Commodities
Exchange), so everything below was measured on the OWNER's live mid-session game at turn 27,
where it is researched but the marketplace itself is unbuilt and unowned. Its shape: 4 buyable
strategics, 6 sell tiles (3 with stock), Ships and Heroes sections tech-locked and refusing, taxes
reading "Not built / No owner / 10%", one transaction in the log. Re-measuring means asking the
owner for that session; nothing in `unlocked` reaches it.

**SEVEN stops** (restructured 2026-08-30 over two rounds — a trading panel is one BOX, Tab moves
between boxes, and the strip a trade is set up in is a box of its own):
`economy:market/sell` → `sell-transaction` → `buy` → `buy-transaction` → `history` → `taxes` →
`log`. The nine-stop shape that split each panel into tabs/rows/band is gone; grep for
`sell-band`/`buy-tabs` and you are reading a stale note. Each trading panel's drawn heading
(`PanelTitle`, found by `PanelName`/`PanelCaption`) is the first row of its own panel, and it is a
row rather than only a name because all four panels hang a sentence on it.

**The stops name themselves.** The two panels take the game's own Sell and Buy button titles
(`%MarketplaceScreenSellButtonTitle` / `%MarketplaceScreenBuyButtonTitle`) as a stop-level context.
The two transaction stops are named after what is being traded — "Selling Titanium" / "Buying
Titanium" (`economy.selling-what` / `economy.buying-what`), falling back to "Sell transaction" /
"Buy transaction" while nothing is picked. The buy side reads the name off the game's own
`SelectedBuyableNameLabel`; the sell side has no such label, so it finds the tile whose
`SelectionToggle.State` is true and names it through the SAME `SalableName` the tile is named by —
which is what keeps an unlocated luxury's real name out of the stop's title.

**Each trading panel is three regions**, walked with `ui.regionNext`/`ui.regionPrev`:
`<panel>/heading` (the caption, unnamed — it announces itself), `<panel>/filters` ("Filters", the
section radios as one row) and `<panel>/available` ("Available"). The **buy panel has FOUR region
targets, not three**: the sort headers sit in `economy:buy/available` and the `GraphSheet`'s data
rows get a region of their own (`economy:buy/reg:0`) because the sheet always sets one — the
"Available, table" title is pushed by the screen over both, so it is announced once, on the header
row the jump lands on, and the second jump into the rows says only the row. Expect
`Purchasable Items → Filters → Available, table + Name header → Titanium row`.

**The multiplier chords and their hints.** The game reads the physically held modifier inside its
own click handler (`MarketplacePanel.GetQuantityToAddFromClick`, `OnQuantityPlusCb` :368-379), so
Ctrl+Enter (`ui.selectToggle`) and Shift+Enter (`ui.selectRange`) replay the SAME press on the
sellable tiles, the buy rows and the strip's two steppers — five at a time and the whole stock. A
vtable with only `OnActivate` does nothing for those two actions; they are their own entries and
had to be wired on the steppers explicitly. Every one of the four surfaces ends its buffer with the
two hints (`hint.market-five` / `hint.market-all`), added in one place (`MarketChordHints`) and
gated on the control being offered, so a refusing tile says neither. **Injected actions carry no
held modifier**, so `POST /input ui.selectToggle` on Increment moves the quantity by ONE: that
proves the dispatch reaches the button, never the multiplier. The multiplier itself needs
`POST /key` with the game in the foreground and stays a manual-test line.

**A buy line carries NO tooltip of its own.** `GuiTableLine.Tooltip` is null on every line of
`BuyableItemsGuiTable` (measured 2026-08-30); the Resource-class dossier hangs on the drawn CELLS,
`ColumnName` included. `TableSheet.Explains` is the fallback that answers this — the primary cell's
own tooltip, for the row's sections and its pointer aim — and it fires only where the line's
tooltip was null or drew nothing, so every other table in the mod is untouched. Verify it with
`DevProbe.Tooltip()` on a FOCUSED buy row: `shown:true`, `class:"Resource"`, the header and
description features. In `/gui/graph?buffers=1` only the focused row shows those lines, because a
class-backed tooltip's buffer is empty until the tooltip window draws it.

**And the row is the ONLY place that dossier is offered.** `GuiTableCellBuyable` writes the same
class, content and target onto every buyable column (`GuiTableCellBuyableName`, `…Price`, `…Stat`
all extend it), so the name, the stock and the price used to promise the same sentences three
times. `TableSheet.CellVtable` now drops a cell tooltip that is the same SURFACE — matched by
`AgeWidgets.SameTooltip`, i.e. class + content + target, never by text — as the one the row's
primary declares (owner ruling 2026-08-30). The check on a focused quantity or price cell is a
buffer of exactly one line ("Price, 135") while `DevProbe.Tooltip()` on that same cell still shows
the drawn Resource dossier: the pointer aim is deliberately kept, so a sighted observer sees the
hover the mouse would have drawn while the blind player is offered the dossier once, on the row.
`DevProbe.TooltipParity()` stays clean through this because `NotificationAudit.Covering` matches by
ancestry — the row's primary node stands on the LINE, an ancestor of every cell — so the cell's
still-painted tooltip is covered and read by the row's node rather than counted uncovered.

**The buy list is a TABLE, the sell list is not** (owner ruling 2026-08-30, after a same-day
reversal). The game binds `BuyableItemsGuiTable` to the column set its SECTION declares —
`Public/Gui/GuiElements[Marketplace].xml`: three columns for the resource sections, ten for Ships
and Heroes — so the columns are the game's own data and `TableSheet.Rows` reads them generically;
nothing in `EconomyScreen` counts columns, and a ten-column section would inherit ten (UNVERIFIED:
both are tech-locked here). The sell list is pooled plain toggles with no column set at all, so it
is one resource per row (`Cells.EmitLinear`), walked with Up/Down; the dense wrapped strip and its
`GridRowKey` are retired.

**The price graph is a table of its own stop** (`economy:market/history`, shipped 2026-08-30),
declared AFTER the Purchasable Items stop rather than at its drawn position between the filters and
the table (owner-approved). Rows are the buy table's own lines in drawn order, cells are the plotted
value rounded to a whole number through `Gui.FormatAmount`, and columns are turns captioned with the
turn-log's own wording (`hud.turn-log-turn`, "Turn 27") over the DISPLAYED turn number (snapshot
turn + 1, which is what the game's X axis draws). **The columns run NEWEST FIRST** (owner ruling
2026-08-30): the first cell right of a row's name is the current price, and the walk goes back in
time — the reverse of the drawn axis, which runs oldest-left. Columns are trimmed to the earliest
snapshot any listed resource has inside the game's own window
(`SimulationProperties.Marketplace.TradableHistorySpanTurnCount`, 20 live), so a resource that
started later reads "empty" in the OLDEST columns. The oracle is the game's own data, not the
picture: index `GuiBuyable.History` from `/eval` (`turn + 1` and `Mathf.RoundToInt(value)`) and
compare cell for cell — on the turn-27 fixture that is Titanium 15–27 (20→43), Hyperium 16–27,
Antimatter 20–27, Adamantian 22–27, so column 1 reads "Turn 27, 43" for Titanium and column 2
"Turn 26, 41". Tab into the stop lands on the first resource row and says "Price history, table,
Titanium, 1 of 4"; Up from there reaches the caption row, whose buffer holds the panel's own
"Evolution of market value over time". When the game shows `NoDataAvailableGroup` the stop is that
one drawn readout instead (unsighted — the fixture always has data).

**The section radios drop their leading icon.** The game writes the category's icon into the label
and the engine's symbol registry names it, so "Titanium Strategics (3)" was really the Strategics
filter wearing the name of one of its own resources. `AgeText.LabelWithoutLeadingIcon` is what both
panels' filter rows use now: expect "Strategics (3)", "Luxuries (3)", "Ships (3)", "Heroes (1)".

**Focusing a history row also scrolls the buy table to that resource's line** — once per arrival,
never per frame, never on the way out, and only when the line is outside the viewport
(`EconomyScreen.Reveal` → the shared `ScrollIntoView.Reveal`, which returns before writing anything
when the gap is zero). There is no scroll-TO call to prefer over the wheel replay:
`AgeControlScrollView` is public only in `ResetUp/Down/Left/Right` and `MouseWheel`, and the
clamping, the scrollbar placement and the `OnScroll` message all sit behind the private
`ConstraintAndPlace` (`firstpass/AgeControlScrollView.cs` :290-306, :476+).
**This has never fired for real.** No fixture has a scrollable buy section: the four strategics are
171px of a 258px viewport, `IsVerticalScrollBarVisible` is false, and the view refuses to move —
`MouseWheel(-1)` and `MouseWheel(+1)` both leave `VirtualArea.Y` at 0, and a forced
`VirtualArea.Y = -130` is put back to 0 by the engine's next position recompute, so the
out-of-view state cannot even be staged. What IS proved here is the negative: walking all four
history rows leaves `VirtualArea.Y` at 0 with nothing in the log. A landing scroll needs a section
longer than its viewport (luxuries, 6 rows, still fits; Ships and Heroes are tech-locked).

**The curve set does NOT depend on scroll position** — worth knowing before assuming the scroll is
what makes a curve exist. `TradableHistoryCurvesPanel.Refresh` :100-109 collects its series with
`GetComponentsInChildren<GuiTableLineBuyable>()` and keeps whichever have a bound `GuiBuyable`;
neither `AgeControlScrollView` nor `AgeTransform` contains a single `SetActive`, and nothing in
`Configure`/`ConstraintAndPlace` touches a child's visibility — a scroll only moves `VirtualArea.Y`
and clips at render time. Measured on this fixture: the enumeration is 6 (four bound resources plus
two pooled lines left `<unbound>` at alpha 0, which the panel's `is GuiBuyable` filter drops). So
the mod's history table cannot lose a row to scrolling, and the auto-scroll is a visual courtesy —
it puts the highlighted resource's line where a sighted observer can see it — rather than a fix for
a missing curve.

**The graph highlights the focused row** (visual only, owner-approved). While the cursor is on a
history row the mod writes `Enable` on the curve widgets under
`TradableHistoryCurvesPanel.TradableHistoryCurvesContainer` — bright for that resource, dim for the
rest — and hands the graph back with the panel's own `OnSelectedItemChanged()` when focus leaves,
on screen pop, and on teardown. The curves carry no back-reference to their resource: they are
bound positionally to the list the panel builds with
`BuyableItemsGuiTable.GetComponentsInChildren<GuiTableLineBuyable>()`, which is POOLED order, not
drawn order, so the index is recomputed with that same call. The check is a `crop-shot.ps1` pair on
`[368,246,766,176]`: one bright curve while a row is focused, all four bright after Tab out. Never
drive this with `BuyableItemsGuiTable.SelectedLine` — that is real trade state.

**Each trading strip is ONE row**, in the order the equation is written: the selected buyable's
name (buy side, drawn only once something is picked), the ship spawn point (buy side, drawn only
for a ship — unsighted, Ships is tech-locked), Price (captioned with the game's
`%MarketplaceScreenHeaderPriceTitle`), Decrement, the quantity edit, Increment, then the trade
button — or, once `IsEmpirePointBuyoutUnlocked`, the Dust and Influence pair, both titled with the
game's `%MarketplaceScreenBuyButtonTitle` and valued by their own drawn total. The total already
carries its currency as a word (`Gui.GetSymbolString` writes an icon token that `AgeText.Clean`
names), so "Buy, button, Dust 52" and "Buy, button, Influence 52" need no composition. The drawn
"-", "×" and "=" glyphs are punctuation and are deliberately not nodes.

**Proving a stepper presses the game's button** needs a selection first, and a selection is real
state: activate a sell tile (`economy:salable/0`), Tab to the Sell transaction stop, walk right to
Increment, activate, then probe `SalableItemsPanel.QuantityTextField.Label.Text`. The game writes
that box SYNCHRONOUSLY from its own quantity setter, so the spoken "Quantity N" is already the new
number. **The restore is a section-radio round trip** — activate the other section radio and then
the original one; `OnTabChanged` sets `SelectedTradableItem = null`, which puts quantity, unit
price, total and the selected-name label back to "-"/0/"". Do this for the buy panel and the sell
panel separately; there is no safe direct clear (`OnClickTradableItem(null, 0)` dereferences
`SelectedGuiTradable` and throws). Reaching the buy panel's Filters region takes TWO
`ui.regionPrev` from the data rows, not one — the first lands on the sort headers, and pressing
Right/Enter there re-sorts the table instead (recoverable: click the Name header again). **Read the
selection BEFORE you start**: this screen only exists
on the owner's live session, so whatever is selected when you arrive is state to put back, not a
clean slate — the round trip clears it rather than restoring it, and re-selecting plus N presses of
Increment is how you get back to "Antimatter, quantity 4".

**The log is a region per turn.** `MarketplaceExchangeInformationsPanel.Refresh` draws a header
line above each turn's group, and a header is the wrapper it built with no transactions in it
(`TradableTransactionLine.GuiTradableTransaction.TradableTransactions.Count == 0`) — so the header
text names the region and each transaction is a single-entry row inside it. The panel's heading and
its `NotOwnerLabel` sit in a `economy:log/head` region of their own, so the stop is regioned all
the way through and `ui.regionNext` from the heading reaches "Turn 27".

**The tax box is four single-entry rows**: the heading, then Location (the game's button, refusing
with its own reason while unbuilt), Owner and Tax rate — the last three captioned by the mod
(`economy.location` / `economy.owner` / `economy.tax-rate`) because the game draws the three values
with no word over any of them. The OWNER's form (rate editor + its two steppers + the Set button,
`MarketplaceTaxesPanel.OwnedGroup`) is fixture-blocked and its rows have never been heard.

## Military and fleet selection

**Military and fleet-selection.** **Never press Retrofit**: it is immediate, with no confirmation.
A ship tile's SECOND click (Ctrl+Alt+Enter) opens that ship's design read-only and is a safe round
trip: `Gui.GuiService.ShowWindow<MilitaryScreen>()`, pick a fleet row (Enter) to draw the ship list,
Tab twice, `ui.doubleClick` → "Ship Design: Settler", close with
`Gui.GuiService.GetWindow<ShipDesignModalWindow>().HandleInput(InputAction.Exit)`. The first open
raises the Architects tutorial — minimize it. The star-system HANGAR is empty in `unlocked` ("No ships
in the hangar"), so the second host for that gesture is the selected-fleet panel, reached with the
`GalaxyFleet` select route in ES2 facts.
A FLEET ROW's second click (`MilitaryScreen.OnLineDoubleClick`) shows the fleet on the map instead,
and what the camera then does — one move, and the docked-fleet case that used to make three — is
`test-recipes/fleets.md`, **A docked fleet's landing**.
A force-shown fleet-selection window must never have a row SELECTED — `ProcessSelection` NREs on a
null `CheckValidity`. Create raises the Architects tutorial page in this save, so minimize it
afterwards. Restore the camera when done.

## The ship designer

**The ship designer.** Open it by reflection on the private `Cb`s, and take the panel instance with
`GetComponentInChildren` on the WINDOW — the hero window hosts a second one, and grabbing the wrong
instance reads a page nobody is on. **Never press Create or Apply.** Only civilian hulls exist in
this save. Restore `SelectedGuiShipDesign` and the toggles: `ShowDetailedStatsToggle` persists
across opens, the category filter does not — and the two hosts hold INDEPENDENT toggles, so the
designer's state says nothing about the hero window's. The detailed toggle gates exactly the three
`Detailed*` panels and nothing else; `Accuracy`/`Evasion` are hidden in the PREFAB (no fixture can
show them) and `SpecialStatsTable` only fills for a mining probe. Reopening from `/eval` needs a
`GuiShipDesign`: take one off the `ShipDesignItem` children rather than constructing it. **Edit
raises the Architects tutorial**, whose page node swallows navigation — minimize it before walking
anything.
**The slots are grouped by module TYPE** (`SlotOrder` — `ES2Access/Core/UI/SlotOrder.cs`), so the drawn order survives
only inside one type. On the Patrol design (a Small Zolya-class hull) the "Module slots" panel walks
*"Module slots, empty, button, Defense Module, 1 of 6" / "String Gravitics Engine, button,
draggable, 2 of 6" / "Improved Probes, button, draggable, 3 of 6" / "empty, button, Weapon Module,
4 of 6" / "Basic High-I Slugs, button, draggable, 5 of 6" / "Drop here to remove, 6 of 6"* — the
engine slot is a defence-AND-support slot, which is why it reads with the defence ones. Fitting or
removing a module must never move a slot: the key is the slot's, not the module's.
**Two of the three slot markers need another RULESET, not just a bigger hull.** Multiplier and
pairing exist only in `HullDefinitions[Balancing].xml` (ES2 facts), so no faction hull in this save
draws either, and a heavy mount needs a medium or large hull (its medium-hull instance is behind
`TechnologyImproveHull3`). Sight them by LENDING hull data — designer opened in Create mode with
`Bind(null)`, hull taken from `Gui.GuiWrapperProviderService.GuiHulls`, left through the
confirm-lose-changes path (measured 2026-08-19, the design list 9 before and 9 after):
`HullMedium01Balancing` reads *"empty, button, Weapon Module, Times 2 Multiplier, Symmetrical (x2
cost), 6 of 9"*, `HullLarge01Balancing` reads Times 4, and `HullLarge01Terrans` — a real rendered
faction hull — reads *"empty, button, Weapon Module, Special Module, Heavy Mount, 6 of 11"* with the
slot measuring 57×57 against its neighbours' 44×44. A FILLED slot says none of the three. A
perceptual pass on the dots and the pairing circle needs a game started on the Balancing ruleset.

**The hull drop list, from `/eval` in five requests** (the cheapest route to a class-backed drop-list
entry — the ship-hull list is one of the seven target-table lists). The designer opens in CREATION
mode with no ship at all: `var w = Gui.GuiService.GetWindow<ShipDesignModalWindow>(false);
w.Bind(null); Gui.GuiService.ShowWindow(w);` — the graph then reads
`Ship Design: creating` with a `shipdesign/info/hull` combo on "None". Reach it with `/input ui.next`
(the heading stop holds only the title), `POST /type` `hull`, `/input ui.back` to clear the search,
then `/input ui.activate`: `screen.drop-list | Hull` opens with `None / Karga-class / Zolya-class`.
`/input ui.down` onto Karga-class and `DevProbe.Tooltip()` answers class `ShipHull` with
`PanelFeatureHeader` (Karga-class, Hull (Ship Design)), `PanelFeatureHullInfo` (Role Colonizer, Size
Small, Command Points 1) and `PanelFeatureCosts` (Cost: 50 Industry). `/input ui.back` closes the
list; the designer itself must be closed through the MODAL-STACK DRAIN
(`test-recipes/fixtures.md`, "Resetting game state") —
hiding it leaves it
`ModalOnTop` with `Shown` false, and every modal opened afterwards then reads as buried.

## Hero inspection

**Hero inspection.** Bind, open, switch pages and close from `/eval`. An unrecruited `GuiHero` is
the read-only fixture. For a skill point, set `Level = 2` and `Refresh`, then restore by reloading
the save. Page switches raise tutorial popups — minimize them. Cheaper than `/eval`: the whole
family — overview → ship design page → skill tree page — walks from the Academy's own Inspect
button with `/input` in `unlocked`; each page switch raises a tutorial to minimize.
`HandleInput(InputAction.Exit)` BACK-STEPS hub modes (skill page → overview → closed), one call
per level — verify closure by the `/gui/graph` header, never by absence of an error; reopen with
`Bind(ActiveHeroes[0])` + `SwitchHubMode(mode, true)` + `ShowWindow<HeroInspectionModalWindow>()`.
**A two-level skill tooltip without a skill point** (reversible, no Apply): reflect the
definition into `SkillTreeEditionPanel`'s private `leveledUpSkillDefinitions`, call
`heroPreview.UnlockSkill(def, GetTotalSkillLevel(def), true)`, set `Dirty = true`, then LEAVE the
node and RETURN before probing (a still-focused node's tooltip answers pre-mutation content);
reset with `window.gameObject.SendMessage("OnResetSkillsCb", DontRequireReceiver)` and assert
`IsSkillsModified == false` and points back to 0. `unlocked` has no relic-skill hero, no
two-mastery starting skill, and no natural skill point.

**A hero with BRANCH skills is a fixture nothing ships with** — a starting skill is in no branch
(ES2 facts), so a fresh hero's wheel draws no dots and every overview branch reads as the plain node
it always did. Make one from `/eval` and undo it with `POST /loadsave`:
`h.UnlockSkill(h.SkillTrees[t].Stages[s].Skills[k].GetSkillDefinitionForHero(h), level, true, false, turn)`
(the `true` is `forFree`, so no point is spent and no order is posted), then
`window.OverviewPanel.SkillTreeOverviewPanel.Dirty = true` and the same on `SkillTreeEditionPanel`.
The hero is `Gui.PlayerEmpire.GetAgency<DepartmentOfEducation>().ActiveHeroes[0]`; bind the list as
`System.Collections.IList` (a `foreach` poisons the REPL session). Measured on the stage snapshot
with trees 0/0/0 at level 0, 0/0/1 at level 1, 0/1/0 at level 0 and 2/0/0 at level 0, the overview's
`hero:skills` stop reads *"Universal skills, group, collapsed"*, *"United Empire Skills"* (a plain
node — nothing unlocked there), *"Counselor skills, group, collapsed"*, and Universal expands to
*"Optimal Operations Expert, level 1 of 2, 1 of 3"*, *"Cosmic Castaway, level 2 of 2, 2 of 3"*,
*"Transformational Leadership, level 1 of 2, 3 of 3"* — rings inner-first. `ui.right` on a branch
expands it AND steps onto the first child; `ui.left` collapses back onto the branch.

**The overview's skill children are read through a scratch carrier**, because the overview's dot
carries no tooltip of its own (ES2 facts). So `DevProbe.Tooltip()` on a focused child is the only
proof the dossier draws — it answers class `HeroSkill` with the same `PanelFeatureHeader` /
`PanelFeatureDescription` / `PanelFeatureSkillEffectsSets` the skill page's dot answers — and the
crop for it comes off `GetWindow<GuiTooltipWindow>().AgeTransform.GetGlobalPosition()`, since the
panel is anchored over whichever 16×16 dot is focused.

## Troops and the tactics deck

**Troops and the tactics deck** are both non-committing until Confirm, which makes them safe to walk
whole. A refusal is provable from BOTH sides by injecting one: force the game's own refusal state,
read the spoken reason, and put it back.
**The deck opens straight from `/eval`** — `Gui.GuiService.ShowWindow<PlayCardDeckModalWindow>()`,
no military screen needed — and closes with `HandleInput(InputAction.Exit)`. `[Beginner] test` turn
21 draws four available tactics (Team Spirit, Barrage Fire, Power to Shields, Revive and Rebuild),
three of them already in the set, plus two locked slots. **Opening it advances the game's own
tutorial page to "Military"** — harmless while the popup is minimized, but it is a fixture change to
know about.

## Diplomacy, the academy pair and the forced-show sweep

**Diplomacy, the academy pair and the sweep** are largely forced-show work: bind what the window
needs, set `Visible=true`, read, then `Unbind` and hide, and re-diff the graph dump to prove nothing
was left behind. A forced show proves STRUCTURE, not content. **Never press** any diplomacy action,
any negotiation button (closing an unsigned negotiation still posts an order — ES2 facts; the
negotiation table's own recipe is **The negotiation table** in `modals-and-outgame.md`), or
anything on the pirate page while there are no pirate systems (its `Refresh` throws). **The
`AcademyModalWindow` Bind wedge**: a half-bind survives the probe and leaves the window unusable —
recover with `Unbind` plus a re-issued `POST /loadsave`, and never force-show a DLC modal without
its data.

## A table's double click

**A table's double click** (`TableSheet.ShowOnMap`, every table): from `unlocked`,
`Gui.GuiService.ShowWindow<EmpireScreen>()` lands the cursor on the Systems Management table's row;
`POST /input ui.doubleClick` must open that system's management page (heard as the tutorial popup
plus "Zoom level 14 of 15", confirmed by `/gui/graph` reading `Star system`). Note every
`GuiTableLine` in the game carries a `DoubleClickButton`, so the absent-means-silent guard is not
reachable from a fixture — the silent tables are the ones whose client handler is empty (ES2 facts).

## The caption sweep

**The caption sweep.** On the economy, senate, recipe-creation and negotiation windows, a box whose
heading carries NO tooltip must no longer have a heading ROW (the box is still named on the way
in); a box whose heading DOES carry one keeps it. The negotiation pressure band is named by the
game's drawn title ("Pressure", or "War Exhaustion" with a war on) rather than by the mod's word.
Diff `/gui/graph?buffers=1` for those four screens against a pre-merge capture: only heading rows
may disappear.

## Usage hints on the empire screens

All from the `/eval` openers above. `ShowWindow(GetWindow<TechnologyScreen>())` then expand a
quadrant and a stage for "Ctrl+Shift+Enter to queue it first" on a technology (the
`research:suggested` stop has no such hint — those nodes only jump);
`ShowWindow(GetWindow<MilitaryScreen>())` for "Ctrl+Alt+Enter to show and select fleet" (exactly
one per row, on column 0); `ShowWindow(GetWindow<EmpireScreen>())` for "Ctrl+Alt+Enter to open
system management screen" (both openings pop a tutorial page — minimize first).
**The two CARRY hints are derived, not declared**, so they appear on every draggable surface with no
per-screen wiring: the ship designer's module list reads "Space to drag ⟨module⟩." with
nothing held, and its slots read "Enter to drop ⟨module⟩." while one is (measured 2026-08-29 —
the cheapest live proof of the generalization, since it shares no code with the population rings).
A construction or research queue LINE only shows the drag hint where the queue really offers a
reorder (two or more lines), and a hangar ship row only where the hangar holds ships.
**A queue line is named from the MODEL, never from what the line draws**, because the caption
auto-truncates (`gui.md`, "A drawn label may be an ELLIPSIS of itself"). The evidence pair both
queue stops answer, measured 2026-08-29 on `unlocked` turn 35 with Ita selected: the row, its drag
and its hint all read *"Xeno-Industrial Infrastructure"* where the label draws "Xeno-Industrial." —
`empire:detail/queue` (reached by clicking a system row's Construction cell) and `system:queue` on
the system management page, which is the same reader (`SystemPanels.Queue`) from its second caller.
Getting to that page from the empire table is TWO activations on the row: the first selects the
system, and `ui.doubleClick` on a row already selected opens it.

## Fixture-blocked

- On the wheel: dependency links, Disabled technologies and their reasons, buyout, a
  scrolling queue, and a deed WON or LOST (**The technology wheel**).
- "Show location", the numeric "(x/y)" progress branch, a quest awaiting an objective choice,
  the minor-faction button, a cooperative quest's podium (**The pinned quest**,
  **The quest journal**); the turn-3 fixture draws exactly one card under every filter.
- The gene hunter, assimilation, relics, a real election, an enabled Abolish, a drawn history
  graph, an empty senator slot and the outpost panel (**The senate family**).
- The Marketplace tab and therefore the buy table in every SAVED fixture (**The economy page**);
  it exists only on the owner's live session (**The marketplace tab**), and even there: the
  Ships and Heroes sections and so the ship spawn-point picker, the tax box's OWNER form (rate
  editor, its steppers, the Set button), the graph's "No data available" branch, and the
  not-owner label the log draws while the empire may not see the exchanges.
- The star-system HANGAR is empty in `unlocked` (**Military and fleet selection**).
- Multiplier and pairing slot markers without another RULESET; `Accuracy`/`Evasion`;
  `SpecialStatsTable` outside a mining probe (**The ship designer**).
- A relic-skill hero, a two-mastery starting skill and a natural skill point
  (**Hero inspection**).
- The Academy is tutorial-gated on `[Beginner] test` (**Diplomacy, the academy pair**).
- The whole EMPIRE page is tutorial-gated on `[Beginner] test`; use `[Beginner] access test`
  or `unlocked` (**The empire page**).
- The specialization list's ROWS: no save has an available planet improvement, terraformation
  or anomaly reduction, so `screen.planet-constructibles` can only ever be opened onto an
  EMPTY list — on the empire route and on the system-management route alike (**The empire
  page**; the same blocker under **The planet constructible panel has no fixture either** in
  `systems-and-planets.md`).
- The absent-means-silent guard on a table's double click (**A table's double click**).
