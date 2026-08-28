# The mod's own settings window

The clone of the game's options window: getting there, the key-binding table, the Scanner tab
and the physical key paths.

## Screen model: a menu entry over a cloned options window

**THE MOD'S OWN SETTINGS ARE A MENU ENTRY, NOT A HOTKEY** (owner ruling 2026-08-23;
`ES2Access/UI/ModOptions/ModSettingsNode.cs`). One synthetic `GraphNodes.Button` labelled
"Mod settings" (`mod-settings.entry`), declared immediately AFTER the game's own Options entry
on the main menu (`mainmenu:mod-settings`, after the `MainMenuSettings` group closes — Options
is an entry with a flyout, so the node has to be a sibling of the group and not one of its
children) and on the pause menu (`gamemenu:mod-settings`, after the entry whose button is wired
to `OnOptionsCb`). Nothing is drawn for it, so a sighted player never meets it — the shape the
graph already uses for things the game does not draw. **No key binding, and no gating**: both
menus are static and are exactly where the game opens its own Options, and Apply, Cancel and
Escape all pop back to the menu by the game's own hand. Enter shows the mod's cloned options
window with the skin matching the menu (`OutGameSkin = !Gui.IsInGame`). Ctrl+M was weighed and
set aside; it remains verified free should a hotkey ever be wanted.

**The window is the game's options window, and reads as one** — same tab bar, same rows, same
button bar, same Escape/Apply/Cancel, so `OptionsScreen` serves it with one change (`Window()`
answers whichever instance is shown). Its screen name is "Mod settings" (`screen.mod-settings`),
not the game's "Options", because the two are the same window class and a player arriving must not
be told they are in the game's settings. Categories are a data-driven list (`ModOptions.Categories`)
drawn in list order.

**THE WINDOW HAS EXACTLY TWO TABS — "Scanner" and "Controls" — EVERYWHERE**, main menu included
(owner ruling 2026-08-24; `ES2Access/UI/ModOptions/`). The Scanner tab was in-game only for as
long as its columns were a snapshot of the galaxy being played; they come from the game's
DATABASES now, so neither page needs a game and the window is never rebuilt for crossing that
line. The key-binding category is CALLED "Controls" — the game's own key for its own key-binding
page — which is what makes the game's own window logic do three right things for it: the tab draws
itself with `%OptionToggleControlsTitle` (the game's words, every language), leaving with unapplied
changes asks the binding question rather than the generic one, and the **Reset to Defaults** button
appears on the button bar for that tab and no other. The binding rows' own key semantics are
`docs/interaction.md`, **The mod's Controls tab**.

## Getting there

**Getting there is the player's own route, and only that route counts.** In game: open the pause
menu (`Gui.GuiService.ShowWindow(Gui.GuiService.GetWindow<GameMenuModalWindow>())` from `/eval`
is the same thing Escape does), then `/input ui.down` five times from Save Game and
`/input ui.activate` — "Mod settings, button, 6 of 9", then "Mod settings" + "Scanner, tab,
selected, …, 1 of 2". On the MAIN MENU the entry sits after Options as 8 of 9; the whole main-menu route
is untested live from an in-game fixture, and **is no longer checkable from inside a running game**:
`GET /gui/graph?screen=screen.main-menu` now answers `screen inactive: it declared no controls`, and
`&ungated=1` answers the same (measured 2026-08-28) — the main-menu window is not drawn from in
game, so there is nothing for the by-key build to declare. That check is what once caught the node
being declared in the wrong branch, since the main menu's Options entry is a GROUP with a flyout
while the pause menu's is a flat button; redo it from an OUT-GAME session, where the screen is
active.

**From there, one route serves every tab and both buttons bars.** The window opens on the Scanner
tab; `ui.down` on the tabs stop reaches Controls. `ui.next` from the tabs stop reaches
`options:rows`, `ui.next` again `options:buttons` (`ui.home` = Cancel, `ui.end` = Apply; on the
Controls tab a third button, Reset to Defaults, sits between them at
`options:button/ResetButton/OnModResetCb`). Pause menu → Options is 5 of 9, Mod settings 6 of 9.
`/input ui.back` does NOT close THIS window: the options screen leaves Escape to the game and an
injected action presses no key — use the window's own Cancel button. (That is this window's answer,
not a general rule: the seven modals listed in `interaction.md` DO consume Back and press their own
close control, so `ui.back` closes those.) Cancel with changes raises the game's
own confirmation (`ui.end` then `ui.activate` confirms) and lands back on the pause menu.

