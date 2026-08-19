# The targeting-cancel fleet swap — known issue, deliberately unfixed

**Status (owner ruling 2026-08-20): leave at parity.** The mod does nothing about this
today, on purpose. This file exists so a future session can fix it — most likely by
option 2 below — if it ever becomes a real problem for players. Everything here was
measured live on 2026-08-20 (fixture `[Midgame] quests fleets`, turn 3).

## Symptom

At a system holding more than one of the player's fleets, cancelling ANY armed
targeting mode (probe launch measured; the shape is shared) re-opens the fleet panel
for the **first fleet at the docking slot**, not the fleet that armed the mode. The
player hears it happen — "Target selection ended", then "Fleet panel open for
1st Heroes Navy" when Patriots was the actor — so nothing is silent, but it is
surprising: the player who cancels is almost always still thinking about the fleet
they were commanding.

## Mechanism (the game's, hop by hop)

1. Escape → `GuiManager.HandleInput` Exit branch →
   `((ProbeLaunchingCursor)CurrentCursor).SwitchToGalaxyCursor()`
   (`GuiManager.cs:2103-2109`). Mouse right-click reaches the SAME method
   (`ProbeLaunchingCursor.OnCursorClick`, `ProbeLaunchingCursor.cs:129,177-183`) —
   keyboard and mouse are byte-identical here. The mod leaves this Escape to the game
   deliberately (`ES2Access/UI/CursorTargeting.cs` `EscapeIsOurs`).
2. `SwitchToGalaxyCursor` selects the **docking slot**, not the origin fleet:
   `service2.Select(dockingSlotWithFleet)` (`ProbeLaunchingCursor.cs:55-70`).
3. Arming had hidden the fleet panel, and `FleetsScreen.OnBeginHide` runs
   `UnselectAllGarrisons()` (`FleetsScreen.cs:925-943`), so the slot's selection is
   empty when the cancel re-selects it.
4. `FleetsScreen.Cursor_SelectionChanged` → `RefreshGarrisonSelection`
   (`FleetsScreen.cs:1364-1382` → `:1116-1129`): with nothing selected it defaults
   **positionally** — `Garrisons[0]`, or `Garrisons[1]` if `[0]` is a Hangar. At
   Dusay the list is `[Hangar 678, Heroes 1296, Patriots 1298]`, so Heroes wins
   whichever fleet armed the mode.

## What it is NOT (measured, do not re-derive)

- **Not an idle-fleet preference.** `EndTurnWindow.SelectIdleFleet` (what the mod's
  Enter on a docked fleet calls, `GalaxyHudScreen.cs` `SelectFleet`) takes an explicit
  fleet and contains no preference at all (`EndTurnWindow.cs:1387-1412`). The actual
  prefer-idle logic (`GetNextIdleFleet`, `:1373-1385`) is called ONLY by the game's
  own Next-idle-fleet button.
- **Not caused by the fleet having acted.** The control run — arm, cancel, nothing
  launched, full movement — swaps identically. No spent-state flag is involved.
- **Enter on a fleet's map row is CORRECT in every measured state** (four runs,
  verified by GUID against the game's selection list). A 2026-08-19 report that Enter
  "selected the other fleet" was this cancel-swap still standing, read before the
  Enter was pressed.
- **The mouse is currently WORSE, not better**: a docked fleet has no `FleetLabel`
  (zero instantiated — only the shared dock label exists), and `DockLabel.OnClick`
  accumulates four duplicate `DockLabelsWindow.OnDockLabelClicked` subscribers
  (game bug, `DockLabelsWindow.cs:103` re-subscribes per pooled `ShowLabel`), so one
  click advances the garrison cycle four times — a no-op at a two-fleet system. The
  keyboard's Enter is the only reliable per-fleet selection at a dock.

## Fix options, when the day comes

1. **Mod-side re-select on the mod's own cancel path**: after "Target selection
   ended", re-select the actor via `FleetsScreen.SelectIdleFleet(fleet)` or
   `SelectGarrisonRadioMode(fleet)` (both public, `FleetsScreen.cs:672,:742`), using
   `ProbeLaunchingCursor.ProbeOriginFleet` (public getter). Keyboard-only divergence
   from the mouse.
2. **PREFERRED (owner-indicated): Harmony postfix on
   `ProbeLaunchingCursor.SwitchToGalaxyCursor`** re-selecting the origin fleet —
   fixes keyboard AND mouse in one place, i.e. repairs the game rather than diverging
   from it. Cautions for the implementing stage: it changes behavior sighted players
   currently see; check the OTHER targeting cursors' cancel paths for the same shape
   (only the probe cursor was measured — each cursor's `SwitchToGalaxyCursor` /
   cancel needs its own look before generalizing); Harmony ids are unique-per-load as
   always; verify with the GUID probe of `FleetsScreen.SelectedGarrisons`, never the
   spoken panel name alone.
3. **Announce the mismatch** instead of fixing it ("panel opened for a different
   fleet"). Weakest option: the owner ruled the current announcement sufficient.

## Repro

`[Midgame] quests fleets`: `1st Patriots Navy` (GUID 1298, 2 probes) and
`1st Heroes Navy` (GUID 1296) both orbit Dusay (node 535). Select Patriots, arm
Launch Probes, press Escape (or Backslash through the mod) — the panel reopens for
Heroes. GUID oracle: `FleetsScreen.SelectedGarrisons`. Full measurement transcripts
are in the 2026-08-20 fleet-selection measurement stage report (session "various
smaller features").
