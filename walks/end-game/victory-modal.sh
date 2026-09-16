#!/bin/sh
# The victory half of the end-game pair: the "You are Victorious" modal the player's own
# score victory raises.
#
# The score screen behind it is NOT walked here. On a victory the game plays the faction's
# outro cutscene over the page first, which is about a minute of video for a reading the
# defeat route already takes (defeat-and-score-screen.sh) -- the page itself is bound the
# same way either way, and which way OUT it draws is the from-journal question, not this one.
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
restore_fixture
done_