## Reading it

**Reading it.** `GET /gui/graph` on the mod window gives three stops exactly as the game's options
window does: `options:tabs` (two tabs, "Scanner" and "Controls"), `options:rows`, and
`options:buttons` (Cancel, Apply — Apply "unavailable, No modification detected." until something
changes, which is also the proof that the option getter's instance is stable; on the Controls tab a
third button, "Reset to Defaults", sits between them). The Controls tab's row ids are
`options:1TabPanel/keys/row<hash>c{0,1,2}` (a three-column sheet), the panel's own children
`<index><action key>KeyMapping`.

## Rebinding without a keyboard

**Rebinding without a keyboard.** The physical capture is the game's own and cannot be driven from
`/eval`; write the value instead:
`item.Option.Value = new Amplitude.Unity.Input.InputBinding("<action>",
Amplitude.Unity.Input.KeyCombination.FromString("Ctrl+K", "+"), KeyCombination.None);
w.OnOptionChanged(item.Option);` where `w` is `ES2Access.UI.ModOptions.ModOptions.Window()` and
`item` is the `OptionKeyMappingItem` on the row whose transform name matches. Then all four
follow in the same frame: the drawn field (`item.PrimaryKeyBindingField.Label.Text`),
`ChordNames.Of(ModEntry.Input, "<action>", 0)`, `NodeHints.Chord("<action>", 0)` — the delegate
every usage hint renders through — and `ApplyButton.AgeTransform.Enable`.

## Apply, Cancel and the file

**Apply, Cancel and the file.** Apply and Cancel are pressed through the mod's own activate path
(`ui.next` twice to the button stop, `ui.home`/`ui.end`, then `ui.activate`). Apply hides the
window and lands the cursor back on the "Mod settings" entry it was opened from; Cancel with
changes raises the game's own "Are you sure you want to quit without saving?" box (a mod screen —
`ui.end` then `ui.activate` for Confirm) and restores every row. The file is
`<game>\BepInEx\plugins\ES2Access\settings.cfg`: after Apply it holds
`keys.<action> = <action>:Ctrl+K,` for exactly the actions that moved, and moving one back to its
default takes its line out again (the file goes to 0 bytes when nothing is moved).

**Mouse clicks on mod-drawn rows.** The Scanner tab's category headers and Clear buttons are
Cancel-button clones re-aimed at a per-row `ModRowClick` receiver, so a mouse click and `ui.activate`
converge on the same `ModRows.Activate` (a mouse group-toggle is silent by design; a mouse Clear
speaks because the line belongs to `ScannerEditor.Clear` itself). Wiring probe from `/eval`: the
row's `AgeControlButton.OnActivateObject` is the row, `OnActivateMethod` is `OnModRowClicked`, and
`SendMessage`-ing that pair flips `ScannerRows.Expanded(n)` (verified 2026-08-28); the physical
mouse-pick itself is manual-only.

## Reload-restore

**Reload-restore.** `POST /reload` destroys the clone (teardown by name) and rebuilds it on the
next frame; a shown window closes with it and the pause menu is focused again cleanly. The rebind
survives, because it is read from the file at `ModEntry.Start`:
`ChordNames.Of(ModEntry.Input, "ui.goToLocation", 0)` still answers the new chord and
`ModBindings.Moved("ui.goToLocation")` is true.

## Key bindings, the table

