#!/bin/sh
# The population overview modal (binds itself on show).
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_modal screen.population PopulationModalWindow
capture modal "population overview modal"
exitwin PopulationModalWindow
done_
