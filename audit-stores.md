# Store / platform audit — is there a GOG (or other desktop store) code path?

Decompile + game-data + mod-source only. No live game. Every claim carries a `file:line` cite
or is marked UNVERIFIED. Paths are relative to `decompiled/` unless stated.

---

## 0. Bottom line

**There is no store abstraction in this build. There is no GOG code, no Galaxy SDK, no Epic, no
generic "platform"/"distributor" layer. Steam is the only store the shipped managed code knows.**

What exists instead is a **Steam-absent degradation path** that Amplitude built deliberately and
left on by default (`Amplitude.Unity.Framework/Application.cs:28` —
`enableOfflineModeWhenSteamClientIsDown = true`). Every store question therefore collapses into
one question the mod can actually answer: *what does the mod do when `IsSteamRunning` is false and
`SteamApps`/`SteamFriends`/`SteamUser` are null?*

Answer: **nothing changes, because the mod never calls a Steam API and never computes ownership
itself.** A grep for `Steam` over `ES2Access/`, `ES2Access.Loader/`, `ES2Access.Tests/` returns 17
hits and every one is a comment, a `ModStrings` key, or a read of a *game-built* object
(`LobbyInfo`, `GuiLobbyInfo`, `GuiTableCellDownloadableContent`). Zero calls into
`Amplitude.Interop.Steamworks`, `ISteamService`, `ISteamUGCService`, or
`IDownloadableContentService`.

**Concrete work required for GOG: one line of English wording (`loadsave.cloud`), and one README
word.** Everything else is either identical or degrades exactly as vanilla degrades. Details in §5
and §6.

One caveat that no amount of decompiling can settle, stated up front so it is not mistaken for a
finding: the GOG build's `Assembly-CSharp.dll` may not be byte-identical to the Steam one (see
§4.3). The mod compiles against the Steam build's types. That is a general binary-compatibility
risk, not a store-logic risk, and it is UNVERIFIED without a GOG install.

---

## 1. How the store surface was enumerated (not from memory)

