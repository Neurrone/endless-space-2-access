# Distribution

- **The zip is a committed skeleton plus a staged payload.** Commit the runtime skeleton
  (mod loader, speech library, licenses, config) as `release-template/`. A build script
  stages the freshly built payload into it and zips; it CLEARS its staged roots first,
  so the zip can only contain what this run produced, and reads the version from the
  project file — one source. Ordinary dev builds (which deploy to the game folder) must
  never write into the template: that is how stale or machine-specific files ship.
- **Publishing is a separate script that gates everything and builds nothing.** It never
  builds, tags, or pushes — every precondition (tag, zip, changelog entry) is a hard
  failure for the author to fix. Keeping build and publish apart is what makes a release
  reproducible from a clean tree.
- **Redistribution obligations travel in the zip**: the license and notice files of
  every third-party dll you ship, at the zip root where a curious player finds them.
- **Install UX for a blind audience**: extract at the game root, no installer; a README
  with exact screen-reader steps; and the mod SPEAKS on boot, because the first
  utterance is the player's only smoke test that the install worked. If you do ship an
  installer, both SoC and wotr-access independently landed on Rust installers after
  antivirus false-positives killed other packagers.
- **A tester channel can be committed build artifacts** kept fresh by a pre-commit hook
  (wotr-access) — zero-infrastructure distribution to testers between releases.
- **Player docs are a book, not the repo docs.** A separate docs source (ES2: mdBook)
  with per-screen guides, a command reference, and a changelog written in player
  language, each entry relative to the LAST RELEASE ("docked fleets are back in the
  scanner"), never in commit language. CI builds and deploys it on release, so the book
  can never trail the zip.
- **A game sold on two stores can diverge** — renamed classes, stripped fields. Never
  name a divergent member (the IL embeds the type name; it fails at RUNTIME on the
  other store, invisible to any build on one machine). Reflect through one seam file,
  and add an offline test that fails the suite if any other file names a divergent
  member. (ES2's divergences: `docs/architecture.md` § GOG vs Steam in that repo.)
