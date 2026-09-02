# ES2 application architecture — reverse-engineering notes

Documents how the game works, not how the mod works — the game's own layering, and the
store-divergence seams the mod must respect. Sources cited as `decompiled/<Assembly>/<File>.cs`;
regenerate with `.\decompile.ps1`. GUI specifics are in `gui.md`.

## Application and services

- Root MonoBehaviour: `EndlessSpace2Application : Amplitude.Unity.Framework.Application`
  (`Assembly-CSharp/EndlessSpace2Application.cs`; base in
  `Assembly-CSharp-firstpass/Amplitude.Unity.Framework/`).
- **Service locator** — the single most important pattern for mod code:
  `Amplitude.Unity.Framework.Services.GetService<T>()` (static, callable from anywhere).
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
- **`IEndTurnService` is `GameManager`, and it OUTLIVES a galaxy; `Gui.Game` is what changes.**
  Loading a save from a running session hands back the same `IEndTurnService` instance either side
  while `Gui.Game` becomes a different object. So a watcher that re-baselines on
  "my service instance changed" never notices a load at all, and keeps a table keyed by entity
  GUIDs the new galaxy has already re-used (measured: stale foreign-fleet rows survive a
  load). The rule the mod follows (`ForeignFleetWatch.Follow`): the SUBSCRIPTION follows the
  service, the TABLE follows the `Game`. And a galaxy does not finish arriving in the frame its
  `Game` does — a load restores the client's visibility layers by a path that is not
  `EntityVisibility.SetLayer`, so a baseline taken then sees fleets the watch never saw a crossing
  for. A baseline is a WINDOW, not a snapshot (`ForeignFleetWatch.Arriving`).

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

The concrete event types are `Assembly-CSharp/Event*.cs`. Most derive from `EmpireEvent`
(carries `.Empire`) — filter to the player's empire before narrating.

Alternative higher-level source mirroring the in-game notification list:
`IGuiNotificationService.PlayerEmpireNotificationsCollectionChanged`
(`Assembly-CSharp/GuiNotificationManager.cs`); `GuiNotification` subclasses resolve entities via
`IGameEntityRepositoryService.TryGetValue(guid, out entity)`.

## Keyboard input

- `InputManager` (`Assembly-CSharp/InputManager.cs`, extending
  `Assembly-CSharp-firstpass/Amplitude.Unity.Input/InputManager.cs`). Bindings are declarative
  `InputBinding`s persisted to the registry; every bindable action is a `StaticString` in
  `Assembly-CSharp/InputAction.cs` — a closed set, and effectively the game's full shortcut list.
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

Per-empire subsystems are "Departments" (`Assembly-CSharp/Department*.cs`). Many expose
collection-change events — prefer subscribing over polling.

All entities (`Fleet`, `Ship`, `ColonizedStarSystem`, `Empire`, …) implement
`IGameEntity { GameEntityGUID GUID }` and resolve via `IGameEntityRepositoryService`.

## Localization of data-driven names

- `Gui.Localize(key)` / `Gui.GetLocalizedTitle(guiElementName)` for anything keyed (`%`-prefixed).
- Procedural names (star systems, planets) and empire names are plain strings, already final
  (`Empire.LocalizedName => Name`).

## GOG vs Steam — where the two builds diverge

- **The GOG build renames the galaxy class.** Top-level `Galaxy` (Steam) is `GalaxyIngame` on
  GOG, because GOG ships `GalaxyCSharp.dll` (the GOG Galaxy SDK, namespace `Galaxy.Api`) and the
  names would collide. The `Game.Galaxy` PROPERTY keeps its name, and `GameNodes` /
  `StarSystemNodes` are member-identical. **Policy: the mod never names the type** — a member
  reference compiled against one store's assembly fails at RUNTIME on the other, not at build
  time. `ES2Access/UI/GameGalaxy.cs` is the single reflected seam, and the offline
  `ES2Access.Tests/StoreDivergenceTests.cs` fails the suite if any other file names a divergent
  member.
- **The GOG build strips the Steam Workshop fields.** `ModdingScreen` lacks
  `SteamWorkshopButton` and `WorkshopLegalAgreementButton` (+ `Label`); `ModdingAvailableModsPanel`
  lacks `WorkshopFilterToggle`. `ES2Access/UI/SteamWorkshop.cs` reflects them and answers null on
  GOG.
- **`decompiled/` is a Steam-era snapshot** (generated 29 Jul 2026, before the GOG switch) and
  disagrees with the live assemblies on both points above. Regenerating via `decompile.ps1` would
  produce the GOG view, and the Steam DLLs now live only on the other machine. When the snapshot
  and the live game disagree, verify against the live assembly:
  `ilspycmd -t <Type> "<game>\EndlessSpace2_Data\Managed\Assembly-CSharp.dll"`.

## Modding support and per-frame work

The game has **no native code-mod API** — only data-driven XML "Runtime Modules"
(`Assembly-CSharp/RuntimeManager.cs`) and Steam Workshop content. BepInEx + Harmony is the way in.

Per-frame mod work belongs in the plugin's own persistent MonoBehaviour `Update` (BepInEx
provides this), never in a `GuiWindow.SpecificUpdate` (stops when that window hides). Services
may be null early in boot and after session teardown — see the caveat under Application and
services.
