using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    public static partial class TooltipFeatures
    {
        // ---- the ship stat block ----

        /// <summary>
        /// A ship's stats, each number given the name the game itself has for it.
        ///
        /// The prefab draws six of them as bare numbers with a picture beside each - health,
        /// movement, manpower, command points, offensive and defensive power - and puts the pictures
        /// in a band of their own, so nothing in the drawn layout attaches "1500/1500" to health.
        /// The names are not the mod's to invent either: each stat has a title in the game's own
        /// strings (<c>GuiShipDesign.ShipStat*</c> through <c>Gui.GetTitle</c>), which is the same
        /// word the ship design screen writes beside the same number.
        ///
        /// Everything else the feature draws - the role and size rows, the balance caption - already
        /// reads correctly from its own rows, so the naming is a SUBSTITUTION and the rows are then
        /// read exactly as any other feature's.
        /// </summary>
        private static Dictionary<AgeTransform, Naming> ShipStatNames(PanelFeatureShipInfo ship)
        {
            Dictionary<AgeTransform, Naming> named = new Dictionary<AgeTransform, Naming>();
            Name(named, ship.HealthLabel, GuiShipDesign.ShipStatHealth);
            Name(named, ship.MovementPointsLabel, GuiShipDesign.ShipStatMovement);
            Name(named, ship.ManpowerLabel, GuiShipDesign.ShipStatManpower);
            Name(named, ship.CommandPointsLabel, GuiShipDesign.ShipStatCommandPoints);
            Name(named, ship.OffensivePowerLabel, GuiShipDesign.ShipStatOffensiveMilitaryPower);
            Name(named, ship.DefensivePowerLabel, GuiShipDesign.ShipStatDefensiveMilitaryPower);
            Balance(
                named,
                ship.OffensiveBalanceGauge,
                GuiShipDesign.ShipStatOffensiveMilitaryPower
            );
            Balance(
                named,
                ship.DefensiveBalanceGauge,
                GuiShipDesign.ShipStatDefensiveMilitaryPower
            );
            return named;
        }

        // ---- the fleet stat blocks ----

        /// <summary>
        /// A fleet's stats, each number given the name the game itself has for it.
        ///
        /// The same prefab shape as a ship's - a picture in one band and the figure it names in the
        /// next - and the same six figures, because a fleet is what its ships add up to. Two of the
        /// names are the fleet's own rather than a ship's: command points are what the fleet list
        /// already calls them, and the four counts by hull size are named by the sizes themselves.
        /// The size counts are drawn as a strip of items, so the names have to reach inside one.
        ///
        /// <c>PanelFeatureGarrisonInfoEmbedded</c> is this feature plus the two military power
        /// figures, so it is read as this feature plus two more names.
        /// </summary>
        private static Dictionary<AgeTransform, Naming> GarrisonStatNames(
            PanelFeatureGarrisonInfo garrison
        )
        {
            Dictionary<AgeTransform, Naming> named = new Dictionary<AgeTransform, Naming>();
            CommandPoints(named, garrison.CommandValue);
            Name(named, garrison.HealthLabel, GuiShipDesign.ShipStatHealth);
            Name(named, garrison.MovementLabel, GuiShipDesign.ShipStatMovement);
            Name(named, garrison.ActionPointLabel, DepartmentOfTheTreasury.Resources.ActionPoint);
            CountsBySize(named, garrison.CountBySizeTable);

            PanelFeatureGarrisonInfoEmbedded embedded =
                garrison as PanelFeatureGarrisonInfoEmbedded;
            if (embedded != null)
            {
                Name(
                    named,
                    embedded.OffensivePowerLabel,
                    GuiShipDesign.ShipStatOffensiveMilitaryPower
                );
                Name(
                    named,
                    embedded.DefensivePowerLabel,
                    GuiShipDesign.ShipStatDefensiveMilitaryPower
                );
            }

            return named;
        }

        /// <summary>
        /// The four fleet prefabs the game draws WITHOUT the caption column, each figure given the
        /// same word its full-sized cousin already reads with.
        ///
        /// A fleet list that outgrows its box swaps the panel it draws each fleet with: over three
        /// garrisons the compact embedded panel, over five a footer counting the rest, and an
        /// automated delivery fleet gets a third panel again. None of them is a
        /// <c>PanelFeatureGarrisonInfo</c>, so none of them was reached by the reader that already
        /// knows these words - and every one of them draws its command points as a bare number
        /// hard against the fleet's NAME, which reads as if the figure were part of what the fleet
        /// is called ("1st Patriots Navy 1").
        ///
        /// The compact panel loses the most: its health, offense and defense are bare as well, and
        /// its four counts by hull size are named by nothing at all, because the size TEXTURES the
        /// duplets draw are not in the picture vocabulary. All of it is the vocabulary
        /// <see cref="GarrisonStatNames"/> already reads, applied to this prefab's own fields.
        /// </summary>
        private static Dictionary<AgeTransform, Naming> CompactGarrisonNames(
            PanelFeatureGarrisonCompactInfoEmbedded garrison
        )
        {
            Dictionary<AgeTransform, Naming> named = new Dictionary<AgeTransform, Naming>();
            CommandPoints(named, garrison.CommandValue);
            Name(named, garrison.HealthLabel, GuiShipDesign.ShipStatHealth);
            Name(named, garrison.OffenseLabel, GuiShipDesign.ShipStatOffensiveMilitaryPower);
            Name(named, garrison.DefenseLabel, GuiShipDesign.ShipStatDefensiveMilitaryPower);
            CountsBySize(named, garrison.CountBySizeTable);
            return named;
        }

        /// <summary>An automated delivery fleet's card - see <see cref="CompactGarrisonNames"/>. Its
        /// role, size, cargo and destination rows are captioned by the prefab and read correctly on
        /// their own; the command points and the movement pair are the two figures it draws
        /// bare.</summary>
        private static Dictionary<AgeTransform, Naming> AutomatedFleetNames(
            PanelFeatureGarrisonInfoAutomatedFleet garrison
        )
        {
            Dictionary<AgeTransform, Naming> named = new Dictionary<AgeTransform, Naming>();
            CommandPoints(named, garrison.CommandValue);
            Name(named, garrison.MovementLabel, GuiShipDesign.ShipStatMovement);
            return named;
        }

        /// <summary>The "+ N more fleets" footer - see <see cref="CompactGarrisonNames"/>. The one
        /// figure it draws is the command points of the fleets it did NOT list, and drawn against the
        /// count in the title it reads as a repetition of that count ("+ 2 more fleets 2").</summary>
        private static Dictionary<AgeTransform, Naming> MoreFleetsNames(
            PanelFeatureAdditionalGarrisons more
        )
        {
            Dictionary<AgeTransform, Naming> named = new Dictionary<AgeTransform, Naming>();
            CommandPoints(named, more.CommandValue);
            return named;
        }

        /// <summary>What the fleet list calls a fleet's command points, put in front of the figure
        /// four separate prefabs draw with only an icon beside it.</summary>
        private static void CommandPoints(
            Dictionary<AgeTransform, Naming> named,
            AgePrimitiveLabel label
        )
        {
            Name(named, label, AgeText.Title(CommandPointsTitle));
        }

        /// <summary>
        /// The little ship-design card a reinforcement list draws, up to six at a time.
        ///
        /// It is the ship stat block with the caption column taken out: five figures drawn as bare
        /// numbers behind icons, and the ship's SIZE drawn as nothing but a picture - and that
        /// picture, unlike the stat icons, is not in the vocabulary, so the size was lost entirely
        /// while the strip's generic size symbol contributed the word "Ship". The stat names are the
        /// game's own (<see cref="ShipStatNames"/> reads the same six on the full-sized panel); the
        /// role and size WORDS come from the provider the card was bound to, which is where the
        /// full-sized panel writes them from too (<c>PanelFeatureShipInfo.RefreshShipInformation</c>).
        /// </summary>
        private static Dictionary<AgeTransform, Naming> ShipDesignStatNames(
            PanelFeatureShipDesignInfoEmbedded design
        )
        {
            Dictionary<AgeTransform, Naming> named = new Dictionary<AgeTransform, Naming>();
            Name(named, design.ShipMovementPointsLabel, GuiShipDesign.ShipStatMovement);
            Name(named, design.ShipCommandPointsLabel, GuiShipDesign.ShipStatCommandPoints);
            Name(named, design.ShipManpowerLabel, GuiShipDesign.ShipStatManpower);
            Name(named, design.ShipOffensivePowerLabel, GuiShipDesign.ShipStatOffensiveMilitaryPower);
            Name(named, design.ShipDefensivePowerLabel, GuiShipDesign.ShipStatDefensiveMilitaryPower);

            IShipInfoProvider provider = Target(design) as IShipInfoProvider;
            if (provider != null)
            {
                Picture(
                    named,
                    design.ShipRoleIcon,
                    StatTitle(GuiShipDesign.ShipStatRole),
                    Title(provider.Role)
                );
                Picture(
                    named,
                    design.ShipSizeIcon,
                    StatTitle(GuiShipDesign.ShipStatSize),
                    Title(provider.Size)
                );
            }

            return named;
        }

        /// <summary>A picture that is standing in for a fact, said as the caption and the word the
        /// game has for what it is showing.</summary>
        private static void Picture(
            Dictionary<AgeTransform, Naming> named,
            AgePrimitiveImage image,
            string title,
            string value
        )
        {
            string text = TooltipText.Captioned(title, value);
            if (image != null && image.AgeTransform != null && !string.IsNullOrEmpty(text))
            {
                named[image.AgeTransform] = new Naming { Text = text };
            }
        }

        /// <summary>The game's word for one of its own named things - a hull size, a ship role.
        /// </summary>
        private static string Title(Amplitude.StaticString name)
        {
            try
            {
                return Amplitude.StaticString.IsNullOrEmpty(name)
                    ? null
                    : AgeText.Clean(Gui.GetTitle(name));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The thing a panel feature was bound to. The engine keeps it as a protected field
        /// and the prefabs that need it here do not hold on to the provider themselves, so it is read
        /// back off the base class rather than re-derived from what the feature drew.</summary>
        private static object Target(GuiPanelFeature feature)
        {
            try
            {
                if (_target == null)
                {
                    _target = typeof(Amplitude.Unity.Gui.GuiPanelFeature).GetField(
                        "target",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    );
                }

                return _target == null ? null : _target.GetValue(feature);
            }
            catch (Exception e)
            {
                Log.Warn("tooltip: reading a feature's target threw: " + e);
                return null;
            }
        }

        private static System.Reflection.FieldInfo _target;

        /// <summary>
        /// An invasion's two sides, each side's manpower given the game's word for manpower.
        ///
        /// The panel draws one row per opponent: the empire, then the manpower it is bringing, then
        /// one icon and count per troop type. The troop types name themselves - their textures ARE in
        /// the picture vocabulary - but the manpower total behind its own icon is bare, and it is
        /// drawn at the HEAD of the troop row, so the reading opened "120 Infantry 4", which states
        /// something false rather than merely losing a caption.
        /// </summary>
        private static Dictionary<AgeTransform, Naming> GroundBattleNames(
            PanelFeatureGroundBattleInfo battle
        )
        {
            Dictionary<AgeTransform, Naming> named = new Dictionary<AgeTransform, Naming>();
            Manpower(named, battle.AttackerItem);

            AgeTransform table = battle.DefenderItemsTable;
            List<AgeTransform> rows = table == null ? null : table.Children;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                Manpower(
                    named,
                    rows[i] == null ? null : rows[i].GetComponent<GroundBattleOpponentItem>()
                );
            }

            return named;
        }

        private static void Manpower(
            Dictionary<AgeTransform, Naming> named,
            GroundBattleOpponentItem opponent
        )
        {
            if (opponent != null)
            {
                Name(
                    named,
                    opponent.ManpowerAmount,
                    DepartmentOfTheTreasury.Resources.EmpireManpower
                );
            }
        }

        /// <summary>
        /// A minor civilization's card, whose four middle rows are captioned by a picture each.
        ///
        /// Personality, faction trait, relation and ally are drawn as an icon and one word, one under
        /// the other, and two of those words say nothing at all on their own ("UNKNOWN", "None").
        /// The captions are the game's own titles for the four facts - the same four the minor
        /// diplomacy screen reads its rows with, so a player meets one wording in both places.
        /// </summary>
        private static Dictionary<AgeTransform, Naming> MinorFactionNames(
            PanelFeatureMinorFaction faction
        )
        {
            Dictionary<AgeTransform, Naming> named = new Dictionary<AgeTransform, Naming>();
            MinorFactionCard card = faction.MinorFactionCard;
            if (card == null)
            {
                return named;
            }

            Name(named, card.MajorTraitLabel, AgeText.Title(MinorPersonalityTitle));
            Name(named, card.MinorTraitLabel, AgeText.Title(MinorFactionTraitTitle));
            Name(named, card.RelationLabel, AgeText.Title(MinorRelationTitle));
            Name(named, card.AllyLabel, AgeText.Title(MinorAllyTitle));
            return named;
        }

        private const string MinorPersonalityTitle = "%MinorFactionMajorTraitTitle";

        private const string MinorFactionTraitTitle = "%MinorFactionMinorTraitTitle";

        private const string MinorRelationTitle = "%MinorFactionRelationTitle";

        private const string MinorAllyTitle = "%MinorFactionCurrentAllyTitle";

        /// <summary>
        /// What a law asks of the party backing it, the ladder that requirement is a rung of, and - on
        /// a law the party is not yet good enough for - where it actually stands.
        ///
        /// The panel writes the REQUIREMENT in words and says everything else on a bar with no numbers
        /// on it: ticks for the party's own scale, and a notch, drawn only while the requirement is
        /// unmet, for where the party stands. So the two laws a player most needs to tell apart - the
        /// one that can be passed and the one that cannot - read identically, and the adjective the
        /// panel does write names a rung of a ladder nothing says the length of
        /// (<see cref="PoliticsExperience"/>).
        ///
        /// The naming is on the BOX the three are drawn in rather than on each of them, because the
        /// order they are read in is not the order they are laid out: the notch is a sibling drawn
        /// before the ticks and parked between two of them, so a reader that named each widget would
        /// read the party's standing in the middle of the ladder. One naming for the whole bar keeps
        /// the ladder whole and puts where-we-stand after it.
        ///
        /// A fact of its own (<see cref="TooltipPart.OwnLine"/>): the bar is drawn under the
        /// requirement's caption and value, and running them together would read the law's requirement
        /// and the party's standing as one sentence about neither.
        /// </summary>
        private static Dictionary<AgeTransform, Naming> PoliticsExperienceNames(
            PanelFeaturePoliticsExperiencePrerequisite feature
        )
        {
            Dictionary<AgeTransform, Naming> named = new Dictionary<AgeTransform, Naming>();
            AgeTransform bar = PoliticsExperience.Bar(feature);
            string text = PoliticsExperience.BarText(feature);
            if (bar == null || string.IsNullOrEmpty(text))
            {
                return named;
            }

            named[bar] = new Naming { Text = text, OwnLine = true };
            return named;
        }

        /// <summary>
        /// How many ships of each hull size, each count named by its size.
        ///
        /// The table holds one duplet per size - a symbol and a figure - in the order the feature
        /// fills it (<c>PanelFeatureGarrisonInfo.Initialize</c>), so the size a duplet stands for is
        /// its position and nothing in the duplet itself. A size the fleet has none of is drawn faded
        /// rather than dropped, which is why all four are read.
        /// </summary>
        private static void CountsBySize(
            Dictionary<AgeTransform, Naming> named,
            AgeTransform table
        )
        {
            if (table == null)
            {
                return;
            }

            try
            {
                List<AgeTransform> children = table.Children;
                for (int i = 0; i < children.Count && i < ShipSizes.Length; i++)
                {
                    ValueDuplet duplet =
                        children[i] == null ? null : children[i].GetComponent<ValueDuplet>();
                    if (duplet != null)
                    {
                        Name(named, duplet.Value, ShipSizes[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("tooltip: naming the ship counts threw: " + e);
            }
        }

        /// <summary>The hull sizes a garrison feature counts by, in the order it fills its table with
        /// them.</summary>
        private static readonly Amplitude.StaticString[] ShipSizes =
        {
            Ship.ShipSizeSmall,
            Ship.ShipSizeMedium,
            Ship.ShipSizeLarge,
            Ship.ShipSizeMothership,
        };

        /// <summary>
        /// The two military power figures of the fleet a gauge is drawn for, and the split each gauge
        /// draws - the same block a ship's tooltip carries, fed by the fleet's totals.
        ///
        /// The feature is the balance bars and these two numbers, and only the bars' shared caption is
        /// written in words - so without the naming the panel says "74" and "123" under
        /// "Projectile-Energy Balance", and without <see cref="Balance"/> the split the sighted player
        /// reads off the bars is never said at all.
        /// </summary>
        private static Dictionary<AgeTransform, Naming> PowerBalanceNames(
            PanelFeatureMilitaryPowerBalance power
        )
        {
            Dictionary<AgeTransform, Naming> named = new Dictionary<AgeTransform, Naming>();
            Name(named, power.OffenseLabel, GuiShipDesign.ShipStatOffensiveMilitaryPower);
            Name(named, power.DefenseLabel, GuiShipDesign.ShipStatDefensiveMilitaryPower);
            Balance(
                named,
                power.OffensiveBalanceGauge,
                GuiShipDesign.ShipStatOffensiveMilitaryPower
            );
            Balance(
                named,
                power.DefensiveBalanceGauge,
                GuiShipDesign.ShipStatDefensiveMilitaryPower
            );
            return named;
        }

        private static void Name(
            Dictionary<AgeTransform, Naming> named,
            AgePrimitiveLabel label,
            Amplitude.StaticString stat
        )
        {
            Name(named, label, StatTitle(stat));
        }

        /// <summary>Two whole phrases as the one line the reader hands over, joined by the
        /// translator's colon connective. Null where either half is missing, so a row the game left
        /// half-drawn goes on reading as whatever it drew.</summary>
        private static string Joined(string caption, string value)
        {
            return string.IsNullOrEmpty(caption) || string.IsNullOrEmpty(value)
                ? null
                : ModStrings.Format(ModStrings.CaptionedColon, caption.Trim(), value.Trim());
        }

        /// <summary>The same substitution the <c>Name</c> pair make, for a caller that has already
        /// composed the line.</summary>
        private static void NameText(
            Dictionary<AgeTransform, Naming> named,
            AgePrimitiveLabel label,
            string text
        )
        {
            if (label == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            named[label.AgeTransform] = new Naming { Text = text };
        }

        private static void Name(
            Dictionary<AgeTransform, Naming> named,
            AgePrimitiveLabel label,
            string title,
            bool ownLine = false
        )
        {
            if (label == null)
            {
                return;
            }

            try
            {
                named[label.AgeTransform] = new Naming
                {
                    Text = TooltipText.Captioned(title, AgeText.Label(label)),
                    OwnLine = ownLine,
                };
            }
            catch (Exception e)
            {
                Log.Warn("tooltip: naming a stat threw: " + e);
            }
        }

        /// <summary>
        /// What the game calls a stat.
        ///
        /// The element database is asked first, because that is where the game itself gets the word.
        /// One entry in it points at a string that was renamed and never repointed - command points
        /// declare "%ShipStatCommandsTitle", which no longer exists - and a key that did not resolve
        /// comes back looking exactly like itself, so the engine's own naming convention is the
        /// second try and silence is the third. A stat named "%ShipStatCommandsTitle" out loud would
        /// be worse than one with no name at all.
        /// </summary>
        private static string StatTitle(Amplitude.StaticString stat)
        {
            return AgeText.Title(Gui.GetTitle(stat)) ?? AgeText.Title("%" + stat + "Title");
        }

        /// <summary>What the fleet list calls a fleet's command points - preferred over the ship stat
        /// of the same name so that a fleet is described in the words the fleet rows already use.
        /// </summary>
        private const string CommandPointsTitle = "%FleetListTableCommandPointsTitle";

        // ---- the hero card ----

        /// <summary>
        /// A hero's card, where three of the figures are drawn away from the words that name them.
        ///
        /// The level is the awkward one: its caption is a prefab of its own laid out one row ABOVE the
        /// figure (<c>HeroDetailedCard.RefreshExperience</c>), so the drawn rows pair "Level" with the
        /// affinity beside it and the level itself with the hero's class - two lines, neither of them
        /// true. The pairing is therefore made here, by field, and the result is marked as a fact of
        /// its own so the class's row does not swallow it.
        ///
        /// The masteries are the other: the row prefab has no label for the skill's name at all
        /// (<c>HeroMasteryLine</c> leaves <c>ClassTitle</c> null in the tooltip's version of it), and
        /// the name lives on the wrapper the row hands its own tooltip. That wrapper is where it is
        /// read from - the alternative, walking the mastery database in the order the panel fills its
        /// rows, gets the same four words by trusting two orders to agree.
        /// </summary>
        private static Dictionary<AgeTransform, Naming> HeroCardNames(PanelFeatureHeroInfo hero)
        {
            Dictionary<AgeTransform, Naming> named = new Dictionary<AgeTransform, Naming>();
            HeroDetailedCard card = hero.Card;
            if (card == null)
            {
                return named;
            }

            Name(named, card.LevelLabel, HeroCards.LevelCaption(), true);
            Silence(named, Caption(hero.AgeTransform, HeroCards.LevelTitle, 0));
            Name(named, card.UpkeepLabel, HeroCards.UpkeepCaption());
            Masteries(named, card.HeroMasteryPanel);
            return named;
        }

        /// <summary>The label a prefab drew a translation key into, which is how a caption with no
        /// field of its own is found. The key is compared, not the translated words, so this holds in
        /// every language.</summary>
        private static AgeTransform Caption(AgeTransform widget, string key, int depth)
        {
            if (widget == null || depth > MaxDepth)
            {
                return null;
            }

            try
            {
                AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
                if (label != null && label.Text == key)
                {
                    return widget;
                }

                List<AgeTransform> children = widget.Children;
                for (int i = 0; i < children.Count; i++)
                {
                    AgeTransform found = Caption(children[i], key, depth + 1);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("tooltip: looking for a caption threw: " + e);
            }

            return null;
        }

        /// <summary>A widget whose words have been said somewhere better says nothing here.</summary>
        private static void Silence(Dictionary<AgeTransform, Naming> named, AgeTransform widget)
        {
            if (widget != null)
            {
                named[widget] = new Naming();
            }
        }

        /// <summary>Each mastery's level given the name of the skill it measures, taken off the
        /// wrapper the row built for its own tooltip.</summary>
        private static void Masteries(
            Dictionary<AgeTransform, Naming> named,
            HeroMasteryPanel panel
        )
        {
            if (panel == null || panel.MasteryLinesContainer == null)
            {
                return;
            }

            try
            {
                List<AgeTransform> lines = panel.MasteryLinesContainer.Children;
                for (int i = 0; i < lines.Count; i++)
                {
                    HeroMasteryLine line =
                        lines[i] == null ? null : lines[i].GetComponent<HeroMasteryLine>();
                    if (line == null || line.Tooltip == null)
                    {
                        continue;
                    }

                    GuiHeroSkillMastery mastery = line.Tooltip.Target as GuiHeroSkillMastery;
                    if (mastery != null)
                    {
                        Name(named, line.LevelLabel, AgeText.Clean(mastery.Title));
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("tooltip: naming a hero's masteries threw: " + e);
            }
        }

        // ---- the blocks of effects ----

        /// <summary>
        /// What a skill, a hero, a planet or an honor action would DO, read as the stack of blocks the
        /// panel draws it as.
        ///
        /// The feature is a caption ("Effects:") over a table of blocks, each block a situation the
        /// effects apply in over one line per effect
        /// (<c>PanelFeatureEffectsSets.Bind</c> fills the table from one prefab,
        /// <c>PanelFeatureEffectsSetsItem.Bind</c> fills a block). Bands cannot express it: the SKILL
        /// variant draws the level this block is about in a narrow column down the left, one row per
        /// block, so the caption "Level" bands with the first block's situation and the figure under it
        /// bands with that block's first effect - "Level On System (if assigned)" and
        /// "1 +1 Industry per Population on Planets", every word present and every one of them attached
        /// to the wrong thing. The blocks are therefore walked as the game filled them, and the level
        /// said once, in front of the block it belongs to.
        ///
        /// A tooltip with two levels in it is two of these features side by side
        /// (<c>SkillTreeEditionPanel</c> binds a current-level panel and a next-level one), each read
        /// on its own, so "Level 1" and "Level 2" fall out of reading them in order.
        /// </summary>
        private static void EffectSets(PanelFeatureEffectsSets sets, List<string> lines)
        {
            TooltipText.AddLines(lines, AgeText.Label(sets.TitleLabel));

            PanelFeatureSkillEffectsSets skill = sets as PanelFeatureSkillEffectsSets;
            if (skill != null)
            {
                TooltipText.AddLines(
                    lines,
                    TooltipText.Captioned(HeroCards.LevelCaption(), AgeText.Label(skill.LevelLabel))
                );
            }

            AgeTransform table = sets.SetsTable;
            List<AgeTransform> blocks = table == null ? null : table.Children;
            for (int i = 0; blocks != null && i < blocks.Count; i++)
            {
                AgeTransform drawnBlock = AgeWidgets.DrawnChild(blocks, i);
                if (drawnBlock == null)
                {
                    continue;
                }

                PanelFeatureEffectsSetsItem block =
                    drawnBlock.GetComponent<PanelFeatureEffectsSetsItem>();
                if (block == null)
                {
                    continue;
                }

                TooltipText.AddLines(lines, AgeText.Label(block.TitleLabel));
                AgeTransform effects = block.EffectMapper == null
                    ? null
                    : block.EffectMapper.EffectLinesTable;
                List<AgeTransform> drawn = effects == null ? null : effects.Children;
                for (int line = 0; drawn != null && line < drawn.Count; line++)
                {
                    // The table retires a line it no longer needs by fading it out rather than hiding
                    // it (GuiEffectMapper.UnloadEffects), so a block that shrank still holds the
                    // previous binding's words in a child that is still Visible.
                    AgeTransform effect = AgeWidgets.DrawnChild(drawn, line);
                    if (effect != null)
                    {
                        TooltipText.AddLines(
                            lines,
                            AgeText.Label(effect.GetComponent<AgePrimitiveLabel>())
                        );
                    }
                }
            }
        }

        // ---- a constellation's dossier ----

        /// <summary>
        /// What the map's own dossier on a stretch of sky says: who holds it, who found it, how far
        /// off holding it the player is, and what holding it is worth.
        ///
        /// Four separate facts, and the panel writes each of them into a LABEL OF ITS OWN
        /// (<c>PanelFeatureConstellationControl.Bind</c>) - so the feature already knows where the
        /// lines are and there is nothing for a geometric reading to work out. Which is what makes
        /// this one worth typing: the label the game hangs a constellation's name on is CULLED at
        /// every camera position the player ever plays at (ES2 facts), so the panel is only ever drawn
        /// with its rows unmeasured - every rect reads (0,0,0,0) - and row banding, having nothing to
        /// band by, fuses all four facts into one line. Read off the feature's own fields the answer
        /// does not depend on the panel having been laid out at all.
        ///
        /// The bonus block is the one conditional half: a constellation whose ownership grants nothing
        /// has its caption and its effect table HIDDEN rather than emptied, and the effect lines
        /// themselves are retired by FADING (<c>GuiEffectMapper.UnloadEffects</c>), so a block that
        /// shrank still holds the previous binding's words in children that are still Visible.
        /// </summary>
        private static void ConstellationDossier(
            PanelFeatureConstellationControl panel,
            List<string> lines
        )
        {
            AddLabel(lines, panel.OwnerLabel);
            AddLabel(lines, panel.DiscovererLabel);
            AddLabel(lines, panel.OwnershipControlLabel);
            AddLabel(lines, panel.ConstellationBonusLabel);

            GuiEffectMapper mapper = panel.ConstellationEffectMapper;
            // Content: the constellation's effects are this constellation's only while the mapper is drawn.
            if (mapper == null || mapper.AgeTransform == null || !mapper.AgeTransform.Visible)
            {
                return;
            }

            AgeTransform table = mapper.EffectLinesTable;
            List<AgeTransform> drawn = table == null ? null : table.Children;
            for (int i = 0; drawn != null && i < drawn.Count; i++)
            {
                AgeTransform effect = AgeWidgets.DrawnChild(drawn, i);
                if (effect != null)
                {
                    TooltipText.AddLines(
                        lines,
                        AgeText.Label(effect.GetComponent<AgePrimitiveLabel>())
                    );
                }
            }
        }

        /// <summary>One of a feature's own labels as its own line, skipped where the feature has
        /// switched that label off.
        ///
        /// Every one of the four is a CAPTION and a value ("Owner: No owner"), and the game bullets
        /// two of them with a picture of what the caption is about - a crown, an explorer - which is
        /// the caption's own word said twice over. So the line is read without the icon it opens with
        /// (<see cref="AgeText.LabelWithoutLeadingIcon"/>, owner ruling 2026-08-20); the icons written
        /// INSIDE these sentences, which stand in for nouns the sentence has not got, are untouched -
        /// the ownership line still counts star systems.</summary>
        private static void AddLabel(List<string> lines, AgePrimitiveLabel label)
        {
            if (label != null && label.Visible)
            {
                TooltipText.AddLines(lines, AgeText.LabelWithoutLeadingIcon(label));
            }
        }

        /// <summary>The split a balance bar draws, as the words the bar itself never writes - see
        /// <see cref="BalanceGauges"/>, which is also what the ship designer's own copy of this bar
        /// reads through. This prefab draws no captions over the two columns, only the block heading,
        /// so without the substitution the split a sighted player reads off the bar is never said.
        ///
        /// The block draws TWO of these bars, one for each military power, and both take the same pair
        /// of side words - so the split alone said the same sentence twice over ("Projectile 100%,
        /// Projectile 100%") with nothing to tell the weapons balance from the defences balance. Each
        /// bar is therefore given the name of the power it breaks down, which is the figure the game
        /// draws immediately beside it and the thing its own hover sentence says it is the balance of.
        /// The sentence itself stays a sentence: it hangs on the bar as a nested tooltip the drawn
        /// panel never writes out, and it is how the ship designer's own copy of the bar - a stop of
        /// its own, where the tooltip is announced - already tells its two bars apart.</summary>
        private static void Balance(
            Dictionary<AgeTransform, Naming> named,
            RepartitionHorizontalGauge gauge,
            Amplitude.StaticString power
        )
        {
            if (gauge == null)
            {
                return;
            }

            string text = TooltipText.Captioned(StatTitle(power), BalanceGauges.Text(gauge));
            if (!string.IsNullOrEmpty(text))
            {
                named[gauge.AgeTransform] = new Naming { Text = text };
            }
        }
    }
}
