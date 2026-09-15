#!/bin/sh
# The journal modal -- the end-game summary the score and victory screens open.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_modal screen.journal JournalModalWindow
capture modal "journal modal"
hidewin JournalModalWindow
done_
