#!/bin/sh
# The Empire Eliminated window, in its own-elimination form: the player's empire is put out
# of the game, which is the one state that turns that window into an end-game page with a
# score-screen button on it rather than a notification about somebody else.
#
# This route finishes the game and puts the fixture back itself -- see endlib.sh.
set -u
. "$(dirname "$0")/../lib.sh" "$@"
. "$(dirname "$0")/endlib.sh"

quiet_popups
ensure_galaxy
mark_summaries
eliminate_player
shown EmpireEliminatedNotificationWindow 30000 || echo "   NOTE: the elimination window did not show"
focused screen.notification 20000 || echo "   NOTE: screen.notification was not focused"
capture window "empire eliminated window"
restore_fixture
done_
