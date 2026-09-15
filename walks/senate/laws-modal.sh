#!/bin/sh
# The laws modal (binds itself on show).
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_modal screen.laws LawsManagementModalWindow
capture modal "laws modal"
exitwin LawsManagementModalWindow
done_
