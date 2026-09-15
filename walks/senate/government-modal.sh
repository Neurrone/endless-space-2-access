#!/bin/sh
# The government modal (binds itself on show).
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_modal screen.government GovernmentModalWindow
capture modal "government modal"
exitwin GovernmentModalWindow
done_
