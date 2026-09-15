#!/bin/sh
# The selected-fleet panel: select the first fleet type-ahead can reach on the map tree and
# dump the HUD with the panel up; then the A-B-A rebind check of its pooled action buttons
# (FleetActionItem) against a second fleet -- A is the fleet drawing MORE actions, so that B
# retires some. The action set differs in KIND between fleets (a toggle against a button),
# which is the rebind the game's hint write skips a branch on.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

FLEETACTS='t = Gui.GuiService.GetWindow<FleetsScreen>(false).FleetActionsPanel.FleetActionsTable;'
ensure_galaxy
FLEETS=$(fact '((System.Func<string>)(() => { var sb = new System.Text.StringBuilder(); System.Collections.IList fs = (System.Collections.IList)Gui.PlayerEmpire.GetAgency<DepartmentOfDefense>().Fleets; for (int i=0;i<fs.Count;i++){ if (sb.Length>0) sb.Append("|"); sb.Append(((Fleet)fs[i]).LocalizedName); } return sb.ToString(); }))()')

# closepanel -- the panel a previous selection left up owns the focused screen, and
# type-ahead would search it instead of the map tree.
closepanel() {
  evs '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow<FleetsScreen>(); if (w != null && w.Shown) w.HandleInput(InputAction.Exit); return "fleet panel closed"; }))()'
  hidden FleetsScreen 5000
}
# selfleet <name> -- select the fleet on the map and echo how many action buttons the panel
# then draws. Non-zero exit when type-ahead cannot reach it.
selfleet() {
  closepanel
  inp ui.focusMap
  findland "$1" || return 1
  inp ui.click
  shown FleetsScreen 10000 || return 1
  frame; frame
  sed "s|@@TARGET@@|fleets:action/|" "$CS/rebind-walk.cs" > "$TMP/gw.cs"
  curl -s -X POST --data-binary "@$TMP/gw.cs" "$HOST/eval?speech=0" > "$TMP/gw.out"
  unjson "$TMP/gw.out" | sed -n 's/^nodes=//p'
}

FA=""; FB=""; CA=0; CB=0
if [ -n "$FLEETS" ]; then
  OIFS=$IFS; IFS='|'
  for f in $FLEETS; do
    IFS=$OIFS
    if c=$(selfleet "$f"); then
      if [ -z "$FA" ]; then FA=$f; CA=${c:-0}
      elif [ "$f" != "$FA" ]; then FB=$f; CB=${c:-0}; break
      fi
    fi
    IFS='|'
  done
  IFS=$OIFS
fi

if [ -z "$FA" ]; then
  skip "no fleet of this empire is reachable on the map tree - selected-fleet panel not captured"
  done_; exit 0
fi
if [ "${CB:-0}" -gt "${CA:-0}" ]; then SWAP=$FA; FA=$FB; FB=$SWAP; fi   # A is the richer panel
echo "   discovered: fleets drawing $CA and $CB action buttons"

selfleet "$FA" > /dev/null
at fleet-selected
dump panel
ghosts "galaxy + selected-fleet panel"
gwalk graph a 'fleets:action/'
gpool pool  a "$FLEETACTS"
if [ -z "$FB" ]; then
  skip "fewer than two of this empire's fleets are reachable on the map tree - the fleet-action rebind is unreachable"
else
  selfleet "$FB" > /dev/null
  at fleet-b-selected
  gpool poolb b "$FLEETACTS"
  selfleet "$FA" > /dev/null
  at fleet-a-reselected
  gwalk graph a2 'fleets:action/'
  gpool pool  a2 "$FLEETACTS"
  aba graph pool
fi
closepanel
done_
