#!/bin/sh
# The defeat half of the end-game pair, and the score screen it opens.
#
# An AI is given the score victory, which raises the "You have been Defeated" modal; the
# modal's own Score screen button is then pressed through the mod, which is the route a
# player takes and the only one that proves the button reaches anything. The defeat variant
# is the one that carries the score screen because the victory variant plays a minute of
# cutscene over it first (victory-modal.sh).
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

# The modal's way on. Addressed by the key the shared button reading mints for the prefab's
# own ScoreScreenButton, landed on by the words it reads back.
snap "$TMP/modal.txt"
if ! tkey "$TMP/modal.txt" 'victory-achieved/ScoreScreenButton[^]]*\]' 1; then
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

# The two selectors are the page's own select-then-act, and everything else follows the
# game's rebind: a second empire is picked, the page read again, and the player's empire put
# back so the dump above and this one differ only by the pick.
snap "$TMP/scores.txt"
if [ "$(nkeys "$TMP/scores.txt" 'victory:empire/[0-9]*\]')" -lt 2 ]; then
  skip "the score screen lists fewer than two empires - the second-empire reading is unreachable"
else
  tkey "$TMP/scores.txt" 'victory:empire/[0-9]*\]' 2 && inp ui.click
  frame; frame
  capture score-screen-second-empire "score screen, a second empire picked"
  tkey "$TMP/scores.txt" 'victory:empire/[0-9]*\]' 1 && inp ui.click
  frame; frame
fi

# And a second figure, which re-plots every curve and is what the history table is a reading
# of; put back the same way.
snap "$TMP/scores.txt"
if [ "$(nkeys "$TMP/scores.txt" 'victory:figure/[0-9]*\]')" -lt 2 ]; then
  skip "the score screen lists fewer than two figures - the second-figure reading is unreachable"
else
  tkey "$TMP/scores.txt" 'victory:figure/[0-9]*\]' 2 && inp ui.click
  frame; frame
  capture score-screen-second-figure "score screen, a second figure plotted"
  tkey "$TMP/scores.txt" 'victory:figure/[0-9]*\]' 1 && inp ui.click
  frame; frame
fi

restore_fixture
done_
