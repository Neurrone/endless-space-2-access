#!/bin/sh
# Family: the academy page and the hero pickers. NEVER presses Confirm or a card's
# Content button.
#
# Hero SELECTION and hero INSPECTION need the empire to own a hero: the window shows but
# the mod screen never activates without one. Both are attempted and recorded as skipped
# when the fixture has no hero; the by-key walk (09) covers them either way.
set -u
FAMILY=heroes; . "$(dirname "$0")/lib.sh" "$@"

prologue

openwin AcademyScreen 2500; evq "$CS/tut.cs"; pause 800
capture 01-academy "academy page"
hidewin AcademyScreen; pause 800

openwin HeroCompleteListModalWindow 2000; evq "$CS/tut.cs"; pause 600
capture 02-hero-list "hero complete list"
exitwin HeroCompleteListModalWindow; hidewin HeroCompleteListModalWindow

NHERO=$(fact '((System.Func<string>)(() => { var d = Gui.PlayerEmpire.GetAgency<DepartmentOfEducation>(); if (d == null) return "0"; return ((System.Collections.IList)d.ActiveHeroes).Count.ToString(); }))()')
echo "   discovered: ${NHERO:-?} heroes"
if [ "${NHERO:-0}" -ge 1 ] 2>/dev/null; then
  openwin HeroSelectionModalWindow 2200; evq "$CS/tut.cs"; pause 600
  capture 03-hero-selection "hero selection modal"
  exitwin HeroSelectionModalWindow; hidewin HeroSelectionModalWindow
else
  skip "the empire owns no hero - hero-selection and hero-inspection windows not captured"
fi

epilogue
