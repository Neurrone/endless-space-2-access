# Politics

## The Senate

The senate screen has four panels: the assembly, the senators, the laws in force, and the census. Each card's full text is in the [review buffer](buffers.md), and a card's costs are read as part of the card.

## Government

The government window has a heading, your current government, the governments you could change to, the cost of a change, and buttons. A choice you cannot make says why.

## Laws

The laws screen has a heading, the filters, the law cards, the detail of the selected law, and buttons. Filters are a choose-one group.

## Population

The population screen has a heading, the list of your populations, the detail of the selected one, its politics, and buttons. This is where a population's traits and political leanings are read.

## Elections

An election runs as a wizard of twelve steps, and every step announces itself. `Next Step` and `Previous` are its own buttons; each step has its own panels, and `Tab` cycles through them as usual.

Two steps deserve explanation, because the game shows most of their content as graphics.

### Vote Breakdown by System

Focus lands on the system's row. The system and its parties are one row; press `Right` to walk along it:

```text
Dusay, System 1 of 1, 1 of 4
Industrialists, 1, has tooltip, 2 of 4
Scientists, 2, has tooltip, 3 of 4
Militarists, 1, has tooltip, 4 of 4
```

Each party's description — what it is, what it wants, and what supports it — is in its review buffer. Press `Up` from the row to reach the previous-system and next-system arrows.

`Tab` reaches the Political Trends column, which the game draws as six unlabeled bars. It reads as a list — `Industrialists, 1 of 4`, `Scientists, 2 of 4`, and so on — followed by the empire total and the counting progress as a sentence, for example `4 of 4 representatives counted`.

The game normally advances to the next system every second and a half. The mod stops that rotation while you are on this step, so the current system stays put; use the arrows to change system.

### Results

The results step reads the winners, the laws that passed, and the outcomes. Each winner's card and badges are walked as one row.
