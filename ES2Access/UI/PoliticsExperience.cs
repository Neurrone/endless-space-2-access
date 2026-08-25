using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// The bar a law's prerequisite panel draws under "Required Political experience", and the three
    /// different things it draws on it.
    ///
    /// The panel writes ONE thing in words: the standing the law REQUIRES ("Required Political
    /// experience: Potent Scientists"), reddened while the party is short of it
    /// (<c>PanelFeaturePoliticsExperiencePrerequisite.Bind</c> :55). Everything else about the law's
    /// reach is geometry on a bar carrying no numbers at all, and it is three separate facts:
    ///
    /// - the FILL, stretched to the requirement (:62) - the same fact the red words already say;
    /// - the TICK dividers, which are the party's own scale: one segment per distinct experience score
    ///   any of that party's laws asks for, drawn at its share of the largest
    ///   (<c>SenatorBaseCard.FormatExperienceMarkers</c> :42-67). They are what makes the requirement
    ///   a position rather than a bare adjective - "Potent" means nothing until the player knows the
    ///   party's ladder runs Established, Entrenched, Potent;
    /// - the NOTCH, parked at where the party stands now (:63) and drawn ONLY while the requirement is
    ///   NOT met (:64 - <c>Visible = required &gt; current</c>), which is the whole difference between
    ///   a law that can be passed and one that cannot.
    ///
    /// Each of those is read as what it is DRAWN as. The tier words are the game's own: the experience
    /// database maps a share of the maximum to a tier name (<c>GuiPolitics.FindExperienceTitle</c>),
    /// which is what a senator's card writes under the same party's bar
    /// (<c>SenatorBaseCard.RefreshPoliticsExperience</c> :165-185). So every word here is asked of a
    /// DRAWN position - the tick's, the notch's - and never of the model behind the bar: the game's own
    /// experience counts are written nowhere on this panel, so "3 of 15" is not the panel speaking.
    ///
    /// Only PAINTED ticks count. The group is pooled (<c>ReserveChildren</c>), so a party with a
    /// shorter ladder than the one drawn before it leaves the surplus segments parked, and a party
    /// with one requirement has the group hidden outright (:56) - a ladder with one rung is no scale
    /// and says nothing.
    ///
    /// The notch's caption is the mod's, phrased on the game's own required-experience caption beside
    /// it. The sentence the prefab hangs on the notch - which says what it is and, in the game's own
    /// words, that the party has to win more elections before this law can be voted - is the notch's
    /// own dossier.
    ///
    /// Main-thread only.
    /// </summary>
    public static class PoliticsExperience
    {
        /// <summary>
        /// The party's ladder, as the bar's ticks divide it: one line per tick, in the order they are
        /// drawn, each the tier that tick ends in and how far along the bar it ends.
        ///
        /// A tick spans [previous threshold, its own], so the thresholds are the ticks' RIGHT edges -
        /// which is also where the tier the segment belongs to is asked from. Empty where the game
        /// draws no scale.
        /// </summary>
        public static IList<string> Scale(PanelFeaturePoliticsExperiencePrerequisite feature)
        {
            List<string> lines = new List<string>();
            try
            {
                AgeTransform group = feature == null ? null : feature.ExperienceMarkersGroup;
                if (
                    group == null
                    || group.Parent == null
                    || !AgeWidgets.Paints(group)
                )
                {
                    return lines;
                }

                IList<AgeTransform> ticks = group.Children;
                for (int i = 0; ticks != null && i < ticks.Count; i++)
                {
                    AgeTransform tick = ticks[i];
                    if (!AgeWidgets.Paints(tick))
                    {
                        continue;
                    }

                    string tier = Tier(tick.PercentRight);
                    if (string.IsNullOrEmpty(tier))
                    {
                        continue;
                    }

                    string line = ModStrings.Format(
                        ModStrings.CaptionedColon,
                        tier,
                        Percent(tick.PercentRight)
                    );
                    if (!lines.Contains(line))
                    {
                        lines.Add(line);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("politics: reading the experience scale threw: " + e);
            }

            return lines;
        }

        /// <summary>Where the party stands now, as the panel draws it: the tier the notch sits in, in
        /// the game's own word for it, and the share of the bar it is drawn at. Null where the game is
        /// not drawing that notch at all - which is every law whose requirement the party already
        /// meets.</summary>
        public static string Current(PanelFeaturePoliticsExperiencePrerequisite feature)
        {
            AgeTransform marker = Marker(feature);
            if (marker == null)
            {
                return null;
            }

            try
            {
                // PercentRight is where Bind put it: current experience as a percentage of the most
                // any law of this party asks for (:60-63), which is both the position the player sees
                // and the normalized value the tier database is keyed by.
                float reach = marker.PercentRight;
                string tier = Tier(reach);
                return string.IsNullOrEmpty(tier)
                    ? null
                    : ModStrings.Format(
                        ModStrings.PoliticsCurrentExperience,
                        tier,
                        Percent(reach)
                    );
            }
            catch (Exception e)
            {
                Log.Warn("politics: reading the experience marker threw: " + e);
                return null;
            }
        }

        /// <summary>Everything drawn ON the bar, as the lines it is worth on a surface that has no
        /// stops to hang them from - the scale, then where the party stands, then the game's own
        /// sentence about that. In the order the panel draws them rather than the order the widgets
        /// are listed: the notch is a sibling drawn BEFORE the scale it is read against, and left to
        /// the layout it would be read between two rungs of the ladder.</summary>
        public static string BarText(PanelFeaturePoliticsExperiencePrerequisite feature)
        {
            List<string> lines = new List<string>();
            IList<string> scale = Scale(feature);
            for (int i = 0; i < scale.Count; i++)
            {
                lines.Add(scale[i]);
            }

            string current = Current(feature);
            if (!string.IsNullOrEmpty(current))
            {
                lines.Add(current);
            }

            string note = AgeText.Tooltip(Note(feature));
            if (!string.IsNullOrEmpty(note))
            {
                lines.Add(note);
            }

            return lines.Count == 0 ? null : string.Join("\n", lines.ToArray());
        }

        /// <summary>The game's own sentence about the notch, for the node that reads the panel on a
        /// screen. Null while the notch is not drawn, so a law the party can pass says nothing extra.
        /// </summary>
        public static AgeTooltip Note(PanelFeaturePoliticsExperiencePrerequisite feature)
        {
            AgeTransform marker = Marker(feature);
            try
            {
                return marker == null ? null : marker.AgeTooltip;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The current-standing notch, but only while the panel is drawing it - which is what
        /// a stop pointed at it exists by.</summary>
        public static AgeTransform Marker(PanelFeaturePoliticsExperiencePrerequisite feature)
        {
            try
            {
                AgeTransform marker =
                    feature == null ? null : feature.PoliticsCurrentExperienceMarker;
                return AgeWidgets.Paints(marker) ? marker : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The box the fill, the ticks and the notch are all drawn in: the one widget that
        /// stands for the whole bar, for a reader that speaks for all three at once.</summary>
        public static AgeTransform Bar(PanelFeaturePoliticsExperiencePrerequisite feature)
        {
            try
            {
                AgeTransform gauge = feature == null ? null : feature.PoliticsExperienceGauge;
                return gauge == null ? null : gauge.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The game's word for the tier a drawn position falls in.</summary>
        private static string Tier(float reach)
        {
            return AgeText.Clean(GuiPolitics.FindExperienceTitle(reach / 100f));
        }

        /// <summary>How far along the bar something is drawn, as the share of it that is.</summary>
        private static string Percent(float reach)
        {
            return (int)Math.Round(reach) + "%";
        }
    }
}
