# walks/end-game/endlib.sh -- shared helpers for the routes that END the game. Sourced, never run.
#
# Three of this folder's scenarios reach their screen by finishing the game, which no other
# family does. Two things follow, and they are the whole reason this file exists.
#
# The LEVER is an order, never the turn-end button. The game's own debug window posts the same
# orders from a button (`DebugUIWindow_Victory`), so none of them is phase-bound; the natural
# route - lowering the victory manager's last turn and ending the turn - is deliberately not
# here, because ending a turn writes an autosave that records the game as won and the fixture
# would never come back.
#
# The RESTORE is a save load, because `run.sh`'s drain cannot undo a finished game: there is
# no window to close. A route that posts one of these orders reloads the newest save itself
# and waits until the mod is back on the galaxy, so the scenario after it starts where every
# other family starts.

# quiet_popups -- put every open notification popup away through the manager, leaving the
# notifications themselves pending.
#
# These routes need it twice over. A notification popup sits above the galaxy HUD and above
# the score screen, so one open over the page swallows the keys this route presses; and
# loading a save re-raises the turn's popups, so the restore below would otherwise hand the
# next scenario a fixture the walk does not start from.
#
# The manager's own hide, never the window's: the manager clears its current-notification
# slot as well, and a slot left set suppresses every later auto-open (docs/notifications.md).
# Nothing is dismissed - the read flags opening one sets are put back, the same as
# notifications/popup.sh does, so the strip is left as it was found.
quiet_popups() {
  qp_state=$(fact '((System.Func<string>)(() => { if (Gui.PlayerEmpire == null || Gui.GuiNotificationService == null) return ""; var sb=new System.Text.StringBuilder(); System.Collections.IList ns=(System.Collections.IList)Gui.GuiNotificationService.GetPlayerEmpireGuiNotifications(); for(int i=0;i<ns.Count;i++){ if(sb.Length>0) sb.Append("|"); sb.Append(((GuiNotification)ns[i]).AlreadyRead); } return sb.ToString(); }))()')
  evs '((System.Func<string>)(() => { var mgr = Gui.GuiNotificationService; if (mgr == null || Gui.PlayerEmpire == null) return "no notification service yet"; mgr.HideAllGuiNotifications(); return "hid all; current=" + (mgr.CurrentGuiNotification == null ? "null" : "SET"); }))()'
  if [ -n "$qp_state" ]; then
    evs "((System.Func<string>)(() => { if (Gui.PlayerEmpire == null || Gui.GuiNotificationService == null) return \"no notification service yet\"; string[] want = \"$qp_state\".Split('|'); System.Collections.IList ns=(System.Collections.IList)Gui.GuiNotificationService.GetPlayerEmpireGuiNotifications(); int n=0; for(int i=0;i<ns.Count && i<want.Length;i++){ var g=(GuiNotification)ns[i]; bool w = want[i]==\"True\"; if(g.AlreadyRead!=w){ g.AlreadyRead=w; n++; } } return \"restored \"+n+\" read flags\"; }))()"
  fi
}

# mark_summaries / prune_summaries -- the journal rows this route is about to create, and the
# undo for them.
#
# Finishing a game writes an end-game summary to disk (`GameStatisticsManager.SaveEndGameSummary`),
# which is a row in the journal for good - so a route that ends the game leaves the journal
# one row longer every time it runs, and `journal-modal` dumps every row. Two runs of this
# folder would then differ by rows nobody's change put there. So the summaries the journal
# already holds are recorded by the file name each is stored under, and anything that is not
# one of them afterwards is the route's own debris, deleted through the game's own call - the
# same one the journal's Delete button reaches. Nothing the route did not create is touched:
# the match is by name, not by date or by count.
# ev_body <c# expression> -- evs, but the answer is echoed: what was pruned belongs in the
# scenario's own log, since nothing else records it.
ev_body() {
  printf '%s' "$1" > "$TMP/inline.cs"
  curl -s -X POST --data-binary "@$TMP/inline.cs" "$HOST/eval?speech=0" | sed 's/.*"result":"//; s/","error.*//; s/^/   /'
  echo
}

mark_summaries() {
  SUMMARY_MARK=$(fact '((System.Func<string>)(() => { System.Collections.IList xs = (System.Collections.IList)Gui.GuiWrapperProviderService.GuiEndGameSummaries; var sb = new System.Text.StringBuilder(); for (int i = 0; i < xs.Count; i++) { if (sb.Length > 0) sb.Append("|"); sb.Append(((GuiEndGameSummary)xs[i]).EndGameSummary.Name); } return sb.ToString(); }))()')
  echo "   discovered: $(printf '%s' "$SUMMARY_MARK" | awk -F'|' '{print NF}') end-game summar(ies) already in the journal"
}

