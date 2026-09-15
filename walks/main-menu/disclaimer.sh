#!/bin/sh
# The boot disclaimer, if this session can still raise it.
set -u
. "$(dirname "$0")/../lib.sh" "$@"; . "$(dirname "$0")/menulib.sh"

ensure_menu
openwin DisclaimerModalWindow screen.disclaimer
if isfocused screen.disclaimer; then capture modal "disclaimer"; else skip "the disclaimer did not open"; fi
backhome "the disclaimer"
done_
