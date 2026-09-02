using System.Collections.Generic;
using ES2Access.Core.Speech;
using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// What a multi-select list says about itself. Two rules with edges: a row always says which side
    /// of the selection it is on, and a range reports the WHOLE selection - except where there is no
    /// range to report, which is where the caller has to fall back to the row's own state rather than
    /// announce "1 ships selected".
    /// </summary>
    [Collection(ModStringsCollection.Name)]
    public class SelectionTextTests
    {
        public SelectionTextTests()
        {
            ModStrings.Reset();
        }

        [Fact]
        public void MembershipSpeaksBothStates()
        {
            Assert.Equal("selected", SelectionText.Membership(true));
            Assert.Equal("not selected", SelectionText.Membership(false));
        }

        [Fact]
        public void RangeNamesTheCountAndBothEnds()
        {
            List<string> names = new List<string> { "Vanguard", "Patrol", "Settler" };
            Assert.Equal("3 ships selected, Vanguard to Settler", SelectionText.Range(names));
        }

        [Fact]
        public void RangeOfTwoIsStillARange()
        {
            List<string> names = new List<string> { "Vanguard", "Settler" };
            Assert.Equal("2 ships selected, Vanguard to Settler", SelectionText.Range(names));
        }

        [Fact]
        public void OneOrNoneIsNotARange()
        {
            Assert.Null(SelectionText.Range(new List<string> { "Vanguard" }));
            Assert.Null(SelectionText.Range(new List<string>()));
            Assert.Null(SelectionText.Range(null));
        }

        [Fact]
        public void AnEndWithNoNameIsNotReported()
        {
            Assert.Null(SelectionText.Range(new List<string> { null, "Settler" }));
            Assert.Null(SelectionText.Range(new List<string> { "Vanguard", "" }));
        }

        /// <summary>The range is a counted pair, so a three-form language's paucal reaches it - which
        /// is the whole reason the pair has a singular nobody ever hears.</summary>
        [Fact]
        public void AThreeFormLanguageTakesThePaucalForASmallRange()
        {
            ModStrings.Install(
                new Dictionary<string, string>
                {
                    { ModStrings.FleetsShipRange, "one {0} {1} {2}" },
                    { ModStrings.FleetsShipsRange, "many {0} {1} {2}" },
                    {
                        ModStrings.FleetsShipsRange + PluralRules.FewSuffix,
                        "few {0} {1} {2}"
                    },
                },
                "russian"
            );

            List<string> three = new List<string> { "Vanguard", "Patrol", "Settler" };
            Assert.Equal("few 3 Vanguard Settler", SelectionText.Range(three));

            List<string> five = new List<string>
            {
                "Vanguard",
                "Patrol",
                "Settler",
                "Probe",
                "Scout",
            };
            Assert.Equal("many 5 Vanguard Scout", SelectionText.Range(five));
        }

        [Fact]
        public void TranslationsReorderTheWholeSentence()
        {
            ModStrings.Install(
                new Dictionary<string, string>
                {
                    { ModStrings.FleetsShipsRange, "{1}から{2}まで、{0}隻選択" },
                    { ModStrings.NavNotSelected, "未選択" },
                }
            );

            Assert.Equal(
                "VanguardからSettlerまで、3隻選択",
                SelectionText.Range(new List<string> { "Vanguard", "Patrol", "Settler" })
            );
            Assert.Equal("未選択", SelectionText.Membership(false));
        }
    }
}
