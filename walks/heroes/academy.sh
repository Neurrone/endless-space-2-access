#!/bin/sh
# The academy page. NEVER presses Confirm or a card's Content button.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_screen screen.academy AcademyScreen
capture page "academy page"
done_
