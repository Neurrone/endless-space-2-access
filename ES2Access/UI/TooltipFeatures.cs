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
    public static partial class TooltipFeatures
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

                PanelFeaturePlayDeck deck = feature as PanelFeaturePlayDeck;
                PanelFeatureShipInfo ship = feature as PanelFeatureShipInfo;
                PanelFeatureGarrisonInfo garrison = feature as PanelFeatureGarrisonInfo;
                PanelFeatureMilitaryPowerBalance power =
                    feature as PanelFeatureMilitaryPowerBalance;
                PanelFeatureHeroInfo hero = feature as PanelFeatureHeroInfo;
                PanelFeatureShipDesignInfoEmbedded design =
                    feature as PanelFeatureShipDesignInfoEmbedded;
                PanelFeatureGarrisonCompactInfoEmbedded compact =
                    feature as PanelFeatureGarrisonCompactInfoEmbedded;
                PanelFeatureGarrisonInfoAutomatedFleet automated =
                    feature as PanelFeatureGarrisonInfoAutomatedFleet;
                PanelFeatureAdditionalGarrisons more = feature as PanelFeatureAdditionalGarrisons;
                PanelFeatureGroundBattleInfo invasion = feature as PanelFeatureGroundBattleInfo;
                PanelFeatureMinorFaction minor = feature as PanelFeatureMinorFaction;
                PanelFeaturePoliticsExperiencePrerequisite standing =
                    feature as PanelFeaturePoliticsExperiencePrerequisite;

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
                else if (compact != null)
                {
                    reading.Reader = "garrison-compact";
                    named = CompactGarrisonNames(compact);
                }
                else if (automated != null)
                {
                    reading.Reader = "garrison-automated";
                    named = AutomatedFleetNames(automated);
                }
                else if (more != null)
                {
                    reading.Reader = "more-fleets";
                    named = MoreFleetsNames(more);
                }
                else if (design != null)
                {
                    reading.Reader = "ship-design-stats";
                    named = ShipDesignStatNames(design);
                }
                else if (invasion != null)
                {
                    reading.Reader = "ground-battle";
                    named = GroundBattleNames(invasion);
                }
                else if (minor != null)
                {
                    reading.Reader = "minor-faction";
                    named = MinorFactionNames(minor);
                }
                else if (standing != null)
                {
                    reading.Reader = "politics-experience";
                    named = PoliticsExperienceNames(standing);
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
                else if (deck != null)
                {
                    reading.Reader = "play-deck";
                    named = PlayDeckNames(deck);
                }
                else
                {
                    reading.Reader = "default";
                    Defaulted(reading.Feature);
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

            // The engine's own test for "the player can see this child"
            // (<see cref="AgeWidgets.DrawnChild"/>). StrictVisibility is no exemption: it tells the
            // ARRANGER to keep counting a faded child's slot, and the renderer skips it all the same.
            List<AgeTransform> shown = new List<AgeTransform>();
            List<AgeTransform> children = widget.Children;
            for (int i = 0; i < children.Count; i++)
            {
                AgeTransform child = AgeWidgets.DrawnChild(children, i);
                if (child != null)
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

        // ---- which features nobody has written a reader for ----

        /// <summary>
        /// The feature classes that have been read by the DEFAULT reader since this assembly loaded.
        ///
        /// The default reader is not a defect - most features lay their words out in rows and the
        /// banding is right about them - but a feature that divorces a value from its caption is
        /// invisible in speech, in the tree dump and in the review buffer alike, so the only way one
        /// ever surfaces is if somebody looks. This is the looking: every class the fallback answers
        /// for is logged once and kept, so a session's worth of play leaves a list of exactly the
        /// features nobody has judged, in the log and in <c>DevProbe.Tooltip</c>. Never spoken.
        /// </summary>
        public static ICollection<string> DefaultRead
        {
            get { return _defaulted; }
        }

        private static readonly HashSet<string> _defaulted = new HashSet<string>();

        private static void Defaulted(string feature)
        {
            if (string.IsNullOrEmpty(feature) || _defaulted.Contains(feature))
            {
                return;
            }

            _defaulted.Add(feature);
            Log.Info("tooltip feature read by default: " + feature);
        }
    }
}
