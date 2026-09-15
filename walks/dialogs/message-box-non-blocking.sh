#!/bin/sh
# The non-blocking message box (the game raises it on disconnect), with a harness-authored
# message.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_modal screen.message-box-non-blocking MessageBoxNonBlockingWindow \
  '((GuiManager)Gui.GuiService).ShowMessageNonBlocking("walk probe", MessageBoxType.INFORMATIVE, null)'
capture box "non-blocking message box"
hidewin MessageBoxNonBlockingWindow
done_
