# ES2 application architecture — reverse-engineering notes

Documents how the game works, not how the mod works. Sources cited as
`decompiled/<Assembly>/<File>.cs`; regenerate with `.\decompile.ps1`. GUI specifics are in
`es2-gui-framework.md`.

## Application and services

- Root MonoBehaviour: `EndlessSpace2Application : Amplitude.Unity.Framework.Application`
  (`Assembly-CSharp/EndlessSpace2Application.cs`; base in
  `Assembly-CSharp-firstpass/Amplitude.Unity.Framework/`).
- **Service locator** — the single most important pattern for mod code:
  `Amplitude.Unity.Framework.Services.GetService<T>()` (static, callable from anywhere). ~117
  `I*Service` interfaces: `IGuiService`, `IGameService`, `ISessionService`, `IEventService`,
  `IInputService`, `ILocalizationService`, `IEndTurnService`,
  `IPlayerControllerRepositoryService`, `IGameEntityRepositoryService`, …
- **Caveat**: services register asynchronously (each manager's `BindServices()` coroutine) and
  are torn down when returning to the main menu. Never cache a service across scene/session
  transitions; re-acquire and null-check, or use the `GetServiceWeakRef<T>()` polling pattern
  seen throughout `Assembly-CSharp/Gui.cs`.

## State machines

- **Runtime layer** (outgame vs lobby): `Assembly-CSharp/Runtime.cs` registers
  `RuntimeState_Bootstrapper` / `RuntimeState_OutGame` / `RuntimeState_Lobby`; transitions via
  `FiniteStateMachine.PostStateChange(typeof(...))`.
- **In-game turn layer**: `GameClientState_Turn_Begin/Main/End/...`
  (`Assembly-CSharp/GameClientState_*.cs`). Empire simulation ticks inside these states, not in
  Unity `Update`.
- Per-frame simulation pump while a game is active: `Session.Update()` →
  `GameClient.Update()` (`Assembly-CSharp/Session.cs`).

## Deterministic player actions: the Order system

Nearly every player action is an `Order` subclass (~400 files, `Assembly-CSharp/Order*.cs`):
`OrderColonize`, `OrderAssignHero`, `OrderChangeDiplomaticRelationState`, … Orders serialize
client→server (a local server always exists, even single-player) and are validated server-side.

Posting (verified, `Assembly-CSharp/PlayerController.cs`):

```csharp
Order order = new OrderMoveIdleFleets(playerEmpire.Index);
playerEmpire.PlayerControllers.Client.PostOrder(order);
// or: Gui.GetActivePlayerController().PostOrder(order);
// overload with `out Ticket` + event handler exists for completion callbacks
```

This is the same pipeline the UI uses — prefer it over simulating clicks whenever an order
exists for the action.

**End Turn is not an order** (verified, `Assembly-CSharp/GameManager.cs`): the End Turn button
calls `IEndTurnService.TryToEndTurn()`, which runs registered validators (`CanEndTurn()`)
before transitioning. Call the same service from mod code.

## Events — the narration source

`IEventService` (`Assembly-CSharp-firstpass/Amplitude.Unity.Event/IEventService.cs`, implemented
by `EventManager`) exposes one hook (verified):

```csharp
Services.GetService<IEventService>().EventRaised += (s, e) => {
    GameEvent evt = e.RaisedEvent;   // switch on concrete type
};
```

~280 concrete event types (`Assembly-CSharp/Event*.cs`): `EventBeginTurn`,
`EventConstructionCompleted`, `EventBattleWon/Lost`, `EventTechnologyUnlocked`,
`EventDiplomaticRelationChange`, `EventPlanetColonized`, … Most derive from `EmpireEvent`
(carries `.Empire`) — filter to the player's empire before narrating.

Alternative higher-level source mirroring the in-game notification list:
`IGuiNotificationService.PlayerEmpireNotificationsCollectionChanged`
(`Assembly-CSharp/GuiNotificationManager.cs`); `GuiNotification` subclasses resolve entities via
`IGameEntityRepositoryService.TryGetValue(guid, out entity)`.

## Keyboard input

- `InputManager` (`Assembly-CSharp/InputManager.cs`, extending
  `Assembly-CSharp-firstpass/Amplitude.Unity.Input/InputManager.cs`). Bindings are declarative
  `InputBinding`s persisted to the registry; every bindable action is a `StaticString` in
  `Assembly-CSharp/InputAction.cs` (44 actions: `EndTurn`, `EmpireScreen` F1…F8, `QuickSave`,
  camera controls, …) — effectively the game's full shortcut list.
- Dispatch (verified): base `InputManager` polls bindings and walks a priority array of
  `IInputHandler`s (`CanHandleInput()` / `HandleInput(inputAction)`); registered priorities:
  ViewManager 1, CameraManager 2, GuiManager 5. The ES2 override of
  `HandleInput` gates on `AgeManager.Instance.FocusedControl.IsKeyExclusive` (text-field focus).
- For mod hotkeys, options are: patch `InputManager.HandleInput` (prefix), register an
  `IInputHandler` (limited to existing named actions), or poll `UnityEngine.Input` from the
  plugin's own `Update` — the latter is independent of the game's modal-focus gating, but must
  itself respect `AgeManager.Instance.FocusedControl` to avoid stealing keys from text fields.

## Reading game state

```csharp
Game game = (Game)Services.GetService<IGameService>().Game;      // Empires, Galaxy, Academy
Empire player = Gui.PlayerEmpire;                                 // GUI-context convenience
var systems = player.GetAgency<DepartmentOfTheInterior>().ColonizedStarSystems;
var fleets  = player.GetAgency<DepartmentOfDefense>().Fleets;
```

Per-empire subsystems are "Departments" (`Assembly-CSharp/Department*.cs`, 15): Interior
(colonies), Defense (fleets), Science, Industry (construction; raises
`EventConstructionCompleted`), Treasury, Education (heroes), ForeignAffairs, … Many expose
collection-change events (`FleetsCollectionChange`, `ActiveHeroesCollectionChange`) — prefer
subscribing over polling.

All entities (`Fleet`, `Ship`, `ColonizedStarSystem`, `Empire`, …) implement
`IGameEntity { GameEntityGUID GUID }` and resolve via `IGameEntityRepositoryService`.

## Localization of data-driven names

- `Gui.Localize(key)` / `Gui.GetLocalizedTitle(guiElementName)` for anything keyed (`%`-prefixed).
- Procedural names (star systems, planets) and empire names are plain strings, already final
  (`Empire.LocalizedName => Name`).

## Modding support and per-frame work

The game has **no native code-mod API** — only data-driven XML "Runtime Modules"
(`Assembly-CSharp/RuntimeManager.cs`) and Steam Workshop content. BepInEx + Harmony is the way in.

Per-frame mod work belongs in the plugin's own persistent MonoBehaviour `Update` (BepInEx
provides this), never in a `GuiWindow.SpecificUpdate` (stops when that window hides). Services
may be null early in boot and after session teardown — see the caveat under Application and
services.
