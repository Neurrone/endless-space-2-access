#!/bin/sh
# Family: the in-game dialogs and option windows -- pause menu, the mod's own settings
# window (both tabs, reached the player's own way), the game options modal, load/save,
# rename, the journal, the non-blocking box, the recipe-creation modal.
# NEVER presses Load, Save, Delete, Confirm, Exit Game or a row's double click.
#
# Two windows REMEMBER their selected tab across opens:
#   * the mod's settings window -- the route selects its first tab explicitly before the
#     first dump, and leaves it there;
#   * the game's own options modal -- the route never touches its tabs, so whichever tab a
#     human last left it on is the one both walks dump. Leave it on Video.
set -u
FAMILY=dialogs; . "$(dirname "$0")/lib.sh" "$@"

prologue

# ---- pause menu, and the mod's settings window reached through it ---------------------
openwin GameMenuModalWindow 2000
capture 01-game-menu "game menu (pause)"
snap "$TMP/gm.txt"
MODSET=$(label_of "$TMP/gm.txt" 'gamemenu:mod-settings')
if [ -n "$MODSET" ] && tland "$MODSET"; then
  echo "   discovered: pause-menu entry [$MODSET]"
  inp ui.activate; pause 2500
  inp ui.home; inp ui.activate; pause 1000          # force the FIRST tab
  capture 02-mod-settings-tab-1 "mod settings, first tab"
  inp ui.down; inp ui.activate; pause 1200
  capture 03-mod-settings-tab-2 "mod settings, second tab"
  inp ui.up; inp ui.activate; pause 1000            # leave it on the first tab
  inp ui.next; inp ui.next; inp ui.home; inp ui.activate; pause 2000   # Cancel
  at "after-mod-settings-cancel"
else
  skip "the pause menu declares no mod-settings entry - both settings-tab dumps not captured"
fi
evq "$CS/drain.cs"; pause 1200

# ---- the game's own options modal -----------------------------------------------------
openwin OptionsModalWindow 2200
capture 04-game-options "game options modal"
exitwin OptionsModalWindow; hidewin OptionsModalWindow; evq "$CS/drain.cs"; pause 1200

# ---- load/save (LOAD from game; nothing is ever loaded or written) --------------------
evs '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow("LoadSaveModalWindow") as LoadSaveModalWindow; w.LoadSaveMode = LoadSaveModalWindow.LoadSaveType.LoadFromGame; Gui.GuiService.ShowWindow(w); return "loadsave shown="+w.Shown; }))()'
pause 2000
capture 05-load-save "load/save modal (Load from game)"
hidewin LoadSaveModalWindow; evq "$CS/drain.cs"; pause 1200

# ---- rename box -- seeded with a harness-authored constant, never a fixture name ------
evs '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow<RenameModalWindow>(); w.OriginalName = "walk probe"; Gui.GuiService.ShowWindow(w); return "rename shown="+w.Shown; }))()'
pause 1500
capture 06-rename "rename box"
exitwin RenameModalWindow; hidewin RenameModalWindow; evq "$CS/drain.cs"; pause 1200

# ---- the journal ----------------------------------------------------------------------
openwin JournalModalWindow 2200
capture 07-journal "journal modal"
hidewin JournalModalWindow; evq "$CS/drain.cs"; pause 1200

# ---- the recipe-creation modal (never press Confirm) ----------------------------------
evs '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow<RecipeCreationModalWindow>(); w.GuiRecipeSlot = new GuiRecipeSlot(0, false); Gui.GuiService.ShowWindow(w); return "recipe shown="+w.Shown; }))()'
pause 1800
capture 08-recipe-creation "recipe creation modal"
hidewin RecipeCreationModalWindow; evq "$CS/drain.cs"; pause 1200

# ---- the non-blocking box --------------------------------------------------------------
evs '((GuiManager)Gui.GuiService).ShowMessageNonBlocking("walk probe", MessageBoxType.INFORMATIVE, null)'
pause 1500
capture 09-non-blocking-box "non-blocking message box"
hidewin MessageBoxNonBlockingWindow; evq "$CS/drain.cs"; pause 1200

# leave no game-owned text field holding the keyboard
evs 'AgeManager.Instance.FocusedControl = null'
epilogue
