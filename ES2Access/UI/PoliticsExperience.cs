using System;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// How far the party backing a law actually IS from the standing the law asks for - the marker the
    /// prerequisite panel draws on its experience bar, and the sentence the game hangs on that marker.
    ///
    /// The panel writes one thing in words: the standing the law REQUIRES ("Required Political
    /// experience: Potent Scientists"). What it draws is a bar filled to that requirement with a second
    /// marker parked at where the party stands now, and the game only draws that marker while the
    /// requirement is NOT met (<c>PanelFeaturePoliticsExperiencePrerequisite.Bind</c> :64 -
    /// <c>Visible = required &gt; current</c>) and reddens the required words beside it (:55). Neither
    /// the position nor the colour is a word, so a reader that only takes labels says the same thing on
    /// a law the empire can pass and one it cannot.
    ///
    /// A textless gauge reads as its drawn proportion, and here the game has its own words for that
    /// proportion: the experience database maps a share of the maximum to a tier name
    /// (<c>GuiPolitics.FindExperienceTitle</c>), which is exactly what a senator's card writes under
    /// the same party's bar (<c>SenatorBaseCard.RefreshPoliticsExperience</c> :165-185 - the tier
    /// alone, with no party symbol after it). So the marker is read as the tier it is standing in,
    /// asked of the DRAWN position rather than of the model, and the sentence the prefab hangs on the
    /// marker - which says both what it is and, in the game's own words, that the party has to win
    /// more elections before this law can be voted - rides along as the marker's own tooltip.
    ///
    /// Main-thread only.
    /// </summary>
    public static class PoliticsExperience
    {
        /// <summary>The tier the current-standing marker is drawn in, in the game's own word for it, or
        /// null where the game is not drawing that marker at all - which is every law whose requirement
        /// the party already meets.</summary>
        public static string Standing(PanelFeaturePoliticsExperiencePrerequisite feature)
        {
            AgeTransform marker = Marker(feature);
            if (marker == null)
            {
                return null;
            }

            try
            {
                // PercentRight is where Bind put it: current experience as a percentage of the most
                // any law of this party asks for (:60-63), which is the normalized value the tier
                // database is keyed by.
                return AgeText.Clean(GuiPolitics.FindExperienceTitle(marker.PercentRight / 100f));
            }
            catch (Exception e)
            {
                Log.Warn("politics: reading the experience marker threw: " + e);
                return null;
            }
        }

        /// <summary>The game's own sentence about the marker, for the node that reads the panel on a
        /// screen. Null while the marker is not drawn, so a law the party can pass says nothing extra.
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

        /// <summary>The current-standing marker, but only while the panel is drawing it.</summary>
        private static AgeTransform Marker(PanelFeaturePoliticsExperiencePrerequisite feature)
        {
            try
            {
                AgeTransform marker =
                    feature == null ? null : feature.PoliticsCurrentExperienceMarker;
                return marker != null && AgeWidgets.Paints(marker.Parent, marker) ? marker : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
