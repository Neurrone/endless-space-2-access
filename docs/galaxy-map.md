# ES2 facts — the galaxy map

The map's labels and what an empire may know, lanes and the map's own drawing, probes,
targeting modes and the scan view, and the camera and its view levels. Planet cards, colonies,
outposts and the influence/colonizability facts live in `planets.md`; fleets and their orders in
`fleets.md`. Index and charter: `README.md`.

## Galaxy labels and what an empire may know

- **The map's NAME gate is `StarSystemLabel`'s, and it is looser than the mouse's.** The label
  shows at exploration ≥ 2 AND (visibility Known or ≥ 3) (`ShowOrHideIfVisibleByEmpire`
  :1514-1522), draws `GameNode.LocalizedName` at ≥ 2 and the literal `"???"` below it
  (`RefreshEmpireNameLabel` :1894-1921), and the window binds a label to EVERY `GameNode` —
  special nodes included, with no special-node branch on the name. So
  `GalaxySpecialNodeCursorTarget`'s stricter ≥ 3 governs TARGETING, not naming: between
  exploration 2 and 3 the map names a special node the mouse cannot yet click. Measured live:
  every Unrevealed node's label hidden, so a lane preview into the dark never shows the far
  end's name anywhere — the mod's "an unexplored system" placeholder is exact parity.
- **A `SpecialNode` IS a `StarSystemNode`, and the map names it one exploration step later.**
  Nebulae, dust clouds and the rest are drawn in a star's place and have rows in the galaxy tree, so
  any `is StarSystemNode` enumeration (including `Galaxy.StarSystemNodes`) already contains them and
  the Academy and quest nodes; what needs doing is the threshold, not the enumeration.
  `GalaxySpecialNodeCursorTarget.VisibleByCurrentEmpire` (:22-27) needs exploration ≥ 3 where
  `GalaxyStarSystemCursorTarget` (:89-94) takes ≥ 2. Mod policy: places = systems + specials, and
  `GalaxyHudScreen.Perceived` gates a `SpecialNode` at 3 (measured evidence pair: a special and an
  ordinary node forced to the same exploration 2 answer False and True; counting only non-special
  nodes made inspect mode's Enter a silent no-op on the Solar Nebula, B10 6805, the one special in
  `[Beginner] test`). **The Academy and quest sites are NOT `SpecialNode`s** — each is an ordinary
  `StarSystemNode` carrying the `WorldAcademy` / `QuestNodeTag` tag (the label has its own
  `AcademyIconGroup`). `SpecialNode` means the eight stellar-phenomenon kinds (Black Hole,
  Asteroid Field ×2 definitions, Collapsing Star, Solar Nebula, Neutron Star, Nebular Clouds,
  Rejuvenation Field): no planets, same zoom/click as a star, and the KIND is named only by
  the dossier's category line (`GuiSpecialNode.TooltipClass = "SpecialNode"`,
  `Gui.GetLocalizedTitle(SpecialNodeDefinition.Name)` — e.g. "Solar Nebula" where an ordinary
  star reads "Star System (White Star)"); the label and `LocalizedName` ("B10 6805") never
  say it.
- **An undiscovered system's label carries `Enable=True` from the prefab.** A sweep of all 87
  `StarSystemLabel`s found ~80 with `RequestManagementViewButton.Enable == True` and the visibility
  chain false. Any offer gated on `Enable` alone would exist for the whole galaxy; the drawn-chain
  test has to come first.
- On the pooled `StarSystemLabel` set only labels whose ROOT `Visible` is true have run their
  `Refresh*`: 135 of 136 hold prefab-default `Visible=true` on the badge groups, and the
  ancestor walk in `AgeWidgets.Visible` is the only thing keeping them silent.
- **Pooled label widgets keep the PREVIOUS system's values** (a hidden `TraitorCountLabel` read a
  stale "1"), so every label read is gated on ancestor-walked visibility.
  `DualGarrisonsLabelButtons.OnClick` selects `garrisons[0]` only — a duplicate affordance,
  deliberately omitted.
- **The map's star/fleet/mote labels are pooled and re-bound as the camera slides** (2026-08-17):
  a tooltip widget captured for a place goes stale within the 0.3 s camera glide, so anything
  aiming the pointer at a map thing must resolve the widget from the ENTITY per frame —
  `GalaxyHudScreen.MapMark` is the one lookup, shared by the inspect cell's aim (see the code
  comment at its `AddSystem` site for the mechanism).
- **`ContextualIconInvasion.AgeTooltip.Content` is prefab-authored and never cleared.**
  `RefreshInvasionContextualIcon` (:748-749) clears only `Class` and `Target`, so the
  `%StarSystemLabelInvasionDescription` sentence sits in `Content` on every label forever —
  harmless only because readers gate on the icon being drawn. A reader that skips the
  visibility gate reads a phantom invasion.
- **`Gui.GetTitle` has no GuiElement for `InvadedStarSystem`, `CitadelDefense` or
  `GuardedColonizedStarSystem`** — the raw `%…Title` key (or a "(missing GuiElement)" marker)
  comes back, so those states have NO game-authored noun; mod phrases required. And the
  `StarSystem` tooltip class carries `PanelFeatureTimeBubblesContainer` but NO guard or
  citadel feature (`GuiTooltipDescriptions.xml`) — the guard ring is the map's only telling.
- **A drawn empire colour cannot identify a minor faction**: all twelve minor empires share one
  grey (0.627³), and the neutral/unknown fills are white differing only in alpha (0.753/0.251).
  Where colour is not injective, gate on the drawn COUNT and read identity from the writer's own
  data source (`RefreshEmpireColoredBar` :1851-1867 — not Lost, not Ghost, visibility ≥ 1,
  player inserted first).
- **`StarSystemLabel.RequestManagementViewButton` is the map's only route into a colony's page, it has
  no tooltip, and its `Enable` IS the ownership test — which makes it stricter than the view level
  behind it.** `RefreshStarSystemNameLine` (:1750) writes `Enable = MainColonizedStarSystem != null &&
  MainColonizedStarSystem.Empire == Parent.LookingEmpire`, and `StarSystemLabel` :1626-1648 assigns
  `MainColonizedStarSystem` only while the state is `Colony` — so the button is drawn dead on an
  OUTPOST of ours, while `RequestStarSystemManagementViewLevel` (:1224-1247) opens the page for any
  system of ours that is not `Lost`. A mod reading "can the player do this" off the button's `Enable`
  under-offers by exactly the outpost case. Measured turn 18: Heka `enable=False`, repository
  `State == Outpost`, `IsBlackedOut == false`, and `GalaxyViewLevels.OpenSystem` opens the page —
  whose outpost half (`OutpostInfoSidePanel`, the outpost-action checkboxes) the system screen already
  reads. The widget carries no `AgeTooltip` at all (measured on the drawn label), so `Visible &&
  Operable` answers "is this a colony of mine" without asking the model, and anything declaring the
  button has to bring its own name.
- **Whose colour a system's label paints, and whose dossier it binds, both count only a full COLONY.**
  `StarSystemLabel.RebuildColonizedStarSystemsList` (:1623-1650) walks the
  `IColonizedStarSystemRepositoryService.GetValues(node.NodePosition)` colonies, keeps the ones at
  `Visibility[player] >= 1` whose `State` is `Colony`, and prefers the player's own — the last such
  becoming `MainColonizedStarSystem`. So an OUTPOST has no owner by that rule: Heka's star dossier is
  titled "Heka" and Osulo's "Osulo - Niris" (`GuiStarSystem.Title`), and "is this system mine" is asked
  of `DepartmentOfTheInterior.ColonizedStarSystems` instead (the same list the tree's owned region is
  built from), where an outpost counts. The mod's `VisibleColony` answers a different question (what
  claim is DRAWN here, outposts included), so a dossier the mod builds itself must use
  `GalaxyHudScreen.LabelColony`, or the same card reads differently either side of a zoom.
- **A system's star, its name label and its population count all carry the SAME dossier** — one
  `GuiStarSystem`, three widgets on `StarSystemLabel` (`StarTooltip`, `StarSystemNameLabel`,
  `PopulationCountGroup`; measured identical `Target` on Osulo). The deposits are the label's only
  OTHER renderer-assembled dossiers (`LuxuryItem_*`, class `ResourceDepositGroup`), and they are bound
  only while the map is actually drawing that label — a label the camera has pooled away answers with
  the prefab's class `ResourceDeposit`, no target and no title.
- **The game's own words for a system's owner and for a home system.** The system dossier's header is
  `GuiStarSystem.Title` = `"{name} - {GuiEmpire.GetLeaderName(colony.GUID, player)}"`, gated on
  `StarSystemNode.PlanetsVisibility[player]`; `GetLeaderName` already answers `%EmpireUnknownTitle`
  ("Unknown Empire") for an empire the player has not met and names a MINOR civilization per system
  (`LesserEmpire.GetLesserName(contextGUID)`). There is no game word for "unowned" on a system —
  `%MarketplaceScreenNoOwnerTitle` "No owner" is the nearest (owner-chosen). `%HomeSystemTitle` is
  `"Home System "` WITH a trailing space. The map's own home-system icon is NARROWER than the model:
  `StarSystemLabel.RefreshHomeSystemLine` (:2267-2275) draws it only for `IsMajorHomeSystem`, so a
  minor civilization's home — which is the whole of that civilization — is marked nowhere;
  `StarSystemNode.IsHomeSystem` (`HomeSystemEmpireIndex != -1`) is the model's answer and is set on
  every home system in the galaxy from generation, so reading it ungated would name unexplored stars.
  **Mod policy:** owner and home word alike are gated on a colony the player can SEE at that node
  (`Visibility >= 1`, non-ghost — the same gate `SystemInfluence` already names owners by), so the fog
  gives nothing away.
