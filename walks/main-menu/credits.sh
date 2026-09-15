#!/bin/sh
# The credits, which REPLACE the menu on the way in.
set -u
. "$(dirname "$0")/../lib.sh" "$@"; . "$(dirname "$0")/menulib.sh"

ensure_menu
openwin CreditScreen screen.credits
if isonscreen screen.credits; then capture page "credits"; else skip "the credits did not open"; fi
backhome "the credits"
done_
