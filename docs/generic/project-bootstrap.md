# Project bootstrap

Standing up the repo and toolchain for a new game's mod.

## Runtime detection and consequences

The game's runtime dictates everything. For Unity:

| Signal | Meaning | TFM | Consequences |
|---|---|---|---|
| `Managed\Assembly-CSharp.dll`, old `mscorlib` (Unity ≤2017-ish) | Mono, .NET 2.0/3.5 profile | `net35` | No Tasks, no `System.Collections.Concurrent`, no `ManualResetEventSlim`; use `Queue<T>`+`lock`+`ManualResetEvent`. No `LPUTF8Str` — marshal UTF-8 by hand. C# 7.3 syntax is fine (compiles down). |
| Modern Mono (Unity 2018+) | Mono, .NET 4.x profile | `net472`/`net48` | Most BCL available; still no managed COM on Mono |
| `GameAssembly.dll`, interop assemblies | IL2CPP | `net6.0`+ | BepInEx 6 runs plugins on bundled CoreCLR; Il2CppInterop proxies; full modern BCL |

Confirm empirically: check `mscorlib.dll`'s version and an assembly's `ImageRuntimeVersion`
(`v2.0.50727` vs `v4.0.30319`) — a DLL built for the wrong image version will not load, which
also constrains every third-party dependency you vendor.

## Repo layout

```
<Game>Access/            reloadable mod library (features; Core/ is BCL-only)
<Game>Access.Loader/     permanent BepInEx plugin (dev server, mod lifecycle)
<Game>Access.Tests/      xunit, compiles Core/ sources by link — no game refs
vendor/                  native/vendored deps with provenance NOTICEs (prism, mcs)
decompiled/<Assembly>/   reference-only, gitignored, regenerable via decompile.ps1
docs/                    game-specific research notes
docs/generic/            these docs
GamePaths.props.template machine paths template (real GamePaths.props gitignored)
run-game.ps1             build + deploy + launch
decompile.ps1            regenerate decompiled/ from the game's Managed folder
```

## csproj patterns

- SDK-style projects targeting old TFMs need
  `Microsoft.NETFramework.ReferenceAssemblies` (PrivateAssets=all).
- Game and loader assemblies referenced by `HintPath` into the game's `Managed`/BepInEx core,
  all `Private=false` — they already exist in-process; never copy them.
- A `DeployToGame` target (`AfterTargets="Build"`, gated on the game dir existing) copies
  artifacts into place. Two-speed split: the loader's target uses `SkipUnchangedFiles` (its
  files are locked while the game runs), the mod's target overwrites unconditionally (that is
  the hot-reload path — the mod DLL is loaded from bytes and never locked).
- Machine specifics live in `GamePaths.props`, imported with a `Condition="Exists(...)"`;
  commit only the template.
- The tests project compiles the mod's `Core/**/*.cs` **by file link** with no game or engine
  references. This build-enforces the rule that `Core/` stays BCL-only and unit-testable —
  if someone adds a `UnityEngine` using to Core, the test project stops compiling. That is a
  feature (pattern from tangledeep_access and wotr-access).

## Loader installation and first boot

BepInEx: Doorstop (`winhttp.dll` + `doorstop_config.ini`) + `BepInEx\core` in the game dir.
Verify with a boot: `BepInEx\LogOutput.log` shows the chainloader, plugin discovery, and your
plugin's first log lines. That log is the primary diagnostic until the dev server exists.

## Decompilation

`decompile.ps1` reads the Managed path from `GamePaths.props` and runs
`ilspycmd -p` per assembly into `decompiled/<Assembly>/` (project mode: one file per type,
namespace folders, global-namespace types at the folder root). Decompile more than
`Assembly-CSharp`: the engine/framework layer often lives in a *different* assembly
(`Assembly-CSharp-firstpass`, publisher DLLs) and contains the UI framework, input system,
and service infrastructure you actually need to hook. Call `ilspycmd` through `cmd /c` with
stderr suppressed — its update nag on stderr otherwise becomes a terminating PowerShell error.

`decompiled/` is reference-only and gitignored: regenerable, not redistributable.

## Source files

[`src/bootstrap/`](src/bootstrap/) — the mod, loader, and tests csprojs (deliberate
examples, not synced mirrors — their bytes carry the source mod's release metadata),
`decompile.ps1`, `run-game.ps1`, `wait-game.ps1` (block until the game reaches a state),
`crop-shot.ps1` (the cropped-screenshot evidence pair), `GamePaths.props.template`, and
`gitignore.example` (rename to `.gitignore` on copy).
