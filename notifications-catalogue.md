# Every ES2 notification: how to raise it from the REPL, and its status

## 0. Status (read first)

Every notification the game can produce has been checked against the mod, either in play or by raising
its event from the REPL, and reads correctly as of 2026-09-16 — except the popups in §4, which only real
play can produce. §1 is how the pipeline decides what a raise does; §2 is the handle bank and the one-line
raise per notification (repeatable, for regression); §3 is what the REPL cannot build; §4 is what is left.

Fixes that came out of the checks, all committed: `7c7cb1c`, `d0c4362`, `8eb528e` (the DLC popup, 2026-09-14);
`9c1bb4d`, `1a5bbd8` (a popup's first-show fade is completed on the ready frame so the cursor lands on
its words; typewriters keep typing); `3849513` (a notification whose game-side title/description throws
reads as empty, never as the previous notification's drawn words); `4df556b` (such a strip row declares
no tooltip section); `ed309f5` (headings name the blocks they head; the truce sides read as two
caption-named nodes; the displacement fold and population lines are named); `060050a` (the notification
walk clears the popup slot).

**Operating rules (owner rulings).** Never Dismiss, Validate or close-all from a stage, and never `ui.click`
a popup's button bar: those delete notifications. Open a pending one with
`Gui.GuiNotificationService.ToggleGuiNotification(n)`, close with `HideAllGuiNotifications()`, put
`AlreadyRead` back, and leave `CurrentGuiNotification` null (a stale one silently blocks every later
popup and the keyboard zoom; clear it through its private setter). A raise while the slot is null
auto-opens the FIRST unread auto-pop notification, not necessarily the new one. The scan-table events
never open a window; raising one tests only the arrival line. Read `docs/dev-loop.md` and
`docs/notifications.md` before touching source; a popup's fix is normally one entry in
`ES2Access/Screens/NotificationScreen.Variants.cs`.

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
nothing to read there.

---

## 2. Raising, one line per notification

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

Also bound this session: `EMP1`/`EMP2` = `Gui.Game.Empires[1]`/`[2]`, `MAJ1` = `(MajorEmpire)Empires[1]`,
`MINOR`/`MINOR2` = `(MinorEmpire)Empires[4]`/`[5]`, `PIR` = `(PirateEmpire)Empires[14]`, `SYS2` = `CSS[1]`,
`PLANET` = first planet of `NODE`, `FLEET` = `FLL[0]`, `SHIP` = first ship of `FLEET`, `HERO` = `HRL[0]`,
`MGR` = `Gui.GuiNotificationService`, `NL` = `(IList)MGR.GetPlayerEmpireGuiNotifications()`, `SNODE` = the
first `SpecialNode` in `Gui.Game.Galaxy.GameNodes`. Empire indices are one save's; re-read them. Each line
below is pasted as `EV.Notify(<body>);`. Every constructor, enum member and definition name was checked
against the decompile or read live.

### Verified in play by the owner (2026-09-16)

- `EventOnFleetNotificationWindow` — finished sleep (finished explore raised, below)
- `HeroUpdateNotificationWindow` — level up, recruited, unassigned (injured, recovered raised, below)
- `LawBaseNotificationWindow` — forced law activated
- `LuxuryDiscoveredNotificationWindow`
- `NarrativeEventCompletedNotificationWindow`
- `TechnologyStageUnlockedNotificationWindow`
- `BattleSetupNotificationWindow`, `BattleReportNotificationWindow`, `GroundBattleSetupNotificationWindow`, `GroundBattleOutcomeSelectionNotificationWindow` — with Ctrl+L
- `LawCancelledNotificationWindow`
- `PopulationChangeNotificationWindow`
- `ConstructionQueueEmptyNotificationWindow` — construction and research
- `ConstructionCompletedNotificationWindow`; `TechnologyUnlockedNotificationWindow`
- `CuriosityDiscoveredNotificationWindow`; `SpecialNodeEventNotificationWindow` — discovered
- `ElectionSurveyNotificationWindow`
- `DiplomaticRelationChangeNotificationWindow`
- `DiplomaticInteractionNotificationWindow`; `EventMajorEmpireMet`
- `ContextualDiplomaticExchangeUpdateNotificationWindow`
- `EmpireIntroductionNotificationWindow`; `MinorEmpireMetNotificationWindow`
- `QuestCompletedNotificationWindow`; `QuestBegunNotificationWindow`
- `OutpostToColonyNotificationWindow`; `PlayDeckFreeCostNotificationWindow`; `PopulationCollectionThresholdReachedNotificationWindow`
- `HeroRecruitmentNotificationWindow`; `NewDownloadableContentNotificationWindow` (fixed `7c7cb1c`, `d0c4362`, `8eb528e`)
- `InformationNotificationWindow` via `EventGalaxyDiscoveryMilestoneReached`, `EventLowEmpireManpower`, `EventTimeBubbleCreated`, `EventUniquePlanetDiscovered`

