#!/bin/sh
# The victory half of the end-game pair: the "You are Victorious" modal the player's own score
# victory raises, the score screen behind it, and the cutscene that page can replay.
#
# A won game is the only state that draws the Replay cutscene button (VictoryScreen.OnBeginShow
# :174 makes it visible on a victory and nothing else), so this is the one route that can prove
# the page's third button reaches anything. Pressing it opens the game's cutscene window over
# the page; the route reads what that screen declares and waits for it to hand the keyboard
# back, which is the half a dump of the button alone would not show.
#
# The score screen's own content is read by defeat-and-score-screen.sh - the page is bound the
# same way either way - so what is captured here is the outcome block, which says different
# words on a victory, and the actions band, which draws a third button.
#
# This route finishes the game and puts the fixture back itself -- see endlib.sh.
set -u
. "$(dirname "$0")/../lib.sh" "$@"
. "$(dirname "$0")/endlib.sh"

quiet_popups
ensure_galaxy
mark_summaries
win_for 'Gui.PlayerEmpire.Index'
shown VictoryAchievedModalWindow 30000 || echo "   NOTE: the victory/defeat modal did not show"
focused screen.victory-achieved || echo "   NOTE: screen.victory-achieved was not focused"
capture modal "victory modal"

if ! stepto 'victory-achieved/ScoreScreenButton' ui.down 6; then
  skip "the victory modal declared no score-screen button - the won page is unreachable from it"
  restore_fixture
  done_
  exit 0
fi

inp ui.click
# The outro cutscene plays over the page first, so the wait is for the page to be the focused
# screen, not merely shown.
shown VictoryScreen 60000 || echo "   NOTE: the score screen did not show"
focused screen.victory 60000 || echo "   NOTE: screen.victory was not focused"
at won-page-arrival
capture score-screen "score screen, won"

if goto 'victory/' 'victory/WatchVideoButton'; then
  inp ui.click
  if focused screen.cutscene 15000; then
    capture cutscene "the replayed outro cutscene"
    # And it must give the keyboard back on its own: a page that ends up behind a finished
    # video nobody can leave is the defect this half is here to catch.
    focused screen.victory 90000 || echo "   NOTE: the score screen did not come back after the cutscene"
  else
    skip "pressing Replay cutscene raised no cutscene screen"
  fi
else
  skip "the won page declared no Replay cutscene button"
fi

restore_fixture
done_
