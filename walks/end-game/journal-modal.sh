#!/bin/sh
# The journal modal -- the end-game summary the score and victory screens open -- and the
# confirmation box behind a row's Delete entry button.
#
# The box is opened and CANCELLED, never confirmed: a row of the journal is a game the player
# finished and deleting one is not the walk's to do. Cancelling is also the half worth proving,
# since it is the way out a player takes when the box was opened by mistake.
set -u
. "$(dirname "$0")/../lib.sh" "$@"
. "$(dirname "$0")/endlib.sh"

ensure_modal screen.journal JournalModalWindow
capture modal "journal modal"

snap "$TMP/journal.txt"
ROWKEY=$(key_nth "$TMP/journal.txt" 'journal:row[^]]*c0\]' 1)
if [ -z "$ROWKEY" ]; then
  skip "the journal holds no finished game - the delete-entry confirmation is unreachable"
else
  # The row first, then across it: the Delete entry button is a table CELL, which no search
  # offers on its own.
  ROW=$(printf '%s' "$ROWKEY" | sed 's/c0$//')
  if stepto "$ROWKEY" ui.down 12 && stepright "${ROW}c8p1"; then
    inp ui.click
    if focused screen.message-box 15000; then
      capture delete-confirmation "the delete-entry confirmation"
      if stepto 'messagebox:cancel' ui.down 6; then
        inp ui.click
        focused screen.journal 15000 || echo "   NOTE: the journal did not come back after Cancel"
      else
        echo "   NOTE: the confirmation declared no Cancel - leaving it through the game's Exit"
        exitwin MessageBoxWindow
      fi
    else
      skip "pressing Delete entry raised no confirmation screen"
    fi
  else
    skip "the journal's delete-entry button could not be reached"
  fi
fi

hidewin JournalModalWindow
done_