prune_summaries() {
  [ -n "${SUMMARY_MARK:-}" ] || return 0
  ev_body "((System.Func<string>)(() => { var svc = Amplitude.Unity.Framework.Services.GetService<IGameStatisticsManagementService>(); if (svc == null) return \"no statistics service\"; string[] had = \"$SUMMARY_MARK\".Split('|'); System.Collections.IList xs = (System.Collections.IList)Gui.GuiWrapperProviderService.GuiEndGameSummaries; var doomed = new System.Collections.ArrayList(); for (int i = 0; i < xs.Count; i++) { var g = ((GuiEndGameSummary)xs[i]).EndGameSummary; bool known = false; for (int k = 0; k < had.Length; k++) { if (had[k] == g.Name) known = true; } if (!known) { doomed.Add(g); } } for (int i = 0; i < doomed.Count; i++) { svc.DeleteEndGameSummary((EndGameSummary)doomed[i]); } return \"pruned \" + doomed.Count + \" summar(ies) this route wrote; journal now \" + ((System.Collections.IList)Gui.GuiWrapperProviderService.GuiEndGameSummaries).Count; }))()"
}

# ai_empire -- the index of a major empire that is not the player's and is still in the game.
# Discovered, never written down: which empires a save holds is the fixture's business.
ai_empire() {
  fact '((System.Func<string>)(() => { var es = Gui.Game.Empires; int p = Gui.PlayerEmpire.Index; for (int i = 0; i < es.Length; i++) { MajorEmpire m = es[i] as MajorEmpire; if (m != null && i != p && !m.HasBeenEliminated) return i.ToString(); } return ""; }))()'
}

# win_for <empire index> -- give that empire the score victory, which is what raises the
# victory/defeat modal. The player's own index makes it a victory, anyone else's a defeat.
win_for() {
  evs "Gui.PlayerEmpire.PlayerControllers.Server.PostOrder(new OrderAchieveVictory($1, (Amplitude.StaticString)\"VictoryScore\"))"
}

# eliminate_player -- put the player's own empire out of the game, which raises the Empire
# Eliminated window. Irreversible in this session; the caller restores the fixture.
eliminate_player() {
  evs 'Gui.PlayerEmpire.PlayerControllers.Server.PostOrder(new OrderEliminateEmpire(Gui.PlayerEmpire.Index))'
}

# to_menu -- leave whatever end-game page is up for the main menu, which is where a save load
# is accepted. The score screen's own handler is used where it is up, so the game takes the
# route it takes for a mouse; the journal is hidden rather than left holding the screen.
to_menu() {
  evs '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow<VictoryScreen>(); if (w != null && w.Shown) { typeof(VictoryScreen).GetMethod("OnBackToMenuCb", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(w, new object[]{ null }); } var j = Gui.GuiService.GetWindow<JournalModalWindow>(); if (j != null && j.Shown) { Gui.GuiService.HideWindow(j); } Gui.GuiService.ShowWindow<MainMenuScreen>(); return "menu"; }))()'
}

# restore_fixture -- the newest save, back in game. The one place in `walks/` that loads a
# save outside `walk-all.sh --reset`, and it is not a fixture choice: it is the undo for a
# game this route finished. The wait is repeated because the dev server caps one wait at
# about a minute and a load takes most of that.
restore_fixture() {
  to_menu
  waitfor 'Gui.GuiService.GetWindow<MainMenuScreen>() != null && Gui.GuiService.GetWindow<MainMenuScreen>().Shown' 20000 \
    || echo "   NOTE: the main menu did not come back"
  prune_summaries
  curl -s -X POST --data-raw '' "$HOST/loadsave" > "$TMP/loadsave.out" 2>&1
  cat "$TMP/loadsave.out"; echo
  if grep -q 'not ready' "$TMP/loadsave.out"; then
    pause 3000
    curl -s -X POST --data-raw '' "$HOST/loadsave" > "$TMP/loadsave.out" 2>&1
    cat "$TMP/loadsave.out"; echo
  fi
  # In game first, and only then the galaxy: the load raises the turn's notification popups,
  # and while one is up the galaxy is not the screen the mod reports. Repeated because the
  # dev server caps one wait at about a minute and a load takes most of that.
  rf_i=0
  while [ "$rf_i" -lt 3 ]; do
    if waitfor 'Gui.PlayerEmpire != null && ES2Access.ModEntry.Screens.Current != null' 55000; then
      # Put the popups away until they stay away: hiding the one that is up lets the manager
      # auto-open the next of the turn's queue, so one pass is not always enough.
      qp_i=0
      while [ "$qp_i" -lt 4 ]; do
        quiet_popups
        onscreen screen.galaxy 5000 && break
        qp_i=$((qp_i+1))
      done
      isfocused screen.galaxy || echo "   NOTE: the galaxy HUD is not the focused screen"
      tut
      return 0
    fi
    rf_i=$((rf_i+1))
  done
  echo "   NOTE: the fixture did not come back in game"
  return 1
}

# stepright <key ERE> [limit] -- walk right along the row the cursor is on until the focused
# node's key matches. A table row's non-primary cells cannot be landed on by type-ahead - the
# search contributes one result per row and filters the other columns out by their column
# stamp - so a button the game draws as a table CELL is reached the way a player reaches it,
# by stepping across the row it is in.
stepright() {
  sr_i=0
  while [ "$sr_i" -lt "${2:-16}" ]; do
    snap "$TMP/sr.txt"
    grep '^ *> ' "$TMP/sr.txt" | grep -qE "\[$1" && return 0
    inp ui.right
    sr_i=$((sr_i+1))
  done
  return 1
}
