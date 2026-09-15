#!/bin/sh
# The DLC browser, which REPLACES the menu on the way in.
set -u
. "$(dirname "$0")/../lib.sh" "$@"; . "$(dirname "$0")/menulib.sh"

ensure_menu
openwin DLCModalWindow screen.dlc
if isfocused screen.dlc; then capture browser "DLC browser"; else skip "the DLC browser did not open"; fi
backhome "the DLC browser"
done_
