#!/bin/sh
# The hero complete list.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_modal screen.hero-complete-list HeroCompleteListModalWindow
capture modal "hero complete list"
exitwin HeroCompleteListModalWindow
done_