### Verified by REPL raise and the owner's pass (2026-09-16)

One line per event; the five marked **fixed** read wrongly at first and were fixed in `ed309f5`. The six
scan-table events add no strip entry; their arrival lines were heard.

```
new EventConstellationOwnerChanged(EMP, Gui.Game.Galaxy.Constellations[0], false, false)
new EventConstellationOwnerChanged(EMP, Gui.Game.Galaxy.Constellations[1], true, false)
new EventConstellationOwnerChanged(EMP, Gui.Game.Galaxy.Constellations[2], true, true)
new EventFleetFinishedAutoExplore(EMP, FLEET)
new EventFactionSwitched(EMP)
new EventForceTruceAnswered(EMP, EMP1, ForceTruce.State.Accepted, new DiplomaticTribute[] { new DiplomaticTribute(EMP1, EMP, "Luxury1", 100f, 10, DiplomaticTribute.Source.ForceTruce) }, new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<GameEntityGUID, float>>(), new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<GameEntityGUID, float>>(), 50f, 25f)   # fixed ed309f5 (ForceTruceAnsweredNotificationWindow)
new EventHeroInjured(EMP, HERO)
new EventHeroRecovered(EMP, HERO)
new EventNewUnlockedContent(EMP, "UnlockedContentArchivist")   # fixed ed309f5 (NewUnlockedContentNotificationWindow)
new EventPlanetDestroyedWarning(EMP, FLEET, PLANET, "Obliterator")
new EventBailiffReport(EMP, new System.Collections.Generic.List<AuctionInstruction>(new AuctionInstruction[] { new AuctionInstruction(AuctionInstruction.AuctionType.Improvement, AuctionInstruction.AuctionAction.Sell, "Test Improvement", SYS.LocalizedName, AuctionInstruction.AuctionLocationType.StarSystem, new AuctionInstruction.ResourceAmount("Luxury1", 50f)), new AuctionInstruction(AuctionInstruction.AuctionType.Ship, AuctionInstruction.AuctionAction.Scrap, FLEET.LocalizedName, SYS.LocalizedName, AuctionInstruction.AuctionLocationType.Fleet, new AuctionInstruction.ResourceAmount("Luxury1", 20f)) }))
new EventTradingBlockade(EMP, NODE)
new EventTradingBlockade(EMP, SYS2.Node)
new EventTreatiesCancelled(EMP, new DiplomaticAbility(Amplitude.Unity.Framework.Databases.GetDatabase<DiplomaticAbilityDefinition>().GetValues()[1], 5), EMP1)
new EventOnRelicsCollected(EMP, FLEET, NODE, 5f)
new EventOnRelicsCollectionCanceled(EMP, FLEET, NODE, EventOnRelicsCollectionCanceled.ReasonOfCancelation.FleetPositionChanged)
new EventLostRootsConnectivity(EMP, new System.Collections.Generic.List<GameEntityGUID>(new GameEntityGUID[] { NODE.GUID, SYS2.Node.GUID }))
new EventMetaplotFinished()   # fixed ed309f5 (MetaplotFinishedNotificationWindow)
new EventAcademyDiplomaticMoodMessage(EMP, EMP1, "Greetings from the Academy.", AcademyAttitudeType.Allied, AcademyDialogueType.Hello)
new EventAcademyDiscovered(EMP, EMP, NODE)
new EventAcademyRequestFinishing(EMP, "Luxury1")
new EventOnEmpireRelicSlotLocked(EMP, NODE, "EmpireRelicSlot00")
new EventLodestoneReward(EMP, "Luxury1", 100)
new EventPlayDeckNewSlot(EMP)
new EventPopulationBoostEnded(EMP, (Amplitude.StaticString)(new System.Collections.ArrayList(((System.Collections.IDictionary)SYS.PopulationsByAffinity).Keys)[0]))
new EventHackingOperationLeftUnassigned(EMP)
new EventTradingSubsidiaryDestroyed(new TradingCompanyStructure(TradingCompanyStructureType.Subsidiary, new GameEntityGUID(999015UL), SYS2.Node.NodePosition, 5, EMP.Index, new GameEntityGUID(999016UL)))
new EventTradingSubsidiaryLost(new TradingCompanyStructure(TradingCompanyStructureType.Subsidiary, new GameEntityGUID(999017UL), SYS2.Node.NodePosition, 5, EMP.Index, new GameEntityGUID(999018UL)))
new EventAcademyColonized(EMP, EMP1, NODE)
new EventAcademyLevelup(2)
new EventAllianceVictoryImminent(new Alliance(new GameEntityGUID(999002UL), "Test Alliance", 4), new VictoryCondition("VictoryScore"))
new EventAnarchyStarted(EMP, EMP.GetSenat().Government)
new EventAutomatedFleetLost(EMP, EMP1, FLEET)
new EventBailiffImminent(EMP, new System.Collections.Generic.List<Amplitude.StaticString>(new Amplitude.StaticString[] { "Luxury1" }))
new EventOutpostLost(EMP, EMP1, SYS)
new EventContextualDiplomaticExchangeCancelled(EMP, new ContextualDiplomaticExchange(new GameEntityGUID(999003UL), EMP, EMP1, NODE, Amplitude.Unity.Framework.Databases.GetDatabase<ContextualDiplomaticTermDefinition>().GetValues()[0], null, new System.Collections.Generic.Dictionary<Amplitude.StaticString, ContextualDiplomaticTermDynamicEffectContext>()))
new EventCuriosityFailed(EMP, new Curiosity(new GameEntityGUID(999004UL), Amplitude.Unity.Framework.Databases.GetDatabase<CuriosityDefinition>().GetValues()[0], Gui.Game.Empires.Length))
new EventCuriosityGuardian(EMP, new Curiosity(new GameEntityGUID(999005UL), Amplitude.Unity.Framework.Databases.GetDatabase<CuriosityDefinition>().GetValues()[0], Gui.Game.Empires.Length))
new EventEmpireCancelledAttackRestriction(EMP, EMP1, EMP2)
new EventEmpireFreeSuperColonization(EMP)
new EventEnemyAutomatedFleetsDestroyed(EMP, EMP1, NODE, 2, 50f)
new EventFactionPactCancelled(EMP, EMP1)
new EventFleetActionReady(EMP, FLEET, "Sleep")
new EventGhostPlanetLost(EMP, PLANET)
new EventGhostSystemDiscoveredOnOwnedSystem(EMP, EMP1, NODE)
new EventGhostSystemLost(EMP, NODE)
new EventGovernmentStarted(EMP, EMP.GetSenat().Government)
new EventHackingBeaconCreationFailed(EMP, NODE)
new EventHiddenHomeSystemDetected(EMP, EMP1, SYS)
new EventInfluencedSystemPopulationConsumed(EMP, EMP1, SYS, (Amplitude.StaticString)(new System.Collections.ArrayList(((System.Collections.IDictionary)SYS.PopulationsByAffinity).Keys)[0]))
new EventInformationMessage(EMP, "NotificationLowEmpireManpower", 0UL)
new EventJuggernautLocalActionEnded(EMP, NODE, "JuggernautLocalAction")
new EventMarketplaceAdvertisementPosted(new MarketplaceAdvertisement(EMP.Index, "Luxury1", 5, false, true))
new EventMarketplaceEventStarted(new MarketplaceEvent("MarketplaceEventContext01Luxury1DemandHigh1", 5, "Luxury1"))
new EventMinorAndPirateHackingUnlocked(EMP)
new EventMinorEmpireDead(EMP, EMP1, MINOR, true, null)
new EventMinorEmpireIntegrated(EMP, MINOR)
new EventMinorEmpireRazed(EMP, MAJ1, MINOR, true)
new EventSuzerainChanged(EMP, EMP2, MAJ1, MINOR)
new EventMinorEmpireUnderInfluence(EMP, MINOR2, true)
new EventMothershipLost(EMP, NODE, new Mothership(new GameEntityGUID(999006UL), EMP))
new EventMothershipReclaimed(EMP, NODE, new Mothership(new GameEntityGUID(999007UL), EMP))
new EventNewForeignOutpostOnRootedNode(EMP, EMP1, NODE)
new EventOutpostConcurrenced(EMP, EMP1, SYS)
new EventOutpostLocked(EMP, SYS)
new EventOutpostLockedWithHonor(EMP, SYS2)
new EventPirateEmpireEliminated(PIR)
new EventPoliticsUnlocked(EMP, Amplitude.Unity.Framework.Databases.GetDatabase<PoliticsDefinition>().GetValues()[0])
new EventRebellionImminent(EMP, NODE)
new EventRebellionRogueFleetCreated(EMP, NODE, FLEET)
new EventRelicsSpawned(EMP, 3)
new EventRemainsSpawned(EMP, new Remains(new GameEntityGUID(999008UL), Amplitude.Unity.Framework.Databases.GetDatabase<RemainsDefinition>().GetValues()[0], PLANET, 10, null, Amplitude.StaticString.Empty))
new EventResourcesStolenFromPirates(EMP, NODE, new System.Collections.Generic.Dictionary<Amplitude.StaticString, float>() { { "Luxury1", 10f } }, 0.5f)
new EventResourcesTransferEndedOnFleet(EMP, FLEET, SYS, "NotificationResourcesTransferred")
new EventResourcesTransferStartedOnSystem(EMP, EMP1, SYS, "NotificationResourcesTransferred", "NotificationResourcesTransferred")
new EventRootsColonized(EMP, NODE)
new EventShipLockedInGarrisonRecovered(EMP, SHIP, FLEET)
new EventSystemGainedFromInfluence(EMP, SYS, EMP1)
new EventSystemLostToInfluence(EMP, SYS2, EMP1, null)
new EventSystemMarkedByPirates(EMP, NODE)
new EventTimeBubbleUnused(EMP)
new EventTradingHeadquarterUnlocked(EMP)
new EventTreatyCancelled(EMP, new DiplomaticAbility(Amplitude.Unity.Framework.Databases.GetDatabase<DiplomaticAbilityDefinition>().GetValues()[1], 5), EMP1)
new EventEmpireVictoryImminent(EMP, new VictoryCondition("VictoryScore"))
new EventAcademyRoleFinishing(EMP)
new EventOnAcademyDiplomaticExchange(EMP, EMP1, "Academy Test Title", "The Academy has a request for you.", "Effect one, Effect two", AcademyAttitudeType.Neutral, AcademyDialogueType.Sendresources, false)
new EventOnAcademyDiplomaticExchange(EMP, EMP1, "Academy Roles Title", "Roles have been distributed.", "Effect three", AcademyAttitudeType.Allied, AcademyDialogueType.Roleshavebeendistributed, true)
new EventDisplacementReport(EMP, SYS, new System.Collections.Generic.List<StarSystemImprovementDefinition>(), new System.Collections.Generic.Dictionary<Amplitude.StaticString, int>() { { (Amplitude.StaticString)(new System.Collections.ArrayList(((System.Collections.IDictionary)SYS.PopulationsByAffinity).Keys)[0]), 2 } })   # fixed ed309f5 (DisplacementReportNotificationWindow)
new EventDisplacementFailed(EMP, SYS, SNODE)   # fixed ed309f5 (DisplacementReportNotificationWindow)
new EventEmpireEliminated(EMP2)
new EventIonWaveReport(EMP, EMP1, new IonWaveReport(NODE, EMP.Index))
new EventObliteratorAttackReport(EMP, EMP1, new ObliteratorAttackReport(NODE, ObliterationDefense.None), new System.Collections.Generic.Dictionary<int, ObliteratorVictimReport>() { { EMP1.Index, new ObliteratorVictimReport(NODE, ObliterationDefense.Shield) } })
new EventObliteratorVictimReport(EMP, EMP1, new ObliteratorVictimReport(NODE, ObliterationDefense.Shield))
new EventTechnologyNeeded(EMP)
new EventSpecialNodeLost(EMP, EMP1, SNODE)
new EventSpecialNodeOwned(EMP, EMP1, SNODE)
new EventTraitorRemoved(EMP, EMP1, NODE, 1)   # scan table: arrival line only
new EventHackingOperationDetected(EMP, NODE)   # scan table: arrival line only
new EventHackingProgramActivationFailed(EMP, Amplitude.Unity.Framework.Databases.GetDatabase<HackingProgramDefinition>().GetValues()[0], NODE)   # scan table: arrival line only
new EventTraitorPopulationDiscovered(EMP, SYS)   # scan table: arrival line only
new EventTraitorPopulationRemovalFailed(EMP, SYS)   # scan table: arrival line only
new EventTraitorPopulationRemovalSuccessful(EMP, SYS)   # scan table: arrival line only
```

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


