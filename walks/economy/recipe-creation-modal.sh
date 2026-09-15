#!/bin/sh
# The recipe-creation modal the economy page's recipes panel opens, bound to an empty slot.
# NEVER presses Confirm.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_modal screen.recipe-creation RecipeCreationModalWindow \
  '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow<RecipeCreationModalWindow>(); w.GuiRecipeSlot = new GuiRecipeSlot(0, false); Gui.GuiService.ShowWindow(w); return "recipe shown="+w.Shown; }))()'
capture modal "recipe creation modal"
hidewin RecipeCreationModalWindow
done_
