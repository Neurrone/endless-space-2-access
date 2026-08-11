using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// A hero's card, wherever the game draws one.
    ///
    /// <c>HeroDetailedCard</c> is one prefab family drawn by five surfaces - the Academy's card strip
    /// (<c>AcademyScreen.RefreshHeroCard</c> :380-401), the hero-selection modal (:86-107), the
    /// recruitment popup (:102-114), the hero inspection window's overview card and the fleet and colony
    /// hero panels - and each of them shows a DIFFERENT subset of the same bands. Which subset is not a
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
    /// <see cref="Lines"/>/<see cref="Sections"/> (the face), <see cref="Name"/>, and
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
        private const string LevelTitle = "%HeroCardLevelTitle";
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
        /// Everything the card is showing, band by band in the order <c>Refresh</c> writes them, for the
        /// review buffer.
        ///
        /// A band whose tooltip the game wrote as plain words contributes those words too - the health
        /// band's explanation, the assignment's - because they are one sentence the game authored about a
        /// figure the player is reading. A band whose tooltip names a CLASS contributes nothing here: its
        /// words do not exist until the tooltip window draws them, and the card is one node, so the only
        /// way to draw an affinity dossier is to point at the affinity icon. That is the known gap in
        /// this reading, not an omission.
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

        /// <summary>Who the hero is: the senator line when the game has drawn it, then affinity,
        /// politics and class - three words the card writes with an icon each, whose dossiers are the
        /// class-backed tooltips this reading cannot reach.</summary>
        private static void Definition(HeroDetailedCard card, List<string> lines)
        {
            if (Drawn(card.PoliticalLeaderLine))
            {
                string senator = AgeText.Label(card.PoliticalLeaderLabel);
                Add(lines, null, string.IsNullOrEmpty(senator) ? Localized(SenatorTitle) : senator);
            }

            Add(lines, null, AgeText.Label(card.AffinityLabel));
            Add(lines, null, AgeText.Label(card.PoliticsLabel));
            Add(lines, null, AgeText.Label(card.ClassLabel));
        }

        /// <summary>How far along the hero is: experience out of what the next level needs, the level,
        /// and the points they have not spent - a group the card only draws while there are any
        /// (<c>RefreshExperience</c> :358-369).</summary>
        private static void Experience(HeroDetailedCard card, List<string> lines)
        {
            Add(lines, Localized(ExperienceTitle), AgeText.Label(card.ExperienceLabel));
            Add(lines, Localized(LevelTitle), AgeText.Label(card.LevelLabel));
            if (Drawn(card.UnspentSkillsGroup))
            {
                Add(lines, Localized(UnspentPointsTitle), AgeText.Label(card.UnspentSkillsValue));
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
        /// </summary>
        private static void Masteries(HeroDetailedCard card, List<string> lines)
        {
            HeroMasteryPanel panel = card.HeroMasteryPanel;
            AgeTransform container = panel == null ? null : panel.MasteryLinesContainer;
            if (container == null || !Drawn(container))
            {
                return;
            }

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

        /// <summary>What the hero costs and what they are doing: upkeep, the assignment's own name and
        /// the sentence the game wrote about it, the cooldown while the card is drawing one, and the
        /// relics a Templar hero carries (<c>RefreshAssignment</c> :372-401 draws each group by its own
        /// condition, so what is DRAWN is the question, not what the hero has).</summary>
        private static void Assignment(HeroDetailedCard card, List<string> lines)
        {
            Add(lines, Localized(UpkeepTitle), AgeText.Label(card.UpkeepLabel));
            Add(lines, null, AgeText.Label(card.AssignmentLabel));
            AddTooltip(lines, Tooltip(card, card.AssignmentTooltip));
            if (Drawn(card.Cooldown))
            {
                Add(lines, Localized(CooldownTitle), AgeText.Label(card.CooldownLabel));
            }

            if (Drawn(card.RelicsGroup))
            {
                Add(lines, Localized(RelicsTitle), AgeText.Label(card.RelicsLabel));
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
            try
            {
                if (card == null)
                {
                    return null;
                }

                AgeControlButton[] found = card.GetComponentsInChildren<AgeControlButton>(true);
                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i] != null && found[i].OnActivateMethod == handler)
                    {
                        return found[i];
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("hero card: looking for the '" + handler + "' button threw: " + e);
            }

            return null;
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
            if (widget == null || !AgeWidgets.Visible(widget) || !AgeWidgets.Operable(widget))
            {
                return;
            }

            AgeControlButton it = button;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Button(
                () => Localized(titleKey),
                () => AgeWidgets.PressPropagating(it),
                () => AgeWidgets.Operable(AgeWidgets.Transform(it)),
                tooltip
            );
            AgeWidgets.Point(vtable, it);
            Cells.Add(cells, widget, ControlId.Referenced(button, keyPrefix + "/" + key), vtable);
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
            return widget != null && AgeWidgets.Visible(widget);
        }

        private static string Localized(string key)
        {
            try
            {
                return AgeText.Clean(Gui.Localize(key));
            }
            catch (Exception)
            {
                return null;
            }
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
