# Introduction

Endless Space 2 Access is a screen reader accessibility mod for Endless Space 2, a turn-based 4X strategy game set in the Endless universe. It narrates the game's screens and gives every one of them keyboard navigation, so that a blind or visually impaired player can run an empire without a mouse.

## Features

- Full keyboard operation of the game: every screen the mod covers is walked with Tab and the arrow keys, and every command is a key
- Tree-based reading of each screen: panels are Tab stops, and what is inside them is a tree you expand, collapse and search
- A galaxy map read as a tree of places, each with coordinates measured from your home system, where a starlane is something you travel down rather than a line you look at
- A scanner that answers "what is near me" by category, nearest first, without changing anything on screen
- An inspect mode that sweeps a square of galaxy with the arrow keys and reads out everything inside it, fog included
- The game's own scan mode made usable: the lens is announced whenever the zoom changes it, and its legend is readable
- Review buffers for re-reading long tooltips, dossiers and the chat log a line at a time
- Type-ahead search on every list and tree: start typing and the focused panel jumps to what you named
- Keyboard drag and drop for the queues, populations, ships and cards the game only lets a mouse move
- Speech through your own screen reader via Prism, with SAPI as a fallback

## Status

This is version 0.1.0, the first public test release. Treat it as a test build: it has been developed against the base game in single player, and that is where it has been used.

- Base game, single player: covered. All the screens listed in this book are navigable.
- Multiplayer: untested. The mod does not know about the mode, and the chat surfaces it adds are only exercised in single player, so multiplayer-only states (ready flags, kicks, alliance chat) have not been played through.
- DLC: partly covered. Content that ships with the expansions is not all modelled — the Penumbra hacking dashboard is not, Supremacy's Behemoth mechanics are barely tested, and some faction-specific surfaces have never been seen by the mod's author.
- English only. The mod's own phrases exist in English; text the game itself writes arrives in whatever language the game is set to.

Please report anything that reads wrong, and anything you cannot reach by keyboard.

## Links

- [Mod GitHub repository](https://github.com/Neurrone/endless-space-2-access)
- [Latest mod release](https://github.com/Neurrone/endless-space-2-access/releases/latest)
- [Endless Space 2 on Steam](https://store.steampowered.com/app/392110/Endless_Space_2/)
- [Endless Space 2 on GOG](https://www.gog.com/en/game/endless_space_2)
- [Changelog](changelog.md)
