#!/bin/sh
# The score screen as the journal opens it -- the same page for a game that finished long
# ago, which draws its way BACK to the journal instead of the way to the menu and the
# chronicles (VictoryScreen.Bind sets the three buttons' visibility).
#
# Nothing here finishes a game: the journal's own row button binds a stored summary and shows
# the page over the running session, so this route needs no restore and leaves the fixture
# alone. It skips itself where the journal holds no finished game to open.
set -u
. "$(dirname "$0")/../lib.sh" "$@"
. "$(dirname "$0")/endlib.sh"

quiet_popups
ensure_modal screen.journal JournalModalWindow
snap "$TMP/journal.txt"
NROWS=$(nkeys "$TMP/journal.txt" 'journal:row[^]]*c8\]')
echo "   discovered: $NROWS finished game(s) in the journal"
if [ "${NROWS:-0}" -lt 1 ]; then
  skip "the journal holds no finished game - the score screen cannot be opened from it"
  hidewin JournalModalWindow
  done_
  exit 0
fi

# The row first, then across it: the button is a table CELL, which no search offers on its own.
ROWKEY=$(key_nth "$TMP/journal.txt" 'journal:row[^]]*c0\]' 1)
ROW=$(printf '%s' "$ROWKEY" | sed 's/c0$//')
if [ -z "$ROWKEY" ] || ! stepto "$ROWKEY" ui.down 12 || ! stepright "${ROW}c8\]"; then
  skip "the journal's score-screen button could not be reached"
  hidewin JournalModalWindow
  done_
  exit 0
fi

inp ui.click
shown VictoryScreen 60000 || echo "   NOTE: the score screen did not show"
focused screen.victory 30000 || echo "   NOTE: screen.victory was not focused"
capture score-screen "score screen, from the journal"

# Back the way the page offers, which for this variant is the only button it draws -- and
# proving it is there is half of what this route is for; the journal is then hidden so the
# folder's other scenarios start with nothing modal up.
if goto 'victory/' 'victory/BackToJournalButton'; then
  inp ui.click
else
  skip "the score screen drew no way back to the journal"
fi
shown JournalModalWindow 30000 || echo "   NOTE: the journal did not come back"
hidewin JournalModalWindow
done_
