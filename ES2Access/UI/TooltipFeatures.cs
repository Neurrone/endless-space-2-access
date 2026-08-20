using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using UnityEngine;

namespace ES2Access.UI
{
    /// <summary>
    /// Reading one panel feature of a drawn tooltip.
    ///
    /// Every tooltip in the game is assembled the same way: the tooltip's CLASS is looked up in a
    /// description database, which names an ordered list of little prefabs, and each prefab is
    /// handed the thing the tooltip is about and writes its own piece of the panel
    /// (<c>GuiTooltipWindow.AddPanelFeature</c>). There are 149 of those classes, and a feature that
    /// wants sub-features gets them added as further SIBLINGS in the same table, so what the window
    /// is showing is always one flat, ordered list of features.
    ///
    /// A feature is therefore the unit a tooltip is really made of, and the unit a reader should
    /// work in. The predecessor of this file did not: it banded the whole window into drawn rows and
    /// read each band. That is right about a panel most of the time and wrong in the two places
    /// where a feature does not lay its words out in a line - a caption drawn above the value that
    /// belongs to it, a strip of repeated items whose captions and numbers sit in two rows - and the
    /// failure is silent, because every word is there, just divorced from the number it names. The
    /// ship design tooltip read "1500/1500 5/5" and "Long Medium Short" then "10% 50% 95%".
    ///
    /// So the reading is scoped: a feature's own subtree is banded, never the window. Two shapes the
    /// bands cannot express are recognised by what the game itself does rather than by a list of
    /// class names - a run of items spawned from one prefab, and a bar that draws a proportion and
    /// writes no number - and five features have readers of their own. Four of them because the game
    /// gives their numbers names that are nowhere in the panel: a ship's stats, a fleet's, the two
    /// military power figures, and a hero's card. The fifth is the stack of effect blocks a skill or a
    /// planet would gain, whose captions are drawn in a column BESIDE the blocks rather than in them.
    /// Everything else keeps the banding, which is what it was
    /// always right about, and a feature nobody has written a reader for lands there too. Which
    /// reader each feature used is reported so a gap shows up in a probe rather than in the player's
    /// ears.
    ///
    /// Main-thread only.
    /// </summary>
    public static class TooltipFeatures
    {
        /// <summary>How deep inside a feature to look for its words.</summary>
        private const int MaxDepth = 8;

        /// <summary>What one panel feature said, and how it was read.</summary>
        public struct Reading
        {
            /// <summary>The feature class the game instantiated - the component's own type, not the
            /// prefab's object name, which the game varies for the same class.</summary>
            public string Feature;

            /// <summary>Which reader answered: "default" for the scoped banding every unknown
            /// feature falls back to, the name of a typed one, or "skip" where the feature draws
            /// nothing to read.</summary>
            public string Reader;

            public List<string> Lines;
        }

        /// <summary>
        /// What the feature rooted at <paramref name="widget"/> is saying.
        ///
        /// A feature the window is not showing says nothing, and that is load-bearing rather than
        /// tidy: the window POOLS its features rather than destroying them, so a tooltip that once
        /// had six still has the other four hanging off it holding whatever was hovered before. The
        /// caller filters those; this reads what it is given.
        /// </summary>
        public static Reading Read(AgeTransform widget)
        {
            Reading reading = new Reading
            {
                Feature = "(none)",
                Reader = "skip",
                Lines = new List<string>(),
            };

            try
            {
                GuiPanelFeature feature =
                    widget == null ? null : widget.GetComponent<GuiPanelFeature>();
                if (feature != null)
                {
                    reading.Feature = feature.GetType().Name;
                }

                // The game's own two "this draws no words" flags, asked of the feature instead of
                // guessed from a three-pixel-high rectangle full of nothing.
                if (widget == null || (feature != null && (feature.IsSeparator || feature.IsSpacing)))
                {
                    return reading;
                }

                PanelFeatureEffectsSets sets = feature as PanelFeatureEffectsSets;
                if (sets != null)
                {
                    reading.Reader = "effect-sets";
                    EffectSets(sets, reading.Lines);
                    return reading;
                }

                PanelFeatureConstellationControl sky =
                    feature as PanelFeatureConstellationControl;
                if (sky != null)
                {
                    reading.Reader = "constellation";
                    ConstellationDossier(sky, reading.Lines);
                    return reading;
                }

                PanelFeatureShipInfo ship = feature as PanelFeatureShipInfo;
                PanelFeatureGarrisonInfo garrison = feature as PanelFeatureGarrisonInfo;
                PanelFeatureMilitaryPowerBalance power =
                    feature as PanelFeatureMilitaryPowerBalance;
                PanelFeatureHeroInfo hero = feature as PanelFeatureHeroInfo;

                Dictionary<AgeTransform, Naming> named = null;
                if (ship != null)
                {
                    reading.Reader = "ship-stats";
                    named = ShipStatNames(ship);
                }
                else if (garrison != null)
                {
                    reading.Reader = "garrison-stats";
                    named = GarrisonStatNames(garrison);
                }
                else if (power != null)
                {
                    reading.Reader = "power-balance";
                    named = PowerBalanceNames(power);
                }
                else if (hero != null)
                {
                    reading.Reader = "hero-card";
                    named = HeroCardNames(hero);
                }
                else
                {
                    reading.Reader = "default";
                }

                bool items;
                ReadScoped(widget, named, reading.Lines, out items);
                if (items)
                {
                    reading.Reader += "+items";
                }

                return reading;
            }
            catch (Exception e)
            {
                Log.Warn("tooltip: reading a panel feature threw: " + e);
                reading.Reader = "failed";
                return reading;
            }
        }

