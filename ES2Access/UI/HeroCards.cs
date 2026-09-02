using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// A hero's card, wherever the game draws one.
    ///
    /// <c>HeroDetailedCard</c> is one prefab family drawn by five surfaces - the Academy's card strip
    /// (<c>AcademyScreen.RefreshHeroCard</c> :380-401), the hero-selection modal (:86-107), the
    /// recruitment popup (:102-114), the hero inspection window's overview card and the "Hero" dossier
    /// tooltip itself (<c>PanelFeatureHeroInfo</c> draws one inside the tooltip window; the fleet and
    /// colony hero panels do NOT - the fleet's hero is a portrait with the one dossier tooltip, measured
    /// 2026-08-26) - and each of them shows a DIFFERENT subset of the same bands. Which subset is not a
    /// guess: the card carries a public flag per band (<c>DisplayNameAndImage</c>,
    /// <c>DisplayDefinition</c>, <c>DisplayExperience</c>, <c>DisplayHealth</c>, <c>DisplaySkills</c>,
    /// <c>DisplayAssignment</c>, <c>DisplayDescription</c>, <c>DisplayShip</c>,
    /// <c>HeroDetailedCard.cs:18,26,44,56,62,66,84,90</c>) and <c>Refresh</c> (:238-299) writes a band
    /// only while its flag is set. So one reader off those flags serves every consumer, and each of them
    /// inherits the bands its own surface never draws.
    ///
    /// A card is the worked "card" case: its readout is the hero's NAME plus whatever role and selected
    /// state the consumer's own surface gives it, and the substance - politics, level, masteries,
    /// upkeep, assignment - lives in the review buffer. So what is offered here is
    /// <see cref="Lines"/>/<see cref="Sections"/> (the face), <see cref="Name"/>,
    /// <see cref="Dossiers"/> for the pages the card draws no words for at all, and
    /// <see cref="Buttons"/> for the buttons the card itself draws. What kind of control a card IS - a
    /// radio in the Academy strip, a plain readout in an inspection window - is the consumer's, because
    /// only the consumer knows what a click on it does.
    ///
    /// Where the game draws a figure with an icon and no caption, the caption is the game's own word for
    /// that figure from its own string table, never a paraphrase: <c>%HeroCardExperienceTitle</c> ("XP"),
    /// <c>%HeroCardLevelTitle</c>, <c>%HeroCardUpkeepTitle</c>, <c>%HeroCardPoliticalLeaderTitle</c>
    /// ("Senator") are the card's own keys; the three the card family has a Description for and no Title
    /// (unspent points, cooldown, relics) borrow the word the game writes over the same figure elsewhere
    /// - <c>%HeroInspectionRemainingSkillPointsTitle</c> on the skill-tree page,
    /// <c>%AssignmentCooldownBaseDurationTitle</c>, <c>%HeroRelicTitle</c>.
    ///
    /// The three handlers the card wires for GOD MODE only - <c>OnExperienceCb</c>, <c>OnCooldownCb</c>,
    /// <c>OnHealthCb</c> (:431-456, each returning immediately unless <c>GodGalaxyCursor.IsGuiInGodMode</c>)
    /// - are read as figures and never offered as controls.
    /// </summary>
    public static class HeroCards
    {
        /// <summary>The game's own word for a figure the card draws as an icon and a number. See the
        /// class remarks for where each one comes from.</summary>
        private const string SenatorTitle = "%HeroCardPoliticalLeaderTitle";

        private const string ExperienceTitle = "%HeroCardExperienceTitle";

        /// <summary>Public because the tooltip reader also has to recognise the caption the card draws
        /// ABOVE the level, which it can only do by the key the label still holds
        /// (<see cref="TooltipFeatures"/>); the WORD itself is <see cref="LevelCaption"/>.</summary>
        public const string LevelTitle = "%HeroCardLevelTitle";

        private const string UnspentPointsTitle = "%HeroInspectionRemainingSkillPointsTitle";
        private const string UpkeepTitle = "%HeroCardUpkeepTitle";
        private const string CooldownTitle = "%AssignmentCooldownBaseDurationTitle";
        private const string RelicsTitle = "%HeroRelicTitle";

        /// <summary>The card's own buttons, by the handler the prefab wires them to. Which transform
        /// carries each one is prefab data, so they are found by what they DO.</summary>
        private const string AssignmentLocationHandler = "OnShowAssignmentLocationCb";

        private const string InspectHandler = "OnInspectClickedCb";

        /// <summary>The hero a card is bound to - the identity a consumer keys its node on, since the
        /// cards themselves are pooled and re-bound by index on every refresh.</summary>
        public static Hero Hero(HeroDetailedCard card)
        {
            try
            {
                return card == null || card.GuiHero == null ? null : card.GuiHero.Hero;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the card is calling the hero. Read off the label the card drew rather than off
        /// the wrapper, because a rename rewrites the label
        /// (<c>HeroDetailedCard.Hero_OnRenameCb</c>).</summary>
        public static Func<string> Name(HeroDetailedCard card)
        {
            HeroDetailedCard it = card;
            return () =>
            {
                try
                {
                    return it == null || !it.DisplayNameAndImage
                        ? null
                        : AgeText.Label(it.NameLabel);
                }
                catch (Exception)
                {
                    return null;
                }
            };
        }

        /// <summary>
        /// The paragraph a card draws PERMANENTLY when it is asked to - the hero's own description on the
        /// recruitment popup, inside a scroll view. Always-drawn text, so a consumer speaks it as part of
        /// the card's readout rather than leaving it to the tooltip rule; null on every card whose
        /// <c>DisplayDescription</c> is off, which is most of them.
        /// </summary>
        public static Func<string> Description(HeroDetailedCard card)
        {
            HeroDetailedCard it = card;
            return () =>
            {
                try
                {
                    return it == null || !it.DisplayDescription
                        ? null
                        : AgeText.Label(it.DescriptionLabel);
                }
                catch (Exception)
                {
                    return null;
                }
            };
        }

        /// <summary>The card's declared content: its whole drawn face as buffer lines, then the card's
        /// own tooltip - the "Hero" dossier the game hangs on <c>HeroTooltip</c>, which is the one
        /// tooltip that belongs to the card as a whole rather than to one band inside it.</summary>
        public static IList<NodeSection> Sections(HeroDetailedCard card)
        {
            HeroDetailedCard it = card;
            return GraphNodes.Sections(() => Lines(it), Tooltip(card, card == null ? null : card.HeroTooltip));
        }

        /// <summary>
        /// Every dossier a mouse could reach by hovering something INSIDE the card, as the card's child
        /// nodes (<see cref="TooltipChildren"/>): the affinity, class and politics lines, one per
        /// mastery, the sentence over the experience gauge, the sentence over the mastery heading, and
        /// the hero's ship on the cards that draw one.
        ///
        /// One sweep of the card's whole subtree in the order the prefab lays it out - which is the
        /// order the card draws it, measured on the recruitment card (the gauge, then the three
        /// definition lines, the mastery heading, the four masteries, the ship) - with each tooltip
        /// offered to both collectors: a renderer-assembled dossier earns a node through
        /// <see cref="TooltipChildren.Add"/>, a sentence the game wrote in words through
        /// <see cref="TooltipChildren.AddPlain"/>, and everything else - the empty tooltip a prefab
        /// hangs on decoration, the dummy line the card keeps hidden - earns nothing. Dispatching per
        /// tooltip rather than sweeping twice is what keeps the two kinds interleaved in DRAWN order:
        /// the gauge's sentence comes first on this card and the mastery heading's sits in the middle
        /// of the definition lines, and two passes would push both to one end.
        ///
        /// The card's OWN dossier is not among them. It is the one tooltip that belongs to the card as a
        /// whole rather than to a band inside it, so the card node itself carries it
        /// (<see cref="Sections"/>) and points at it; a child node for it would be the card explaining
        /// itself a second time.
        ///
        /// Empty where the card is not keeping its tooltips bound at all (<c>HasTooltips</c>), for the
        /// same reason <see cref="Tooltip"/> answers null there: what is left in the fields is the
        /// previous hero's.
        /// </summary>
        /// <param name="declared">The tooltip the CALLER put on the card's own node, where that is not
        /// the card's <c>HeroTooltip</c> - the hero-selection card declares the sentence the game writes
        /// on the card's own transform when this hero cannot take the assignment. Excluded here for the
        /// same reason the card's own dossier is: a tooltip the card announces and points at is not also
        /// a child of itself. Measured 2026-08-28: without it the recruitment card's refusal was both
        /// the node's spoken sentence and the first entry under it.</param>
        public static List<TooltipChildren.Dossier> Dossiers(
            HeroDetailedCard card,
            AgeTooltip declared = null
        )
        {
            List<TooltipChildren.Dossier> found = new List<TooltipChildren.Dossier>(8);
            try
            {
                // Content: which cards contribute dossiers to a reading.
                if (card == null || !card.HasTooltips || !AgeWidgets.Visible(card.AgeTransform))
                {
                    return found;
                }

                Scratch.Clear();
                AgeWidgets.EffectiveTooltips(
                    card.AgeTransform,
                    Scratch,
                    TooltipReach.Own | TooltipReach.Descendants,
                    CardDepth
                );
                AgeTooltip whole = Tooltip(card, card.HeroTooltip);
                for (int i = 0; i < Scratch.Count; i++)
                {
                    AgeTooltip tooltip = Scratch[i];
                    if (
                        AgeWidgets.SameTooltip(tooltip, whole)
                        || AgeWidgets.SameTooltip(tooltip, declared)
                    )
                    {
                        continue;
                    }

                    TooltipChildren.Add(found, tooltip);
                    TooltipChildren.AddPlain(
                        found,
                        tooltip == null ? null : tooltip.AgeTransform
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("hero card: reading the card's dossiers threw: " + e);
            }

            return found;
        }

        /// <summary>How far inside a card its explanations are hung. Measured on the recruitment card:
        /// the ship group sits four levels down and a mastery line six, so the resolver's own default of
        /// four would find the ship and nothing else.</summary>
        private const int CardDepth = 8;

        // Reused rather than allocated per card: the sweep runs inside a per-frame screen build and a
        // caller consumes it before the next card is read.
        private static readonly List<AgeTooltip> Scratch = new List<AgeTooltip>(12);

        /// <summary>
        /// Everything the card is showing, band by band in the order the card DRAWS them - which is the
        /// order <c>Refresh</c> writes them in everywhere but the definition band (see
        /// <see cref="Definition"/>) - for the review buffer.
        ///
        /// A band whose tooltip the game wrote as plain words contributes those words too - the health
        /// band's explanation, the assignment's - because they are one sentence the game authored about a
        /// figure the player is reading. A band whose tooltip names a CLASS contributes nothing here: its
        /// words do not exist until the tooltip window draws them, and one node can only ever point at
        /// one of them. Those are <see cref="Dossiers"/>, which the consumers hang under the card as
        /// nodes of their own.
        /// </summary>
        public static IList<string> Lines(HeroDetailedCard card)
        {
            List<string> lines = new List<string>();
            if (card == null)
            {
                return lines;
            }

            try
            {
                if (card.DisplayNameAndImage)
                {
                    Add(lines, null, AgeText.Label(card.NameLabel));
                }

                if (card.DisplayDefinition)
                {
                    Definition(card, lines);
                }

                if (card.DisplayExperience)
                {
                    Experience(card, lines);
                }

                if (card.DisplayHealth)
                {
                    Add(lines, null, AgeText.Label(card.HealthLabel));
                    AddTooltip(lines, Tooltip(card, card.HealthTooltip));
                }

                if (card.DisplaySkills)
                {
                    Masteries(card, lines);
                }

                if (card.DisplayAssignment)
                {
                    Assignment(card, lines);
                }

                if (card.DisplayDescription)
                {
                    AddLines(lines, AgeText.Lines(AgeText.Label(card.DescriptionLabel)));
                }

                if (card.DisplayShip)
                {
                    Add(lines, null, AgeText.Label(card.ShipTitle));
                    AddTooltip(lines, Tooltip(card, card.ShipTooltip));
                }
            }
            catch (Exception e)
            {
                Log.Warn("hero card: reading the card threw: " + e);
            }

            return lines;
        }

        /// <summary>Who the hero is: the senator line when the game has drawn it, then affinity, class
        /// and politics - three words the card writes with an icon each, whose dossiers are the
        /// class-backed tooltips this reading cannot reach.
        ///
        /// In the order the card DRAWS them, which is not the order <c>Refresh</c> writes them in
        /// (affinity, politics, class): the three labels' measured rects put class before politics, and
        /// the buffer reads a card the way a sighted player reads it.</summary>
        private static void Definition(HeroDetailedCard card, List<string> lines)
        {
            if (Drawn(card.PoliticalLeaderLine))
            {
                string senator = AgeText.Label(card.PoliticalLeaderLabel);
                Add(lines, null, string.IsNullOrEmpty(senator) ? AgeText.Title(SenatorTitle) : senator);
            }

            Add(lines, null, AgeText.Label(card.AffinityLabel));
            Add(lines, null, AgeText.Label(card.ClassLabel));
            Add(lines, null, AgeText.Label(card.PoliticsLabel));
        }

        /// <summary>How far along the hero is: experience out of what the next level needs, the level,
        /// and the points they have not spent - a group the card only draws while there are any
        /// (<c>RefreshExperience</c> :358-369).</summary>
        private static void Experience(HeroDetailedCard card, List<string> lines)
        {
            Add(lines, AgeText.Title(ExperienceTitle), AgeText.Label(card.ExperienceLabel));
            Add(lines, AgeText.Title(LevelTitle), AgeText.Label(card.LevelLabel));
            if (Drawn(card.UnspentSkillsGroup))
            {
                Add(lines, AgeText.Title(UnspentPointsTitle), AgeText.Label(card.UnspentSkillsValue));
            }
        }

        /// <summary>
        /// One line per mastery, which is one per <c>HeroMasteryDefinition</c> in the game's database
        /// (<c>HeroMasteryPanel.Refresh</c> :39-56) - the level reached out of the highest this hero's
        /// definition allows.
        ///
        /// The mastery's NAME is not always drawn: <c>HeroMasteryLine.Bind</c> (:35-46) writes it into
        /// <c>ClassTitle</c> only where the prefab has one, and otherwise the line is an icon and a
        /// figure whose only name is on the wrapper hung on its tooltip - which is what
        /// <see cref="AgeWidgets.TooltipTitle"/> answers.
        ///
        /// The heading over them is the game's own word for what the group IS, and reading the lines
        /// without it leaves four levels belonging to nothing (<see cref="Heading"/>).
        /// </summary>
        private static void Masteries(HeroDetailedCard card, List<string> lines)
        {
            HeroMasteryPanel panel = card.HeroMasteryPanel;
            AgeTransform container = panel == null ? null : panel.MasteryLinesContainer;
            if (container == null || !Drawn(container))
            {
                return;
            }

            Add(lines, null, Heading(panel, container));
            HeroMasteryLine[] found = container.GetComponentsInChildren<HeroMasteryLine>(true);
            for (int i = 0; i < found.Length; i++)
            {
                HeroMasteryLine line = found[i];
                if (line == null || !Drawn(line.AgeTransform))
                {
                    continue;
                }

                string name = AgeText.Label(line.ClassTitle);
                if (string.IsNullOrEmpty(name))
                {
                    name = AgeWidgets.TooltipTitle(line.Tooltip);
                }

                Add(lines, name, AgeText.Label(line.LevelLabel));
            }
        }

        /// <summary>The word the panel writes over its masteries. <c>HeroMasteryPanel</c> declares the
        /// line prefab and the container and nothing else, so the heading is found by where it is drawn
        /// - the label the panel draws OUTSIDE the container the lines are in - rather than by a field
        /// it does not have or a prefab name it could be renamed under.</summary>
        private static string Heading(HeroMasteryPanel panel, AgeTransform container)
        {
            AgePrimitiveLabel[] found = panel.GetComponentsInChildren<AgePrimitiveLabel>(true);
            for (int i = 0; i < found.Length; i++)
            {
                AgeTransform at = found[i] == null ? null : found[i].AgeTransform;
                if (at == null || !Drawn(at) || AgeWidgets.Under(at, container))
                {
                    continue;
                }

                string text = AgeText.Label(found[i]);
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            return null;
        }

        /// <summary>What the hero costs and what they are doing: upkeep, the assignment's own name and
        /// the sentence the game wrote about it, the cooldown while the card is drawing one, and the
        /// relics a Templar hero carries (<c>RefreshAssignment</c> :372-401 draws each group by its own
        /// condition, so what is DRAWN is the question, not what the hero has).</summary>
        private static void Assignment(HeroDetailedCard card, List<string> lines)
        {
            Add(lines, AgeText.Title(UpkeepTitle), AgeText.Label(card.UpkeepLabel));
            Add(lines, null, AgeText.Label(card.AssignmentLabel));
            AddTooltip(lines, Tooltip(card, card.AssignmentTooltip));
            if (Drawn(card.Cooldown))
            {
                Add(lines, AgeText.Title(CooldownTitle), AgeText.Label(card.CooldownLabel));
            }

            if (Drawn(card.RelicsGroup))
            {
                Add(lines, AgeText.Title(RelicsTitle), AgeText.Label(card.RelicsLabel));
            }
        }

        /// <summary>
        /// The buttons the card itself draws, as child nodes of it - the one that puts the galaxy view on
        /// the hero's assignment, and the one that opens the inspection window.
        ///
        /// Found by the handler each is wired to rather than by a field, because the card exposes none of
        /// them. Pressed with <see cref="AgeWidgets.PressPropagating"/>, because a button INSIDE the card
        /// is inside the card's own toggle: clicking it with a mouse also selects the card
        /// (<c>HeroDetailedCard.OnSwitchCb</c> :431-441), and a keyboard press that skipped that half
        /// would open the inspection window for the hero while the strip still shows another one picked.
        ///
        /// The three god-mode handlers are deliberately not among them, and the game's own double click
        /// (<c>OnDoubleClickCb</c>) is not wired here: on the Academy screen it is the Inspect button
        /// again, and that button exists.
        /// </summary>
        public static void Buttons(List<Cell> cells, HeroDetailedCard card, string keyPrefix)
        {
            Button(cells, card, AssignmentLocationHandler, "%AcademyScreenShowAssignmentLocationTitle", keyPrefix, "locate");
            Button(cells, card, InspectHandler, "%AcademyScreenInspectButtonTitle", keyPrefix, "inspect");
        }

        /// <summary>The card's button wired to <paramref name="handler"/>, or null when this card's
        /// prefab does not draw one.</summary>
        public static AgeControlButton Wired(HeroDetailedCard card, string handler)
        {
            return AgeWidgets.WiredTo(AgeWidgets.Transform(card), handler);
        }

        private static void Button(
            List<Cell> cells,
            HeroDetailedCard card,
            string handler,
            string titleKey,
            string keyPrefix,
            string key
        )
        {
            AgeControlButton button = Wired(card, handler);
            AgeTransform widget = AgeWidgets.Transform(button);
            if (widget == null || !AgeWidgets.Operable(widget))
            {
                return;
            }

            AgeControlButton it = button;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Button(
                () => AgeText.Title(titleKey),
                () => AgeWidgets.PressPropagating(it),
                () => AgeWidgets.Operable(AgeWidgets.Transform(it)),
                tooltip
            );
            AgeWidgets.Point(vtable, it);
            Cells.Add(cells, widget, ControlId.For(button, keyPrefix + "/" + key), vtable);
        }

        /// <summary>A tooltip only where the card is keeping its tooltips at all: <c>HasTooltips</c> off
        /// means <c>Refresh</c> never bound them, so whatever is left in the fields is the previous
        /// hero's or the prefab author's.</summary>
        private static AgeTooltip Tooltip(HeroDetailedCard card, AgeTooltip tooltip)
        {
            try
            {
                return card != null && card.HasTooltips ? tooltip : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool Drawn(AgeTransform widget)
        {
            // Content: which lines are gathered into a card's reading.
            return widget != null && AgeWidgets.Visible(widget);
        }

        /// <summary>The game's word for a hero's level, for the other places that draw the number
        /// bare - the system page's governor gauge among them. One caller of the key, so the two read
        /// alike whatever language the game is in.</summary>
        public static string LevelCaption()
        {
            return AgeText.Title(LevelTitle);
        }

        /// <summary>The game's word for what a hero costs, for the other places that draw the figure
        /// bare - the tooltip reader's naming pass among them. One caller of the key, for the reason
        /// <see cref="LevelCaption"/> is one.</summary>
        public static string UpkeepCaption()
        {
            return AgeText.Title(UpkeepTitle);
        }

        private static void Add(List<string> lines, string caption, string value)
        {
            string line = new MessageBuilder().ListItem(caption).ListItem(value).Build();
            if (!string.IsNullOrEmpty(line) && !lines.Contains(line))
            {
                lines.Add(line);
            }
        }

        private static void AddTooltip(List<string> lines, AgeTooltip tooltip)
        {
            Func<IList<string>> read = AgeWidgets.TooltipLines(tooltip);
            if (read != null)
            {
                AddLines(lines, read());
            }
        }

        private static void AddLines(List<string> lines, IList<string> source)
        {
            for (int i = 0; source != null && i < source.Count; i++)
            {
                if (!string.IsNullOrEmpty(source[i]) && !lines.Contains(source[i]))
                {
                    lines.Add(source[i]);
                }
            }
        }
    }
}
