# Heroes and the Academy

## The Academy

The academy screen has your heroes and the actions on the academy itself: recruiting, the academy's standing, and its current offers. A hero card is read in the bands the game draws it in.

The academy's diplomacy page has your relation with the academy, the academy's own panel, the actions, and your treasury. Its two expansion windows (including the ships panel) are screens of their own.

## Picking a Hero

Wherever the game asks you to choose a hero — a governor for a system, an admiral for a fleet — a hero-selection window opens with the candidate cards and buttons. Each card's full description is in the [review buffer](buffers.md), and the Inspect button opens the full inspection window.

The complete hero list is its own window: one line per hero, plus buttons.

## Inspecting a Hero

The hero inspection window has three pages: the overview, the skill wheel, and the hero's ship design. The pencil buttons in the overview's ship and skill boxes open the other two pages.

On the overview, `Tab` cycles through the hero's title and identity, their ship, their card, their skills summary, their story, and the buttons: assign, unassign, and whatever else the game offers.

The **skill page** adds the skill wheel and the panels beside it. The wheel has three branches of four rings each, with one or two skills per ring, and the mod exposes it as that tree: branch, then ring, then skill. Each skill reads its state, cost and effect. A skill you cannot take yet says what it is waiting for.

Spending a point is not immediate: `Enter` on a skill adds it to a pending set. Apply commits the pending picks; Reset discards them.