- **A system has TWO star tooltips** — the label's and `PlanetLabelsWindow_SystemOrbital.StarTooltip`
  — swapped by the camera; both class-backed, so only the drawn one has words: resolve at READ
  time, never remember one. At orbital zoom the label group's top edge leaves the screen (y=-1
  measured), so a tooltip anchored to it draws clamped away from it — a camera-dependent pointer
  must be re-committed on camera change (`GalaxyHudScreen.FollowCamera`). **They are also bound
  differently**: the label's is `GuiStarSystem.Instantiate(node, MainColonizedStarSystem)` while the
  one the orbital window parks over the star answers plain "Osulo" where the label's answers
  "Osulo - Niris". This is the GAME's own inconsistency, older than the mod, and it is why the star
  card's NAME still changes when the camera comes all the way in.
- **The remaining label readouts**: the KOTH score figure exists only in
  `KingOfTheHillScoreLine`'s ROW tooltip content; deposit exploited-state =
  `StarSystemLabelDepositItem.ResourceImage.AgeTransform.Enable`; `DualGarrisonsLabelButtons` ship
  counts sit on each button's `ShipCountLabel`; `AcademyGroup`'s own tooltip is bound to the
  SYSTEM dossier (`StarSystemLabel:1777`) — never read it as the group's own.
- **`StarSystemLabel` prefab-authors a `%…Description` into every contextual icon's tooltip
  `Content`** and rewrites it at refresh, so a drawn-gated reader always has the game's own
  sentence — but some icons' content only fills once they are drawn.
- Hangar labels are drawn from `IVisibleGalaxyHangarRepositoryService` gated on `ShipsCount > 0`;
  the click is `Select(CursorTarget)` + `ChangeCursor(GalaxyGarrisonCursor)`, and
  `Hangar.LocalizedName` is `"%HangarTitle (⟨node⟩)"`.
- **A system's deposit strip is bound at every zoom and FADED at the orbital level.** Measured on
  Primus: with the camera in on the system, `DepositsMainTable.Visible` is true, its items carry
  live `GuiResourceDepositGroup` targets and each item's own `Alpha` is 1 — but the enclosing
  `DepositsMainLine.Alpha` is 0, so `AgeWidgets.Painted` (which walks ancestors) says false and the
  icons are not on screen. At the systems view level the whole line paints. So `Visible` is NOT the
  test for "the game is drawing this deposit"; `Painted` is.
- **A planet's circle is drawn at every zoom the system's planets are declared at.** Measured across
  the whole slider on `[Beginner] test`: `PlanetCirclesTable`'s children keep their `PlanetSimple`
  tooltips even at the constellation band, so the mod's own carrier for a planet dossier is a
  fallback that this fixture never reaches.
- **`Constellation.Exploration[empire]` is a STALE aggregate**: it recomputes only on
  node-exploration events, counts member systems at node state ≥ 4 (visited, not merely seen),
  and at turn 1 all five constellations read 0 — including the one the empire lives in. It is
  the constellation label's own show gate; the mod's grouping mirrors it exactly.
- **`ConstellationLabel` is CULLED, not just alpha-faded**: `ConstellationLabelsWindow` hides any
  label whose `CulledIn` is false, and `MarkLabelsCulling` reruns on every camera-POSITION change
  (`SpecificUpdate`) — a one-shot force is undone by any pan, which is why `ConstellationLabelHold`
  re-asserts per frame; `window.Dirty = true` is a complete reflection-free restore (`Refresh`
  calls `MarkLabelsCulling` unconditionally). In `unlocked` no `GalaxyConstellation` is ever
  culled in at ANY camera position. Constellation GUIDs in `unlocked`: Canista 1, Andromeda 72,
  Vela 264, Herkules 516 (home), Fornax 713.
- **Alpha is not a gate for the constellation tooltip family**: a held label at play zoom is
  `Shown=True, Alpha=0` and its tooltip still fills and reads — `ConstellationLabel.Refresh`
  writes Content/Target/Class regardless of alpha.
- Live hull-oracle result for `unlocked` (2026-08-20): `regions 5, members 136, outside own
  hull 0, inside another hull 22, classified elsewhere 0` — the interlock is real (22 members
  sit inside a neighbour's hull) and the nearest-member tie-break resolves every one.
- **`EntityExploration.SetState` only ever RAISES a state** (:87-100), so a fog test that needs
  a lower state must write the byte in the by-reference `GetCurrentStates()` array and put it
  back (test-recipes). `GetCurrentStates()` is public and by-reference — no reflection needed
  to force or restore an exploration byte, because the states array IS the storage.
- **What is on a PLANET, and who is allowed to know it** (2026-08-22, read from the game; the
  scanner's five world categories are built on exactly these gates and nothing wider):
  - The tree declares planet nodes at `node.Exploration[empire] >= 2` **and**
    `node.PlanetsVisibility[empire.Index]` (`GalaxyHudScreen.PlanetsDeclared`, the condition
    `AddPlanets` already had). Below that a system has no planet rows at all, so a scanner result
    standing on a planet there would be a jump with nowhere to land.
  - The orbital card only shows what a planet IS — its type, its anomalies, its deposits — once
    the system is **surveyed**, `Exploration >= EntityExploration.State.Revealed`
    (`GalaxyHudScreen.Surveyed`); below it the circles are grey unknowns and `PlanetName` answers
    with the game's own `%PlanetStatusUnknownTitle`.
  - **Curiosities are the exception and it is the game's**: `Curiosity.CanBeSeen(empire)`
    (`Curiosity.cs:303-315`) asks the definition's own prerequisites (detection technology) and
    the authorization, and never the survey. `GuiPlanet.GetRemainingCuriosities` is that same test
    plus a panel ordering, over a list field it reuses between calls — read
    `planet.Curiosities` + `CanBeSeen` directly rather than holding its answer.
  - Names: `new GuiAnomaly(definition, planet).Title` (no `Title` override — the title is the
    DEFINITION's, so it can be memoized per definition), `new GuiCuriosity(c).Title` (the title of
    `CuriosityDefinition.DisplayedType`), and for a deposit `new GuiResource(definition
    .RelatedResourceDefinition)` — whose own `IsLuxury`/`IsStrategic` count the SystemLuxury and
    SystemStrategic types in with theirs. The HUD writes "Titanium", never "Titanium-70".
  - Size and type: `Gui.Localize("%PlaneSizeAndTypeFormat", Gui.Localize(Gui.GetTitle(Size)),
    Gui.Localize(Gui.GetTitle(Type)))` — size first, and the key's misspelling is the game's.
  - The five outputs of a world nobody has settled are `PlanetInitialFood`/`Industry`/`Dust`/
    `Science`/`Prestige` off the planet's own simulation object (`FidsiEnumerator.LoadPlanet`,
    uncolonized branch), named by `Gui.GetLocalizedTitle` of the property — the panel draws an icon
    beside each and writes the word nowhere. The orbital card deliberately draws PIPS instead for
    an unsettled world; the planet page reads the numbers, and the mod follows the page (owner
    ruling 2026-08-22).
- **`EmpirePosition.Known` is what reveals a foreign capital, and it is not enough on its own.**
  `DepartmentOfIntelligence.RefreshEmpirePosition` (:479-535) sets `Known = true` for another
  empire once ANY of that empire's colonies is explored (≥ 4) or in sight (≥ 3) with the colony
  itself visible (≥ 1); the position it stores is the HOME system's when the home system is among
  those, and otherwise the empire's highest-influence visible colony. When nothing is visible it
  writes the home position anyway and sets `Known = false` — so the stored position equals the
  home system's for an empire the player knows nothing about, and a gate on the position alone
  would leak every capital in the galaxy. The diplomacy lens draws its home circle off `Known`
  (`GalaxyStarSystem.ContentForDiplomaticScanViewForHomeSystem.Update`) and iterates MAJOR empires
  only. **Mod policy:** a foreign home system is named only when `Known` is true AND the known
  position is the home system's; minor factions are not asked, matching the lens. Measured in
  `[Beginner] test` turn 21: all three AI majors read `Known=False` while their stored positions
  are exactly their (unseen) home systems — Leaper/Baten, St Chaoiver/Jundur, Doria/Lonica.
- **A lane leads somewhere UNEXPLORED when the far end is not perceived, not when the lane is
  unrevealed** (2026-08-22). The map draws a link at `MapVisibility.Drawn` (intensity ≥
  PartiallyRevealed), and `GalaxyHudScreen.LanesOf` is the one list every part of the page numbers
  lanes from (clockwise from north, per system). A wormhole is a `Link` like any other and passes
  the same test, but is dropped entirely for an empire without `HasWormholeTechnology` — the
  game's own neighbour search skips them the same way. Walking the perceived systems and taking
  each one's drawn links whose far end fails `MapVisibility.Perceived` therefore enumerates every
  unexplored way out EXACTLY ONCE: the other end is by definition a system the walk never reaches.
- **`GalaxyQuestMarker` is a world object, not a culled label**: `UpdateVisibility` (:157-165)
  only asks whether `Marker.Empires` lists the active player's empire, so enumerating quest
  markers from the journal with that one test matches the picture at every zoom. A marker's
  position resolves through whatever it is bound to (`QuestMarker.GalaxyPosition`), which is
  `GalaxyPosition.Zero` when the target has none.
- **A quest marker's position resolves through the thing it is bound to, and only five kinds of thing
  have a place in this tree.** `QuestMarker.GalaxyPosition` (:13-45) looks `BoundTargetGUID` up in the
  entity repository; `QuestMarker.Load()` (:136-151) is what fills its `Target`. A node, a planet's
  system, a curiosity's system, a colony's system and the node a fleet is standing at all answer with
  a `NodePosition`; anything else - a fleet in mid-lane included - has none, which is what makes a
  marker "out in the open". `QuestMarker.GUID` is its own identity, which is how two markers of two
  quests at one star stay apart. Registering one by hand (`IQuestManagementService.Register` after
  `Load()`) is the only way to see any of this on either fixture — both have ZERO markers.

## Lanes, lines and the map's own drawing

- **A starlane is ONE `Link` shared by both end systems**, so per-system nodes built from a link
  must key STRUCTURALLY (measured as a focus teleport on a fog-off build).
- **A link's drawn line is built full-length at ignition and only TINTED by exploration.**
  `GalaxyWarplink.Ignite` uses both extremities' real positions unconditionally;
  `GetIntensityFromState` (:362-372) paints Localized/Identified at intensity 0 — invisible —
  and PartiallyRevealed+ at 1. Existence of the geometry was never the question; visibility is
  the intensity. Mod policy: `MapVisibility.Drawn(link)` gates the tree's lanes at
  ≥ PartiallyRevealed.
- **The lane gate, second half**: a starlane's line is built end-to-end at link creation and
  tinted uniformly from the link's own state (`GalaxyLink.Refresh` :247-252 passes the SAME
  state for both extremities); what shortens a lane into the dark to a stub is the FOG SHADER —
  `FOWRendererService` publishes the empire's distance field as a global `_DistanceToFOW`
  texture (:347) the map's world materials sample. So `Exploration >= PartiallyRevealed`
  answers "is this lane lit", never "lit HERE" (measured 2026-08-20).
