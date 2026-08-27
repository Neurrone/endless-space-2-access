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
            // Content: which fields contribute a line to the card's reading.
            if (AgeWidgets.Visible(widget))
            {
                fields.Add(AgeText.Label(label));
            }
        }

        /// <summary>A row per thing the table holds, rather than the table's text in one lump: two
        /// worlds' worth of findings routinely repeat a word - two lines both rated "Poor" - and a single
        /// readout of the whole table would drop the second as a duplicate.
        ///
        /// Each item is asked what it SAYS (<see cref="AgeWidgets.ItemText"/>) rather than only for its
        /// drawn text, because that is the reader every table of findings shares. On THIS panel the
        /// drawn text is always the answer: its anomaly, curiosity and deposit items are all instances of
        /// a plain typewriter label (<c>ResourceDepositDiscovery</c> and its siblings carry
        /// <c>AgePrimitiveLabel</c> + <c>AgeModifierTypewriter</c> and nothing else - no icon, no amount,
        /// no <c>ResourceDepositItem</c>), and the panel writes a whole sentence into each from a
        /// template of its own (<c>PlanetLabel_SystemDiscovery.RefreshResourceDepositItem</c> :218-224
        /// writes "%StarSystemDiscoveryResourceDepositTitle" = "{0}: {1}", the resource's name against
        /// its deposit category - measured 2026-08-27: "Adamantian: Average Deposit"). The wrapper-title
        /// fallback inside <c>ItemText</c> never fires here, so nothing on this card needs the caption
        /// the SYSTEM MANAGEMENT card's deposits need - that card's rows are the icon-and-amount
        /// <c>ResourceDepositItem</c> prefab, a different shape entirely.
        ///
        /// Visibility is the whole gate: all four tables are loaded with <c>StrictVisibility</c> on
        /// (<c>PlanetLabel_SystemDiscovery.Load</c> :78-84), so <c>ReserveChildren</c> retires a surplus
        /// row by switching it OFF rather than by parking it at alpha 0, and a hidden row is a row the
        /// previous world's findings are still written on.</summary>
        private static void AddItems(List<string> fields, AgeTransform table)
        {
            // Content: the same, for a whole table of them.
            if (!AgeWidgets.Visible(table))
            {
                return;
            }

            IList<AgeTransform> items = Children(table);
            for (int i = 0; items != null && i < items.Count; i++)
            {
                // Content: which items contribute a line.
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
