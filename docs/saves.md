# The save system

What the game's save machinery gives a mod to hold on to: which campaign is being played, and
how to reach one save file by name. (Chartered 2026-08-31; grew out of the bookmarks work.)

- **A CAMPAIGN HAS ONE GUID, minted at its first save and carried verbatim ever after.**
  `GameManager.GameSaveDescriptor` (reachable as the `IGameSerializationService` the manager
  publishes) is created and stamped `Guid.NewGuid()` the first time the game is written and never
  re-stamped (`GameManager.UpdateGameSaveDescriptor`, `GameManager.cs:726-767`), so every save-as,
  autosave, quicksave and load of any of them shares it — which makes it the one honest name for
  "this game the player is playing", across a save list where titles repeat (`%AutoSaveFileName`
  five times over) and turns move. A game nobody has saved yet has no descriptor at all, so the
  answer is null. Measured 2026-08-31: loading a save of another campaign swaps the GUID
  (`ee4517b1…` → `1a0cd9c2…`) and loading back restores it. The mod keys its map bookmarks on it
  (`ES2Access/UI/Bookmarks/MapBookmarkStore.cs`) and notices the change by POLLING that property
  once a frame beside the game's own identity, rather than patching the save and load paths — one
  question answers both, and a new game, a load and a first save are the same event to it. The
  poll compares the raw `Guid` and only mints the string form on a change, so the once-a-frame
  question allocates nothing.
- **`GameManager.RetrieveGameSaveDescriptor(path)` reads a save's descriptor off disk without
  loading it**, and `Session.GameClient.Disconnect(GameDisconnectionReason.ClientLoadSave, 0,
  descriptor)` is the in-game load route `POST /loadsave` itself takes — together they load ONE
  named file when the title would be ambiguous.
- The descriptor also carries the in-memory-only `SourceFileName` (the active file's path, set on
  save) and the display names `Title`/`LocalizedTitle`/`TitleWithTurn`
  (`GameSaveDescriptor.cs:43-78`) — the titles are what the save list and `DevProbe.Saves()` show,
  and they are NOT identity: autosaves reuse them freely.