**Reading it.** Both windows read the same way. `/input ui.next` into `options:rows` lands on the
first row's name cell — on the GAME's tab "Controls, table, Confirm, Enter, ⟨description⟩", on the
MOD's "Controls, table, Move up, Up Arrow, Move the cursor to the control above., 1 of 59" (measured
2026-08-28) — and then
`ui.right` / `ui.left` cross the columns ("Primary key, Enter, button" / "Secondary key, empty,
button" / back onto "Action, Confirm, …"), `ui.down` stays in the column and names the row it landed
in ("Cancel, empty, button, 2 of ⟨n⟩"). Cell ids are `options:⟨panel⟩/keys/row⟨hash⟩c⟨0|1|2⟩`.
**The MOD table's row count is 59** — one per `action.*.title` key in the locale, and the same figure the
physical-key measurements below read back ("48 of 59"). The figure read 57 until 2026-08-28, when the
idle-fleet chord was added and the line was found two actions stale; re-check it with
`grep -c '"action\..*\.title"' ES2Access/locale/english.json` whenever an action is added.

**Driving a clear.** `/input ui.clear` on a key cell — the cell announces its new "empty" as a live
part, Apply lights, and the game's own value follows
(`(Amplitude.Unity.Framework.Services.GetService<IInputOptionsService>()).InputBindingsValidate
.ToString()` → `Validate: , `). Cancel on the window puts it back. The claim is checkable with
`DevProbe.Claims("Delete")`: `claims:true` on a key cell, false on the name cell and off the screen.

**Driving a COMMIT (which `Option.Value` alone does not do).** Writing the option's value skips the
game's commit method, so it raises no conflict box and no overlap warning. Drive the real thing:
set `item.PrimaryKeyBindingField.KeyCombination = KeyCombination.FromString("Ctrl+H", "+")` and then
invoke the private `OnLoseFocusCb` with `item.PrimaryKeyBindingField.gameObject`. That is the path a
finished capture takes, conflict check included.

**The overlap warning, both ways.** Game side: on the Controls tab, commit `InputBindingsQuickSave`
onto `Ctrl+H` (the mod's `ui.focusEmpire`) — the box reads "While the mod's Go to the empire banners
is active, the game's Quick Save will not fire" and the binding still lands (`QuickSave: Ctrl + H`).
Mod side: on the mod's own Controls tab, commit `ui.goToLocation` onto `F1` — "While the mod's Show
on the map is active, the game's Empire Screen will not fire", and `ui.goToLocation: F1` sticks. TWO
buttons: Confirm keeps it, Cancel puts the row back on what it held (measured: binding
`ui.up` onto `KeypadEnter` warned about End Turn, and Cancel restored `ui.up: UpArrow` with Apply
going back to unavailable). Either answer reads the cell out and leaves the settings window SHOWN
with the cursor on the key cell — the reported "window vanishes after Confirm" did not reproduce
through `/input`.

**Simulating a capture without a keyboard.** `/input ui.activate` on a key cell speaks the prompt and
DOES hand over: an injected action holds no key, so the two clear frames pass at once and
`AgeManager.Instance.FocusedControl` becomes that `AgeControlKeyBindingField`. To capture a CHORD,
write it into the field first — `var kc = new Amplitude.Unity.Input.KeyCombination();
kc.KeyCodes.Add(UnityEngine.KeyCode.KeypadEnter); f.KeyCombination = kc;` — then
`AgeManager.Instance.FocusedControl = null`, which is the FocusLoss the released key would have
caused. (Build the combination that way: a `List<KeyCode>` local poisons the REPL session.)
Capturing the chord the row is ALREADY on is the equal-guard case: nothing commits, Apply stays
unavailable, and the cell is still read out ("Up Arrow" twice — once as the field builds it, once as
the mod confirms what stuck). **An ESCAPE ending is that same simulation with `KeyCombination.None`**
— the blank the field is holding when the engine takes its focus away — and it needs a row with BOTH
slots filled to show anything: on a row whose secondary is empty the game's own equality check reads
the blank as no change and the cell re-reads its old chord. Measured 2026-08-24 on both windows (mod
row `ui.up` given a secondary first, game row "Navigate Forward (Battle)", Up Arrow + W): the cell
re-reads "empty", Apply lights, and the window's Cancel — `ui.next` to the buttons, `ui.home`,
`ui.activate`, then `ui.end` + `ui.activate` at the "quit without saving your control bindings" box —
puts the chord back. The PHYSICAL Escape stays a manual item: `POST /key` refuses while the game is
not foregrounded.

## The Scanner tab

**Screen model: each of the three custom-category slots is one drawn control that OPENS AND SHUTS
IN PLACE.** The tab is one page: three headers reading "Custom category {n}: {name}" ("empty" when
the slot is unset), each an expandable GROUP whose rows are drawn under it. Right opens it and steps
into it, Left shuts it, Enter flips it where you stand and says "expanded"/"collapsed"; all three
slots start collapsed every time the window opens. Opening is show-and-arrange, never a rebuild —
the rows exist from the moment the page is built — which is what lets the tree's open-and-step-in
find the children the same frame.

**A slot's block is flat**, and every row in it is a row the game draws:
- **"Name"** — a text box holding the category's name. Typing a name into an EMPTY slot is what
  fills it, and the rest of the page appears; until then the tab holds this box alone. Blank is
  refused ("A custom category needs a name"), and a name already taken — by a built-in category's
  live localized label or by another slot, case-insensitively — is refused with "{name} is already
  the name of a category". Both put back what was there, drawn and spoken.
- **"Keyword {n}"** — one box per keyword. Editing one changes that keyword IN PLACE (its position
  is its column order); blanking one takes it out, speaking "{kw} removed".
- **"Add keyword"** — an empty box after them. What is typed there is added and the box blanks
  itself; a word already in the category is refused with "That keyword is already in this custom
  category".
- **"Clear this custom category"** — a drawn button, no confirmation (Cancel is the undo), speaking
  "Custom category {n} cleared" and leaving the page as the name box alone.
- Then **one SECTION per built-in scanner category**, in scanner order. Its caption is drawn as a
  row and spoken as the section's NAME — "{category}, {n} selected" — never as a control of its
  own, so **Ctrl+left/right walks the thirteen sections** and the rows above the first caption are
  a section too. Under each caption is one checkbox per column: the columns that category writes
  down, then — for the four derived ones — EVERY kind the game's own DATABASES define (owner ruling
  2026-08-24), keyed by the definition's own name, labelled by its localized title and sorted by
  that label; then any stored selector no column answers for, ticked and read as "{key}, not found
  this game" so it can be taken off. The editor offering everything and the SCANNER reporting only
  what it found are two different questions — a category can ask for a luxury nobody has surveyed
  yet.
- **ONE CHECKBOX PER SPOKEN WORD** (owner ruling 2026-08-24). The game defines twins it draws
  identically — an anomaly and its Reduced form, a deposit and its system-wide twin — and the
  scanner's own found columns are keyed by that word, so two boxes could never have meant two
  things: only one of a pair could ever match anything. One box stands for both, keyed by the
  first of their internal names and answering for either, so a category written before the merge
  still ticks and still matches. (The per-category column counts are under **What to check** below.)

Every one of those rows is the game's own prefab over a per-row provider object, so the drawn page
and the spoken page cannot disagree. The text rows are edited by the mod's ordinary text editor
(Enter ends the edit and nothing else, Escape puts back what was there); the commit itself needs a
Harmony prefix, because the game's own text-field row assigns the label OBJECT into the option
(ES2 facts).

**Apply and Cancel are the window's own, with nothing re-implemented.** The edits live in a
`ScannerCustomSlots.Copy()`; the Scanner panel carries ONE invisible game option whose value is
"does the copy differ from what is saved", so the window's own machinery lights Apply, raises its
own `%OptionExitWithoutApplyMessage` on Escape or Cancel, and throws the copy away through that
option's setter when it restores. The copy is written through on the window's hide, which is the
same save-on-hide the keybind rows already have. **Speech order**: an edit that changes the SHAPE
of the page (a name that fills a slot, a keyword added or removed, a clear) rebuilds the page from
the pump rather than from inside the engine's own focus change, and the sentence that goes with it
is said there — so a refusal is heard after the control it left unchanged.

`ui.next` from the tabs stop into `options:rows` lands on the three collapsed slot headers.
`ui.right` opens one and steps into its Name box in one press; `ui.left` shuts it; `ui.activate`
flips it where you stand and answers "expanded"/"collapsed". All three start collapsed every time
the window is shown.

Node ids are all on the one panel: `options:0TabPanel/slot{0..2}Header`, and under a header
`slot{n}Name`, `slot{n}Keyword{i}`, `slot{n}NewKeyword`, `slot{n}Clear`,
`slot{n}Section{categoryKey}` (a caption - drawn, never a node) and
`slot{n}Select{categoryKey}:{columnKey}`. Regions are `options:0TabPanel/head` and
`.../slot{n}Section{categoryKey}`, so `ui.regionNext` walks the thirteen sections; the head region
is what makes the header and the name/keyword boxes a place Ctrl+arrow can leave.

**Driving a text row.** `POST /type` cannot reach a game-owned field and `POST /key` needs the game
foregrounded, so: `ui.activate` on the row, wait a frame for the hand-over
(`TextFieldEditor.Editing` goes true and `AgeManager.Instance.FocusedControl` is the `TextField`),
then from `/eval`

```
AgeControlTextField f = AgeManager.Instance.FocusedControl as AgeControlTextField;
f.Label.Text = "Watch list";
typeof(ES2Access.Screens.TextFieldEditor)
  .GetField("CommitTheNextRelease", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
  .SetValue(null, true);
AgeManager.Instance.FocusedControl = null;
```

`CommitTheNextRelease` is internal, so it needs reflection; without it the focus drop is a CANCEL
and the pre-edit text goes back. MEASURED speech for a name: `"Watch list", "edited",
"Custom category 1, Name, editable, Watch list"` and then the rebuilt page's landing. A refusal
follows the landing - `"Systems is already the name of a category"`, `"A custom category needs a
name"`, `"That keyword is already in this custom category"` - and the box goes back to what it
held. An `ui.activate` on a row while a text edit is still PENDING starts an edit on that row
instead of pressing it (the screen captures raw input while a hand-over is waiting); recover with
`TextFieldEditor.Abandon(); AgeManager.Instance.FocusedControl = null;`.

**What to check.** Apply lights only once something differs
(`ModOptions.Window().ApplyButton.AgeTransform.Enable`); Cancel with changes raises the game's own
"Are you sure you want to quit without saving?" (`screen.message-box` - `ui.end` then
`ui.activate` confirms) and leaves `ScannerCustomSettings.Slot(0)` and the file untouched; Apply
hides the window and writes `scanner.custom.1 = Watch list|systems:neutral|Dusay`, which survives
`POST /reload`. Clearing then applying takes the key out of `settings.cfg` altogether. The slot's
HEADER follows: "Custom category 1: Watch list" once named, "Custom category 2: empty" while a slot
stands empty.

**The stale-selector row** needs a selector the galaxy cannot answer. Write one behind the editor
(`ScannerCustomSettings.Slots.Slot(0).AddSelector(new ScannerSelector("luxury","NoSuchResource"));
ScannerCustomSettings.Save();`) and reopen: the Luxury section offers "NoSuchResource, not found
this game, checkbox, checked"; unticking it takes the selector out (the row stays until the page is
next built, which is what lets the player change their mind before Apply).