| Sweep | Result |
|---|---|
| Filenames matching `*gog*`, `*epic*`, `*store*`, `*platform*`, `*distrib*` over both assemblies | nothing store-related. Only `RenderHeads.Media.AVProVideo/Platform.cs` (a video plugin's OS enum) and false positives on ES2's own *Galaxy* (the map) |
| Content grep `\bGOG\b`, `Gog`, `galaxy64`, `GalaxyCSharpGlue`, `Epic`, `EOS`, `Uplay`, `Origin\b`, `Distributor`, `DRM` | **zero hits** except `PopulationModifiersTraitSecondaryDustHappyEpic` (a trait name) and `SeekOrigin` |
| Game's `Managed/` folder listing (`<game>\EndlessSpace2_Data\Managed`) | no `Galaxy*.dll`, no `GalaxyCSharp*`, no Epic/EOS assembly. Store natives in the game root are `steam_api64.dll` + `steam_api_dotnetwrapper64.dll` only |
| `Registry.xml` (game root, 10 lines) | no store/platform key at all |
| Steam-named types | one namespace, `Amplitude.Unity.Steam/` (38 files), plus `Amplitude.Interop/Steamworks.cs` (3632 lines of P/Invoke), `Amplitude.Unity.Achievement/SteamAchievementManager.cs`, `Amplitude.Unity.Session/SteamLobbyDataController.cs`, and two 7-line ES2 subclasses (`Assembly-CSharp/SteamManager.cs`, `SteamNetworkingManager.cs`) |

**What decides which store path runs:** nothing. It is not compile-time (one build, no `#if`
survivors), not launch-arg (the only command-line parsing in `Application.LoadRuntime` is `+mod`
at `Amplitude.Unity.Framework/Application.cs:1207-1224`, and `DownloadableContentManager`'s own is
`+dlc`/`-dlc` at `Assembly-CSharp/DownloadableContentManager.cs:340-395`), and not data-driven
(`Registry.xml` names no store). The single runtime branch is
**"did `SteamAPI_Init()` succeed?"**.

### The one seam Amplitude built and then hardwired

`Amplitude.Unity.Session/IApiLobbyDataController.cs` is a 3-member interface — exactly the shape a
second platform's lobby backend would implement — and `LobbyDataController`'s constructor
hardwires the only implementation:

```csharp
// Amplitude.Unity.Session/LobbyDataController.cs:49-55
public LobbyDataController(Session session)
{
    Session = session;
    apiLobbyDataController = new SteamLobbyDataController { Session = session };
}
```

No factory, no service lookup. So even the one abstraction that anticipated a second store is not
reachable from configuration in this build.

---

## 2. The Steam-absent path, precisely

This is the profile any non-Steam desktop store (or a Steam user with the client closed) presents,
and it is the yardstick for every verdict below.

1. `Application.Ignite` calls `SteamInitialize()` → `Steamworks.SteamAPI.Init()`
   (`Application.cs:908`, `:1355-1358`). **Unguarded** — if `steam_api64.dll` is missing entirely
   this throws `DllNotFoundException` out of the ignition coroutine into
   `Manager_Ignite_CoroutineExceptionCallback` (`:1227-1232`) → `Quit()`. So a store build *must*
   ship a `steam_api64.dll` (real or shim) or a differently-compiled assembly.
2. `SteamManager.BindServices` (`Amplitude.Unity.Steam/SteamManager.cs:557-580`) calls
   `InitializeSteam` (`:620-645`). On `Init()` returning false **with offline mode on** it logs
   *"Failed to initialize the Steam API; now running in offline mode…"* and leaves `LastError == 0`
   — so execution continues and **`ISteamService`, `ISteamMatchMakingService`,
   `ISteamServerService`, `ISteamClientService` are all still registered** (`:574`, `:578-580`),
   with `IsSteamRunning == false` and `SteamApps`/`SteamFriends`/`SteamUser` null (the interop
   accessors return null on a zero handle — `Amplitude.Interop/Steamworks.cs:94-101` and siblings).
   With offline mode **off** it logs an error and `Application.Quit()`.
3. That the services stay registered is load-bearing for the *game*, not just the mod:
   `Assembly-CSharp/MainMenuItem.cs:97` does
   `Services.GetService<ISteamService>().IsSteamRunning` with **no null check**, so an unregistered
   `ISteamService` would NRE on every main-menu refresh. Same shape at
   `DLCItem.cs:63` and `GameSaveDescriptor.cs:108`. Conclusion: **any working store build must
   register `ISteamService`** — which means mod code may assume it exists, and (better) the mod
   never needs it.
4. `ISteamUGCService` likewise registers unconditionally with `IsSteamUGCRunning == false`
   (`Amplitude.Unity.Steam/SteamUGCManager.cs:55-69`), so `ModdingScreen`'s
   `Gui.WaitForServiceThenAssign(...)` at `:307` still completes and the screen still becomes ready.
5. Single player is explicitly designed for it: `PlayerRepository.GetPlayerBySteamID` has a
   no-Steam branch that returns the one `PlayerType.Graphical` player
   (`Assembly-CSharp/PlayerRepository.cs:65-75`), and `Session.GenerateSessionName` falls back to
   the base name (`Session.cs:545-562`).
6. Identity: `Application.UserName` stays `"Default"` (`Application.cs:829`) because
   `SteamGetSteamUserName` no-ops on a null `SteamUser` (`:1340-1352`).
7. **Language falls back to English.** `Amplitude.Unity.Localization/LocalizationManager.cs:77-84`
   is the *only* writer of `CurrentLanguage`, and its only non-default source is
   `SteamService.SteamApps.GetCurrentGameLanguage()`. ES2's subclass
   (`Assembly-CSharp/LocalizationManager.cs`, 6 lines) overrides only
   `MissingLocalizationReportingEnabled`. No registry key, no option, no command-line override
   anywhere. **So on a store with no Steam client, ES2 is English-only.**
8. **All DLC becomes unowned.** See §4.

---

## 3. Per-subsystem verdicts

| Subsystem | Steam dependence | Non-Steam / other-store behavior | Mod impact |
|---|---|---|---|
| DLC ownership | `DownloadableContent.IsSubscribed` = `SteamApps?.BIsSubscribedApp(SteamAppId) ?? false` (`Assembly-CSharp/DownloadableContent.cs:43`); `IsInstalled` = `BIsDlcInstalled` (`:36-42`) | every DLC gets `Accessibility = None`; all four expansions' surfaces stay hidden | **works unchanged** — mod reads the game's own visibility, never ownership (§4) |
| DLC "Buy" | `SteamFriends.ActivateGameOverlayToStore`, **with fallback** `Process.Start(StoreLink)` (`DLCItem.cs:81-96`); `StoreLink` is a hardcoded `store.steampowered.com/app/<id>` URL (`DownloadableContent.cs:50`) | button drawn but **disabled** (`DLCItem.cs:63` `BuyButton.Enable = service.IsSteamRunning`), so the fallback is unreachable from a click | mod must read *whichever of toggle/buy is visible* and speak the disabled case — store-agnostic by construction (§4.4) |
| Multiplayer lobby list | Steam matchmaking only; `LobbyInfo` is ~35 `GetLobbyData` reads off a Steam lobby (`LobbyInfo.cs:153-202`) | **Join Game entry disabled** with `FailureFlags.SteamNotRunning` on its tooltip (`MainMenuItem.cs:91-102`), so `JoinGameScreen` is unreachable | **degrades, correctly**. The mod already treats a disabled entry as information, not absence (`ES2Access/Screens/MainMenuScreen.cs:28-30`), and `ES2Access.Tests/UI/TooltipPartTests.cs:215-216` asserts the spoken form |
| Networking transport | one transport: Steam P2P (`Amplitude.Unity.Steam/SteamNetworkingManager.cs`; ES2's subclass is empty). No LAN, no direct IP | no multiplayer at all | nothing to do |
| Lobby data replication | `LobbyDataController` hardwires `SteamLobbyDataController` (`:49-55`) | lobby/session data never leaves the machine; solo sessions still work (they use the same `Session` with mode `Single`) | nothing to do |
| Friend invite | `SteamFriends.ActivateGameOverlayInviteDialog` (`NewGameCompetitorSlotsPanel.cs:181-201`) — fully null-guarded, logs a warning and returns | button present in MP+hosting only, which is unreachable without Steam | nothing to do |
| Overlay join request | `RuntimeState_Lobby.OnGameLobbyJoinRequested` (`:142-168`), name via `LobbySlot.FindUsername` behind `if (Steamworks.SteamAPI.IsSteamRunning)` else `"unknown"` | never fires | nothing to do |
| Player rename in lobby | requires `IsSteamRunning` (`NewGameEmpireSlotPanel.cs:129-133`) | refused with a log warning | already recorded as fixture-blocked (`docs/test-recipes.md:138`) |
| Chat identities | `ChatMessage` carries `From`/`To` SteamIDs; display uses `PlayerName` | MP-only, so moot | nothing to do |
| Cloud saves | **not a Steam API at all.** The toggle writes registry key `Settings/Steam/CloudRemoteStorage` (`EndlessSpace2Application.cs:25`, `LoadSaveModalWindow.cs:165`, `:518-521`) and that key only *redirects the save directory* to `<saves>\Cloud` (`EndlessSpace2Application.cs:86-94`, `:142-150`). Steam syncs that folder externally | the tick still works, the folder is still used, only the off-machine sync is absent. `CloudToggleGroup` has **no code writer** (only the field declaration, `LoadSaveModalWindow.cs:42`) so the row is always visible | **works unchanged**; only the mod's fallback *wording* names Steam (§5) |
| Workshop / mods | `ISteamUGCService`, `IsSteamUGCRunning` | `ModdingScreen.OnSteamWorkshopCb` has a `Process.Start` fallback (`:453-464`); the legal-agreement button silently no-ops (`:445-452`). **`ResourcesExportScreen.OnSteamWorkshopCb:169-173` is UNGUARDED** and would NRE on a null `SteamFriends` — but no `AgeControlButton` field in that class references it, so it is probably dead prefab wiring (UNVERIFIED) | screen stays reachable and readable; the mod's model there is minimum-pass anyway (`ES2Access/Screens/MenuDestinationScreens.cs:171-203`) |
| Achievements | `SteamAchievementManager` guards everything behind `steamService.IsSteamRunning` (`Amplitude.Unity.Achievement/SteamAchievementManager.cs:30-52`) | achievements silently inert | mod has no achievement feature |
| Localization | language from Steam only (`LocalizationManager.cs:77-84`) | **English only** | `ES2Access/Localization/ModLocale.cs` follows `ILocalizationService.CurrentLanguage`, so the mod's own strings land in the same language the game landed in. **Degrades identically — correct by construction** |
| Dev tooling | `run-game.ps1:110` launches `EndlessSpace2.exe` directly, not `steam://` | works on any install | none. `GamePaths.props` just needs the GOG path (already a per-machine gitignored file) |

---

## 4. DLC on a non-Steam store — the answer for the pending DLC-window stage

### 4.1 Ownership has exactly one source, and it is Steam

`DownloadableContentManager.Register<T>()` (`Assembly-CSharp/DownloadableContentManager.cs:407-440`)
is the only place accessibility is seeded:

```
IsSubscribed  -> |= Subscribed
  IsInstalled -> |= Installed
    IsDynamicActivationEnabled ? registry "Preferences/DownloadableContents/DownloadableContentN/Activated"
                              : |= Activated
```

and both predicates are Steam calls (`DownloadableContent.cs:36-43`). They are `virtual`, and
**no `DownloadableContentN` overrides either** (grep over all 24 subclasses returns only the
`SteamAppId` consts). `AllowGameOverlayToStore` and `StoreLink` are likewise virtual with zero
overrides. So a GOG build cannot get ownership right without *changing that C#*.

### 4.2 The DLC data is already on disk

367 files named `*_DLC{1..5}*` ship in `<game>\Public\` on this install, which owns **no DLC depot
at all** (`audit-dlc-mechanics.md` §2). So DLC content ships with the base game and is gated purely
by the in-memory `Accessibility` flags plus `DownloadableContentRestriction` wildcard checks
(`DownloadableContent.TryCheckAgainstRestrictions`, `:74-137`).

Two consequences:

- **The planned `AddAccessibility` unhide is store-independent and should work.**
  `IDownloadableContentService.AddAccessibility` (`DownloadableContentManager.cs:141-154`) is a pure
  `Accessibility |= flags` on the in-memory object — no Steam call, no file check, no network. It
  has **zero callers in the whole game** (grep for `AddAccessibility(`/`RemoveAccessibility(`
  returns only the interface and the implementation), so nothing will fight it or recompute over it
  *outside the lobby path below*. Flipping `Subscribed|Installed|Activated` makes `Available` true,
  restrictions pass, and the gated XML/GUI elements are present on disk to be found.
- Asset caveat, UNVERIFIED: `DownloadableContentRestrictionCategory.Mapping` entries map DLC prefab
  paths to base-game replacements when unowned (e.g. `DownloadableContent10.cs:18-24`), which is
  consistent with either "DLC prefab present but substituted" or "DLC prefab absent, substitution
  avoids a missing asset". Unhiding could therefore surface a missing model. Irrelevant to reading
  a screen; relevant if a stage judges a screenshot.

### 4.3 The `Shared` flag has a lifecycle that will eat a naive REPL poke

`Shared` is **not ownership** — it is "the host shared this with the lobby", carried in Steam lobby
data key `"sbs"` as a bitfield of DLC numbers:

- `RuntimeState_Lobby.OnDownlodableContentSharingChanged` (`:172-190`) recomputes `Shared` for every
  DLC from `Session.GetLobbyData("sbs", 0u)` — **clearing it where the bit is absent**.
- The `SharedByServer` branch (`:400-430`) first does
  `foreach (...) item2.Accessibility &= ~Shared;` and then rebuilds the bitfield from DLC whose
  `Accessibility & Available == Available` (i.e. from Steam-derived ownership), or from the save's
  own stored `"sbs"` when `"rdcol"` is false.

So for a stage that wants DLC surfaces visible: **add `Subscribed|Installed|Activated` (before the
session opens), not `Shared`.** A `Shared`-only poke is wiped at session creation; ownership flags
survive and cause `Shared` to be *derived* as true by the host path. This is a per-process,
in-memory change — it does not touch the registry (only `DLCModalWindow.OnApplyCb:158-180` persists
`Activated`) and does not survive a restart. All of it is store-independent.

### 4.4 What the DLC window does when nothing is owned — and what that means for the stage

`DLCModalWindow` lists **every registered DLC** of the current tab type that has a GUI element
(`FilterGuiDLCs`, `:127-137`), owned or not. `DLCItem.Refresh` (`:29-70`) then picks one of three
shapes per row:

- `Type == Addon` → no toggle, no buy button, no icon;
- `Subscribed` → **Activate toggle** visible, enabled only if `IsDynamicActivationEnabled &&
  Installed`, and when not installed the tooltip becomes
  `Gui.FormatFailure(..., FailureFlags.DLCNotInstalled)`;
- otherwise → **Buy button** visible, `Enable = service.IsSteamRunning`, toggle hidden.

**On any Steam-less store, every non-Addon row is the third shape with a disabled Buy button.** So
the stage's model must be driven by which of `ActivateToggle.AgeTransform.Visible` /
`BuyButton.AgeTransform.Visible` the window drew, plus the standard enabled/refusal reading — never
by the mod asking who owns what. Written that way it is correct on Steam, on GOG, and on a Steam
install with the client closed, with no store-specific branch.

Two vanilla fragilities the stage should know about (they are the game's, not the mod's, but a
tester will hit them): `DLCItem.cs:63` dereferences `Services.GetService<ISteamService>()` with no
null check, and the game's own English text hardcodes Steam
(`%DLCModalWindowBuyButtonDescription` = *"Click to open the Steam Store page associated with this
content"*). The mod reads the game's words, so it will correctly say "Steam Store" on GOG — that is
the game's inaccuracy to own, not the mod's.

---

## 5. Mod cross-reference — every place the mod touches Steam-shaped state

Grep over `ES2Access/`, `ES2Access.Loader/`, `ES2Access.Tests/` for `Steam|SteamIDLobby|IsSteamRunning|LobbyInfo|DownloadableContent|IsShared`. Full list:

| Site | What it reads | Verdict on GOG / Steam-down |
|---|---|---|
| `ES2Access/Screens/MenuDestinationScreens.cs:434-451` `LobbyOf` | `lobby.SteamIDLobby.UInt64AccountID` as the row key | **unreachable** (no lobby list without Steam) and already null-safe + try/caught: returns `null` on a null `SteamIDLobby` |
| same file `:456-467` `LobbyName`, `:471-484` `RowExtras`, `:519-529` `PlayerCount`, `:533-561` `ContentNames` | `LobbyInfo.Name`, `GuiLobbyInfo.VictoryConditions`, `CurrentPlayerCount`/`SlotCount`, the drawn `DLCItemMinimal` tooltips | all read game-built objects, all try/caught. Unreachable without Steam; unchanged with it |
| same file `:420-431` `Searching` | `JoinGameScreen.RefreshButton.Enable` | unreachable; no Steam call |
| `ES2Access/Screens/MainMenuScreen.cs:28-30` (doc) + the mod's disabled-entry reading | the game's own `FailureFlags.SteamNotRunning` tooltip | **this is the GOG behavior already implemented and tested.** `ES2Access.Tests/UI/TooltipPartTests.cs:215-216` pins *"Join a multiplayer game Steam is not running"* |
| `ES2Access/Screens/LoadSaveScreen.cs:755-800` cloud row | `LoadSaveModalWindow.CloudToggleGroup`/`CloudToggle`, and the group's drawn label via `OptionsScreen.LabelIn` | **works unchanged.** The game draws `%LoadSaveModalWindowCloudStatusTitle` = *"Use Steam Cloud"*, so the mod speaks the game's words; `ModStrings.LoadSaveCloud` is only the no-label fallback |
| `ES2Access/locale/english.json:143` `"loadsave.cloud": "Steam cloud saves"` | mod-authored | **the one item to change.** It is the only mod-authored spoken phrase that names a store. See §6 |
| `ES2Access/Localization/ModLocale.cs:19-27` | `ILocalizationService.CurrentLanguage` | correct on every store: follows whatever the game resolved (English on a Steam-less run) |
| `ES2Access/Core/Speech/ModStrings.cs:609` (doc comment) | — | comment only |
| `ES2Access/Screens/MenuDestinationScreens.cs:240-266` (doc comments naming Steam) | — | comments only, and accurate |

**Nothing in the mod breaks. Nothing needs a store branch.**

---

## 6. What the mod must actually do differently

Ordered by whether it is real work.

1. **`loadsave.cloud` wording (tiny, real).** `"Steam cloud saves"` is a mod-authored literal that
   is wrong on GOG. It only fires when the game draws no label beside the tick, which on this build
   it does (`%LoadSaveModalWindowCloudStatusTitle`), so the risk is a rare fallback saying the wrong
   store. Cheapest correct form: make the fallback store-neutral — *"cloud saves"* — since the
   game's own label already supplies the store name when there is one. Needs owner sign-off on the
   phrase and a matching edit in every `locale/*.json`.
2. **`README.md:19` says "Endless Space 2 installed (Steam)".** One word: the mod runs on any
   desktop install; only `GamePaths.props` differs. Change to *"(Steam or GOG)"* or drop the
   parenthetical. `GamePaths.props.template`'s Steam default path is fine — it is a hint, and the
   file is per-machine and gitignored.
3. **Nothing for DLC.** §4 — the mod reads drawn visibility, not ownership. The planned
   `AddAccessibility` unhide is store-independent; the only real gotcha is `Shared`-vs-ownership
   flags (§4.3), which is not a store issue at all.
4. **Nothing for multiplayer.** The Join-Game entry's refusal is already the modelled, tested
   behavior; on GOG a player simply hears it always.
5. **Nothing for cloud saves, achievements, workshop, invites, chat, localization.** §3.
6. **Do not add a store-detection helper.** There is nothing to detect: the managed code exposes no
   store identity, and every consumer the mod cares about is already expressed as
   drawn/enabled/refused state, which is the right thing to read regardless.

### A free, high-value test the owner can run today

Because `EnableOfflineModeWhenSteamClientIsDown` is hardcoded true, **the Steam build already
reproduces the store-less profile: quit the Steam client entirely and launch
`EndlessSpace2.exe`.** That gives `IsSteamRunning == false`, null `SteamApps`/`SteamFriends`/
`SteamUser`, zero owned DLC, English localization, and a disabled Join Game — i.e. everything a GOG
install would show the mod except GOG's own ownership answer. That is the cheapest possible
verification of the paragraphs above, and it needs no second store purchase. (One risk to expect:
Steam being *closed* is different from `steam_api64.dll` being *absent*; the latter is the only
hard-fail path, §2.1, and it cannot be reproduced this way.)

---

## 7. What the generic docs lacked or got wrong

Candidates only; applying the generic-docs bar is main-agent work. I expect (1) to be the only one
that clears it.

1. **Candidate, weak-to-moderate.** No generic chapter mentions *store/platform coupling* as a
   thing to map. The reusable insight is not "look for a store abstraction" (there wasn't one) but
   the inversion that made this audit cheap: **a store's mod-visible surface is just a set of
   services that fail to initialize, so the portable question is "what does the mod do when the
   platform service is absent?" — and the answer is free if the mod reads drawn/enabled/refused
   state instead of computing entitlement.** That is arguably already implied by
   `making-screens-accessible.md`'s read-what-the-game-drew discipline; the only genuinely new bit
   is that **the game often ships its own store-absent fallback (offline mode) that doubles as a
   test fixture** — one line, plausibly in `dev-server.md` or the reverse-engineering chokepoint
   list, and only if a second game pays for it. Not paid for yet: this stage shipped no defect.
2. **Rejected — already covered.** `reverse-engineering.md:104-113` already says to record the
   game's own call site rather than a derived expression; §4.3's `Shared` lifecycle is exactly that
   rule paying off, not a gap.
3. **Rejected — ES2-specific.** The DLC ownership mechanism, the `"sbs"` bitfield, the
   language-from-Steam fact, and the offline-mode default all belong in `docs/es2-facts.md`. The
   DLC-window row shapes (§4.4) belong in the DLC stage's own notes.
4. **Observation, not a doc change.** `reverse-engineering.md:66-80` tells you to survey a whole
   subclass family before trusting a prototype. Applied here (all 24 `DownloadableContentN`, zero
   `IsSubscribed` overrides) it produced the single most load-bearing fact in this report. The rule
   worked; that is evidence for keeping it, not for adding to it.

## 8. Token audit

- ~30 tool calls. Filename/content sweeps over both decompiled assemblies (batched, several
  patterns per call); targeted `sed` reads of `SteamManager`, `Application`, `DownloadableContent`,
  `DownloadableContentManager`, `DLCItem`, `DLCModalWindow`, `RuntimeState_Lobby`,
  `LocalizationManager`, `LobbyDataController`, `SteamUGCManager`, `PlayerRepository`; mod-source
  greps + reads; two `Public/` data checks; two localization greps.
- Two WebSearch calls (~2k tokens) to settle whether a GOG build exists at all — it does (GOG sells
  ES2 Definitive Edition and separate expansion products, Windows only). Cheap and it changed the
  framing from "hypothetical" to "real but uninspectable".
- One near-miss: an early `AddAccessibility|Accessibility |=` grep matched hundreds of
  `DownloadableContentN.cs` restriction lines and cost ~4k tokens. Re-running it excluding
  `DownloadableContent[0-9]*.cs` gave the four real writers immediately. Lesson for a brief:
  when a family of generated data classes shares a vocabulary with the machinery that consumes it,
  exclude the data files in the first grep, not the second.
- Read no full-frame screenshots, ran no dev-server call, edited nothing outside the scratchpad.
