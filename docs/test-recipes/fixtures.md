# Test fixtures — the saves, and how to leave one as you found it

Which save shows what, the tools that undo a probe, the tutorial popup every session starts by
minimizing, the two resets that put a wedged session back, and the proofs that need no game at
all. Per-family blocked lists live in the sibling file for that family; this file holds only what
is true of the saves and of getting back to a known state.

## The three saves

**All three fixture saves are missing from disk** (measured 2026-08-26: `DevProbe.Saves()`
reported only the owner's personal saves and autosaves — no `[Beginner] test`, no
`[Midgame] quests fleets`, no `unlocked`). Every recipe naming one of them is
fixture-blocked until the owner rebuilds it. Recipes are written against these fixture
saves only, never against a personal save — a personal save is specific to one machine
and one playthrough, so nothing durable may depend on its contents.

The owner keeps three, and `DevProbe.Saves()` reports their titles: **`[Beginner] test`** (the
working fixture, turn 21 — Dusay a colony, Heka an outpost, two free-movement fleets on
Dusay→Heka legs), **`[Midgame] quests fleets`** (turn 3 — the one with a quest pinned: fleets in
orbit, multi-page tutorials, two idle fleets in one berth), and the **`unlocked`** save described
below. Never create or advance one — a stage blocked on game state reports the block rather than
making a fixture.

**The "unlocked" save** — every screen unlocked, the TECHNOLOGIES not (turn 1; the per-screen
gate table was measured live and is not preserved in the repo). Recipes that say "this save"
without naming one mean that save, and it is why so many screens read structurally right and
content-poor.

## Fixture tools that keep a save as you found it

**Make a whole empire's fleets genuinely visible** with `player.VisionSharingBits |= other.Bits`
then `IVisibilityService.ForceRefresh(-1L, true)` — sharing propagates only on a layer CHANGE, so
without the forced refresh nothing happens; **`IEndTurnService.TryToEndTurn()` answers false the
first time** (validators speak their warnings) — call it twice; **after `POST /loadsave`, re-run
the REPL setup** — a `var` bank silently keeps the DEAD game's objects (cost one false "the mod is
broken"); and a **save round trip without touching the fixture** is
`IGameSerializationService.SaveGame(...)` to a scratch title, reload, delete the file.

## Minimizing the tutorial popup

This is the step every galaxy session starts with, and it is the ONLY telling of it: every other
recipe that says "minimize the tutorial" means this one. `POST /input` does not
work on `screen.tutorial` — `ui.down`/`ui.right` answer `unconsumed` and `ui.end`/`ui.next` answer
`consumed` while moving no cursor and speaking nothing (measured twice, 2026-08-16). Invoke the
game's own handler instead: `TutorialPopupPanel.OnMinimizeCb`, private, no arguments, by reflection
from `/eval`. Take the panel from `FindObjectsOfType<TutorialPopupPanel>()` and pick the one that is
`IsBound && Shown` — `FindObjectOfType` (singular) handed back an unbound instance on one launch and
`OnMinimizeCb` threw an NRE inside itself (its `tutorial` field is null there). Guard on
`MinimizeToggle != null && !MinimizeToggle.State` so a re-run is idempotent.
**A MULTI-page tutorial popup is fixture-blocked in `[Beginner] test`** (its only in-progress
tutorial has one page); `[Midgame] quests fleets` has the 6-page `Tutorial_Fleets` in progress —
selecting a fleet in the galaxy tree raises it. Page counts per tutorial:
`Public/Gui/GuiElements[Tutorials].xml`; the in-progress set is
`DepartmentOfInternalAffairs.QuestJournal[QuestState.InProgress]` filtered to
`TutorialDefinition`.

**Entering a system re-opens the tutorial.** The first time the camera reaches a view level,
the game pops that level's tutorial page — so an Enter-on-a-colony test leaves the popup
un-minimized. Put it back (`TutorialPopupPanel.MinimizeToggle`, then send its `OnSwitchMethod`)
before calling the run done. The same route reaches a popup nested inside a window:
`Gui.GuiService.GetWindow<TutorialWindow>().GetComponentInChildren<TutorialPopupPanel>(true)
.MinimizeToggle` through `AgeWidgets.Toggle`. A popup that arrives EXPANDED and takes the
keyboard is collapsed and re-expanded without walking to it by replaying its own arrow —
`MinimizeToggle.State = true/false` then `SendMessage(OnSwitchMethod)`.

