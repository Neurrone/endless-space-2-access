# Every ES2 notification, how to raise it from the REPL, and what the mod does with it

## 0. Resuming this work (read first)

**Purpose.** The owner wants every notification popup checked against the mod by RAISING it from the
REPL rather than waiting for the game to produce it. This file is the worklist: each entry below has the
event constructor, what the popup draws, the mod's coverage, and a ready `/eval` body where a raise is
possible. Work group A first (no variant, a body beyond title and description), then B (the roadmap's
own unsighted list), then spot-check C, and raise D only as regression.

**What is already done (2026-09-14).** The technique was proven on the new-content (DLC) popup:
`Services.GetService<IEventService>().Notify(new EventNewDownloadableContent(Gui.PlayerEmpire, dlc))`,
one per DLC enumerated through the NON-generic `IEnumerable` over `IDownloadableContentService`. Fixes
that came out of it, all committed: a `Variants` entry naming the popup's own title label as its lead so
the body reads in drawn order and arrival lands on the name (`7c7cb1c`); a `Variant.Notes` hook that reads
a picture whose only words are its tooltip (same commit, the tutorial badge); a multi-line drawn row now
announces its lines as separate parts and buffers each once (`d0c4362`, `8eb528e`). One GENERIC defect
was found and left for an owner decision: the popup screen pushes on the window's ready frame, which can
be several frames before the popup's own labels are written, so the cursor can land on a control that
exists early (measured 9 frames on the DLC popup; recorded in `docs/notifications.md`). A general fix
would hold the landing until the popup settles, across all windows.

**Operating rules (owner rulings).**
- NEVER activate Done, Dismiss, Validate or the close-all control, and never inject `ui.click` on a
  popup's button bar: those delete notifications the owner wants to inspect. Leave raised popups PENDING
  (minimized) at the end and say how many.
- Minimize: a physical Escape (`POST /key`, which pops the next unread one) — an injected `ui.back`
  answers `unconsumed` on a popup because Escape is the game's key. The clean routes from `/eval` are
  `Gui.GuiNotificationService.HideAllGuiNotifications()` (hide, keep) and
  `Gui.GuiNotificationService.ToggleGuiNotification(n)` to re-open a strip row (hides all, shows that one,
  no next-unread). Opening a row sets its `AlreadyRead`; put it back through the non-public setter if it
  matters.
- A stale `CurrentGuiNotification` silently blocks every later popup: clear it through its private
  setter before a raising session and leave it null at the end.
- The scan-table bindings (20) never open a window; raising one tests only the arrival line.
- Read `docs/dev-loop.md` (routes, REPL gotchas, stage hygiene) and `docs/notifications.md` (measured
  popup facts and mod policies) before touching source; the fix for a popup is normally one entry in
  `ES2Access/Screens/NotificationScreen.Variants.cs` (fields: `Words`, `Tables`, `Choices`, `Cards`,
  `Expanders`, `Badges`, `Confirm`, `Gateways`, `Timer`, `Notes`), never a per-popup reader.

**Verification kit per popup.** Baseline `GET /speech?since=0` for the cursor, raise, then: the arrival
line from `/speech`; `GET /gui/graph?edges=1&buffers=1` (the popup screen fits the 800-line cap);
`/gui/age?window=<Window>&visibleOnly=1` with rects for the drawn order; `DevProbe.NotificationParity()`,
`TooltipParity()`, `Coverage()`, `Ghosts()`; a `crop-shot.ps1` of the popup rect; a walk with `/input`
(`ui.down`/`ui.up`/`ui.left`/`ui.right`, Tab) reading `/speech`. Compare the mod's order with the drawn
order; a fix is proven by the same set again after `/reload` and a re-raise (raises are repeatable).

**Progress.** Tick entries here as they are sighted, with the commit that fixed them or "clean".
- [x] NewDownloadableContentNotificationWindow — fixed `7c7cb1c`, `d0c4362`, `8eb528e`
- [ ] Group A (12 windows), group B, group C, group D — untouched

---

Scope: the 162 `BindEventAndNotification` entries in `GuiNotificationManager.BuildGameEventToNotificationMapping`
(`decompiled/Assembly-CSharp/GuiNotificationManager.cs:576-742`) plus the 20 `BindEventAndScanNotification`
entries (`:172-192`). 151 distinct `GuiNotification` subclasses, 67 distinct notification windows,
44 of which the mod models specifically in `ES2Access/Screens/NotificationScreen.Variants.cs`.

Everything below is read off the decompile and the mod source. Nothing here was raised or measured live
this session; where a claim rests on something I did not read to the bottom it is marked **unverified**.

---

## 1. How the pipeline decides

`IEventService.EventRaised` → `GuiNotificationManager.EventService_EventRaised` (`:843-858`):

- an `EmpireEvent` goes to **one** empire — `empireEvent.Empire` — so `new EventX(Gui.PlayerEmpire, …)`
  reaches the player and nobody else;
- anything else (a bare `GameEvent`) is recorded **for every empire in turn**, so the player gets a copy
  whatever empire the payload names.

Then `RecordEventForEmpire` (`:742-804`):

1. **Exact type lookup, no base-class fallback.** `guiNotificationTypeByEventType[gameEvent.GetType()]`,
   else `scanGuiNotificationTypeByEventType`, else the event is dropped silently. An event type not in
   either table produces nothing at all.
   - Consequence worth knowing: `BindEventAndNotification(typeof(EventOnPlanet), typeof(NotificationCuriosityDiscovered))`
     (`:651`) is a **dead entry** — `EventOnPlanet` is `abstract`
     (`decompiled/Assembly-CSharp/EventOnPlanet.cs:3`), so no instance can ever have it as its exact type.
2. **Stackable notifications rebind instead of being created** (`IStackableNotification` + `IsRelated`),
   and that path fires `CollectionChangeAction.Refresh`, which the mod never speaks. So a second raise of
   e.g. `EventConstructionCompleted` in the same turn adds a LINE to the standing popup rather than
   producing a new one. The stackables are: `NotificationConstructionsCompleted`,
   `NotificationConstructionQueueEmpty`, `NotificationLawCancelled`, `NotificationPopulationChange`
   (gained/lost/almost-lost), `NotificationTradingBlockade`, `NotificationTreatiesCancelled`,
   `NotificationRelicsCollectionCompleted`, `NotificationRelicsCollectionCanceled`,
   `NotificationOnEmpireRelicSlotLocked`, `NotificationHackingStatusChanged`.
3. **`Bind(gameEvent)` returning false vetoes creation** — nothing is added and nothing is announced.
   29 notification classes carry vetoes beyond the type check; they are listed per entry below.
4. Inserted by ascending `Priority` (game types use -2/-1/0 only), then
   `PlayerEmpireNotificationsCollectionChanged(Add)` fires — this is what the mod's arrival announcement
   (`ModNotifications.Arrived`) hears.
5. **Auto-popup** is asked last, at `:800`: `flag` (a NON-scan mapping) && `empire == Gui.PlayerEmpire`
   && `CanShowNotifications` && `CurrentGuiNotification == null` && (`AutoPopUp || ForceAutoPopup`).

`ShowGuiNotification` (`:511-535`) then refuses **again** if `PauseNotificationPopping && CanBeDelayed`.

### What swallows a raise

| Cause | Symptom | Notes |
|---|---|---|
| Event type not in either table | nothing at all | includes `EventOnPlanet` (abstract, dead entry) |
| `EmpireEvent` naming another empire | nothing for the player | pass `Gui.PlayerEmpire` |
| `Bind` veto | nothing at all | per-type; see the entries |
| **SCAN table membership** | notification exists, popup NEVER opens | the 20 scan bindings; `:786-788` inserts into a second list and `:800` is gated on `flag`. `docs/notifications.md` records the measurement. The mod announces these on arrival by title (`ModNotifications.Standing`). |
| `AutoPopUp` off | notification appears on the strip only | `GetAutoPopupStatus` (`GuiNotificationOptionsManager.cs:2284`) returns **true when no `AutoPopup<Type>` property exists**, and `BindEventAndNotification` seeds each existing property from the registry with `defaultValue: true`. So everything pops unless the player turned it off. 13 bound types have no toggle at all and therefore ALWAYS pop: `NotificationCompetitiveQuestWarning`, `FactionSwitched`, `InfluencedSystemPopulationConsumed`, `MarketplaceAdvertisementPosted`, `OutpostLocked`, `OutpostLockedWithHonor`, `PlanetDestroyed`, `PoliticsUnlocked`, `ResourcesTransferred`, `TradingHeadquarterDestroyed`, `TradingHeadquarterLost`, `TradingSubsidiaryDestroyed`, `TradingSubsidiaryLost`. |
| `CurrentGuiNotification != null` | queued, not lost | the next Dismiss/Minimize opens it (`docs/notifications.md`). **A stale `CurrentGuiNotification` also silently blocks every subsequent popup** — clear it with the private setter recipe in `docs/notifications.md` before a raising session, and leave it null at the end. |
| `PauseNotificationPopping && CanBeDelayed` | arrival silent, popup never comes | the accepted cost named in the 2026-08-28 owner ruling |
| `CanShowNotifications == false` | no popup | set by the game around non-interactive states |