- **The labels/geometry split**: everything the map names out in space EXCEPT lanes is an AGE
  label whose window gates itself — its declaring gate IS the drawn answer. Lanes are world
  geometry occluded by the fog shader, the one class whose place-reading needs a second,
  position-aware gate (`IVisibilityService.IsExplored` per unit square).
- **Fog of war is a per-POINT question with a per-point answer**: `IVisibilityService.IsExplored
  (empire, GalaxyPosition)` samples the empire's fog-of-war distance field — the very field the fog
  is drawn from — so a region can be sampled square by square (121 lookups into a byte array cost
  nothing). There is no second, "currently visible" field for arbitrary points: the map draws ONE
  fog, so there is no unexplored/remembered distinction to resolve for a point. `GalaxyBounds` on the
  same service is NOT the galaxy's extent — it is that field's rect, scaled 2.5× (`VisibilityController
  .GalaxyBoundsScaleFactor`), so anything wanting "where does the galaxy stop" measures
  `Galaxy.GameNodes` instead (`[Beginner] test`: x `[-164.0, 22.8]`, y `[-41.5, 88.3]` from home) —
  as the BOX for anything bounding a move and as the convex HULL for anything describing the shape
  (`GalaxyFrame`; the split and why is under "Probes, targeting modes and the scan view").
  **The game has no UI word for the fog**: "the fog of war" occurs exactly once in the whole English
  corpus, in one quest objective's tooltip, and "miasma" occurs nowhere at all — so a mod that says
  it says it in its own words. The mod's word is **"unexplored"** (`galaxy.inspect.fog*`), naming the
  predicate it actually samples rather than the picture drawn over it.
- **The map draws its own lines through `ILineRendererService`, and two of the six arguments are
  not what they look like.** `CreateLine(pos0, pos1, width, color0, color1, materialType)` +
  `ShowLine` puts a `LineToRender` — a plain record of public fields the manager reads live each
  frame, so moving one is field mutation — into the SAME manager the starlanes use
  (`Services.GetService<ILineRendererService>()` and the galaxy technique's own answer are one
  object, measured). But: **`materialType` is an INDEX into a private `materials[]`** the manager was
  loaded with (`GetMaterialIndex` answers -1 for a foreign material), so it is borrowed off a live
  `GalaxyWarplink.Line` (0 on this build); and **a `Color32` is not a colour** — it is two packed
  16-bit indices into the GPU colour palette (`GalaxyLink.Refresh` and `GalaxyStarSystem`'s
  `defaultWhiteEncodedColor` both build one as `(slot & 0xFF, slot >> 8, slot2 & 0xFF, slot2 >> 8)`),
  so slots come from `Amplitude.Unity.Graphics.Services.GetService<IGPUColorEvolutionService>(5)`
  (`RegisterColorSlot`/`ChangeColorSlot`/`FreeColorSlot`; context 5 is the galaxy's). A line that
  gets either wrong is accepted, reports itself `Visible`, and is simply not on the screen. **The
  `width` argument is ignored by material 0** — 0.1, 2 and 20 all draw the same hairline (measured) —
  and the drawn hue came out a pale cyan whatever colour the slot was registered with, so a mod-drawn
  line is told apart from a starlane by being cyan, not by weight. `ReleaseLine` ×N and
  `FreeColorSlot` on teardown; the manager's own `lineToRenders` count is the check.
  Materials 0-13 are lane, wormhole, diplomacy, trade-route ×3 and hacking-route ×8.
- **A short line's invisibility is about the CAMERA, not the line** (supersedes an earlier reading
  that a 3-unit line is invisible under every `materialType` 0-13 — that was taken at zoom step 9 and
  generalised into a rule about world length; it is not one). The lane shaders eat off each END in
  something closer to SCREEN space, so the same 3-unit line is invisible far out and draws as a solid
  bar close in: crop evidence, the inspect cursor's 3-unit cell edges drawing as four clean bars at the
  zoom the cursor's own camera sits at. **Before working round a length threshold, re-measure it at the
  camera the feature will really be used at.** Thickness IS still dead (the width argument is ignored
  by material 0), so a heavier line has to be several parallel ones.
- **Filled quads and rings are not available on the galaxy view.** `QuadRendererManager` is loaded
  with an EMPTY material list (measured `materials.Count == 0`), every `QuadRenderer` the build
  defines is a distance-field NUMBER (`Amplitude/Galaxy/PathNumber`, the turn markers on a fleet's
  path) and `QuadToRender` needs an `IAtlasElement`, so there is no solid-fill quad to draw with.
  **Rings: still unavailable, but every reason first given for it was wrong** (re-measured
  2026-08-16, crops at each step). `ICircleRendererService` lives at renderer context **0**, read off
  a live orbit ring's own `RendererContextIndex` — asking at 5, where the colour palette lives,
  answers null, and a null service draws nothing and raises nothing, which is what the first
  investigation actually hit. The mask is not the obstacle either: live it is
  `0xFFFFFFFFFFFEDFFF` (only `CurvedLine` and `QuestMarker` cleared), so `PlanetOrbit` — where all
  444 of the game's own live circles sit — and `Line` are both ON. And the manager's shown list IS
  the render source: hiding every `materialType == 0` `CircleToRender` removed the solid planet-orbit
  rings from the screen. Even so, a circle created through `CreateCircle` + `ShowCircle` never
  appeared — not on `Line`, not on `PlanetOrbit`, and not when given a drawn orbit ring's exact
  position, axes, width, material index and encoded colour, at radii from 1 to 9. Whatever the
  remaining difference is, a mod cannot get a ring onto this view; **do not spend a stage on it
  again without a new lead.** (A trap met on the way: writing a live circle's `Radius` proves
  nothing — `CircleRenderer.Draw` re-`Init`s its record from the component every refresh.)
- **THE BORROWED-RENDERER SAGA IS CLOSED: the mod draws its own overlay (2026-08-17).** The three
  bullets above stay true about the game's renderers and stay worth reading before anyone asks one
  of them for a mark — but the inspect cursor no longer uses any of them, and no future map mark
  should start there either. Every borrowed answer failed the same way twice over: the mark is drawn
  IN THE WORLD, so it shrinks with the camera (the case that has to work is a one-unit cell at full
  overview zoom, about one pixel of world), and the mod controls neither thickness (width ignored)
  nor hue (a palette index, not a colour). A `MonoBehaviour` of the mod's own with an `OnGUI` that
  projects the cell's four world corners through `ICameraService.Camera` (`Default Camera`) and
  strokes textured rects round the bounding box has none of those problems: IMGUI composites above
  the whole scene AND above the game's own AGE windows at a low `GUI.depth` (measured — the square
  drew over an open `GuiTooltipWindow`), thickness and a minimum on-screen size are in PIXELS so no
  zoom can thin them away, and the colour asked for is the colour drawn. Cost is four
  `WorldToScreenPoint` calls and eight rects a frame while armed and nothing at all otherwise.
  `ES2Access/UI/InspectMarker.cs` is the worked example. Two engine notes it paid for:
  `WorldToScreenPoint` measures y from the BOTTOM and IMGUI from the top, and the host object must be
  DESTROYED (not disabled) on teardown, because a behaviour surviving a hot reload belongs to an
  assembly the next load cannot reach.

## Probes, targeting modes and the scan view

- **The Expedition fleet action arms no mode**: `FleetActionButtonExpedition.OnClick` plays a
  sound and force-zooms via `galaxyView.SelectGameNode` so a mouse can reach the curiosity
  items on the orbital cards; the accessible path is the curiosity button under the zoomed
  system's planets. It is probe-based (`GuiExpeditionFleetAction : GuiProbeBasedFleetAction`)
  and greys out at 0 probes. A first visit to an undiscovered system routes through the
  discovery cinematic, all of it already spoken.
