# Heroes and the Academy

## The academy

The academy screen walks your heroes and the actions available on the academy itself: recruiting, the academy's own standing, and what it is offering this turn. A hero card is read in the bands the game draws it in, and only the buttons that actually do something are offered — the game's debug handlers are never presented as actions.

The academy's diplomacy page walks your relation with the academy, the academy's own panel, the actions and your treasury. Its two expansion modals (the ships panel among them) are screens of their own.

## Picking a hero

Wherever the game asks you to choose a hero — a governor for a system, an admiral for a fleet — a hero-selection window opens with the candidate cards and its buttons. Each card carries the hero's whole dossier in the review buffer, and Inspect opens the full inspection window over it.

The complete hero list is its own window: one line per hero, plus its buttons.

## Inspecting a hero

The hero inspection window has three pages — the overview, the skill wheel, and the hero's ship design — and which one is up is the game's own state. The pencil buttons in the overview's ship and skill boxes are what open the other two.

On the overview, Tab walks the hero's title and identity, their ship, their card, their skills summary, and their story, then the buttons: assign, unassign, and whatever else the game offers.

The **skill page** adds the wheel and the panels beside it. The wheel is three branches of four rings each, with one or two skills to a ring, so it is declared as the tree it is — branch, then ring, then skill, in the order the game laid them out. Each skill says its state, what it costs and what it gives; a locked ring's skills refuse for free, and a skill you cannot take yet says what it is waiting for, because the game composes that sentence and then never puts it on screen.

Spending a point is not immediate: `Enter` on a skill is the dot's own click, which adds it to a pending set that Apply commits and Reset throws away.