**The column counts are a fact about the BUILD, not about the save**:
Systems 7, Colonizable 2, Unexplored 1, Anomalies 82, Curiosities 15, Luxury 25, Strategic 7,
Contested 1, Fleets 4, Probes 4, and 1 each for pins, missiles and quest markers - the four derived
ones being the whole database plus their own "all", with definitions drawn with the SAME WORD merged
into one checkbox (27 anomaly, 24 luxury and 6 strategic pairs). Note the anomaly keys are the game's own and are not what
a guess would produce: Multiple Moons is `PlanetAnomaly27Alt`.

**The minimised tutorial must NOT be declared over the settings window.** With the military
tutorial minimised, `/gui/graph` on `screen.options` must hold no `hud:tutorial` stop, and
`(Gui.GuiService as GuiManager).IsAnyModalVisible` must read true with `ModalOnTop` naming
`ES2AccessModOptionsWindow`. Hide the window and the bar is back on the galaxy - that pair is the
regression test for the clone's modal registration (ES2 facts).

**Leave the fixture with all three slots cleared** (the Clear button then Apply, or
`ScannerCustomSettings.Clear(0..2)`) - `settings.cfg` goes back to 0 bytes.

## Reset to Defaults on the Controls tab

Only the mod's window has it, and only while the Controls tab is showing: the game's own
`OpenCategory` shows `ResetButton` for the category literally named "Controls", which is what the
mod's key-binding category is called. What the button DOES is repointed at the mod's own handler
(`ModOptionsWindow.OnModResetCb`); the game's own Controls tab is untouched.
Reach it by the route in **Getting there**; `ui.activate` on it
raises the game's own box
("Are you sure you want to reset your control bindings to their default values?"); Confirm puts
every mod action back on its compiled default at once (`ModBindings.Moved(key)` goes false), lights
Apply, and leaves the window SHOWN with the cursor on the Reset button.