- **A probe launch accepts ANY non-zero direction** — `LaunchProbeFleetActionDefinition.
  CheckContext` (:92-95) refuses only a zero vector (`DirectionIsInvalid`); initiator checks
  are docked-in-orbit + movement cost. The galaxy's axes are the compass under "Map, camera and
  view levels" below. A launched probe has already moved one hop of its `Speed` (6 here) when
  created — it never sits on the launch star — and probe speed vs lane length (16.5-26.6) means a
  nearest-star anchor MIGRATES mid-flight. `VisibleEntityLabel` draws at
  `WorldToScreenPoint(Entity.GalaxyPosition)` gated on camera culling + `Visibility >= 3`, so the
  drawn position licenses direction-and-distance words.
- **NOTHING clamps a probe's flight to the galaxy** (measured 2026-08-27, whole chain read).
  `ProbeLaunchingCursor.OnCursorClick` (:141) posts a normalised DIRECTION and no target;
  `DepartmentOfTransportation.MoveProbe` (:44) sets the next position to `pos + dir × speed` with no
  bounds test of any kind; `MoveToProbeAction.ClientFinalize` (:94) only decrements the lifetime. So a
  probe genuinely flies `speed × lifetime` units down any bearing and lifts fog wherever it gets to —
  the fog field is `IVisibilityService.GalaxyBounds`, 2.5× the galaxy (see "Fog of war is a per-POINT
  question" above), so there is real fog out past every star. **The mod therefore has to CHOOSE where
  the map ends, and the choice is the bounding BOX of `Galaxy.GameNodes`** — the frame the inspect
  cursor already roams. A hull round the stars was the earlier answer and was wrong twice over: it is
  not the game's rule (there is no rule), and it disagreed with the cursor, so a bearing could say
  "fully explored to the map edge at 0" for a system the hull put on its rim while the cursor happily
  walked west from that same system for another eighty units. One frame, two shapes over the same
  nodes (`ES2Access/UI/GalaxyFrame.cs`): the BOX bounds anything that MOVES (probe bearings, the
  inspect cursor), the CONVEX HULL describes (the `Ctrl+M` overview's centroid — "where the bulk is",
  which a box's middle can put in empty sky; its width and height are the box's by construction).
  **Specials count as galaxy by decision** — a special node is a named place with coordinates and a
  row in the tree, so a frame drawn round stars alone would strand places the player can steer to
  outside the map.
- **A bearing is read TWICE over two different stretches, and only the approved difference is left**
  (ruled 2026-08-29). The ranges answer "what is out that way" and run to the map's rim however far
  past the probe's reach that is; the leading share answers "what would this launch buy me" and covers
  only the reach-capped flight (`ProbeFootprint`, reach = `Round(ProbeSpeed × ProbeBaseLifetime)` = 30
  on `[Beginner] test`, vision half-width `ProbeVisionRange` = 3.5). That scope difference is the
  owner's decision and is why "unexplored 49 to the map edge at 59" can sit beside "100 percent
  explored". **The alongside stretches are cut to the tiles the share counts**: a flank sample is the
  OUTERMOST lattice tile still inside the vision radius on that step's perpendicular, and membership is
  `ProbeFootprint.InVision` itself, so the two halves of the sentence cannot disagree about a tile.
  Within the probe's reach, then, a bearing the share calls fully explored has nothing alongside it to
  report; past the reach it may still list fog under a 100-percent share, which is the scope difference
  above and by design. What that replaced: one sample per side per step taken at exactly the vision
  radius and then rounded to the lattice, which could push the sample half a tile's diagonal (~0.71)
  PAST the radius while missing tiles inside it that no sample landed on. Measured on Dusay's southeast
  bearing, where every tile within 3.5 of the flight is explored: the row said "100 percent explored;
  fully explored to the map edge at 32" and then read out fifteen alongside stretches, every one of
  them fog from the 3.5-4.25 ring the probe would fly straight past (52 dark tiles sit in that ring);
  it now says the first half and stops.
- **Arming a targeting mode from the fleet-actions stop closes the fleet panel and seats the
  cursor back in the acting fleet's system branch** — the last node if the branch is open,
  the system node if closed. That is reconciliation's doing, not a landing, and it only holds
  when the cursor was in that branch to begin with: from anywhere else the player was left
  standing where they were, with the mode up and no way to it. So the mod no longer relies on
  it — arming seats the cursor on the probe mode's own first bearing itself
  (`GalaxyHudScreen.FollowProbeArming`, 2026-08-19).
- **A `GalaxyLink` game object carries TWO mirrored `GalaxyLinkCursorTarget` halves**
  (start/destination swapped; `GetCursorTarget` picks by where along the line the pointer
  is), and **no targeting cursor consumes a link target** — only the garrison cursor and
  the scan overlay do — so a mode confirmed on a lane refuses silently and writes no hover
  readout. A lane confirm for the pointer-aimed modes therefore aims at the lane's far
  extremity, flipped when the acting fleet stands on it (a zero-length probe heading is the
  game's own refusal).
- **Seven of the nine targeting cursors write a hover readout from `OnCursorEnter`**
  (obliterator: ETA + star-destruction odds + protection warning; take-system, time-bubble,
  the `EntityActionCursor` pair, hacking-program: failure infos). `ProbeLaunching` and
  `CoordinationRequest` declare no enter readout (pointer-aimed), and `HackingOperation`'s
  enter also STORES `hoveredCursorTargets` for its own click, so replaying it would re-aim
  the mouse's next click. `IGuiService.SetFailureInfos` is an EVENT — `GameOverlayTooltipPanel`
  is only a subscriber and can hold stale text with `Visible=false`, so the event, not the
  panel, is the oracle. A VALID target makes four of the modes write nothing at all, and the
  obliterator refuses a non-Behemoth fleet with an EMPTY FailureInfo list.
- **While a TARGETING CURSOR is current, the left click means confirm and nothing else.** There
  are NINE such classes — eight declare `HasUserInstructions` (`ProbeLaunching`,
  `CoordinationRequest`, `TimeBubble`, `ObliteratorFire`, `TakeSystem`, `HackingProgram`,
  `HackingOperation`, `EntityActionCursor`) and `EntityActionCursor`'s two subclasses
  (`PirateMarkCursor`, `HonorActionCursor`) inherit it. All override `OnCursorClick` without
  calling base and return false from `ValidateSelection`, so select and zoom never run under a
  targeting mode — which is what makes Enter-as-confirm the parity answer rather than a
  competing binding. Two aim at the POINTER rather than at a cursor target (`ProbeLaunchingCursor`,
  `CoordinationRequestCursor`), so a confirm for those goes through the order they post. **The
  right button is answered inside each cursor's own `OnCursorClick` and NONE of the nine right
  branches reads a cursor target**: a cancel for seven, one waypoint back or the prompt closed for
  the hacking pair — which is why the mod's Backslash-while-armed needs no node. **Escape is not
  uniform**: six cancel via `GuiManager.cs:2101-2120`, the hacking pair via
  `ScanOverlayWindow.HandleInput:145-181`, and `TakeSystemCursor` has NO Escape route at all —
  its own banner says "Right Click to cancel" and with it up, Exit reaches `GameMenuModalWindow`
  (the mod claims Escape only there and runs that cancel, owner-ruled).
  `HasUserInstructions == true` is exactly that nine-mode set, so it is the banner predicate. The
  instruction window can briefly show the PREVIOUS mode's caption on entry (stale until the next
  refresh).
- **`PanelFeatureProbeFleetActionInfo`'s captions live in the PREFAB** (`%ProbeStockTitle`
  sibling labels), so the "default" reader pairs "Exploration Probes 2/2" correctly — a
  feature class on `default` is only a defect when the DRAWN feature divorces value from
  caption. The game gives Launch Mining Probe the same prefab, so a mining stock is captioned
  "Exploration Probes" — the game's own mislabel, mirrored not corrected.
- **`OrderCreateTimeBubble` does not land from the REPL** (it needs `TimeBubblesStock`, and
  `OrderAddTimeBubbleStock` does not land either); the public route is
  `DepartmentOfTheInterior.CreateTimeBubble(guid, definitionName, node)`.
- **Camera culling is not an information gate.** Every `VisibleEntityLabelsWindow` (probes,
  obliterator projectiles) and the coordination-request window make TWO separate tests per label:
  `RefreshLabelsCulling` keeps only the entities Unity's own `CullingGroup` reports inside the
  world camera (`GalaxyEntityCulling`, registered against `CameraPreRenderHookHandler.WorldCamera`,
  no distance bands set), and `ShowOrHideIfVisibleByEmpire` then applies the real knowledge gate,
  `Visibility[lookingEmpire] >= 3` (Visible). Only the second is about what the player may know.
  **Mod policy:** anything enumerating these things reads the SIMULATION
  (`DepartmentOfDefense.Probes` / `.ObliteratorProjectiles`,
  `ICoordinationRequestRepositoryService`) with the `>= 3` gate, never the drawn-label list — the
  probes were on the drawn list and a whole scanner category disappeared when the camera moved
  (`MapVisibility.Sighted` is that gate; `GalaxyHudScreen.Anchor` is the worked example). The
  label is still attached when one is drawn, because the game assembles a probe's DOSSIER onto the
  label's tooltip at draw time and there is no other source for it — everything else a row says
  (`GuiProbe.Title`, `GuiProbe.RemainingLifetime` + `[turn]`, the owner) comes off the entity.