Title and description default to the class's own **GuiElement registry entry**
(`GuiNotification.Load` → `Gui.GetExtendedGuiElement(GetType().ToString())`), not to code — which is why a
missing or untranslated key shows up as a raw `%key` rather than as an exception.

### What the mod does with a popup that has no variant

`NotificationScreen.Build` reads: the popup's words (the shared description label, but only when it is
**painted inside the window's own tree** and is neither a raw `%key` nor an unfilled `{0}` template),
then either a scroll-view sheet, a declared table sheet, or the drawn rows, then the two control strips.
So a popup whose content is **text the game drew** is served by the generic path. A popup whose content
is a **picture** (a gauge, a chart, a portrait, an icon whose only words are its tooltip), a **cloned-line
table with a caption band**, a **card prefab**, or a **choice set** is the risk: the generic reading has
nothing to read there. That is what sorts group A below.

---

## 2. Groups, most suspect first

### Handle bank (paste once per session; `var`s persist across `/eval` requests)

```csharp
var EV = Amplitude.Unity.Framework.Services.GetService<Amplitude.Unity.Event.IEventService>(); var EMP = Gui.PlayerEmpire; var CSS = (System.Collections.IList)Gui.PlayerEmpire.GetAgency<DepartmentOfTheInterior>().ColonizedStarSystems; var SYS = (ColonizedStarSystem)CSS[0]; var NODE = SYS.Node; var FLL = (System.Collections.IList)Gui.PlayerEmpire.GetAgency<DepartmentOfDefense>().Fleets; var HRL = (System.Collections.IList)Gui.PlayerEmpire.GetAgency<DepartmentOfEducation>().ActiveHeroes;
```

REPL rules this obeys (`docs/dev-loop.md`, "REPL gotchas"): fully qualified, no `using`, **no local whose
type is a constructed generic over a game type** (so `CSS`/`FLL`/`HRL` are bound as `System.Collections.IList`;
a `List<Fleet>` local or any `foreach` over one poisons the session), no captured delegates, index-based
iteration. Passing `new System.Collections.Generic.List<T>()` **inline as an argument** is fine — only a
declared local is the poison.

Every raise below is then one statement of the form
`EV.Notify(new EventX(EMP, …));` — `Notify` returns void, so wrap in an IIFE if you want a string back:
`((System.Func<string>)(() => { EV.Notify(new EventX(EMP)); return "raised"; }))()`.

---

### Group A — no mod variant AND a body beyond title + description (most likely bugged)

Twelve windows. Alphabetical.

---

#### ConstellationEventNotificationWindow

- **Event**: `EventConstellationOwnerChanged(Empire empire, Constellation constellation, bool contestantLostControl, bool hadPreviousContestant)`
- **Notification**: `NotificationConstellationOwnerChanged` (no location; `HasLocation` false)
- **Popup draws**: shared title/description (both `Gui.Localize(…, Constellation.LocalizedName)`; the
  GuiElement is picked per outcome — `…Gained` or `…Lost` suffix — and the lost-to-a-contestant case uses a
  third key, `%NotificationConstellationOwnerChangedLostToEntityDescription`), **plus its own
  `ConstellationDescriptionGroup` containing `ConstellationEffectTitle` and a `GuiEffectMapper EffectMapper`**
  — the bonus the constellation confers, drawn as mapped effect widgets. No extra buttons.
- **Why suspect**: `SpecialNodeEventNotificationWindow`, the same shape, has a variant that declares both of
  its `EffectMapper.AgeTransform`s as tables (`Variants.cs:302-303`). This one has no entry, so the effect
  list is read only insofar as the generic drawn-row walk happens to find text in it.
- **REPL raisable: yes.**
  ```csharp
  EV.Notify(new EventConstellationOwnerChanged(EMP, Gui.Game.Galaxy.Constellations[0], false, false));
  ```
  (`true, false` = lost outright; `true, true` = lost to a contestant — three distinct descriptions, all
  worth a raise.)
- **Mod coverage**: no variant. Roadmap names "constellation event" in the SimpleDescription-family line
  under *"Notification variants awaiting a live sighting"* — i.e. the roadmap expects a baseline, and there
  is none in the table.

---

#### DefenseHackingProgramEncounteredNotificationWindow

- **Event**: `EventDefenseHackingProgramEncountered(Empire empire, GameNode gameNode, HackingOperation hackingOperation)`
- **Notification**: `NotificationDefenseHackingProgramEncountered : NotificationImportantHackingStatusChanged : NotificationHackingStatusChanged : NotificationOnGameNode` — `HasLocation` **true**
- **Popup draws**: title/description formatted with the node name, plus its own **`CancelHackButton`**.
  It also overrides `OnShowLocationCb` to pan and then `ToggleScanView()` (`:27-33`).
- **Why suspect**: an own action button with no variant, and the roadmap already records
  *"Ctrl+L's type dispatch (`NotificationScreen.GoToLocation`) is unproven on … the hacking popup"*.
- **REPL raisable: no.** `EventOnHackingOperation`'s constructor dereferences
  `hackingOperation.StartNode`, `.TargetNode`, `.GUID` and `.Target.NodePosition`
  (`EventOnHackingOperation.cs:11-25`), and `NotificationHackingStatusChanged.Bind` builds a
  `HackingStatusChange` whose constructor does `Instigator.Index` on an `Instigator` the 3-arg path leaves
  null. A `new HackingOperation()` has a null `Target` and NPEs before the event is even constructed.
  Alternative: run a real hacking operation into a defended node.

---

#### EventOnFleetNotificationWindow  (fleet finished auto-explore / finished sleep)

- **Events**: `EventFleetFinishedAutoExplore(Empire, Fleet)` and
  `EventFleetFinishedSleep(Empire, Fleet, SleepFleetAction.InterruptionCause)`
- **Notifications**: `NotificationFleetFinishedAutoExplore`, `NotificationFleetFinishedSleep` (both
  `NotificationOnFleet`; `HasLocation` is the base false — **but** `NotificationOnFleet.GetTitle` swaps to a
  `…Dead` key when the fleet is gone, so a raise on a destroyed fleet reads differently)
- **Popup draws**: title/description formatted with the fleet name, plus its own **`FocusButton`**
  (select-and-centre the fleet).
- **Why suspect**: two of the commonest in-game notifications, an own button, and no variant. Whether the
  Focus button reaches the player at all is unproven.
- **REPL raisable: yes** (needs the player to own a fleet).
  ```csharp
  EV.Notify(new EventFleetFinishedAutoExplore(EMP, (Fleet)FLL[0]));
  ```
  ```csharp
  EV.Notify(new EventFleetFinishedSleep(EMP, (Fleet)FLL[0], SleepFleetAction.InterruptionCause.None));
  ```
  **Veto**: `NotificationFleetFinishedSleep.Bind` returns false for
  `InterruptionCause.PlanetDestructionAvailable`. The other enum members are worth a sweep — the
  description is keyed off the cause (unverified which keys exist).
- **Mod coverage**: no variant, no roadmap line.

---

#### FactionSwitchedNotificationWindow

- **Event**: `EventFactionSwitched(Empire empire)`
- **Notification**: `NotificationFactionSwitched` — title and description straight from the GuiElement, no
  arguments
- **Popup draws**: title/description, plus **`NewPopulationGroup`** (an `AgeTransform` driven by its own
  `NewPopulationGroupModifierSet` — i.e. alpha-animated) holding **`PopulationLabel`**: the new population
  the switch grants.
