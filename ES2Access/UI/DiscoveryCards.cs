using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;

namespace ES2Access.UI
{
    /// <summary>
    /// The planet card the game types onto the screen during a cutscene, as one spoken line.
    ///
    /// The same panel (<c>PlanetLabel_SystemDiscovery</c>) is drawn by two different cutscenes - the
    /// system-discovery flythrough, one card per world, and the colonization scene, one card for the
    /// world being settled - so what it says is read in one place rather than twice.
    ///
    /// What is read is the card's own fields, in the order it draws them, and only the ones it is
    /// showing: a world with no anomalies has no anomaly line. The words come off the card's LABELS
    /// rather than being rebuilt from the model, because half of what the card says exists nowhere else
    /// - "Unique planet" and the pirate-lair warning are text the panel was authored with, and the
    /// anomaly, curiosity and deposit lines are assembled by the panel from templates of its own. The
    /// one field taken from the model instead is the planet's NAME, which the card ellipsizes to fit its
    /// box the way the improvements tiles do.
    ///
    /// Reading the labels is safe despite the typewriter animation. The typewriter does not write the
    /// text a character at a time - it sets the label's whole text at once and then moves a cursor the
    /// RENDERER draws up to (<c>AgeModifierTypewriter</c> only advances the label's CurrentLine and
    /// CurrentCharInLine), so the label holds every word from the first frame and is read complete while
    /// a sighted player is still watching it appear.
    ///
    /// Answers null for a card the game has not filled in yet, which is a passive announcer's cue to
    /// ask again next frame rather than to record the planet as read.
    /// </summary>
    public static class DiscoveryCards
    {
        /// <summary>
        /// What this card says: what the world is called, whether it is one of a kind, whether something
        /// hostile is living on it, how big it is and what kind of place it is, whether anyone has
        /// claimed it, then whatever the survey turned up - anomalies, curiosities, deposits - and last
        /// how good it is at each of the four outputs.
        ///
        /// Allocates its own list rather than reusing one: a caller reads a card ONCE, when the scene
        /// brings a new planet up, not on every frame of the scene.
        /// </summary>
        public static string Read(PlanetLabel_SystemDiscovery card)
        {
            if (card == null)
            {
                return null;
            }

            List<string> fields = new List<string>();
            fields.Add(Name(card));
            AddLabel(fields, card.UniqueSubtitle);
            AddLabel(fields, card.HostilePresenceTitle);
            AddLabel(fields, card.PlanetSizeAndType);
            AddLabel(fields, card.PlanetStatus);
            AddItems(fields, card.PlanetAnomaliesTable);
            AddItems(fields, card.PlanetCuriositiesTable);
            AddItems(fields, card.ResourceDepositsGroup);
            AddItems(fields, card.FidsScoreTable);
            return FieldReadout.Compose(fields);
        }

        /// <summary>What the planet is called, in full. The card writes the same name and then truncates
        /// it to the width of its box - "Cravin." for Cravings - so the model's answer is the one worth
        /// speaking; it is the same string the card started from.</summary>
        private static string Name(PlanetLabel_SystemDiscovery card)
        {
            try
            {
                Planet planet = card.Planet;
                return planet == null ? null : AgeText.Clean(planet.LocalizedName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void AddLabel(List<string> fields, AgePrimitiveLabel label)
        {
            AgeTransform widget = Transform(label);
            if (AgeWidgets.Visible(widget))
            {
                fields.Add(AgeText.Label(label));
            }
        }

        /// <summary>A row per thing the table holds, rather than the table's text in one lump: two
        /// worlds' worth of findings routinely repeat a word - two lines both rated "Poor" - and a single
        /// readout of the whole table would drop the second as a duplicate.
        ///
        /// What each item SAYS (<see cref="AgeWidgets.ItemText"/>), not the text drawn on it: the
        /// anomalies, the curiosities and the deposits are rows of bare pictures, so reading them as text
        /// left the survey's whole findings unspoken - the one thing the cutscene exists to
        /// report.</summary>
        private static void AddItems(List<string> fields, AgeTransform table)
        {
            if (!AgeWidgets.Visible(table))
            {
                return;
            }

            IList<AgeTransform> items = Children(table);
            for (int i = 0; items != null && i < items.Count; i++)
            {
                if (items[i] != null && AgeWidgets.Visible(items[i]))
                {
                    fields.Add(AgeWidgets.ItemText(items[i]));
                }
            }
        }

        private static AgeTransform Transform(AgePrimitiveLabel label)
        {
            try
            {
                return label == null ? null : label.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static IList<AgeTransform> Children(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.Children;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