- **An ally PIN and an obliterator MISSILE are recomposable in full, so neither needs its label.**
  A missile's whole reading is arithmetic on the entity (`ObliteratorProjectileLabel.Refresh`):
  turns = `Ceil(|position − Destination.GalaxyPosition| / Speed)`, or 99 at zero speed; the tooltip
  is `%ObliteratorProjectileLabelDescription(turns, destination)` and the countdown `turns + "[turn]"`
  — **both written for the player's OWN missile only**, which is the game's ruling on what an empire
  may know, and its knowledge gate is the probes' (`Visibility[empire] >= 3`). A pin's message is
  `CoordinationRequest.Message` (the label's field is assigned from it every refresh, so the entity
  is the source and the field is only a possibly-truncated rendering of it); its two sentences are
  `%CoordinationTools⟨RequestType⟩CoordinationRequestTooltip` plus a sender line that branches on
  ownership (`…SenderCoordinationRequestTooltip`, or `…ReceiverCoordinationRequestTooltip` with the
  owner's name + faction); and its DISMISS is two deterministic routes, not a widget click
  (`CoordinationRequestLabel.OnDismissCb`): your own pin posts `OrderRemoveCoordinationRequest`,
  anybody else's is `SetForceHidden(true)` + `UpdateVisiblity(playerEmpire)` — and the label's own
  `Hide()` need not be replayed, because the request raises `VisibilityChanged` and any label hides
  itself off that. A pin's knowledge gate is `CoordinationRequest.IsVisible(empire)` (not
  force-hidden, and shared with the alliance). **Mod policy:** that gate and nothing else — the other
  half of `CanShowRequestLabel`, `ICoordinationRequestRepositoryService.ShowRequestToggle`, is the
  player's global "draw the pins" switch, and whether a reader obeys a display toggle is a design
  question rather than a fact about knowledge (left unobeyed, flagged to the owner).
- **A mining probe is surfaced only in the planet's dossier** (`PanelFeatureMiningProbe.Bind`
  :15-58), and its gates are split: the owner's leader name is written for ANY empire's probe
  (`%PanelFeatureMiningProbeDescription` + `GuiEmpire.GetLeaderName`), while the yield and the
  remaining turns are written for the player's OWN probe alone — and a player's own probe with no
  yield hides the whole feature. `GuiPlanet` is the `IMiningProbeBonusProvider`.
- `PlanetCuriosityItem` is Class-backed yet its `Content` holds real words (`FormatFailureInfos`,
  written in `Refresh`), so the refusal reads off `Content` while the name comes from the wrapper
  (there is no Title label).
- **Probe, obliterator projectile and coordination request carry a bare `GalaxyPosition`** — no
  node, no link; `Fleet` alone stores a leg. `ProbeLabel` draws a countdown only for your OWN
  probe, and `ObliteratorProjectileLabel` writes destination and ETA only for yours.
  `WreckedMothershipLabelWindow` binds `FocusedGameNode` and its items follow the curiosity
  pattern.
- **The game's Space is `ToggleScanView`** (`InputManager.cs:233`, one binding shared with Mouse2) —
  the strategic lens mode that sets `IsInScanView`, drops `IsInNormalView` (hiding the pinned quest
  and most HUD) and repaints the whole map, modelled by `ScanViewScreen`. The mod's drag key
  therefore claims Space only where it can act — a pick-up on the focused control, a live carry,
  or a search collecting the space as text (`ModEntry.CarryKeyClaimed` →
  `GraphNavigator.TakesCarryKey`, owner decision 2026-08-12, after the blanket claim of
  2026-08-11). Everywhere else the key reaches the game and `ScanViewScreen` announces the lens,
  which is what made the hand-back safe; the lens keeps its Mouse2 route. The ONE page that hands
  nothing back is the star-system management page, whose Space the mod claims on every node
  (owner ruling 2026-08-26, `docs/interaction.md`): the scan button drawn there is what still
  reaches the lens.