- **Why suspect**: the content the popup exists to show is inside a group the game FADES in. `docs/notifications.md`
  records that a faded child is skipped by the engine's own `Paints` test, so the label can be absent from the
  reading during the animation and present after — and there is no variant naming it.
- **REPL raisable: yes.**
  ```csharp
  EV.Notify(new EventFactionSwitched(EMP));
  ```
  `Bind` calls `GetMajorGuiPopulation()` on the player's empire; on an empire with no population change the
  group is presumably left hidden (unverified).

---

#### ForceTruceAnsweredNotificationWindow

- **Event**: `EventForceTruceAnswered(Empire empire, Empire empireWhichAnswered, ForceTruce.State state, DiplomaticTribute[] diplomaticTributes, List<KeyValuePair<GameEntityGUID, float>> winnerCompensationsPerEntity, List<KeyValuePair<GameEntityGUID, float>> looserCompensationsPerEntity, float warExhaust, float totalWarExhaustCompensation)`
- **Notification**: `NotificationForceTruceAnswered`
- **Popup draws** (from `ForceTruceBaseNotificationWindow`): `WinnerTitle`/`WinnerLabel`,
  `LooserTitle`/`LooserLabel`, a **`BattlePowerGauge WarScoreGauge`** and `WarScoreLabel`. No breakdown
  tables (that is the *Proposed* sibling), no own buttons beyond the base.
- **Why suspect**: its sibling `ForceTruceProposedNotificationWindow` HAS a variant (the two breakdown
  tables + their expanders, `Variants.cs:379-391`) and the variant lookup walks base types — but the entry
  is on the concrete *Proposed* class, so *Answered* inherits nothing. The war-score gauge is a picture.
- **REPL raisable: with a fake.** `DiplomaticTribute` has a public constructor
  `(Empire provides, Empire receives, StaticString resourceName, float amountTotal, int totalDuration, Source source)`.
  **Veto**: `Bind` returns false when `EmpireWhichAnswered.Index == Empire.Index` — the answering empire must
  be someone else.
  ```csharp
  EV.Notify(new EventForceTruceAnswered(EMP, Gui.Game.Empires[1], ForceTruce.State.Accepted, new DiplomaticTribute[] { new DiplomaticTribute(Gui.Game.Empires[1], EMP, "ResourceDust", 100f, 10, DiplomaticTribute.Source.ForceTruce) }, new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<GameEntityGUID, float>>(), new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<GameEntityGUID, float>>(), 50f, 25f));
  ```
  The enum member names (`ForceTruce.State.Accepted`, `DiplomaticTribute.Source.ForceTruce`) and the
  resource name are **unverified** — check them against `ForceTruce.cs` / `DiplomaticTribute.cs` before the
  paste. Note the `GameEntityGUID` namespace may be global rather than `Amplitude.Unity.Game` (unverified).

---

#### HeroUpdateNotificationWindow  (five notifications ride it)

- **Events**: `EventHeroInjured(Empire, Hero)`, `EventHeroLevelup(Empire, Hero)`,
  `EventHeroRecovered(Empire, Hero)`, `EventHeroRecruited(Empire, Hero, bool notificationOnly = false)`,
  `EventHeroLeftUnassigned(Empire, Hero)`
- **Notifications**: `NotificationHeroInjured` / `HeroLevelup` / `HeroRecovered` / `HeroRecruited` /
  `HeroLeftUnassigned`, all `NotificationHeroUpdate` — `Bind` only wraps the hero in a `GuiHero`; each
  subclass overrides `GetDescription` only.
- **Popup draws**: `PortraitDisplay` (an `AnimatedPortraitDisplay` — picture only), an **`InspectGroup`**
  with `InspectLabel` and an `InspectButtonTooltip` (the own action: open the hero), and an
  **`UnspentSkillsGroup`** with `UnspentSkillsValue` — a bare figure whose caption lives in the group, not
  beside it.
- **Why suspect**: five of the game's most frequent notifications on one window with no variant; a figure
  with no caption on the same shape as the `PopulationChangeNotificationWindow` finding the roadmap already
  records; and an own button whose words are on a tooltip.
- **REPL raisable: yes** (needs the empire to own a hero — `ActiveHeroes` is non-empty).
  ```csharp
  EV.Notify(new EventHeroLevelup(EMP, (Hero)HRL[0]));
  ```
  ```csharp
  EV.Notify(new EventHeroInjured(EMP, (Hero)HRL[0]));
  ```
  ```csharp
  EV.Notify(new EventHeroRecovered(EMP, (Hero)HRL[0]));
  ```
  ```csharp
  EV.Notify(new EventHeroRecruited(EMP, (Hero)HRL[0], true));
  ```
  ```csharp
  EV.Notify(new EventHeroLeftUnassigned(EMP, (Hero)HRL[0]));
  ```
  The unspent-skills band presumably only shows when the hero has skill points (unverified) — raise
  `Levelup` on a hero with points and on one without.

---

#### LawBaseNotificationWindow  (forced law activated)

- **Event**: `EventForcedLawActivated(Empire empire, LawDefinition lawDefinition)`
- **Notification**: `NotificationForcedLawActivated : NotificationSimpleLaw` — `HasLocation` **false**;
  `Bind` keeps only `lawDefinition.Name`; `GetDescription` = the GuiElement description formatted with
  `Law.Title`
- **Popup draws**: **`LawCardSlot` + `LawCardPrefab`** — the whole law is a CARD instantiated into a slot at
  refresh time. Nothing else of its own.
- **Why suspect**: the popup's entire content is a prefab clone with no variant to name it, and the
  sibling `LawCancelledNotificationWindow` (a cloned-line table) DOES have one (`Variants.cs:169`).
  `docs/notifications.md` also records that the Laws-Cancelled prefab hangs two tooltips per line, one of
  them empty — the law card likely carries the real `Law` dossier the same way.
- **REPL raisable: yes.**
  ```csharp
  EV.Notify(new EventForcedLawActivated(EMP, Amplitude.Unity.Framework.Databases.GetDatabase<LawDefinition>().GetValues()[0]));
  ```
  (Index a different element for a different law. Do not bind the database to a local — `IDatabase<LawDefinition>`
  is a constructed generic over a game type.)

---

#### LuxuryDiscoveredNotificationWindow

- **Event**: `EventLuxuryDiscovered(Empire empire, StaticString resourceName)`
- **Notification**: `NotificationLuxuryDiscovered` — `GetDescription` formats the resource's localized title
  AND its symbol string
- **Popup draws**: title/description, plus a **`ResourceTooltip` the window writes at Refresh** — class,
  content and target all set from `GuiResource`. The resource dossier is hung on an icon, so the fact it
  states has no words on screen at all.
- **Why suspect**: exactly the shape `docs/notifications.md` calls out for the new-content popup's tutorial
  badge ("a popup can state a whole sentence with a PICTURE whose only words are its tooltip") — which
  needed a variant `Notes` entry there and has none here.
- **REPL raisable: yes** (the resource name must be a real luxury).
  ```csharp
  EV.Notify(new EventLuxuryDiscovered(EMP, "LuxuryResource1"));
  ```
  The luxury name is **unverified** — read one off the marketplace or
  `Databases.GetDatabase<ResourceDefinition>().GetValues()` first.

---

#### NarrativeEventCompletedNotificationWindow

- **Event**: `EventNarrativeEventCompleted(Empire empire, Quest quest)`
- **Notification**: `NotificationNarrativeEventCompleted : NotificationQuest`; title and description both
  formatted with the quest title
- **Popup draws**: title/description plus its own **`EffectsLabel`** — the consequences the choice had.
- **Why suspect**: the outcome of every narrative choice the player makes is on a label the mod names
  nowhere; its sibling `NarrativeEventBegunNotificationWindow` HAS a variant (the choice table).
  Whether `EffectsLabel` is inside the window's tree at all is unmeasured — four notification prefabs are
  already known to park a bound label OUTSIDE their layout (`docs/notifications.md`).
- **REPL raisable: no.** Needs a live `Quest`, and `Bind` dereferences `Quest.QuestDefinition.Hidden` and
  `.IsInstant`. Alternative: play a narrative event to its end (they arrive on their own within a few turns
  of most starts).

---