        // ---- the default reader ----

        /// <summary>
        /// Everything the feature drew, grouped into the rows it drew them in and read across.
        ///
        /// Scoping this to the feature is the whole change. Banding across the window let a value at
        /// the top of one feature share a band with a caption at the bottom of the one above it -
        /// and, worse, let a feature's own caption fail to reach its own value because a full-width
        /// background belonging to a third feature sat between their middles.
        /// </summary>
        private static void ReadScoped(
            AgeTransform root,
            Dictionary<AgeTransform, Naming> named,
            List<string> lines,
            out bool items
        )
        {
            List<Entry> entries = new List<Entry>();
            items = false;
            Gather(root, named, entries, 0, ref items);
            foreach (List<Entry> row in AgeLayout.Rows(entries, EntryWidget))
            {
                ReadRow(row, lines);
            }
        }

        /// <summary>One drawn row of a feature said as the line it is.</summary>
        private static void ReadRow(List<Entry> row, IList<string> lines)
        {
            // A picture beside a value that has just been given its name in words says the same
            // thing twice: the manpower symbol is only there because the panel wrote no word for
            // "30/30", and once the reader has written one it is decoration.
            bool spoken = false;
            for (int i = 0; i < row.Count; i++)
            {
                spoken |= row[i].Named;
            }

            List<TooltipPart> parts = new List<TooltipPart>();
            for (int i = 0; i < row.Count; i++)
            {
                if (!spoken || !row[i].Icon)
                {
                    parts.Add(new TooltipPart(row[i].Text, row[i].Icon, row[i].Alone));
                }
            }

            TooltipText.AddRow(lines, parts);
        }

        /// <summary>One thing a feature drew that a reader has to account for, still carrying its
        /// transform - grouping into rows needs the rectangle, which is gone once the text has been
        /// read out of it.</summary>
        private struct Entry
        {
            public AgeTransform Widget;
            public string Text;
            public bool Icon;

            /// <summary>Set where a typed reader wrote this text rather than the panel.</summary>
            public bool Named;

            /// <summary>Set where the text is a fact of its own that merely landed in this row.
            /// </summary>
            public bool Alone;
        }

        /// <summary>What a typed reader has decided a widget really says.</summary>
        private struct Naming
        {
            public string Text;

            /// <summary>Whether the row this widget was drawn in belongs to something else - see
            /// <see cref="TooltipPart.OwnLine"/>.</summary>
            public bool OwnLine;
        }

        private static readonly Func<Entry, AgeTransform> EntryWidget = entry => entry.Widget;

        /// <summary>
        /// Every word and every named picture under a widget, and the two things that are neither.
        ///
        /// <paramref name="named"/> is how a typed reader hands a widget its meaning: a label the
        /// panel drew as a bare number, mapped to that number with the name of the stat in front of
        /// it, or mapped to nothing where a second widget has already spoken for it. The
        /// substitution happens here rather than in the typed reader so that row grouping, reading
        /// order and the icon rules go on applying to the result unchanged.
        /// </summary>
        private static void Gather(
            AgeTransform widget,
            Dictionary<AgeTransform, Naming> named,
            List<Entry> entries,
            int depth,
            ref bool items
        )
        {
            if (depth > MaxDepth)
            {
                return;
            }

            Naming replacement;
            if (named != null && named.TryGetValue(widget, out replacement))
            {
                if (!string.IsNullOrEmpty(replacement.Text))
                {
                    entries.Add(
                        new Entry
                        {
                            Widget = widget,
                            Text = replacement.Text,
                            Icon = false,
                            Named = true,
                            Alone = replacement.OwnLine,
                        }
                    );
                }

                return;
            }

            AgePrimitiveLabel label = widget.GetComponent<AgePrimitiveLabel>();
            string text = label != null ? AgeText.Label(label) : PictureName(widget);
            if (!string.IsNullOrEmpty(text))
            {
                entries.Add(new Entry { Widget = widget, Text = text, Icon = label == null });
            }

            // The engine's own test for "the player can see this child", asked the way the engine
            // asks it: transparent counts as hidden unless the parent has declared otherwise.
            List<AgeTransform> shown = new List<AgeTransform>();
            List<AgeTransform> children = widget.Children;
            for (int i = 0; i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child != null && child.Visible && (widget.StrictVisibility || child.Alpha > 0f))
                {
                    shown.Add(child);
                }
            }

