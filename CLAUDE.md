# Endless Space 2 Access

This is a screen reader accessibility mod for Endless Space 2 (ES2), a turn-based 4X strategy game.

## Goals

### Reuseable docs or skills for LLM-assisted game accessibility modding

The primary goal is to generate docs or a skill that could help you with implementing screen reader game accessibility mods for other games more rapidly. For example, it should provide common patterns for common cross-cutting concerns applicable to all games like screen reader integration via Prism, UI implementation, reverse engineering and common tools to help make various types of game mechanics or screens accessible.

I will be pointing you to other game mods that have implemented various things well. The hope is these documents can capture that info so that it is the single place to refer to for such best practices.

It should also help to ensure you know what questions to ask. The ideal goal is for this to help you make large parts of games accessible with direction from me only needed to point you to which screens and mechanics need to be made accessible, or if there are genuine high-level decisions that need to be made.

These documents should be in the `docs/generic` folder. Please have source code in files and reference them from the markdown documentation in cases where it would be better than trying to explain something in pros

### Making Endless Space 2 accessible

This is an important but secondary goal, it is the test vehicle for implementing the above. The objective here is to ensure a screen reader user can operate the game entirely by keyboard

## References

- Look at the `decompiled` for decompiled code
- `docs/generic` for the documentation on game accessibility modding

## Conventions

- Runtime code must stay compatible with Endless Space 2's Unity 5.5 / Mono environment. Assume .NET Framework 3.5 compatibility unless a project is explicitly for tools or tests.
- Uses BepInEx to patch the game with an external command surface.
- Avoid redundant null checks and comments that do not add information.
- Prefer deterministic game actions over simulated input where the game exposes a reliable API.
- Name behavior after what the player can do or perceive, not after incidental implementation details.

## Workflow

After implementing a feature or major change:

1. Offer to check if the game accessibility modding documentation should be updated
2. If I approve it, consult the generic game accessibility mod documentation and check if the documentation should be improved to assist in future tasks
3. If so, propose what changes you would make
4. If I approve, make the changes
5. Reflect on what I could have done better to facilitate your work for future sessions

## Delegation

Do delegate to lower power subagents when appropriate especially for exploring code.

However, updating the game accessibility mod documentation should be done in the main agent.
