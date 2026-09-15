#!/bin/sh
# The battle-tactics deck. NEVER presses Confirm.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_modal screen.battle-tactics PlayCardDeckModalWindow
capture modal "battle tactics deck"
exitwin PlayCardDeckModalWindow
done_
