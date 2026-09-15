#!/bin/sh
# The pause menu. NEVER presses Load, Save, Exit Game.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_modal screen.game-menu GameMenuModalWindow
capture menu "game menu (pause)"
done_