## Resetting game state

**Draining the modal stack from `/eval`** — the reset every multi-screen sweep needs.
`GuiManager`'s `ModalOnTop` can name a window whose `Shown` is FALSE: hiding a modal does not pop
it, and a screen gated on "am I the top modal" (the minor-diplomacy screen is) then never
activates, showing as `screen: none` with the window plainly up. The drain that works: while
`ModalOnTop != null`, re-`Show` it if `!Shown` and then `HandleInput(InputAction.Exit)`; six passes
is more than any real stack needs. Follow it with `HideWindow` on the full screens (senate,
economy, empire, military, technology) and
`Gui.GuiGameWindowService.RequestGalaxyOverviewViewLevel(Gui.PlayerEmpire.GetAgency<DepartmentOfTheInterior>().ColonizedStarSystems[0].Node)`
to land back on the galaxy.

**Closing a full screen when `/key` is refused.** The senate, the population modal and the star
system page do not claim Escape (`DevProbe.Claims("Escape")` → `claims:false`), so
`POST /input ui.back` is a no-op and only a real key would close them. With the game not in the
foreground (`POST /key` answers "the game does not have the foreground"), use
`Gui.GuiService.HideWindow(...)` — never `HandleInput(InputAction.Exit)`, which wedged the screen
stack once. The game menu closes through its own **Resume Game** node. EXCEPTION (measured
2026-08-25): `HideWindow` does NOT close `StarSystemScreen` (it stays `Shown=True`) — leave the
management page with `Gui.GuiGameWindowService.RequestGalaxyOverviewViewLevel(pos)` instead.

## Sighting a surface the fixture never draws

Tier zero: read the prefab's fields off the
UNSHOWN window (`GetWindow<T>(false)`, nothing to restore) — beware prefab `%key` content the game
rewrites at bind (read the bind code first). Then, cheapest first: `Show()` the game's pooled
widget, read, `Hide()` (its next visibility pass restores truth); or set the game's OWN `Visible`
flags/private fields from `/eval`, dump, restore, and re-diff against the untouched dump; or
`Bind` + `Show` a window with data, read, `Unbind` + hide — a forced show proves STRUCTURE, never
content, a half-bind can outlive the probe (restore monotonic setters through their backing
field; `POST /loadsave` if a window wedges; never force-show a DLC modal without its data). Where
the widget is generic over an INTERFACE, LEND it another implementor's data (`Bind(otherOwner,
client)` + `RefreshNow()`) and the game draws real content into the unreachable panel — only lent
data proves content; never commit an action while the binding is lent. When a forced show fights
a per-frame gate, read the authored data (the curve table), not the animated runtime value.

## Offline proofs

**The icon table's coverage proof needs no game.** Run every `<LocalizationPair>` value in
`<game>\Public\Localization\english\*.xml` through `ES2Access.UI.AgeText.Clean`, then
`DevProbe.UnknownIcons()` — `tokens` must be empty, and the expected token counts are the icon
numbers in `gui.md`, "Icons and picture captions".

## Reading a usage hint

A hint reads in `/gui/graph?buffers=1` without focusing anything (`NodeBuffer` feeds both the live
buffer and the dump), so every hint check is one dump plus whatever it takes to get the context on
screen. `ES2Access.UI.Input.ChordNames.Of(ES2Access.ModEntry.Input, "<action>", <index>)` from
`/eval` is the chord half on its own, and the same call on a hand-built `KeyboardBinding` is the
rebind proof without touching the shipped bindings. Which context draws which hint is in that
family's own recipe file.

## Fixture-blocked

- The per-family lists are in the sibling files: `test-recipes/galaxy-map.md`,
  `test-recipes/fleets.md`, `test-recipes/scanner.md`, `test-recipes/systems-and-planets.md`,
  `test-recipes/empire-screens.md`, `test-recipes/modals-and-outgame.md`,
  `test-recipes/notifications.md`, `test-recipes/inspect-and-influence.md`,
  and `test-recipes/mod-settings.md`, each under its own
  `## Fixture-blocked` heading.
- No save shows an UNLOCKED End Turn, so the turn cluster's operable state stays
  code-verified.
- Tutorial progress does NOT live in the save: anything that advances a tutorial page is a
  fixture change that only a re-minimize (or a fresh `POST /loadsave`) puts back.