            if (Repeated(shown))
            {
                items = true;
                AddItems(widget, shown, named, entries, depth);
                return;
            }

            for (int i = 0; i < shown.Count; i++)
            {
                Gather(shown[i], named, entries, depth + 1, ref items);
            }
        }

        /// <summary>
        /// What a widget that draws no words is called, when it is drawing something that stands for
        /// a word at all.
        ///
        /// A stat strip is laid out as icons and numbers in alternating widgets, and the icon is
        /// where the meaning is: "36 37 38 22 9" is what a reader that only looks at labels gets
        /// from a row the player sees as five named quantities. The picture is not decoration there,
        /// it is the column heading. Which is exactly what has to be told apart, because the same
        /// panel draws backgrounds, rules and portraits and none of those is a word anybody wants
        /// read. The test is the icon table (<see cref="IconNames.NameForAsset"/>).
        /// </summary>
        private static string PictureName(AgeTransform widget)
        {
            AgePrimitiveImage image = widget.GetComponent<AgePrimitiveImage>();
            Texture texture = image == null ? null : image.Texture;
            return texture == null ? null : IconNames.NameForAsset(texture.name);
        }

        // ---- repeated items ----

        /// <summary>
        /// Whether these siblings are one item drawn several times.
        ///
        /// A feature that repeats something spawns its items from ONE prefab
        /// (<c>AgeTransform.ReserveChildren</c>), so every item carries the same script the game
        /// wrote for that prefab - a <c>RangeEfficiencyItem</c> and its kin. Two or more siblings
        /// sharing one is the game saying "the same shape, N times", which is exactly the shape
        /// drawn rows cannot express: the items' captions land in one band and their values in the
        /// next, so banding reads every caption and then every number.
        ///
        /// The engine's own building blocks are not that signal - every label in the game is an
        /// <c>AgePrimitiveLabel</c> - so it takes a behaviour written for this prefab, and a nested
        /// panel feature does not count either: those are read as features in their own right.
        /// </summary>
        private static bool Repeated(List<AgeTransform> widgets)
        {
            if (widgets.Count < 2)
            {
                return false;
            }

            Type kind = ItemKind(widgets[0]);
            if (kind == null)
            {
                return false;
            }

            for (int i = 1; i < widgets.Count; i++)
            {
                if (ItemKind(widgets[i]) != kind)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The script the game wrote for this widget's prefab, if there is one. The game's
        /// own scripts sit on a base of their own (<c>GuiBehaviour</c> in the global namespace) that
        /// panel features do not share, so asking for that base already excludes a nested feature as
        /// well as every engine primitive.</summary>
        private static Type ItemKind(AgeTransform widget)
        {
            GuiBehaviour[] behaviours = widget.GetComponents<GuiBehaviour>();
            return behaviours.Length == 0 ? null : behaviours[0].GetType();
        }

        /// <summary>
        /// A run of identical items, each read as the one phrase it is drawn as - unless it is drawn
        /// as several lines, in which case it is read as those.
        ///
        /// A strip laid out ACROSS the panel is one fact with several parts - three range bands, a
        /// row of costs - and reads as one line with the items separated. Items STACKED down the
        /// panel are several facts and read one to a line, which the banding does for free once each
        /// item is a single entry. Which it is comes from where the game put them, so nothing has to
        /// be declared per feature.
        ///
        /// The item that breaks that is the one the repeated prefab is a whole SECTION: a module's
        /// effects are one group prefab per section (<c>PanelFeatureModuleEffects.RefreshTooltip</c>
        /// reserves its children from one <c>GroupPrefab</c>), and a section is a heading over half a
        /// dozen caption-and-value rows. Running an item like that together made "Damages Damage per
        /// Second 22 Critical Hit Chance 5% Weapon Type Projectile …" one line for a player who can see
        /// seven. So an item that is itself drawn over several rows is left to the ordinary banding,
        /// which reads the rows it drew; the phrase is kept for the item that really is one line.
        /// </summary>
        private static void AddItems(
            AgeTransform table,
            List<AgeTransform> items,
            Dictionary<AgeTransform, Naming> named,
            List<Entry> entries,
            int depth
        )
        {
            List<string> phrases = new List<string>();
            List<AgeTransform> said = new List<AgeTransform>();
            List<int> drawn = new List<int>();
            for (int i = 0; i < items.Count; i++)
            {
                int rows;
                string phrase = Phrase(items[i], named, out rows);
                if (!string.IsNullOrEmpty(phrase))
                {
                    phrases.Add(phrase);
                    said.Add(items[i]);
                    drawn.Add(rows);
                }
            }

            if (phrases.Count == 0)
            {
                return;
            }

            if (SideBySide(said))
            {
                entries.Add(
                    new Entry { Widget = table, Text = TooltipText.Items(phrases), Icon = false }
                );
                return;
            }

            for (int i = 0; i < phrases.Count; i++)
            {
                if (drawn[i] > 1)
                {
                    bool nested = false;
                    Gather(said[i], named, entries, depth + 1, ref nested);
                    continue;
                }

                entries.Add(new Entry { Widget = said[i], Text = phrases[i], Icon = false });
            }
        }

        /// <summary>
        /// One item's words as the single phrase it is drawn as.
        ///
        /// An item is small enough that its own rows are never separate facts - a picture beside a
        /// number, a caption above the number it names - so it is banded like anything else and the
        /// bands are then run together. Banding rather than sorting by position is what keeps the
        /// picture in front of its number: an icon and the value beside it are routinely offset by
        /// three pixels, and read down-then-across that offset puts the number first.
        ///
        /// A typed reader's names reach in here too, because what an item is drawn with is routinely
        /// the only bare number in it: the four ship-size counts and the four hero masteries are each
        /// an icon and a figure, and the word for the icon is in the game's data, not in the panel.
        ///
        /// <paramref name="rows"/> is how many lines the item was DRAWN over, which is what decides
        /// whether running it together is a fair reading of it at all - see <see cref="AddItems"/>.
        /// </summary>
        private static string Phrase(
            AgeTransform item,
            Dictionary<AgeTransform, Naming> named,
            out int rows
        )
        {
            List<Entry> entries = new List<Entry>();
            bool nested = false;
            Gather(item, named, entries, 0, ref nested);
            List<List<Entry>> banded = AgeLayout.Rows(entries, EntryWidget);
            rows = banded.Count;

            List<string> lines = new List<string>();
            for (int i = 0; i < banded.Count; i++)
            {
                ReadRow(banded[i], lines);
            }

            return TooltipText.Phrase(lines);
        }

        /// <summary>Whether the items are drawn along a row rather than down a column.</summary>
        private static bool SideBySide(List<AgeTransform> items)
        {
            if (items.Count < 2)
            {
                return false;
            }

            for (int i = 1; i < items.Count; i++)
            {
                if (!AgeLayout.SameRow(items[0], items[i]))
                {
                    return false;
                }
            }

            return true;
        }

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
            Balance(named, ship.OffensiveBalanceGauge);
            Balance(named, ship.DefensiveBalanceGauge);
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
            Name(named, garrison.CommandValue, Word(CommandPointsTitle));
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
        /// The two military power figures of the fleet a gauge is drawn for.
        ///
        /// The feature is the balance bars and these two numbers, and only the bars' shared caption is
        /// written in words - so without this the panel says "74" and "123" under "Projectile-Energy
        /// Balance". The bars themselves are left to the default reader, which says nothing for them:
        /// they carry no text, and the split they draw is about which weapon types make up the power,
        /// not about the power.
        /// </summary>
        private static Dictionary<AgeTransform, Naming> PowerBalanceNames(
            PanelFeatureMilitaryPowerBalance power
        )
        {
            Dictionary<AgeTransform, Naming> named = new Dictionary<AgeTransform, Naming>();
            Name(named, power.OffenseLabel, GuiShipDesign.ShipStatOffensiveMilitaryPower);
            Name(named, power.DefenseLabel, GuiShipDesign.ShipStatDefensiveMilitaryPower);
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
            string title = AgeText.Clean(Gui.GetTitle(stat));
            if (Unresolved(title))
            {
                title = AgeText.Clean("%" + stat + "Title");
            }

            return Unresolved(title) ? null : title;
        }

        /// <summary>A word the game keeps under a translation key of its own rather than on an element
        /// - a column heading, a card's caption. Silence rather than the key, for the same reason
        /// <see cref="StatTitle"/> ends in silence.</summary>
        private static string Word(string key)
        {
            string title = AgeText.Clean(key);
            return Unresolved(title) ? null : title;
        }

        private static bool Unresolved(string title)
        {
            return string.IsNullOrEmpty(title) || title[0] == '%';
        }

        /// <summary>What the fleet list calls a fleet's command points - preferred over the ship stat
        /// of the same name so that a fleet is described in the words the fleet rows already use.
        /// </summary>
        private const string CommandPointsTitle = "%FleetListTableCommandPointsTitle";

        /// <summary>The caption a hero's card draws ABOVE the level it belongs to, and the one it
        /// draws beside a bare upkeep figure as a picture.</summary>
        private const string HeroLevelTitle = "%HeroCardLevelTitle";

        private const string HeroUpkeepTitle = "%HeroCardUpkeepTitle";

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

            Name(named, card.LevelLabel, Word(HeroLevelTitle), true);
            Silence(named, Caption(hero.AgeTransform, HeroLevelTitle, 0));
            Name(named, card.UpkeepLabel, Word(HeroUpkeepTitle));
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
                    TooltipText.Captioned(Word(HeroLevelTitle), AgeText.Label(skill.LevelLabel))
                );
            }