Fakes that do not bind either, measured 2026-09-16: `EventAllianceCreated` (veto: no member the player
knows), `EventTradingHeadquarterDestroyed`/`Lost` (`Bind` NPEs in `GetOwnerTradingCompany()`),
`EventMinigameObjectiveSecured` (`MinigameObjective` is abstract; its subclasses NPE in `Bind` on a fake
team list), `EventSystemUnderInfluence` (the constructor asserts on the system's owner for every empire
pairing tried), `EventTimeBubbleExpired` (silent veto), `EventShipLockedInGarrison` (veto unless already
locked from damage). `Databases.GetDatabase<TechnologyDefinition>()` and `<StarSystemImprovementDefinition>()`
answer null from the REPL in this build.

---

## 4. What is left (2026-09-16)

Never sighted live, and why (the alternative is the only route):

| Popup | Why unsighted | Route |
|---|---|---|
| `ForceTruceProposedNotificationWindow` | needs a real force-truce proposal; its caption pairs are the answered window's, compiled but unseen | be at war long enough for the AI to propose one |
| the alliance case of both truce popups (a side's label carries the member list on its tooltip) | both sides were single empires | a war with an alliance on one side |
| `PirateMissionReportNotificationWindow` | fixture-blocked payload (§3) | be raided by pirates |
| `MetaplotBegunNotificationWindow` | `Bind` needs the live Academy minigame (§3) | play the Academy metaplot |
| the hacking popups: `DefenseHackingProgramEncountered`, `HackingOperationOutcomeSelection` (countdown, one-of-N), `HackingOperationOutcomeApplied`, canceled-by-trace / traced-but-not-canceled, and the 13 scan-table events that need a live operation | every payload needs a live `HackingOperation` (§3) | run a hacking operation into a defended node, from Scan view |
| `CompetitiveQuestWarning`, `DeedCompleted` | live `Quest` (§3) | play a competitive quest / earn a deed |
| `GroundBattleAborted`, `GroundBattleOutcomeApplied` | `GroundBattle` (§3) — the setup, report and outcome-selection windows are owner-tested | abort an invasion; let one resolve |
| `EventAllianceCreated` / `EmpireJoinedAlliance` (the alliance-update popup) | vetoed unless the player knows a member | form or be invited into an alliance |
| `TradingHeadquarterDestroyed` / `…Lost` | `Bind` needs a real trading company (the subsidiary raises sighted the window) | lose a headquarters in play |
| `MinigameObjectiveSecured`, `SystemUnderInfluence`, `TimeBubbleExpired`, `ShipLockedInGarrison` | constructor asserts or `Bind` vetoes on any fake (§3) | real play only |

Owner rulings still open — consequences of the empty-string rule (`3849513`), reported and unchanged:
- a strip row for a notification whose title the game cannot write is a button with no name;
- such a notification arrives silently (the arrival line skips an empty string);
- a popup whose title and description both fail lands the cursor on a words row that says nothing;
- a popup whose description alone fails speaks its title twice (screen name, then the words row's fallback).

Pre-existing findings, not fixed:
- `notification:show-location` is flagged by `NotificationParity` as "says what nothing draws" on every
  popup with that rail: the "(Ctrl+L)" suffix is not drawn text (an audit rule question, not a reading defect);
- the displacement population line's review buffer still holds "x" and "2" as separate lines, though its
  announcement is named;
- the fold tick of a report is read last among the body items while drawn at the panel's top right;
- the resources-transferred strip row is a nameless button because the game's own title is legitimately empty.

Owed manual passes: every stage this day drove the mod through `/input` and `/eval`; the strip and the
popups under physical keys (Escape's next-unread, Alt+arrows, Backslash, Ctrl+L) have not been walked
with a real keyboard since the fixes.