#### NewUnlockedContentNotificationWindow

- **Event**: `EventNewUnlockedContent(Empire empire, StaticString unlockedContentName)`
- **Notification**: `NotificationNewUnlockedContent` — `GetDescription` resolves
  `Gui.GetGuiElement(UnlockedContentName) as ContentGuiElement` and returns `.ContentDescription`
  (**NPEs on an unknown name** — the cast is unguarded)
- **Popup draws**: its own **`TitleLabel`**, a **`LoreScrollView` + `LoreDescriptionLabel`**, and a
  **`PortraitDisplay`**. The shared description label is not where the text is.
- **Why suspect**: this is the **twin of `NewDownloadableContentNotificationWindow`**, which has a variant
  naming `TitleLabel` as the popup's `Words` — added 2026-09-14 precisely because a popup that writes its
  identity into its own label is the row drawn first AND the only one that exists when the cursor lands
  (`docs/notifications.md`). The unlocked-content popup has the same layout and no entry, so it inherits the
  landing bug the DLC variant was written to fix.
- **REPL raisable: yes**, with the one content name found in the corpus
  (`AchievementManager.cs:28,868`):
  ```csharp
  EV.Notify(new EventNewUnlockedContent(EMP, "UnlockedContentArchivist"));
  ```

---

#### PlanetDestroyedNotificationWindow

- **Event**: `EventPlanetDestroyedWarning(Empire empire, Fleet fleet, Planet planet, StaticString sourceType)`
- **Notification**: `NotificationPlanetDestroyed` — `HasLocation` = `DestroyedPlanet != null`, and its own
  `ShowLocation` pans to `DestroyedPlanet.StarSystemNode.GalaxyPosition`; description formats the destroyer's
  leader-and-faction plus the planet name
- **Popup draws**: title/description plus its own **`ReplayDestructionButton`** (re-play the destruction
  cinematic).
- **Why suspect**: `docs/notifications.md` records that the lenient paint test once put a dead "Show
  Location" stop on **Planet Destroyed** specifically; the replay button has never been declared or proven,
  and there is no variant.
- **REPL raisable: yes** (needs a fleet and a planet).
  ```csharp
  EV.Notify(new EventPlanetDestroyedWarning(EMP, (Fleet)FLL[0], (Planet)((System.Collections.IList)NODE.Planets)[0], "Obliterator"));
  ```
  The `sourceType` string is **unverified**; `NotificationPlanetDestroyed.GetDescription` uses it only via
  the GuiElement, so a wrong value most likely shows an unformatted description rather than throwing.

---

#### TechnologyStageUnlockedNotificationWindow

- **Event**: `EventTechnologyStageUnlocked(Empire empire, TechnologyStageDefinition eraDefinition, bool showNotification, bool applyStagePopulationEvents = true)`
- **Notification**: `NotificationTechnologyStageUnlocked` — `GetTitle` is the GuiElement title formatted
  with `%<Quadrant>Title`; **`GetDescription` is NOT overridden**, so it answers
  `%NotificationTechnologyStageUnlockedDescription`, which `docs/notifications.md` measured as having **no
  translation at all** — `Gui.Localize` hands back the raw key, and mod policy then treats the description
  as ABSENT. This popup therefore has to be read entirely off what it draws.
- **Popup draws**: `StageTitle` with a `StageTooltip`, an array **`StageUnlocks: PanelFeatureHeaderEmbedded[]`**
  (the era's unlocks as embedded cards), and a **`DeedGroup`** holding `DeedImage` + `DeedTooltip` +
  `DeedName`.
- **Why suspect**: the single worst combination on the list — no description by policy, no variant, and a
  body made of an embedded-card array plus an icon whose words are on a tooltip. Its twin
  `TechnologyUnlockedNotificationWindow` HAS a variant.
- **REPL raisable: yes.** Veto: `showNotification` must be true.
  ```csharp
  EV.Notify(new EventTechnologyStageUnlocked(EMP, Amplitude.Unity.Framework.Databases.GetDatabase<TechnologyStageDefinition>().GetValues()[0], true, false));
  ```
  Pass `applyStagePopulationEvents: false` so the raise does not perturb the save. Raise it for a stage in
  each quadrant — the title is quadrant-keyed — and for one that carries a Deed and one that does not.

---

### Group B — a variant exists, but the roadmap records it as never sighted or as a known finding

These are modelled; the value of raising them is closing the roadmap's own debt. Quoted from
`docs/roadmap.md` lines ~219-244.

> "**Notification variants awaiting a live sighting** (baseline ships; upgrade per popup on sighting):
> election survey; SimpleDescription-family members with own fields (alliance update — its member table now
> declared, diplomatic relation change, constellation event, deed completed); the 9 header-less line-class
> tables (bailiff + its totals footer, law cancelled, population change (SIGHTED 2026-09-01 and its own
> parity check has a finding: `PopulationChangeNotificationWindow` "says a figure with no caption" on a
> table row — the first of these nine to be seen live, and the finding is the work), trading blockade,
> treaty cancelled, relics ×2, queue-empty, lost-roots connectivity); the line tables declared 2026-09-08
> (construction completed, curiosity discovered, special-node event, quest completed — podium and rewards);
> the hacking picker's countdown; the curiosity popup's body-sheet gateway into system management …; the two
> metaplot popups' lore label declared as the popup's words; one-of-N semantics for the hacking outcome
> picker … ; PirateMissionReportNotificationWindow (fixture-blocked: its `Bind` needs a live
> `AttackSystemPirateDiplomaticAction` — the other five report popups are done);
> DiplomaticInteractionNotificationWindow (MoodMessageLabel, NegotiationContributionPanel)."

> "Ctrl+L's type dispatch (`NotificationScreen.GoToLocation`) is unproven on the two space-battle popups,
> the two ground-battle popups and the hacking popup — their own show-location handlers are code-verified
> against the decompiled overrides only, and no fixture raises the windows."

> "Ground-battle OUTCOME-SELECTION popup: modelled 2026-08-25 … the ENTIRE live sighting is pending (needs
> a decisive victory)."

> "Notification arrival-focus race: a popup's first build can run before its description …"

Raisable members of that list, with the raise:

| Popup | Event | Raise |
|---|---|---|
| Bailiff report (table + totals footer) | `EventBailiffReport(Empire, List<AuctionInstruction>)` | **with a fake** — `AuctionInstruction(AuctionType, AuctionAction, string elementName, string locationName, AuctionLocationType, ResourceAmount savedUpkeep)`; enum members unverified |
| Law cancelled | `EventLawCancelled(Empire, LawDefinition)` | `EV.Notify(new EventLawCancelled(EMP, Amplitude.Unity.Framework.Databases.GetDatabase<LawDefinition>().GetValues()[0]));` — stackable: raise 2-3 for a multi-line table |
| Population change (the "figure with no caption" finding) | `EventPopulationGained/Lost/AlmostLost(Empire, ColonizedStarSystem, PopulationStarSystem)` | **yes**, but the population must come out of `SYS.PopulationsByAffinity` via the non-generic `IDictionary` (see §3 note) |
| Trading blockade | `EventTradingBlockade(Empire owner, GameNode)` | `EV.Notify(new EventTradingBlockade(EMP, NODE));` — stackable |
| Treaty cancelled (table) | `EventTreatiesCancelled(Empire, DiplomaticAbility, Empire partner)` | **with a fake** — `new DiplomaticAbility(definition, turn)` with a `DiplomaticAbilityDefinition` from its database (unverified) |
| Relics collected / cancelled | `EventOnRelicsCollected(Empire, Fleet, StarSystemNode, float)` / `EventOnRelicsCollectionCanceled(Empire, Fleet, StarSystemNode, ReasonOfCancelation)` | `EV.Notify(new EventOnRelicsCollected(EMP, (Fleet)FLL[0], NODE, 5f));` — stackable |
| Construction queue empty | `EventConstructionQueueEmpty(Empire, ColonizedStarSystem)` | `EV.Notify(new EventConstructionQueueEmpty(EMP, SYS));` — stackable |
| Lost roots connectivity | `EventLostRootsConnectivity(Empire, List<GameEntityGUID>)` | `EV.Notify(new EventLostRootsConnectivity(EMP, new System.Collections.Generic.List<GameEntityGUID>(new GameEntityGUID[] { NODE.GUID })));` — veto if the list is empty |
| Construction completed (the real table) | `EventConstructionCompleted(Empire, IGameEntity context, IConstructible)` | **with a lookup** — `SYS` as context, a constructible from `Services.GetService<IConstructibleRepositoryService>()` (name source unverified). Veto if the constructible carries the hide-notification tag. Stackable — raise several for a multi-row table |
| Curiosity discovered | `EventCuriosityDiscovered(Empire, Curiosity, List<CuriosityEffectInfo>, bool)` | **with a fake** — `new Curiosity(guid, curiosityDefinition, empireCount)`; definition from its database (unverified) |
| Special node discovered/lost/owned | `EventSpecialNodeDiscovered(Empire, SpecialNode, EntityExploration.State)` etc. | **yes if the galaxy holds a special node** (enumerate `Gui.Game.Galaxy.GameNodes` for one) |
| Election survey | `EventElectionSurvey(Empire, ElectionResult)` | **with a fake** — `new ElectionResult()` has a public default ctor; whether `Bind` survives an empty one is unverified |
| Alliance update (5 events) | `EventAllianceCreated/Destroyed/Renamed/EmpireJoinedAlliance/LeaveAlliance` | **with a fake** — `new Alliance(guid, "Test", 4)`; but `Bind` ends `if (!flag) return false` where `flag` needs an alliance member the looking player knows, so an empty fake alliance is likely vetoed |
| Diplomatic relation change | `EventDiplomaticRelationChange(Empire, Empire other, StaticString newState, StaticString prevState, bool notifyGUI, Empire initiator)` | **yes** — `notifyGUI` must be true; state names from `DiplomaticRelationState.Names.*` |
| Deed completed | `EventDeedCompleted(Empire, Quest)` | **no** — needs a live Quest |
| Quest completed / begun | `EventQuestCompleted(Empire, Quest)` / `EventQuestBegun(Empire, Quest)` | **no** — needs a live Quest |
| Metaplot begun / finished | `EventMetaplotBegun(Empire, GameEntityGUID, StaticString)` / `EventMetaplotFinished()` | Begun: **no** (veto when the minigame GUID resolves to nothing, and when the player is not on the named team). Finished: **YES, trivially** — `EV.Notify(new EventMetaplotFinished());` (a bare `GameEvent`, no arguments, routed to every empire). This is the cheapest way to sight `MetaplotFinishedNotificationWindow`'s lore label — and note that window is one of the four whose description label is parked OUTSIDE the layout (`docs/notifications.md`) |
| Hacking outcome picker (countdown, one-of-N) | `EventHackingOperationOutcomeSelection(Empire, GameNode, HackingOperation)` | **no** — live HackingOperation |
| Pirate mission report | `EventPirateMissionReport(Empire, GameNode, AttackSystemPirateDiplomaticAction)` | **with a fake** — the action's ctor needs an `AttackSystemPirateDiplomaticActionDefinition` and an `EntityActionContext_StarSystemNode`; roadmap calls it fixture-blocked |
| Diplomatic interaction (4 events on one window) | see group D row | `EventDiplomaticMoodMessage` and `EventMajorEmpireMet` are **yes**; `EventDiplomaticContractStateChange` is effectively **no** (six stacked vetoes) |

---

### Group C — no variant, plain title + description (generic reading probably fine)

Eleven windows. Several add exactly one extra prefab button wired by handler name; those buttons land in
`ButtonsGroup` and are classified by the mod's generic `Sort`, so they should be reachable — worth one
confirming raise each.

| Window | Extra button (handler) | Bindings | Raise |
|---|---|---|---|
| `AcademyDiscoveredNotificationWindow` | none | `EventAcademyDiscovered(Empire, Empire instigator, StarSystemNode)` | `EV.Notify(new EventAcademyDiscovered(EMP, EMP, NODE));` — `HasLocation` overridden |
| `AcademyRequestNotificationWindow` | open the Academy (`OnOpenAcademyWindowCb`) | `EventAcademyRequestFinishing(Empire, string requestedResource)` | `EV.Notify(new EventAcademyRequestFinishing(EMP, "ResourceDust"));` (resource name unverified) |
| `EmpireScreenInformationNotificationWindow` | open the Empire screen, Systems tab (`OnToggleScanCb`) | `EventOnEmpireRelicSlotLocked(Empire, StarSystemNode, StaticString)` | `EV.Notify(new EventOnEmpireRelicSlotLocked(EMP, NODE, "EmpireRelicSlot1"));` (slot name unverified) — stackable |
| `InformationNotificationWindow` | none | **70 bindings** — the whole table below | — |
| `LodestoneRewardNotificationWindow` | none | `EventLodestoneReward(Empire, StaticString resourceName, int value)` | `EV.Notify(new EventLodestoneReward(EMP, "ResourceDust", 100));` |
| `OutpostToColonyNotificationWindow` | manage the system (`OnManageCb`) | `EventOutpostTurnedToColony(Empire, ColonizedStarSystem)` | `EV.Notify(new EventOutpostTurnedToColony(EMP, SYS));` — **veto** unless the system's `AutomaticallyTurnsIntoColony` simulation property is non-zero |
| `PlayDeckFreeCostNotificationWindow` | open the deck panel | `EventPlayDeckFreeCost(Empire)` | `EV.Notify(new EventPlayDeckFreeCost(EMP));` |
| `PlayDeckNewSlotNotificationWindow` | open the deck panel | `EventPlayDeckNewSlot(Empire)` | `EV.Notify(new EventPlayDeckNewSlot(EMP));` |
| `PopulationCollectionThresholdReachedNotificationWindow` | open the population modal | `EventPopulationCollectionThresholdReached(Empire, StaticString affinity)`, `EventPopulationBoostEnded(Empire, StaticString populationName)` | `EV.Notify(new EventPopulationCollectionThresholdReached(EMP, "AffinityUnitedEmpire"));` (affinity name unverified) |
| `ScanViewInformationNotificationWindow` | toggle Scan view | `EventHackingOperationLeftUnassigned(Empire)` | `EV.Notify(new EventHackingOperationLeftUnassigned(EMP));` — **the one hacking notification with no HackingOperation in it, and therefore the only way to sight the hacking popup chrome from the REPL** |
| `TradingNotificationWindow` | open the Economy screen | 4 `EventTradingStructureUpdate` subclasses, each `(TradingCompanyStructure structure)` | **with a fake**: `new TradingCompanyStructure(type, guid, NODE.NodePosition, turn, EMP.Index, guid2)` — the event routes by `structure.GetOwnerEmpire()`, so `ownerIndex` must be the player's |

#### The 70 `InformationNotificationWindow` bindings

Plain title + description (both formatted by the notification), the base Minimize/Dismiss/ShowLocation, and
nothing else. `HasLocation` is true for every `NotificationOnGameNode` descendant (marked ▣).