- **The scan view is a MODE, not a view level**: `IsInNormalView` goes false and only
  `EndTurnWindow` survives, while `TopTitlePanel` keeps the lens-naming label even hidden.
  `ScanViewWindowCaptionsPanel` is a pool that does not clean up (surplus children stay fully
  visible with stale words, arranged past the table's extents), so counts come from the lens's
  own `GuiElement` data through `Prerequisite.Check`. `BattleScanViewWindow` has no header (fall
  back to `Shown`), and `StarSystemOrbitalScanViewWindow` is an unregistered stub.
- **A layer band is NOT a lens: nine descriptors map onto six lens titles**
  (`TopTitlePanel.Load`, :116-124 — Painting+GalaxyMap = Diplomacy, InformativeGalaxy+Constellation
  = Trade, Systems = Economy, System+SystemOverview = the system overview, plus SystemManagement
  and PlanetOverview from the view LEVELS). So three descriptor boundaries fall inside one title
  (steps 0→1, 3→4, 11→12), and crossing one still re-runs the per-layer alpha/position tables over
  the lens window, its sections and every label (`GuiLayeredScanViewWindow.cs:64-88`,
  `LabelMetaModifier.cs:233-262`) — sub-panels and label lines appear and disappear.
  `GalaxyLayerController.cs:78-83` early-returns on an unchanged DESCRIPTOR name, so the descriptor
  is the identity of the drawing and the title is only its heading.
  Mod policy (owner ruling 2026-08-17): `ScanViewScreen.AnnounceLens` speaks the lens at every
  descriptor change, same-name boundaries included — a repeated "Trade" is cheaper than a silent
  redraw.
- **`TradeRouteRenderer` draws in scan view only, own routes only, per-LEG with an undirected
  merge** (three materials: open/blockaded/mixed; the blockade flag ACCUMULATES down a route's
  path, and a route blockaded at either end draws blockaded from its first leg — the picture,
  so the mod copies it). It computes once on entering scan view and never refreshes mid-mode;
  the Economy lens legend captions only two of the three colours. The fixture cannot create a
  trading company (`CreateTradingCompanyPreprocessor` needs the HQ tech AND the improvement
  built — `DepartmentOfCommerce.cs:816-855`).
- **The scan system BAND never draws planets**: `StarSystemManagementScanViewWindow` binds only
  while `FocusedStarSystemNode != null` — the planets belong to the management lens one rung in.
  `StarSystemManagementScanViewPopulationSynergyItem` carries NO AgeTooltip anywhere (the icon
  table names its textures); `PlanetStatusGroup` carries none either.
  `%BonusPopulationDefenseTitle` is absent from localization (the ExtendedGuiElement's AltTitle
  exists).
- **A scan BAND writes no words of its own.** `ScanNodeLabel` has no text: the planet dots and the
  trade dial carry everything, and all of it on CLASS-backed tooltips, so an `AgeWidgets.DrawnLines`
  reading of a band returns the system's name and nothing else — the content has to be read control by
  control. `PlanetCircleItem.Content` is the RAW internal name (the spoken name comes from the
  `GuiWrapper`), and `TradeCompanyGroup` is a SIBLING of `ContentTable`, not a child, so a walk of the
  table misses it.
- **The scan band's gate is painted-ness, and only painted-ness.** `MainMetaModifier.TargetAlphas`
  fades a whole band per camera layer, and the `metaModifiers` a label collects in `Awake` never
  animate the POOLED circles (which are created later), so neither the modifier list nor `Visible`
  answers what is drawn — `AgeWidgets.Painted(ContentTable)` is the band gate and the circles' own
  drawn alpha is the per-dot one.
- **The governor's panel on the system-management lens has no words for the two things it is
  ABOUT.** `StarSystemManagementScanViewHeroPanel` is shown only where the system has an
  `AssignedHero` (`StarSystemManagementScanViewWindow.Bind`), and measured on the drawn panel it
  carries NO `AgeTooltip` anywhere — not on `EfficiencyGroup`, `HeroEfficiencyIcon`, the portrait or
  the root. The hero's NAME is drawn nowhere on it (the portrait is the identity) and lives in the
  panel's private `guiHero`; the dial is geometry alone — `RefreshEfficiency` counts the governor's
  colonized-system skills whose modifier paths are currently valid, divides, and writes the ratio to
  `EfficiencySector.MaxAngle` as an angle, so `MaxAngle / 3.6` IS the percentage and re-deriving the
  skill math would be a second copy of the game's counting rules. The two captions it does draw
  (`%SystemManagementScanViewHeroEffectivenessTitle`, `…HeroOutputTitle`) are plain prefab labels,
  not fields of the class, so they are read as drawn lines rather than by name; the output half
  hides `OutputContentGroup` and shows a `%None` label when the governor adds nothing, and the
  hidden group keeps the prefab's placeholder "999 [prestige]" text, so the reading must be
  `PaintedLines`.
- **The planet lens draws a THIRD table nothing else mentions.** `PlanetScanViewWindow` has
  `PlanetRemainsItemsTable` under the right-hand column (rect 1050,260,220,480), filled from
  `Planet.Remains` and drawn per item only where `!remains.Definition.VisibleInSystemOverview`
  (`PlanetRemainsItem.Refresh`) — each a title plus a paragraph. `unlocked` has no remains on any
  planet of Xiu, so the table is drawn EMPTY there and a stats-only reading of the lens looks
  complete.
- `ScanViewDiplomacyLabel` draws exactly ONE line: on your own home system `SwapToggle.Enable` is
  false, so the second variant never appears.

## Map, camera and view levels

- **"Go and look at THIS place on the map" is three calls, and they nest.** `IGuiGameWindowService`
  is where every reveal in the game ends up, and only three of its members move the galaxy view:
  `RequestGalaxyOverviewViewLevel(IGameEntityWithGalaxyPosition)` (`GuiManager.cs` :1170) forwards
  straight to `RequestGalaxyOverviewViewLevel(Vector3)` (:1175) and DROPS the entity, and
  `ShowQuestLocation(Quest, QuestStep)` (:1264-1286) picks a marker and then calls the same `Vector3`
  overload. So a patch on "the call site" gets the poorest signature — hook all three, and note that
  postfixes fire inner-first, so the richer outer capture naturally overwrites the poorer one it
  caused. Measured: 51 player-facing flows (notifications, panel locate buttons, table double clicks,
  the traitor banner, the next-idle-fleet button) reach the map through them.
  `ES2Access.Screens.GalaxyLocate` is that capture; `GalaxyHudScreen.OnUpdate` consumes it.
- **`ShowQuestLocation` CYCLES markers** through a private `lastShownMarkerIndexByQuest`, keyed on
  quest name + step name — press the pin twice and the camera goes to the next marker. Nothing needs
  to read that dictionary, though: the method resolves its marker and then makes the ordinary
  position request with it, so a hook on the position call already has the chosen marker's own
  position. A quest with NO markers makes no request at all and moves nothing.
- **`RequestStarSystemManagementViewLevel` silently degrades to a galaxy centre.** For a system that
  is blacked out (:1224-1228) or that the player neither owns nor has a traitor in (:1244-1247) it
  calls `RequestGalaxyOverviewViewLevel(component.Position)` instead — no page opens, and the only
  feedback a mouse user gets is the camera sliding. Measured from `unlocked` on a non-owned system:
  the reveal capture fires and the mod says "Shown on the map" rather than announcing a page.
- **`GalaxyView` has two `SelectGameNode` overloads and they do different things.** The one taking a
  `GameNode` force-zooms (`SelectNode` → `ZoomInOnNode`); the one taking the map's own `GalaxyNode` —
  which is what a real left click reaches, via `GalaxyStarSystemCursorTarget.GalaxyStarSystem` — asks
  the colonized-star-system repository first and branches to
  `RequestGalaxyViewLevelChange(typeof(GalaxyViewLevel_SystemManagement), …)` for a colony of the
  player's (`GalaxyView.cs:110-166`), force-zooming only for everything else. **Measured on a
  colonized system, though, that branch does NOT leave the galaxy view**: it lands where
  `ZoomInOnNode` does — zoom step 13, galaxy page still focused, orbital cards drawn (2026-08-20).
  **Re-measured 2026-08-29 and it DID leave**: a replayed click on Dusay (home, `[Beginner] test`
  turn 21) opened the system's own page — "Zoom level 14 of 15", "Dusay, System management", the mod's
  stack showing `screen.star-system`. So the branch is state-dependent and neither reading is safe to
  build on; anything that must know asks the level afterwards. The pointer watch is written not to
  care (`GalaxyPick`): the page going away drops the pick on the pop, and coming back out of the page
  is an arrival that seats the cursor anyway.
  So "what the left click does" is not one answer, and a mod that wants the zoom must call
  `ZoomInOnNode` (or `GalaxyViewLevels.ZoomTo`) rather than the click's own entry point.
- **Every camera move the map makes for a POINTER goes through two calls on `GalaxyView`, and none of
  them passes through `GuiManager`** (census 2026-08-29, the whole `Assembly-CSharp` call graph of
  both names). `SelectGameNode(GalaxyNode)` and `ZoomInOnNode(GalaxyNode)` are the doors; the
  `GameNode` overloads of both funnel into them (`SelectGameNode(GameNode)` → `SelectNode` →
  `ZoomInOnNode`). What arrives there: a LEFT CLICK on an explored star and on a wrecked mothership
  (`GalaxyCursor.OnCursorClick` :150, :165), the WHEEL scrolled in past the deepest step over a
  hovered star (`GalaxyViewCameraController.HandleScrollwheel` :652), and the nine fleet actions whose
  press only brings the camera in — the five `FleetActionButton*`/`FleetActionToggleReclaimMothership`
  ones and `EmpireLocalActionTogglePlanetConstruction.OnToggle` (:23-38), which Terraform, Restore and
  Reduce Anomaly all inherit. Because none of it is a `GuiManager` reveal, `GalaxyLocate` never saw any
  of it and neither did the moved-count: a mouse click moved the picture out from under the page's
  record of it, and the record then swallowed every later attempt to come back in. **Mod policy
  (2026-08-29):** `ES2Access/Screens/GalaxyPick.cs` patches the two doors — every arrival COUNTS a
  move (`GalaxyViewLevels.Moved`) and the node is remembered for the page to seat the tree cursor on
  — through the SAME rule an arrival nobody asked for goes through (`GalaxyHudScreen`'s
  `FollowCentredSystem`/`SeatOnCentredSystem`: one rule, two triggers, differing only in how the
  system is named — an arrival has to ask the camera, a pick says so itself, `ArmPickSeat`). Passive,
  as that rule already was: a cursor reading the map follows and the landing announces itself, a
  cursor reading anything else is left alone and the map stop's remembered row is re-seated silently,
  and a reveal, a fleet action's seat or a fleet-panel handover all name their own place first and
  win. Measured 2026-08-29 on `[Beginner] test`: with the cursor on the End Turn button a replayed
  click on Qarius moved the camera, said nothing and moved no cursor, and the next Ctrl+G landed on
  Qarius; with the cursor on Dusay's row a replayed `ZoomInOnNode(Qarius)` seated and announced
  Qarius one frame later. The mod's own movers are all elsewhere by construction
  (`SnapTo`/`ZoomTo`/`ZoomToStep` drive the LEVEL's `ZoomInOnNode` or the controller; `CenterOn` is
  `CenterOnPoint`; `PanTo`/`OpenSystem` are `GuiManager` calls) — except the zoom ladder's deepest
  step in (`GalaxyViewLevels.EnterSystem`, which takes the click's own path on purpose) and it marks
  itself `GalaxyPick.ByZoomKey`, because a zoom the player made by hand is the one exclusion the count
  keeps (verified live 2026-08-29: the zoom ladder stepping into Primus left `Moves` at 8, recorded no
  pick, moved no cursor and said nothing). **The wheel's deepest step is TRACKED — owner ruling
  2026-08-29** — and the mod stays in sync with it exactly as with a click: it is the one wheel notch
  that changes WHICH place is shown rather than how close it is, so it belongs with the click and not
  with the hand zoom. The ordinary wheel steps (`StartZooming`) still count nothing.
- **The map's right-click undo of a zoom RELOCATES the camera, which is why it is counted while the
  zoom keys are not.** `GalaxyViewLevel_GalaxyOverview.RestoreZoom` (:147-153) calls
  `RestoreLastCameraParameters()`, putting the camera back at the position AND step it had before the
  forced zoom — a different place, not merely a different closeness, so a record still naming the
  clicked star would be describing a picture nobody is looking at. The hand-zoom exclusions all leave
  the camera over the same place, which is the whole difference. It is the only caller in the game
  (`GalaxyCursor.OnCursorClick` :128) and the mod's own `GalaxyViewLevels.RestoreZoom` helper has no
  callers, so counting it on `HasZoomBeenForced` — the same flag the click tests — counts exactly the
  right clicks. Measured 2026-08-29 on `[Beginner] test`, which is the evidence for the split: after a
  click-zoom onto Qarius, `RestoreZoom` put the camera back on PRIMUS (85.0, -1.9 from 64.0, 0.2) with
  `zoomStep` 12 both before and after — the place changed and the closeness did not. A force made at the closest step restores to where it already is (below), and one of
  those is counted too: the cost is a single extra re-frame onto whatever the cursor is reading.
- **The map's right-click undo of a zoom is per-VISIT.** `GalaxyViewLevel_GalaxyOverview.ZoomInOnNode`
  sets `hasZoomBeenForced` and `RestoreZoom` needs it; leaving the overview level and coming back
  (into a system's management page and out again) clears it while the CAMERA stays where the zoom put
  it — measured: `zoomStep` still 12, `HasZoomBeenForced` false. So "come back out" is an offer that
  can disappear under the player without the view changing, and a screen that reports it has to ask
  the flag every time rather than remember having zoomed. **And the converse traps too**: a force
  initiated while ALREADY at step 12 saves step 12 as the parameters to restore, so `RestoreZoom`
  with the flag TRUE can be a talking no-op (measured: the mod spoke, `zoomStep` unmoved,
  flag still set — the engine's restore does not clear it). The mod's backslash therefore never
  calls `RestoreZoom`: `ZoomToStep(node, DefaultZoomStep)` at the focused system whenever
  `ZoomStep > DefaultZoomStep`, which is deterministic in every state the camera can be in.
- **The galaxy camera has 13 zoom steps and only the LAST reaches orbital.** `CanFocusGalaxyEntity()`
  is `zoomStep == ZoomStepsCount - 1`; until then `FocusedStarSystemNode` stays null and
  `PlanetLabelsWindow_SystemOrbital` never shows, and the camera must also be within
  `DistanceMinToCatchFocusOnNode` of the node. Step 3 draws a system's name only, step 9 its whole
  label. `SetZoomStep()` alone swaps the drawn layer WITHOUT moving the camera. At step 12 the focused
  system's own label is pushed off the top of the screen (y ≈ -230). Camera layers per step:
  0 Painting, 1 GalaxyMap, 2-3 InformativeGalaxy, 4-5 Constellation, 6-9 Systems, 10-11 System,
  12 SystemOverview (default 9). **The game's keyboard zoom is unusable as shipped**:
  `ZoomIn:PageUp`/`ZoomOut:PageDown` defaults, but `KeyboardZoomStepByStep=False` so a TAP moves
  nothing (held ramp, one notch per 0.1 s); the galaxy camera answers by POLLING (its `HandleInput`
  is a stub); the system-management and planet-overview controllers answer `InputAction.ZoomIn/Out`
  only while `!AgeManager.IsMouseCovered`. (Operational use: the camera recipe in
  `test-recipes/galaxy-map.md`.)
- **`FocusedStarSystemNode` is where the camera IS, lagging - never where it was sent.** Measured
  2026-08-23 on `[Beginner] test`, four states:

  | what was done | `FocusedStarSystemNode` |
  |---|---|
  | camera in on Ita at step 12 | `Ita` |
  | `ZoomToStep(Ita, 9)` (the mod's own Backslash zoom-out), camera still over Ita | `null`, within 3 frames |
  | the frame `SnapTo(Dusay)` is called (step already 12) | `null` — `Dusay` only on a later frame |
  | mid-flight of a game-led `RequestGalaxyOverviewViewLevel(Heka)` from Dusay at step 12 | `Dusay` for the whole 5-frame slide, `Heka` only once it settles |

  So it is the orbital view's own "which system am I up over" (the `zoomStep == ZoomStepsCount - 1`
  gate above), recomputed from the camera's position a frame or more behind it. **Mod policy
  (2026-08-23): nothing that has to know where the camera is HEADING may gate on it** - a
  follow-the-cursor rule gated on it re-snaps after every zoom-out by hand (the value is null) and
  mis-answers mid-flight (the value is the system being left). `GalaxyHudScreen`'s camera rule keeps
  its own record of the place the camera was last sent to instead, cleared when the page pops. What
  the value is still exactly right for is the two questions it already answers: "is the orbital-card
  surface up" and `Collapse`'s "is the camera still inside the branch I am closing".

- **Selecting a docked fleet frames the FLEET, not its star — close enough to look like nothing
  happened and far enough to take the orbital cards away.** `EndTurnWindow.SelectIdleFleet` (the
  route the mod's own fleet nodes take for a parked fleet) reveals the fleet, and the reveal centres
  the camera on the fleet's docking slot. Measured 2026-08-26 on a docked fleet at an outpost:
  the camera moved from the star's own snap target (67.356, -31.546) to (65.613, -29.908) — 2.4
  units — at the SAME zoom step 12. That is outside `DistanceMinToCatchFocusOnNode`, so
  `FocusedStarSystemNode` went null, `PlanetLabelsWindow_SystemOrbital` hid, and with the cards went
  every planet row's action child: the row still said "1 curiosity" (the label knows) while the
  curiosity BUTTON had no node at all. The same picture — right zoom step, right neighbourhood, no
  focused system — is what any reveal aimed at a thing standing NEAR a star leaves behind.
- **Mod policy (2026-08-26): a camera move by anybody else makes the page's record unbelievable.**
  The record above is what makes the camera rule cheap ("already showing it, move nothing"), and it
  is a record of what the RULE did, so anything else that moves the camera leaves it describing a
  picture nobody is looking at — and it then swallows every later attempt to come back in on that
  place. Reality-polling is not the fix (a per-frame "is the camera really there" assert re-snaps
  after a zoom-out by hand, which the ruling of 2026-08-23 protects). So every such move is COUNTED
  (`GalaxyViewLevels.Moved`, called from the three reveal patches — suppressed or not, since
  suppression only means "not a place to send the cursor" — and from `CenterOn`), and the page's
  record carries the count it was written at: a record from before the last move is not believed.
  Deliberately uncounted: the zoom keys, the wheel and the drag, which is exactly the hand-zoom the
  ruling keeps — each of them leaving the camera over the same PLACE, which is what the exclusion is
  really about, and which is why the two pointer moves that do NOT (the wheel's jump onto a hovered
  star past the deepest step, and the right-click undo) are counted with the click since 2026-08-29,
  above. The bug this fixes was owner-reported: select a docked fleet, Escape, arrow onto a
  planet, and its curiosity was unreachable for the rest of the visit.
- **Mod policy (2026-08-26): the cursor being PLACED is what asks the camera, wherever the placement
  comes from.** A screen seating the cursor — a fleet-panel handover, a landing, the answer to a
  reveal — leaves the player reading that place exactly as their own arrow key would, so it goes
  through the one camera rule and the record decides (stale after the game framed the fleet: the
  camera comes back in; fresh: the hand zoom survives). That makes it the Escape itself, not the
  arrow after it, that brings the camera back — including where the player selected the fleet from
  its own row and the seat lands where the cursor already is.

- **The orbital labels window binds itself ONCE, as it is SHOWN — a camera that crosses between two
  systems in one frame leaves it drawing the one it left.** `PlanetLabelsWindow.OnBeginShow` is the
  only place `StarSystemNode` is assigned (from `FocusedStarSystemNode`; `OnBeginHide` nulls it), and
  `PlanetLabelsWindow_SystemOrbital.OnBeginShow` binds its `StarTooltip` in the same call.
  `GuiManager`'s visibility pass shows the window exactly while `FocusedStarSystemNode != null`, so a
  MOUSE rebinds it on every crossing without anyone arranging it: the camera FLIES, part-way between
  two stars nothing is inside `DistanceMinToCatchFocusOnNode`
  (`GalaxyViewCameraController.GetGalaxyEntityToFocus`), the focus goes null, the window hides, and it
  shows again bound to wherever the camera stopped. The mod's landing SNAPS (owner ruling 2026-08-22),
  so the focus steps system-to-system with no null between and the window is never hidden. Measured
  2026-08-24 on `[Beginner] test`: walking Up from Dusay's row into expanded Libra's last child, and a
  type-ahead landing on `Libra I` from inside Dusay, both left `FocusedStarSystemNode = Libra` and the
  camera at step 12 over Libra while the window still held **Dusay** — so Libra's planets had no
  orbital cards at all (`CardFor` matches by `Planet` reference and finds none among the other
  system's) and each world's tooltip fell back to the `PlanetCirleItem` on the star label, which at
  orbital zoom sits at the top edge of the screen. The ordinary row → row → child walk only looked
  right because the intervening `PanTo` is a 0.3 s flight that supplies the null frame — a faster walk
  breaks that one too. **Mod policy (2026-08-24): the page asserts the invariant every frame rather
  than trusting how the crossing was made** — `GalaxyHudScreen.ShowFocusedSystem` hides and shows the
  window (instant, so the cards are up on the same frame) whenever a window the game already has
  SHOWN is bound to something other than the focused system, and remembers the system it rebound for
  so a bind that will not take cannot become a show every frame.

- **The map's star-system LABELS only re-ask what to draw on frames where the camera MOVED — so a
  camera that is PUT somewhere starves them.** `StarSystemLabelsWindow.SpecificUpdate` (:340-352)
  gates `MarkLabelsCulling()` + `RefreshLabelsVisibilityAndPosition()` on
  `previousCameraPosition != position`, and each pass reads the galaxy entities' own culling flags,
  which the view updates on its own schedule. A FLOWN arrival asks on every frame of the flight and is
  bound to catch the answer; a SNAPPED arrival (`SnapTo` / `CenterOn` + `Settle`) asks exactly ONCE —
  on the frame the camera jumps, a frame before the culling has caught up — and then never again,
  because the camera is already still. The arrived system's label stays marked culled-out and hidden
  for the rest of the session, and with it everything the label carries:
  `RequestManagementViewButton`, `DiplomacyButton`, the conversion buy-outs, the pirate mark. Measured
  2026-08-27: the label read `Shown=False`, `CulledIn=False`, own `Visible=False` while the galaxy
  entity read `((IGalaxyEntityWithCulling)galaxyNode).Visible == True` — the WINDOW's cache was stale,
  not the game's culling. **Mod policy (2026-08-27): make the game ask the question a flight would
  have made it ask.** `GalaxyViewLevels.CatchUpLabels()` writes a far sentinel into the private
  `previousCameraPosition` while the window is shown (missing field ⇒ warn once and no-op), poked on
  the twelve frames after a snap and armed from BOTH `FollowPlace` branches — the open-sky PAN has the
  same bug (a landing on a system's own row at step 9 left the camera centred on Sabel with Sabel's
  label undrawn while three others were drawn). Three frames from snap to drawn, measured. Nothing is
  styled by hand: `ShowOrHideIfVisibleByEmpire` calls `Show()` only while the label is
  `Hiding || !Visible`, so the twelve pokes are no-ops by construction.
- **Two suspects in that chain that are NOT the cause** (probed 2026-08-27, recorded so the next
  reader does not re-derive them): the zoom-layer swap FIRES on a snap —
  `ILayerService.LayerDescriptorCurrent` went `SystemsLayer` (step 9) → `SystemOverviewLayer` (step
  12) with `StarSystemLabelsWindow.CurrentLayerDescriptor` matching, so the `SetZoomStep` →
  `SwapLayerDescriptor` equality guard never swallowed it (the two steps really are different layers —
  the per-step table above). And the window's `LayerService_LayerDescriptorChanged` forwarding the
  descriptor only to labels that are already `Shown` is SELF-HEALING:
  `StarSystemLabel.ShowOrHideIfVisibleByEmpire` (:1514-1539) calls `Show()` and then
  `OnLayerDescriptorChanged(Parent.CurrentLayerDescriptor)` itself (:1526-1527), so a label shown late
  fetches the current descriptor without anyone delivering it.
- **`StarSystemLabel` sits on a GameObject named "Child".** A visibility walk up the parent chain from
  one of the label's buttons that reports "hidden at `Child`" has reached the LABEL itself — not a
  style group inside it, and there is nothing between the two that would need un-staling.
- **The galaxy camera says when it has stopped flying, in two private fields.**
  `GalaxyViewCameraController.isZooming` / `isRecentering` (:134, :146) are set synchronously by
  `StartZoomingOnPosition` / `StartRecentering` and cleared by `StopZooming` (:831-833) and by the
  recentre's own SmoothDamp reaching its target (:963-978), so neither can hang.
  **The camera stopping is NOT the end of the arrival**: the orbital cards a system grows when the
  camera comes in bind over the frames AFTER the flight, the card's own words first and its row of
  buttons after that. Measured 2026-08-22 on Osulo I: at the frame the camera stopped the row read
  "Osulo I, Medium Mediterrane., Colonized, 2 of 8" and 300 ms later
  "Osulo I, group, Medium Mediterrane., Colonized, collapsed, 2 of 8". **Mod policy**
  (`GalaxyHudScreen.LandingSuspended` + `MapSettleFrames` = 20): a landing waits out the flight and
  twenty frames more — the same wait the fleet-action seat already spends on the same widgets.
- **`GalaxyViewCameraController.CenterOnPoint(point, damping)` takes a bare point** and SmoothDamps
  to it, auto-clamped to the galaxy (`ClampCameraPosition`) — the way to move the camera to empty
  space, where `GuiManager.RequestGalaxyOverviewViewLevel` needs an entity and trips the mod's own
  `GalaxyLocate` watch. Damping 0.3 (the game's own figure) reads as one smooth slide per keypress.
  `ForceZoomingOnPosition(step, point)` is the same thing with a zoom step, and is how a camera
  reading is restored exactly.
- **The galaxy's world axes are a fixed compass: +world X is EAST, +world Z is NORTH, and the camera
  never rotates.** `GalaxyPosition` is the flattened world position — `X = world.x`, `Y = world.z`
  (`GalaxyPosition.cs:38-42`) — so the bearing from one node to another, clockwise from north, is
  `atan2(Δx, Δz)` on those two fields and nothing else. Measured against the live camera's own
  `WorldToScreenPoint` at the fixture's home view: from Dusay (screen centre, 640,400 of 1280x800)
  Primus is bearing 38.3° and lands at screen +451,+492 (up and right), Qarius 347.8° at -130,+519
  (up), Rigel 253.8° at -737,-184 (left and slightly down).
  `GalaxyViewCameraController.StartRotating()` is private with zero call sites and the live camera
  reads `euler = (59.5, 0, 0)` — pitch only. Nothing spoken about direction has to track a yaw, and
  a screen that did would be handling a case the game cannot produce.
- **The empire's own origin on that compass is `DepartmentOfTheInterior.HomeSystemNode`**
  (`DepartmentOfTheInterior.cs:655`) — the one place a player already has in their head, which is
  what makes a coordinate pair mean anything. It is a plain settable property with no event, null
  until a home is chosen and replaced wholesale by a new game or a load, so anything caching it
  re-derives on the player empire changing IDENTITY rather than subscribing. Fixture `[Beginner]
  test`: Dusay at raw (68.884, -22.450), and the 13 systems the map names span -43..23 east and
  -42..34 north of it against a whole-galaxy span of -95..92 by -64..66.
- **The system-discovery cutscene is a VIEW LEVEL at the same layer as the galaxy** — a fleet
  action that selects an undiscovered system POPS the galaxy page 2-3 frames after the click
  (not a cover). Measured 193-352 frames (~10-18 s at ~20 fps); it hands the camera back at
  orbital zoom on the system with `DiscoveryStatuses[empire]` set true by the game itself.
  Gates: `EnableDiscoveryCutscenes` (gameplay option) and
  `Application.Preferences.DisableSystemDiscoverySequence`/`Force…`
  (`GalaxyViewLevel_SystemDiscovery.CanBeActivated:152-202`).
- **Stepping between planets re-enters the SAME view level with a NULL blink.**
  `Gui.GuiGameWindowService.CurrentGalaxyViewLevel` goes null for a few frames and the window
  unbinds its planet on every Previous/Next step; `GalaxyViewLevels.LevelThroughTransitions`
  is the view's own non-blinking answer. (Why the planet screen gates on it: its doc comment.)
- **`TopTitlePanel` is the only captioned cluster on the HUD.** It draws the view's own name over
  the zoom/scan controls (`TitleLabel` = "Galaxy View" on the map), while the pinned-quest panel, the
  notification strip and the top-left banner rows draw NO caption at all — measured on
  `/gui/age?window=GameOverlayWindow`: `ControlBanner` holds only `ScreenTogglesTable`,
  `EmpireBanner` only its three value areas plus `CurrentResearchArea`, and `StrategicsBanner` only
  `ResourceItemsTable`. So every word naming those panels and rows is necessarily mod-authored (the
  keys are listed in `interaction.md`), and the one cluster the game DOES caption is the one whose
  mod word deliberately overrides it — "View Controls" over the drawn "GALAXY VIEW", because the
  view's name says which page the player is on and the screen has already said that on arrival
  (owner ruling 2026-08-19).
- **The HUD's screen-strip icons can carry a badge tooltip of their own.** The Senate icon's
  `AdditionalIcon` hangs "The leading political party in the Senate" inside the toggle
  (`ControlBannerToggle`), which is the only place that sentence exists. Mod policy
  (`GlobalHud.AddScreenToggles`): every tooltip inside a toggle is declared in drawn order, the
  button's own speaking and the badges reviewable.
- **The arrivals at the map that "go and look at this" never sees, and the one call they all make**
  (2026-08-28). `GuiManager.RequestGalaxyOverviewViewLevel(Vector3)` (:1174-1200) asks for a view-level
  change ONLY when the current level is not the overview; already on it, it slides the camera with
  `CenterOnPoint`. So the three calls `GalaxyLocate` patches are the whole of "the game led the player
  somewhere on a map that was already up", and they are silent about the two ways the map is ARRIVED at
  with nobody having asked for a place: a save being loaded (`GalaxyView.ActivateAsync`/
  `ReactivateAsync` :379-392 call the private `ActivateGalaxyViewLevelAsync(DefaultGalaxyViewLevel,
  false, GetLocalEmpireMainSystemPosition())` — the empire's first colony, Dusay on `[Beginner] test`)
  and coming back out of a sub-view-level. Both, and every level-changing reveal too, funnel through
  `GalaxyViewLevel_GalaxyOverview.ActivateAsync(bool active, params object[] parameters)` (:46-111),
  which is an ITERATOR — a Harmony prefix on it fires synchronously when the enumerator is built, at
  the call, with the arguments readable. That is the mod's arrival hook (`ES2Access/Screens/
  GalaxyOverviewEntry.cs`); it stands down on `GalaxyLocate.Suppressed` and on a `GalaxyLocate` request
  already captured, which is what keeps a reveal made from INSIDE a system's page (the one reveal that
  really does re-activate the overview) to its one announced landing.
- **The activation's ENTITY is not where the camera goes, and the two ways out of a system's page
  disagree about it** (measured 2026-08-28, `[Beginner] test`). `GetCameraInitialTransformation`
  (:163-188) tests `activationFocusOnLastPosition` FIRST, so with `parameters = [true, entity]` the
  named entity only reaches `SetFocusedGalaxyEntity` and the camera restores
  `cameraTargetInitialPosition` — where the galaxy camera was before the page was opened. The game's own
  Escape out of a system page passes exactly that (`StarSystemScreen.HandleInput` :216-227,
  `RequestGalaxyViewLevelChange(GalaxyOverview, true, StarSystemNode)`), so paging from Dusay to Heka
  inside the page and pressing Escape lands the camera back on **Dusay** (68.884, -22.45, step 9) while
  naming Heka. The mod's own way out — the zoom slider stepping below the page, `GalaxyViewLevels
  .StepZoom(-1)` → `LeaveLevel` — passes `[false, entity]` instead, and that one really does centre the
  named system at orbital zoom (**Heka**, 67.356, -31.546, step 12). **Mod policy (2026-08-28): the
  system a tree cursor is put on comes from the CAMERA's own target and never from the activation's
  arguments** (`GalaxyHudScreen.CentredSystem`) — one rule that is right for both exits, for the load,
  and for a camera the game clamped or refused to move.
- **The galaxy camera is placed one frame AFTER the page is pushed** (measured 2026-08-28, per-frame
  trace of `TargetPositionCurrent` from the page's own update): on the frame the page arrives the camera
  still reads the position the map had before the system's page was opened, and from the next frame on
  it reads the new one and never changes again. Anything asking "what is the map showing" on an arrival
  therefore has to wait — the mod waits `ViewBindFrames` (12) and holds the page's own arrival
  announcement over it through `Screen.BetweenViews`, so the arrival names the system the map is showing
  once rather than the row the page was restored to and then that one.
- **`GameEntityGUID` is in the GLOBAL namespace** — `/eval` bodies that qualify it under a
  namespace fail to compile, and `RequestStarSystemManagementViewLevel` wants the NODE's GUID
  (`…ColonizedStarSystems[0].Node.GUID`), not the colonized system's, which throws
  `KeyNotFoundException` out of `GalaxyEntityFactory`.

## Planet cards, colonies and outposts

Moved to `planets.md`.

## Influence, colonizability and the scanner's kinds

Moved to `planets.md`.
