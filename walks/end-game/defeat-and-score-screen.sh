#!/bin/sh
# The defeat half of the end-game pair, the score screen it opens, and the two ways the score
# screen leads on from there.
#
# An AI is given the score victory, which raises the "You have been Defeated" modal; the
# modal's own Score screen button is then pressed through the mod, which is the route a
# player takes and the only one that proves the button reaches anything. The defeat variant
# is the one that carries the score screen because the victory variant opens it behind the
# outro cutscene (victory-and-cutscene.sh).
#
# The page is read three times - as it arrives, with a second empire picked, with a second
# figure plotted - because the game's model here is select-then-act and a dump of the first
# binding alone proves only the first binding.
#
# Then its own way on: Empire Chronicles into the journal, and the journal's Back to the main
# menu, which is the whole chain a player follows out of a finished game.
#
# This route finishes the game and puts the fixture back itself -- see endlib.sh.
set -u
. "$(dirname "$0")/../lib.sh" "$@"
. "$(dirname "$0")/endlib.sh"

quiet_popups
ensure_galaxy
mark_summaries
AI=$(ai_empire)
echo "   discovered: AI empire index '$AI'"
if [ -z "$AI" ]; then
  skip "no major empire other than the player's is left in the game - the defeat modal is unreachable"
  done_
  exit 0
fi

win_for "$AI"
shown VictoryAchievedModalWindow 30000 || echo "   NOTE: the victory/defeat modal did not show"
focused screen.victory-achieved || echo "   NOTE: screen.victory-achieved was not focused"
capture modal "defeat modal"

# The modal's way on, by the key the shared button reading mints for the prefab's own
# ScoreScreenButton.
if ! stepto 'victory-achieved/ScoreScreenButton' ui.down 6; then
  skip "the defeat modal declared no score-screen button - the score screen is unreachable from it"
  restore_fixture
  done_
  exit 0
fi

inp ui.click
shown VictoryScreen 60000 || echo "   NOTE: the score screen did not show"
focused screen.victory 30000 || echo "   NOTE: screen.victory was not focused"
at score-screen-arrival
capture score-screen "score screen, from the modal"

# A second empire, and back: picking one rebinds the whole left column and re-plots the graph,
# and the reading has to be taken on a rebound page as well as a freshly bound one.
snap "$TMP/scores.txt"
if [ "$(nkeys "$TMP/scores.txt" 'victory:empire/[0-9]*\]')" -lt 2 ]; then
  skip "the score screen lists fewer than two empires - the second-empire reading is unreachable"
else
  goto 'victory:empire/' 'victory:empire/1\]' && inp ui.click
  frame; frame
  capture score-screen-second-empire "score screen, a second empire picked"
  goto 'victory:empire/' 'victory:empire/0\]' && inp ui.click
  frame; frame
fi

# And a second figure, which is what the history table is a reading of.
snap "$TMP/scores.txt"
if [ "$(nkeys "$TMP/scores.txt" 'victory:figure/[0-9]*\]')" -lt 2 ]; then
  skip "the score screen lists fewer than two figures - the second-figure reading is unreachable"
else
  goto 'victory:figure/' 'victory:figure/1\]' && inp ui.click
  frame; frame
  capture score-screen-second-figure "score screen, a second figure plotted"
  goto 'victory:figure/' 'victory:figure/0\]' && inp ui.click
  frame; frame
fi

# Empire Chronicles, which is the page's own route into the journal - and out of the score
# screen, which the game hides behind it.
if goto 'victory/' 'victory/GoToJournalButton'; then
  inp ui.click
  focused screen.journal 30000 || echo "   NOTE: the journal was not focused"
  capture journal-from-chronicles "journal, opened from the score screen"

  # And the journal's own Back, which is where a finished game ends: the main menu.
  if goto 'journal/' 'journal/BackButton'; then
    inp ui.click
    focused screen.main-menu 30000 || echo "   NOTE: the main menu was not focused"
    capture main-menu "main menu, after the journal's Back"
  else
    skip "the journal declared no Back button"
  fi
else
  skip "the score screen declared no Empire Chronicles button"
fi

restore_fixture
done_