            AgeTransform table = sets.SetsTable;
            List<AgeTransform> blocks = table == null ? null : table.Children;
            for (int i = 0; blocks != null && i < blocks.Count; i++)
            {
                if (!AgeWidgets.Paints(table, blocks[i]))
                {
                    continue;
                }

                PanelFeatureEffectsSetsItem block =
                    blocks[i].GetComponent<PanelFeatureEffectsSetsItem>();
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
                    if (AgeWidgets.Paints(effects, drawn[line]))
                    {
                        TooltipText.AddLines(
                            lines,
                            AgeText.Label(drawn[line].GetComponent<AgePrimitiveLabel>())
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
        /// every camera position the player ever plays at (es2-facts), so the panel is only ever drawn
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
            if (mapper == null || mapper.AgeTransform == null || !mapper.AgeTransform.Visible)
            {
                return;
            }

            AgeTransform table = mapper.EffectLinesTable;
            List<AgeTransform> drawn = table == null ? null : table.Children;
            for (int i = 0; drawn != null && i < drawn.Count; i++)
            {
                if (AgeWidgets.Paints(table, drawn[i]))
                {
                    TooltipText.AddLines(
                        lines,
                        AgeText.Label(drawn[i].GetComponent<AgePrimitiveLabel>())
                    );
                }
            }
        }

        /// <summary>One of a feature's own labels as its own line, skipped where the feature has
        /// switched that label off.</summary>
        private static void AddLabel(List<string> lines, AgePrimitiveLabel label)
        {
            if (label != null && label.Visible)
            {
                TooltipText.AddLines(lines, AgeText.Label(label));
            }
        }

        /// <summary>
        /// A bar split between two things, read as the split.
        ///
        /// The gauge writes no text at all: it says what it says by how far each half is drawn out
        /// from the centre, in percent (<c>RepartitionHorizontalGauge.Refresh</c>). A half worth
        /// nothing is left at the centre and hidden, so a bar with neither half drawn is a bar about
        /// nothing and the line is dropped rather than read as "0% to 0%". The caption above the bar
        /// is what names the two sides, and it names them in this order.
        /// </summary>
        private static void Balance(
            Dictionary<AgeTransform, Naming> named,
            RepartitionHorizontalGauge gauge
        )
        {
            if (gauge == null || gauge.LeftGauge == null || gauge.RightGauge == null)
            {
                return;
            }

            try
            {
                bool left = gauge.LeftGauge.Visible;
                bool right = gauge.RightGauge.Visible;
                if (!left && !right)
                {
                    return;
                }

                named[gauge.AgeTransform] = new Naming
                {
                    Text = ModStrings.Format(
                        ModStrings.TooltipBalance,
                        Percent(left ? 50f - gauge.LeftGauge.PercentLeft : 0f),
                        Percent(right ? gauge.RightGauge.PercentRight - 50f : 0f)
                    ),
                };
            }
            catch (Exception e)
            {
                Log.Warn("tooltip: reading a balance gauge threw: " + e);
            }
        }

        /// <summary>How far one half of the bar was pushed out, as a share of the half it could have
        /// filled.</summary>
        private static string Percent(float half)
        {
            return Mathf.RoundToInt(half * 2f) + "%";
        }
    }
}
