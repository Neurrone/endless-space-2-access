# Hot reload

Swap rebuilt mod code into the running game — no quit/restart cycle. Two shipped designs
exist (ES2 Access on old Mono; DiscoAccess on CoreCLR) and they converged on the same shape.

## The loader/mod split

- **Loader** (`<Game>Access.Loader`): the actual mod-loader plugin. Small, stable, **never
  reloads**. Owns the dev server, the REPL, and the mod lifecycle. Because the server lives
  here, a mod build that fails to load leaves `/reload` and `/eval` alive — you fix, rebuild,
  reload again, without restarting the game.
- **Mod** (`<Game>Access.dll`): a plain library (no plugin attribute — the chainloader must
  not also load it). Entry contract: `public static class ModEntry { Start(ModHost); Stop(); }`,
  found and invoked via reflection. All features live here.
- **`ModHost`**: the typed contract the mod is handed — logging, paths, route registration,
  main-thread queue, update-handler slot, coroutine bridge, speech-line notification. The mod
  references the loader assembly (types resolve to the already-loaded copy, so type identity
  holds); the loader must **never** compile-reference the mod, or the runtime would load the
  mod DLL by name from disk and lock it.

## Loading from bytes

`Assembly.Load(File.ReadAllBytes(path))` — never `LoadFrom`/`LoadFile`. The on-disk DLL stays
unlocked, so `dotnet build` overwrites it while the game runs; that is the entire hot loop.
(Split the deploy targets accordingly: loader files copy with `SkipUnchangedFiles` since they
ARE locked; the mod DLL overwrites unconditionally.)

On CoreCLR, load into a collectible `AssemblyLoadContext` whose `Load` returns null so all
shared dependencies resolve to the default context (DiscoAccess). On Mono there is no ALC —
see the leak note below.

**On Mono, rename the assembly before every load — or reload silently does nothing.** Mono
resolves `Assembly.Load(byte[])` through the same identity cache as every other load: bytes
whose assembly name and version match an assembly already loaded are discarded and the *old*
assembly is handed back. Every reload then looks successful from every angle — the counter
increments, stale-build clears, Stop/Start re-run — while the game keeps executing the
previous build's code. Nothing errors, so nothing looks wrong; ES2 Access carried this bug
until a reload was probe-tested. The fix (the same one BepInEx's ScriptEngine uses): rewrite
the identity with Mono.Cecil before loading — read the bytes into an `AssemblyDefinition`,
set `Name.Name` to `"<Mod>-r" + a per-process counter, write it back out, and load *those*
bytes. Only the identity changes; the file on disk, namespaces and type names are untouched,
so nothing else notices — unless code somewhere names the mod assembly by its simple string
name, which is worth grepping for once. BepInEx ships `Mono.Cecil.dll` in its core, so the
reference costs nothing. In any new project, prove reloading works before trusting it: add a
probe field to a JSON endpoint, build, reload, and check the response — timestamps and reload
counters cannot detect a deduplicated load.

## Reload sequence: validate before teardown

A broken build must leave the previous mod running (a screen reader user loses *everything*
when the mod dies). Order:

1. **Prepare** (old mod untouched): read bytes → `Assembly.Load` → find the entry type →
   resolve Start/Stop. Any failure: record the error, count it separately
   (`failedReloadCount`), refuse the swap. The old mod never noticed.
2. **Swap**: stop the old mod, then start the new one. `Stop` throwing is logged and
   unwinding continues. If the new `Start` throws, record it — and the host must
   *defensively* unwind anything the half-started mod registered (routes, update handler,
   coroutines), because a dead assembly must not keep serving.

The host performs the same defensive cleanup after every normal `Stop` too — the teardown
contract is belt and suspenders: the mod unwinds itself, the host unwinds it again.

**Teardown checklist** every feature must satisfy (reload-safety is a per-feature invariant,
not an afterthought): unregister routes; clear the update handler; stop coroutines; shut down
speech/native handles; null static back-references so the old object graph collects;
unpatch Harmony. And on the host side: never hold mod types, `MemberInfo`s, or delegates in
host caches — anything mod-shaped the host keeps must be cleared on unload, or it serves
stale types from the dead assembly after the next reload.

**Harmony rule**: create the Harmony instance with a **unique-per-load id**
(`"<modid>." + Guid`). With a fixed id, an old module's `UnpatchSelf` (which removes by owner
id) can strip a newer load's patches — silent feature death until restart (DiscoAccess hit
exactly this). Under strict swap ordering a fixed id happens to be safe; the unique id costs
nothing and stays safe if ordering ever changes.

## Introspection

`/loader/status` reports: `modLoaded`, `reloadCount`, `failedReloadCount`, `lastReloadError`,
`modAssemblyName` (the per-load identity — a changed value is direct proof a swap reached the
runtime), and **stale-build detection** — the DLL write time captured at load vs. the write
time on disk right now (`staleBuild: true` = a newer build awaits reload). This answers the
two questions an agent actually asks: "did my reload take?" and "am I testing the build I
just made?"

## Runtime capability table

| Runtime | Unload old code? | Cost per reload |
|---|---|---|
| CoreCLR (BepInEx 6) | Yes — collectible ALC | ~none |
| Mono (any Unity Mono era) | **No** — only AppDomain unload exists, and a mod that touches engine objects and patches game code cannot live in a secondary AppDomain | Old assembly image + JITted code stay resident (~hundreds of KB). Dev-only; players load once. Restart the game if a marathon session ever cares. |

## What never hot-reloads

The loader and any contract types it exposes. Changing them requires a game restart — and on
CoreCLR, changing a shared "Core" assembly loaded in the default context has the same limit
(DiscoAccess documents `/reload` "succeeding" and then every `Tick` throwing
`TypeLoadException`). Keep the loader minimal precisely so this rarely matters.

When new observability could live on either side of the boundary, the deciding question is
not reloadability but **polled route vs called probe**: a route costs the loader a restart
to add and pays on every poll, while a mod-side probe hot-reloads, takes arguments, and
catches transients via a frame-polled wait on its own output.

## State across reloads

Best pattern: hold **no mod-side caches of game state** — re-derive from live game objects
every frame/announcement. Then reload "just works" with no serialization. What legitimately
persists lives host-side by design (settings, host speech ring, native audio handles).

## Source files

[`src/hot-reload/ModLoader.cs`](src/hot-reload/ModLoader.cs) (prepare/activate/unload),
[`ModHost.cs`](src/hot-reload/ModHost.cs), [`LoaderPlugin.cs`](src/hot-reload/LoaderPlugin.cs),
[`ModEntry.cs`](src/hot-reload/ModEntry.cs) (the full Stop unwind). Verified behaviors: a
corrupted DLL is refused with the old mod alive; `staleBuild` flips across build/reload; a
probe field added to an endpoint appears after build + reload, and appears renamed under a
fresh `modAssemblyName` — the swap demonstrably replaces code, not just lifecycle state.