Two ways out, and both were measured: Apply hides the window and DROPS every `keys.*` line from
`settings.cfg`; Cancel (answering the game's "quit without saving your control bindings?" question)
puts every rebind back. Restoring a wiped rebind afterwards is one eval:
`ModBindings.Set("<action>", new Amplitude.Unity.Input.InputBinding("<registry string>"));
ModBindings.Persist(); ModSettings.Save();`.

## The physical key paths, and what Escape means where

All of this needs `POST /key` and therefore the game FOREGROUNDED; `hold=250&gap=150`.

**The owner's vanish, and the fix.** From a mod Controls row's primary key cell: `Return` ("Press
the new key combination."), `F1` ("F1", then the overlap box - "Confirmation", "While the mod's … the
game's Empire Screen will not fire."), then `DownArrow DownArrow` to Confirm ("Cancel, button, 2 of
3", "Confirm, button, 3 of 3") and `Return`. What must happen: "F1", "Mod settings", "Controls,
table, F1, button, 48 of 59" - the window still SHOWN, the cursor on the cell, Apply lit. The
regression to watch for is an `Escape` pressed while that box is up: it must be the box's CANCEL
(the row reads its old chord back, the window stays shown, Apply unlit) and must NOT hide the
settings window. Before the registration fix it silently re-aimed the box at the window's
own "discard your changes" handler, and the next Confirm dropped the player on the pause menu with
the rebind reverted (ES2 facts). Check the clone's place in the dispatch list with an `/eval` walk of
`GuiManager.guiWindowsFromBackToFront`: `GameMenuModalWindow` 152, the game's `OptionsModalWindow`
153, the clone 154, `MessageBoxWindow` 166.

**The rest of the capture, physically.** `Return` then `Escape` on a key cell = the cell read back
with whatever the game left in it (empty on a row with both slots filled, the old chord on a row whose
other slot is empty — 2026-08-24: no cancel, no restore); `Return` then `Comma` = the chord twice (the field as it builds it,
then the mod confirming what stuck) with Apply lit; Apply (`ui.next` to the buttons, `ui.end`,
`Return`) hides the window, lands on "Mod settings, button, 6 of 9" and writes
`keys.galaxy.scanCustom1Next = galaxy.scanCustom1Next:F1,`; rebinding back to the default and
applying takes the line out again.

**Type-ahead and scrolling on both tabs.** `POST /type` reaches the mod's type-ahead on
either tab: on Controls, "next result in custom category " answers 3 results and lands on
"Move to next result in custom category 1, …, 48 of 59"; on the Scanner tab with a slot open,
"amianthoid" answers ONE result (the merged twin column, `slot0Selectluxury:Luxury15`). Every landing
must SCROLL: read `ModOptions.PanelOf(w, "Controls").OptionsTable.GetGlobalPosition().y` against
`…OptionsScrollView.Viewport` (y 170, height 468) before and after, and confirm with `crop-shot.ps1`
on the viewport that the landed row is drawn. `ui.end` down the KEY column is the case that used to
fail - the table stayed at y 170 - so test the cells, not only the name column. Clear with `ui.back`
("Search cleared").

## Custom-category sweeps from the map

**Walking a whole custom category (2026-08-24).** The sweep is only provable by walking it to the
END: `/input galaxy.scanCustom1Next` n times, ~0.45 s apart, from the MAP stop, then read the
`"N of M"` tail out of `/speech?since=` — a correct sweep counts 1, 2, 3 … M and wraps to 1, and the
defect it replaced counted 1…6 and dropped back to 2 forever. Three more presses finish it: Shift
(`…Prev`) steps back down the same list; a step INSIDE the system just landed on does NOT end the
sweep (same rounded pair); and a move to another system does, the next press answering "1 of M". A
reload leaves the cursor on `hud:empire`, where the quick keys read `unconsumed` — `ui.next` until
the node id starts `galaxy:` first.

**The quick keys' claim scope.** `DevProbe.Claims("Comma,Period,Slash")` on the galaxy page reads
`claims:true` only while `GalaxyHudScreen.CursorOnMap()` is true - measured false on `hud:quest`, on
`hud:notification/0` and on the settings window (2026-08-24). Reaching the map stop from the HUD is
`ui.prev` until `CursorOnMap()` answers true (three stops back from the control banner on this
fixture).

**A saved selector under the OTHER twin.** Set a slot behind the editor to a kind the
galaxy holds under its planet-side name but SAVE the system-side one -
`ScannerCustomSettings.Slots.Set(2, cat)` with `new ScannerSelector("luxury", "SystemLuxury6")` where
the galaxy shows Transvine (`Luxury6`) - press `galaxy.scanCustom3Next` from the map stop and it
lands on "Transvine on Osulo II, … 1 of 4"; `galaxy.scanSubcategoryNext` reads "Luxury Resources:
Transvine". Clear the slot afterwards (`Slots.Clear(2)`, `Save()`) and check `settings.cfg` is byte
for byte what it was.

## Fixture-blocked

**What the harness cannot reach here.** The physical capture (Enter on a row → prompt → chord →
release → the row speaks what stuck), Escape on the mod window (`POST /key` needs the game
foreground; the registration that makes it work is checkable instead — the clone sits at the FRONT
of `GuiManager.guiWindowsFromBackToFront`, ahead of `GameMenuModalWindow`, and is an
`IInputHandler`), and the whole main-menu route.

- The physical capture end to end, Escape on the mod window, and the whole main-menu route
  (**Getting there**, **The physical key paths**).