| Event (constructor) | Notification | Raisable |
|---|---|---|
| `EventAcademyColonized(Empire, Empire previousEmpire, StarSystemNode)` ▣ | `NotificationAcademyColonized` | yes: `EV.Notify(new EventAcademyColonized(EMP, EMP, NODE));` |
| `EventAcademyLevelup(int level)` | `NotificationAcademyLevelup` | yes (bare `GameEvent` → every empire): `EV.Notify(new EventAcademyLevelup(2));` |
| `EventAllianceVictoryImminent(Alliance, VictoryCondition)` | `NotificationAllianceVictoryImminent` | with a fake — `new Alliance(guid,"x",4)` + `new VictoryCondition("<definitionName>")`; definition name unverified |
| `EventAnarchyStarted(Empire, ActiveGovernment)` | `NotificationAnarchyStarted` | with a fake — the empire's current `ActiveGovernment` off `DepartmentOfPolitics` (accessor unverified) |
| `EventAutomatedFleetLost(Empire, Empire attacker, Fleet)` ▣ | `NotificationAutomatedFleetLost` | yes: `EV.Notify(new EventAutomatedFleetLost(EMP, Gui.Game.Empires[1], (Fleet)FLL[0]));` |
| `EventBailiffImminent(Empire, List<StaticString> concernedResources)` | `NotificationBailiffImminent` | yes: pass `new System.Collections.Generic.List<Amplitude.StaticString>(new Amplitude.StaticString[] { "ResourceDust" })` inline |
| `EventOutpostLost(Empire, Empire instigator, ColonizedStarSystem)` ▣ | `NotificationColonizationFailed` | yes: `EV.Notify(new EventOutpostLost(EMP, Gui.Game.Empires[1], SYS));` |
| `EventCompetitiveQuestWarning(Empire, Quest, int remainingTurns)` | `NotificationCompetitiveQuestWarning` | **no** — live Quest; veto unless `IsParticipantActive` |
| `EventContextualDiplomaticExchangeCancelled(Empire, ContextualDiplomaticExchange)` | `NotificationContextualDiplomaticExchangeCancelled` | with a fake (public ctor needs a `ContextualDiplomaticTermDefinition`; unverified) |
| `EventCuriosityFailed(Empire, Curiosity)` | `NotificationCuriosityFailed` | with a fake — `new Curiosity(guid, definition, empireCount)` |
| `EventCuriosityGuardian(Empire, Curiosity)` | `NotificationCuriosityGuardian` | with a fake, as above |
| `EventEmpireCancelledAttackRestriction(Empire, Empire attacker, Empire victim)` | `NotificationEmpireCancelledAttackRestriction` | yes |
| `EventEmpireFreeSuperColonization(Empire)` | `NotificationEmpireFreeSuperColonization` | yes — `HasLocation` overridden true |
| `EventEnemyAutomatedFleetsDestroyed(Empire, Empire victim, GameNode, int fleetCount, float moneyReceived)` ▣ | `NotificationEnemyAutomatedFleetsDestroyed` | yes: `EV.Notify(new EventEnemyAutomatedFleetsDestroyed(EMP, Gui.Game.Empires[1], NODE, 2, 50f));` |
| `EventFactionPactCancelled(Empire, Empire empireWhoCancelled)` | `NotificationFactionPactCancelled` | yes |
| `EventFleetActionReady(Empire, Fleet, StaticString actionName)` | `NotificationFleetActionReady` | yes — `HasLocation` overridden true |
| `EventGalaxyDiscoveryMilestoneReached(Empire, int ratio)` | `NotificationGalaxyDiscoveryMilestoneReached` | yes: `EV.Notify(new EventGalaxyDiscoveryMilestoneReached(EMP, 50));` |
| `EventGhostPlanetLost(Empire, Planet)` ▣ | `NotificationGhostPlanetLost` | yes (planet off `NODE.Planets` via `IList`) |
| `EventGhostSystemDiscoveredOnOwnedSystem(Empire, Empire instigator, GameNode)` ▣ | `NotificationGhostSystemDiscoveredOnOwnedSystem` | yes |
| `EventGhostSystemLost(Empire, GameNode)` ▣ | `NotificationGhostSystemLost` | yes |
| `EventGovernmentStarted(Empire, ActiveGovernment)` | `NotificationGovernmentStarted` | with a fake — as `EventAnarchyStarted` |
| `EventGroundBattleAborted(Empire, GroundBattle)` ▣ | `NotificationGroundBattleAborted` | **no** — GroundBattle |
| `EventGroundBattleOutcomeApplied(Empire, Empire, GroundBattle, GroundBattleOutcomeDefinition, int, int, ResourceStock[])` ▣ | `NotificationGroundBattleOutcomeApplied` | **no** — GroundBattle; veto if the outcome definition is null |
| `EventHackingBeaconCreationFailed(Empire, GameNode)` ▣ | `NotificationHackingBeaconCreationFailed` | **yes** — no HackingOperation in it |
| `EventHackingOperationOutcomeApplied(Empire, GameNode, HackingOperation)` ▣ | `NotificationHackingOperationOutcomeApplied` | **no** |
| `EventHiddenHomeSystemDetected(Empire, Empire other, ColonizedStarSystem)` ▣ | `NotificationHiddenHomeSystemDetected` | yes |
| `EventHackingOperationCanceledByTrace(Empire, GameNode, HackingOperation)` ▣ | `NotificationImportantHackingStatusChanged` | **no** |
| `EventHackingOperationTracedButNotCanceled(Empire, GameNode, HackingOperation)` ▣ | `NotificationImportantHackingStatusChanged` | **no** |
| `EventInfluencedSystemPopulationConsumed(Empire, Empire instigator, ColonizedStarSystem, StaticString populationName)` ▣ | `NotificationInfluencedSystemPopulationConsumed` | yes |
| `EventInformationMessage(Empire, StaticString messageReference, ulong questInstanceID)` | `NotificationInformationMessage` | yes if the message key exists (`GameClient.cs:2573` is the game's only raise) |
| `EventJuggernautLocalActionEnded(Empire, GameNode, StaticString actionName)` ▣ | `NotificationJuggernautLocalActionEnded` | yes |
| `EventLowEmpireManpower(Empire)` | `NotificationLowEmpireManpower` | yes: `EV.Notify(new EventLowEmpireManpower(EMP));` |
| `EventMarketplaceAdvertisementPosted(MarketplaceAdvertisement)` | `NotificationMarketplaceAdvertisementPosted` | with a fake — `new MarketplaceAdvertisement(EMP.Index, itemName, 5, false, true)`; **`hasNotification` must be true** or `Bind` vetoes. Bare `GameEvent` → every empire |
| `EventMarketplaceEventStarted(MarketplaceEvent)` | `NotificationMarketplaceEventStarted` | with a fake — `new MarketplaceEvent(name, 5, target)`; names unverified |
| `EventMinigameObjectiveSecured(Empire, MinigameObjective)` | `NotificationMinigameObjectiveSecured` | with a fake — `new MinigameObjective(nodePosition, teams)`; `MinigameDefinition.Team[]` source unverified. `HasLocation` overridden true |
| `EventMinorAndPirateHackingUnlocked(Empire)` | `NotificationMinorAndPirateHackingEnabled` | yes |
| `EventMinorEmpireDead(Empire receiver, Empire owner, MinorEmpire, bool empireWasKnown, Empire gifter = null)` | `NotificationMinorEmpireDead` | yes if a minor empire exists; **`empireWasKnown` must be true** |
| `EventMinorEmpireIntegrated(Empire owner, MinorEmpire)` | `NotificationMinorEmpireIntegrated` | yes |
| `EventMinorEmpireRazed(Empire receiver, MajorEmpire attacker, MinorEmpire, bool empireWasKnown)` | `NotificationMinorEmpireRazed` | yes; `empireWasKnown` must be true |
| `EventSuzerainChanged(Empire, Empire previousOwner, Empire newOwner, MinorEmpire)` | `NotificationMinorEmpireSuzerainChanged` | yes; **newOwner must be a `MajorEmpire` and differ from previousOwner** |
| `EventMinorEmpireUnderInfluence(Empire, MinorEmpire, bool isUnderInfluence)` | `NotificationMinorEmpireUnderInfluence` | yes |
| `EventMothershipLost(Empire, GameNode, Mothership)` ▣ | `NotificationMothershipLost` | with a fake — `new Mothership(guid, EMP)` |
| `EventMothershipReclaimed(Empire, GameNode, Mothership)` ▣ | `NotificationMothershipReclaimed` | with a fake, as above |
| `EventNewForeignOutpostOnRootedNode(Empire rootOwner, Empire outpostCreator, GameNode)` ▣ | `NotificationNewForeignOutpostOnRootedNode` | yes; **creator must differ from rootOwner** |
| `EventOutpostConcurrenced(Empire, Empire instigator, ColonizedStarSystem)` ▣ | `NotificationOutpostConcurrence` | yes |
| `EventOutpostLocked(Empire, ColonizedStarSystem)` ▣ | `NotificationOutpostLocked` | yes |
| `EventOutpostLockedWithHonor(Empire, ColonizedStarSystem)` ▣ | `NotificationOutpostLockedWithHonor` | yes |
| `EventPirateEmpireEliminated(PirateEmpire)` | `NotificationPirateEmpireEliminated` | yes if the galaxy has a pirate empire (bare `GameEvent`) |
| `EventPoliticsUnlocked(Empire, PoliticsDefinition)` | `NotificationPoliticsUnlocked` | yes: `…GetDatabase<PoliticsDefinition>().GetValues()[0]` |
| `EventRebellionImminent(Empire, StarSystemNode)` ▣ | `NotificationRebellionImminent` | yes |
| `EventRebellionRogueFleetCreated(Empire, StarSystemNode, Fleet)` ▣ | `NotificationRebellionRogueFleetCreated` | yes |
| `EventRelicsSpawned(Empire, int amount)` | `NotificationRelicsSpawned` | yes: `EV.Notify(new EventRelicsSpawned(EMP, 3));` |
| `EventRemainsSpawned(Empire, Remains)` ▣ | `NotificationRemainsSpawned` | with a fake — `new Remains(guid, definition, planet, value, null, "")`; `RemainsDefinition` from its database (unverified) |
| `EventResourcesStolenFromPirates(Empire, GameNode, Dictionary<StaticString,float>, float ratio)` ▣ | `NotificationResourcesStolenFromPirates` | yes — pass the dictionary inline |
| `EventResourcesTransferEndedOnFleet(Empire, Fleet, ColonizedStarSystem, string notificationEventName)` ▣ | `NotificationResourcesTransferred` | yes (event name unverified) |
| `EventResourcesTransferStartedOnSystem(Empire, Empire instigator, ColonizedStarSystem, string, string)` ▣ | `NotificationResourcesTransferred` | yes (names unverified) |
| `EventRootsColonized(Empire, GameNode)` ▣ | `NotificationRootsColonized` | yes |
| `EventShipLockedInGarrison(Empire, Ship, Garrison, int lockDuration)` | `NotificationShipLockedFromCombat` | with real objects, but **vetoed** unless `GuiShip.IsLockedInGarrisonFromDamage` is already true |
| `EventShipLockedInGarrisonRecovered(Empire, Ship, Garrison)` | `NotificationShipLockedReactivated` | yes with a real ship + garrison |
| `EventSystemGainedFromInfluence(Empire, ColonizedStarSystem, Empire other)` ▣ | `NotificationSystemGainedFromInfluence` | yes |
| `EventSystemLostToInfluence(Empire, ColonizedStarSystem, Empire other, Empire academyGifter = null)` ▣ | `NotificationSystemLostToInfluence` | yes |
| `EventSystemMarkedByPirates(Empire, GameNode)` ▣ | `NotificationSystemMarkedByPirates` | yes |
| `EventSystemUnderInfluence(Empire, ColonizedStarSystem, Empire other, float netConversion)` ▣ | `NotificationSystemUnderInfluence` | yes; **veto if the system's empire `HasGhostSystems`** |
| `EventTimeBubbleCreated(Empire, Empire instigator, GameNode, TimeBubble)` ▣ | `NotificationTimeBubbleCreated` | with a fake — `new TimeBubble(guid, definition, EMP, NODE)` |
| `EventTimeBubbleExpired(Empire, Empire instigator, GameNode, TimeBubble)` ▣ | `NotificationTimeBubbleExpired` | with a fake, as above |
| `EventTimeBubbleUnused(Empire)` | `NotificationTimeBubbleUnused` | yes — and it is a `SoftEndTurnBlocker` |
| `EventTradingHeadquarterUnlocked(Empire)` | `NotificationTradingHeadquarterUnlocked` | yes |
| `EventTreatyCancelled(Empire, DiplomaticAbility, Empire empireWhoCancelled)` | `NotificationTreatyCancelled` | with a fake — `new DiplomaticAbility(definition, turn)` |
| `EventUniquePlanetDiscovered(Empire, GameNode, Planet)` ▣ | `NotificationUniquePlanetDiscovered` | yes — `HasLocation` overridden true |
| `EventEmpireVictoryImminent(Empire, VictoryCondition)` | `NotificationVictoryImminent` | with a fake — `new VictoryCondition("<definitionName>")` |

---

### Group D — a variant entry exists (modelled; raise for regression)

44 windows. Those already covered under group B are not repeated. The rest, with their raise:

| Window | Event(s) | Raisable |
|---|---|---|
| `AcademyRoleNotificationWindow` | `EventAcademyRoleFinishing(Empire)` | **yes** — `EV.Notify(new EventAcademyRoleFinishing(EMP));` |
| `BattleReportNotificationWindow` | `EventBattleReport(Empire, Encounter)` | no — Encounter; four extra vetoes (simple encounter, <2 groups, no left group, player not involved) |
| `BattleSetupNotificationWindow` | `EventBattleSetup(Empire, Encounter)` | no — Encounter |
| `ContextualAcademyDiplomaticExchangeUpdateNotificationWindow` | `EventOnAcademyDiplomaticExchange(Empire receiver, Empire sender, StaticString title, StaticString description, StaticString effects, AcademyAttitudeType, AcademyDialogueType, bool showRolesPanel = false)` | **yes** — every payload is a string; pass `true` for the roles panel to sight that branch too |
| `ContextualDiplomaticExchangeUpdateNotificationWindow` | `EventContextualDiplomaticExchangeStarted/Updated(Empire, ContextualDiplomaticExchange)` | with a fake (public ctor; needs a `ContextualDiplomaticTermDefinition`) |
| `DiplomaticInteractionNotificationWindow` | `EventDiplomaticMoodMessage(Empire, Empire instigator, StaticString moodMessageID)` | **yes** — the cheapest sighting of this window |
|  | `EventAcademyDiplomaticMoodMessage(Empire receiver, Empire sender, StaticString message, AcademyAttitudeType, AcademyDialogueType)` | **yes** |
|  | `EventMajorEmpireMet(Empire whoSaw, Empire whoSeen, IGameEntityWithVision witness)` | **yes** — `EV.Notify(new EventMajorEmpireMet(EMP, Gui.Game.Empires[1], (Fleet)FLL[0]));` (`Fleet` implements `IGameEntityWithVision`) |
|  | `EventDiplomaticContractStateChange(Empire, DiplomaticContract, StaticString previousState)` | effectively no — six stacked vetoes on contract state, options, terms and which empire is receiving |
| `DisplacementReportNotificationWindow` | `EventDisplacementReport(Empire, ColonizedStarSystem, List<StarSystemImprovementDefinition>, Dictionary<StaticString,int>)` | **yes** — pass both collections inline |
|  | `EventDisplacementFailed(Empire, ColonizedStarSystem, SpecialNode)` | yes if the galaxy holds a special node |
| `EmpireEliminatedNotificationWindow` | `EventEmpireEliminated(Empire eliminatedEmpire)` | **yes** — bare `GameEvent`. Raise it for **another** empire first: for the player's own empire `AutoPopUp` is forced true and `IsDismissible` is false, and `docs/notifications.md` records the popup hides Dismiss and Minimize and holds no text in its groups |
| `EmpireIntroductionNotificationWindow` | `EventEmpireIntroduction(Empire)` | **yes** — `Priority` overridden, so it jumps the queue |
| `GroundBattleOutcomeSelectionNotificationWindow` / `GroundBattleReportNotificationWindow` / `GroundBattleSetupNotificationWindow` | `EventGroundBattleOutcomeSelection/Report/Setup(Empire, GroundBattle, …)` | no — GroundBattle |
| `HeroRecruitmentNotificationWindow` | `EventHeroUnlockGaugeFilled(Empire, DepartmentOfEducation.HeroChoice)` | with a fake — `HeroChoice` shape unverified |
| `IonWaveReportNotificationWindow` | `EventIonWaveReport(Empire, Empire instigator, IonWaveReport report)` | **with a fake — and this one is cheap**: `new IonWaveReport(NODE, EMP.Index)` is a public two-argument ctor |
| `MinorEmpireMetNotificationWindow` | `EventMinorEmpireMet(Empire whoSaw, Empire whoSeen, IGameEntityWithVision witness)` | **yes** |
| `NarrativeEventBegunNotificationWindow` | `EventNarrativeEventBegun(Empire, Quest)` | no — live Quest |
| `NewDownloadableContentNotificationWindow` | `EventNewDownloadableContent(Empire, DownloadableContent)` | **yes** — the one already raised this session |
| `ObliteratorAttackReportNotificationWindow` | `EventObliteratorAttackReport(Empire, Empire, ObliteratorAttackReport, Dictionary<int, ObliteratorVictimReport>)` | with a fake — `new ObliteratorAttackReport(NODE, defense)`; `ObliterationDefense` source unverified |
| `ObliteratorVictimReportNotificationWindow` | `EventObliteratorVictimReport(Empire, Empire, ObliteratorVictimReport)` | with a fake — `ObliteratorVictimReport` declares no ctor, so it has an implicit public parameterless one (unverified whether an empty report survives `Bind`) |
| `SpecialNodeEventNotificationWindow` | `EventSpecialNodeDiscovered/Lost/Owned` | yes if a special node exists |
| `TechnologyNeededNotificationWindow` | `EventTechnologyNeeded(Empire)` | **yes** — `EV.Notify(new EventTechnologyNeeded(EMP));` (its `SuggestedTechnologiesPanel` needs the empire to have an empty research queue to be interesting) |
| `TechnologyUnlockedNotificationWindow` | `EventTechnologyUnlocked(Empire, TechnologyDefinition, bool showNotification, bool applyPopulationEvents = true)` | **yes**: `EV.Notify(new EventTechnologyUnlocked(EMP, Amplitude.Unity.Framework.Databases.GetDatabase<TechnologyDefinition>().GetValues()[0], true, false));` — veto if `showNotification` is false or the definition's `Visibility` is `Hidden`. Raise once with a non-empty and once with an EMPTY research queue (the "next research" placeholder case measured 2026-09-02) |

---

### The SCAN table (20 bindings, 4 notification classes) — never opens a popup

`BuildGameEventToScanNotificationMapping` (`:172-192`). These fire the same `Add` event (so the mod
announces them by title on arrival) but the auto-pop call at `:800` is gated on a NON-scan mapping and the
queue drain reads only the main list, so **no window ever opens for them**. Raising them tests the arrival
line, not a popup.

All but one need a live `HackingOperation` or `HackingOperationBackdoor`, and
`NotificationHackingStatusChanged.Bind` additionally builds a `HackingStatusChange` whose ctor does
`Instigator.Index` — null on every 3-arg event path (unverified whether the game ever takes that path).

| Event | Notification | Raisable |
|---|---|---|
| `EventHackingOperationSuccessful/NodeStepComplete/MoveStepComplete/BackdoorDestroyed/BackdoorDiscovered/BackdoorLost/BackdoorSoonDestroyed/CanceledByTargetLoss/CanceledByStartLoss/CanceledByRerouteLoss/TargetReplaced/TraceSuccessful/OutcomesInvalid` | `NotificationHackingOperationSuccessful` / `NotificationHackingOperationBackdoorDiscovered` / `NotificationHackingStatusChanged` | **no** — live `HackingOperation`/`Backdoor` (the ctors dereference `.Target.NodePosition`) |
| `EventHackingOperationDetected(Empire, GameNode)` | `NotificationHackingStatusChanged` | **with a caveat** — the ctor takes no operation, but `Instigator` is null on that path and `HackingStatusChange` NPEs on it (unverified) |
| `EventHackingProgramActivationFailed(Empire, HackingProgramDefinition, GameNode)` | `NotificationHackingStatusChanged` | yes-ish — definition from its database; same null-`Instigator` caveat |
| `EventTraitorPopulationDiscovered/RemovalFailed/RemovalSuccessful(Empire, ColonizedStarSystem)` | `NotificationHackingStatusChanged` | same null-`Instigator` caveat |
| `EventDefenseHackingProgramEncountered` | `NotificationHackingStatusChanged` | **shadowed** — it is in the NON-scan table too, and that lookup wins |
| `EventTraitorRemoved(Empire, Empire instigator, GameNode, int count)` | `NotificationTraitorRemoved` | **yes** — it carries an instigator: `EV.Notify(new EventTraitorRemoved(EMP, Gui.Game.Empires[1], NODE, 1));` |

---

## 3. Cannot be raised from the REPL

| Blocking payload | Notifications | Why | Alternative |
|---|---|---|---|
| `Encounter` | BattleSetup, BattleReport | `new Encounter(guid, orbit, arenaDefinition, replayEmpire, parentGUID)` exists, but `Bind` needs ≥2 populated `Groups` with contenders, garrisons and a leader; four vetoes | fight a space battle, or load a save with one pending |
| `GroundBattle` | GroundBattleSetup, GroundBattleReport, GroundBattleAborted, GroundBattleOutcomeSelection, GroundBattleOutcomeApplied | same shape; the outcome events also veto on a null `GroundBattleOutcomeDefinition` | invade a system (a decisive victory is what the outcome-selection popup needs) |
| `Quest` | QuestBegun (×3 events), QuestCompleted, DeedCompleted, CompetitiveQuestWarning, NarrativeEventBegun, NarrativeEventCompleted | `Bind` reads `Quest.QuestDefinition.Hidden`/`.IsInstant`/`.IsParticipantActive` and the quest's steps | play a quest; narrative events arrive within a few turns of most starts |
| `HackingOperation` / `HackingOperationBackdoor` | the whole hacking family (5 non-scan + 13 scan) | `EventOnHackingOperation`'s ctor dereferences `.StartNode`, `.TargetNode`, `.GUID`, `.Target.NodePosition`; a default-constructed operation has a null `Target` | run a real hacking operation from Scan view |
| `ContextualDiplomaticExchange`, `DiplomaticContract` | ContextualDiplomaticExchangeStarted/Updated/Cancelled, DiplomaticContractStateChange | constructible, but need term definitions and (for the contract) a state/options/terms combination that clears six vetoes | receive a real demand or proposal |
| live minigame | MetaplotBegun | `Bind` vetoes when the minigame GUID resolves to nothing and when the player is not on the named team | play the Academy metaplot. (`EventMetaplotFinished` has **no** such guard and is raisable) |
| `AttackSystemPirateDiplomaticAction` | PirateMissionReport | the ctor needs a definition plus an `EntityActionContext_StarSystemNode`; roadmap calls it fixture-blocked | be raided by pirates |
| a ship already locked from damage | ShipLockedFromCombat | `Bind` vetoes on `!IsLockedInGarrisonFromDamage` | take a ship to low health in a battle |
| `EventOnPlanet` | — | the entry is **dead**: the type is abstract, so `GetType()` never equals it | none; the entry does nothing |

Things that are *not* a blocker but read like one:

- **`PopulationStarSystem`** (population gained/lost/almost-lost) is reachable — but
  `ColonizedStarSystem.PopulationsByAffinity` is a `Dictionary<StaticString, Population>`, so it must be
  walked as `((System.Collections.IEnumerable)((System.Collections.IDictionary)SYS.PopulationsByAffinity).Values).GetEnumerator()`
  and each value cast to `PopulationStarSystem` (which derives from `Population`, so the cast is sound).
  A `Dictionary<…>` local would poison the REPL session.
- **Definitions** (`LawDefinition`, `TechnologyDefinition`, `TechnologyStageDefinition`, `PoliticsDefinition`,
  `RemainsDefinition`, `TimeBubbleDefinition`, `CuriosityDefinition`, `DiplomaticAbilityDefinition`) all come
  from `Amplitude.Unity.Framework.Databases.GetDatabase<T>().GetValues()` — an array, safe to index inline.
  Never bind the `IDatabase<T>` itself to a local.

---

## 4. Counts

| | |
|---|---|
| `BindEventAndNotification` entries (non-scan) | **162** (one of them dead: `EventOnPlanet`) |
| `BindEventAndScanNotification` entries | **20** |
| Total raisable event kinds | **182** |
| Distinct `GuiNotification` classes (non-scan) | 151 |
| Distinct scan notification classes | 4 |
| Distinct notification windows in play | **67** |
| — with a mod variant entry (group D, incl. group B members) | **44** |
| — without a variant | **23** |
| &nbsp;&nbsp;— of those, with a body beyond title+description (**group A**) | **12** |
| &nbsp;&nbsp;— of those, plain title+description (**group C**) | **11** |
| Notification classes with `Bind` vetoes beyond the type check | 29 |
| Bound types with no `AutoPopup` toggle (always pop) | 13 |
| Stackable notification classes (rebind → `Refresh`, no second announcement) | 10 |

Raisability, counted over the 162 non-scan bindings:

| | bindings |
|---|---|
| **yes** — real objects from a live game | **~98** |
| **with a fake** — a constructible payload, some with unverified definition sources | **~34** |
| **no** — needs something only the simulation produces | **~29** |
| dead entry | 1 |

(The three figures are a classification of the table above, not a separate count; the boundary between
"yes" and "with a fake" moves with what the loaded save happens to contain — a minor empire, a pirate empire,
a special node, a hero, a second major empire.)
