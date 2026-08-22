# Speech

Screen reader output via Prism, and the architectural rules that keep speech coherent.

## Prism

[Prism](https://github.com/ethindp/prism) is a native C-ABI library (`prism.dll`) unifying
~20 speech backends (NVDA, JAWS, SAPI, OneCore, braille displays, …). Windows 10+. Every
reference mod uses it the same way, with hand-written P/Invoke bindings — existing C#
wrappers were judged incomplete; the flat C ABI makes direct binding easy and works from any
Mono/.NET vintage.

Usage pattern (the whole API a mod needs):

1. `prism_init` once → context.
2. `prism_registry_create_best` → the best available backend, already initialized: a running
   screen reader if present, else plain TTS. Returns an **owned** backend
   (`prism_backend_free` it; `acquire_best` is the non-owning variant).
3. Prefer `prism_backend_output(text, interrupt)` (speech + braille) and fall back to
   `prism_backend_speak` when the features bitmask says output is unsupported. Probe features
   once at init, not per utterance.
4. `prism_backend_stop` to silence; free backend then `prism_shutdown` on teardown.

ABI facts baked into the binding: `Cdecl` on every import; all strings are UTF-8,
marshaled by hand (null-terminated byte arrays out, scan-for-NUL in); C `bool` is
`UnmanagedType.I1`; `size_t` is `UIntPtr`; handles are `IntPtr`.

**Native loading:** `LoadLibrary` the DLL by *full path* before any by-name `DllImport`
resolves — plugin folders are not on the OS search path. Deploy `prism.dll` where the loader
can name it (game root is the convention).

**Failure policy: degrade to silence, never throw.** Speech init failure (no DLL, no backend)
logs details and leaves the mod running with `Available == false`; every speak call no-ops.
A speech crash must never take the mod down. Corollary for reload-capable mods: something
that survives the mod (host log, or host-owned speech — see below) should report a mod that
failed to start, because a blind player cannot distinguish "mod silent" from "mod dead".

## The two architectural laws

**1. One chokepoint.** All speech flows through a single `Speak(MessageBuilder, interrupt)`
— deliberately no string overload. The builder (see [localization.md](localization.md)) owns
separators and formatting; `Speak` alone calls `Build()`. Null/empty builds no-op, so
producers can compose optional content without guards. This gives one place for
post-processing, the dev-server tap, and localization. Corollary for composers over
optional fields: return **null** when nothing survives, distinct from empty — a caller
diffing state for passive announcement uses that null as "not filled in yet" and must not
commit its watermark on it (the passive-announcement rules in
[ui-navigation.md](ui-navigation.md)); an empty string would silently consume the event.

**2. Never speak from a hook.** Harmony patches and event handlers only set state or enqueue.
A single per-frame pump drains queues and speaks, in a deliberate, fixed order — e.g.
targeting feedback (interrupts) before ambient event log (queued) before danger warnings
(spoken last so they interrupt everything). This sidesteps re-entrancy from patched game
internals and makes interruption deterministic.

Interrupt policy tiers, consistent across the reference mods: direct responses to a
deliberate keypress interrupt; ambient/event narration queues; safety-critical lines are
emitted last-in-frame with interrupt. Silence the previous utterance only once an action is
confirmed to do something — not on every keypress. One exception to "a deliberate keypress
interrupts": a confirmation word for an action that can CLOSE its surface must QUEUE — the
replacement surface announces itself first with an interrupt in the same breath, and an
interrupting confirmation eats that landing (measured: a commit word swallowed the page
announcement the commit caused).

## Content that plays on a clock

Cutscenes, animated sequences and anything else the player WATCHES rather than works needs
lines emitted against a playback position, not against a state diff. Two rules, both learned
the hard way:

**Read the clock the game schedules its OWN subtitles against.** Not the media player's true
position — the game's, whatever accumulator it advances per frame. This looks like the worse
choice, because under dropped frames that accumulator lags the picture. That is exactly why
it is right: the game's subtitles lag with it, so a description written to sit in the gap
between two spoken lines stays in that gap. Time off the real media position instead and the
description walks into dialogue the game is still holding back. It also stops when the game
stops, for free.

**Arm on playback START, not on the window opening.** Engines routinely zero the play clock
just before the first frame and never reset it on unload, so the value read while a video is
still loading belongs to the PREVIOUS one — enough to empty a whole track into the player's
ear at once. Find the callback that fires on the frame the clock is zeroed (ES2:
`OnPlayStarted`) and arm there; use the show/open call only to record WHICH asset is playing.
That call is often the only place a variant argument exists at all, since games pass such
things straight into path-building without storing them.

Both hooks obey law 2: they record, the pump speaks.

## Speech ownership and recovery

Two placements, both shipped by real mods:

- **Mod-side** (ES2 Access): the speech pipeline lives in the reloadable mod and is torn down
  and re-created per reload. Simple; the loader log covers failure reporting.
- **Host-side** (DiscoAccess): the pipeline lives in the never-reloaded host; the mod speaks
  through the host contract. Buys: spoken "mod failed to load" reporting, one native handle
  across reloads.

Either way, provide a recovery affordance eventually: wotr-access binds F8 to force-reset the
speech backend from anywhere, because a player who picked a broken backend can't navigate a
settings menu they can't hear.

## Testing without hearing

The speech class exposes a static `Observer` callback invoked with every spoken string
*before* the availability gate — so a muted, headless run (`*_NO_SPEECH=1` env) still
captures everything for the dev server's `/speech` endpoint and the `/eval` speech appendix.
This is the mechanism that lets an agent verify announcements.

## Source files

Copy nearly verbatim: [`src/speech/PrismNative.cs`](src/speech/PrismNative.cs),
[`src/speech/PrismSpeech.cs`](src/speech/PrismSpeech.cs),
[`src/speech/NativeLoader.cs`](src/speech/NativeLoader.cs). Pump ordering example:
[`src/hot-reload/ModEntry.cs`](src/hot-reload/ModEntry.cs). Obtain `prism.dll` itself from
the Prism project's releases (ship its license files alongside).
