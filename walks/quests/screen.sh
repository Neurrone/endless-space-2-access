#!/bin/sh
# The quest journal.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_screen screen.quests NarrativeScreen
capture journal "quest journal"
done_
