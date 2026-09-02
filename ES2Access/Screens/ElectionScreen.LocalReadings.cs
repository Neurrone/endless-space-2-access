using System;
using System.Collections.Generic;
using System.Reflection;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>What the vote breakdown only draws: the counts read off the local panel own private
    /// fields, and the hold that stops the carousel moving under the cursor.</summary>
    public sealed partial class ElectionScreen
    {
        // ---- what the vote breakdown only draws ----

        /// <summary>The figures behind the step's wordless bars, read once per build rather than once
        /// per bar.</summary>
        private struct LocalCounts
        {
            /// <summary>The shown system's parties, in the order the trends bars were bound from them;
            /// each value is [this system's count, the count through this system].</summary>
            public IList<KeyValuePair<PoliticsDefinition, int[]>> Parties;

            /// <summary>Representatives counted through the shown system.</summary>
            public int Counted;

            /// <summary>The empire's representatives.</summary>
            public int Total;
        }

        // Looked up once: Build runs every tick, and a reflection lookup per bar per frame is a scan at
        // 60 Hz for an answer that never changes shape.
        private static readonly FieldInfo InfosField = Field("starSystemElectionInformations");
        private static readonly FieldInfo IndexField = Field("currentStarSystemIndex");
        private static readonly FieldInfo TotalField = Field("cumulatedRepresentativesCount");
        private static readonly FieldInfo CarouselField = Field("moveCarouselAutomatically");
        private static FieldInfo _countedField;
        private static FieldInfo _partiesField;

        private static FieldInfo Field(string name)
        {
            return GameHandlers.Field(typeof(ElectionLocalPanel), name);
        }

        /// <summary>
        /// What the shown system's bars are drawn from.
        ///
        /// All of it is private on the panel and none of it reaches a label: the per-party counts, which
        /// system the carousel is on, and the empire's total. The struct holding the counts is private
        /// too, so its own fields are looked up off the boxed value the first time one is seen.
        /// </summary>
        private static LocalCounts Counts(ElectionLocalPanel panel)
        {
            LocalCounts counts = new LocalCounts();
            try
            {
                if (InfosField == null || IndexField == null || TotalField == null)
                {
                    return counts;
                }

                counts.Total = (int)TotalField.GetValue(panel);
                System.Collections.IList infos =
                    InfosField.GetValue(panel) as System.Collections.IList;
                int index = (int)IndexField.GetValue(panel);
                if (infos == null || index < 0 || index >= infos.Count)
                {
                    return counts;
                }

                object info = infos[index];
                if (_countedField == null || _partiesField == null)
                {
                    Type type = info.GetType();
                    _countedField = GameHandlers.Field(type, "CumulatedRepresentativesCount");
                    _partiesField = GameHandlers.Field(
                        type,
                        "PoliticsWithLocalScoresAndCumulatedScores"
                    );
                }

                if (_countedField == null || _partiesField == null)
                {
                    return counts;
                }

                counts.Counted = (int)_countedField.GetValue(info);
                counts.Parties =
                    _partiesField.GetValue(info)
                    as IList<KeyValuePair<PoliticsDefinition, int[]>>;
            }
            catch (Exception e)
            {
                Log.Warn("election: reading the vote breakdown's counts threw: " + e);
            }

            return counts;
        }

        /// <summary>
        /// Stop the carousel walking off on its own.
        ///
        /// <c>ElectionLocalPanel.Show</c> starts a coroutine that steps to the next system every 1.5
        /// seconds until a Prev/Next click switches it off (:70,:350-366,:384-400) - so a player reading
        /// the system line has it replaced under them twice a second. Switching the same flag off on
        /// arrival puts the panel in exactly the state a mouse user reaches with one click of an arrow,
        /// and nothing else about the panel changes.
        /// </summary>
        private static void HoldCarousel(ElectionLocalPanel panel)
        {
            try
            {
                if (CarouselField != null)
                {
                    CarouselField.SetValue(panel, false);
                }
            }
            catch (Exception e)
            {
                Log.Warn("election: holding the system carousel threw: " + e);
            }
        }
    }
}
