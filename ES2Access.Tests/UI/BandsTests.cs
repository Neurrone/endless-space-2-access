using ES2Access.Core.UI;
using Xunit;

namespace ES2Access.Tests.UI
{
    /// <summary>
    /// The band table's SHAPE - what the map is worth saying at each distance. Nothing reads the table
    /// yet, which is exactly when its shape is worth pinning down: a band boundary that drifts by one
    /// level changes what a player is told the galaxy holds, and a table nobody calls has no live
    /// evidence to catch it.
    ///
    /// Every assertion here is a line of the approved design, not an invention of this file - the
    /// measured renderings behind each are in the scan-mode design notes.
    /// </summary>
    public class BandsTests
    {
        [Fact]
        public void EveryLevelOfTheLadderIsInABand()
        {
            for (int level = Bands.FirstLevel; level <= Bands.LastLevel; level++)
            {
                Assert.NotEqual(ScanLens.None, Bands.LensAt(level));
            }
        }

        [Fact]
        public void FarOffTheMapIsStretchesOfSkyAndNothingElse()
        {
            for (int level = 1; level <= 2; level++)
            {
                Assert.Equal(BandFidelity.Full, Bands.Shows(level, false, BandKind.Constellations));
                Assert.Equal(BandFidelity.None, Bands.Shows(level, false, BandKind.Systems));
                Assert.Equal(BandFidelity.None, Bands.Shows(level, false, BandKind.Lanes));
                Assert.Equal(BandFidelity.None, Bands.Shows(level, false, BandKind.Fleets));
                Assert.Equal(BandFidelity.None, Bands.Shows(level, false, BandKind.Planets));
            }
        }

        [Fact]
        public void SystemsAndTheirLanesArriveAtThree()
        {
            Assert.Equal(BandFidelity.None, Bands.Shows(2, false, BandKind.Systems));
            Assert.Equal(BandFidelity.Name, Bands.Shows(3, false, BandKind.Systems));
            Assert.Equal(BandFidelity.Name, Bands.Shows(4, false, BandKind.Systems));
            Assert.Equal(BandFidelity.Full, Bands.Shows(3, false, BandKind.Lanes));
            Assert.Equal(BandFidelity.None, Bands.Shows(2, false, BandKind.Lanes));
        }

        [Fact]
        public void FleetsArriveAtFiveAndAtNoScanLens()
        {
            Assert.Equal(BandFidelity.None, Bands.Shows(4, false, BandKind.Fleets));
            for (int level = 5; level <= Bands.LastLevel; level++)
            {
                Assert.Equal(BandFidelity.Full, Bands.Shows(level, false, BandKind.Fleets));
            }

            for (int level = Bands.FirstLevel; level <= Bands.LastLevel; level++)
            {
                Assert.Equal(BandFidelity.None, Bands.Shows(level, true, BandKind.Fleets));
            }
        }

        [Fact]
        public void ASystemBecomesAFullReadingAtSeven()
        {
            Assert.Equal(BandFidelity.Name, Bands.Shows(6, false, BandKind.Systems));
            Assert.Equal(BandFidelity.Full, Bands.Shows(7, false, BandKind.Systems));
        }

        [Fact]
        public void PlanetsAreDotsUntilTheOrbitalCardsAreDrawn()
        {
            Assert.Equal(BandFidelity.None, Bands.Shows(6, false, BandKind.Planets));
            for (int level = 7; level <= 12; level++)
            {
                Assert.Equal(BandFidelity.Dot, Bands.Shows(level, false, BandKind.Planets));
            }

            Assert.Equal(BandFidelity.Full, Bands.Shows(13, false, BandKind.Planets));
        }

        [Fact]
        public void GoingIntoASystemLeavesTheTreeAsThirteenHadIt()
        {
            for (int kind = 0; kind <= (int)BandKind.Empires; kind++)
            {
                Assert.Equal(
                    Bands.Shows(13, false, (BandKind)kind),
                    Bands.Shows(14, false, (BandKind)kind)
                );
                Assert.Equal(
                    Bands.Shows(13, false, (BandKind)kind),
                    Bands.Shows(15, false, (BandKind)kind)
                );
            }
        }

        [Fact]
        public void ALevelOffEitherEndReadsAsItsNearestBand()
        {
            Assert.Equal(
                Bands.Shows(Bands.FirstLevel, false, BandKind.Constellations),
                Bands.Shows(0, false, BandKind.Constellations)
            );
            Assert.Equal(
                Bands.Shows(Bands.LastLevel, false, BandKind.Planets),
                Bands.Shows(99, false, BandKind.Planets)
            );
        }

        [Fact]
        public void TheLensesAreBandsOfTheSameLadder()
        {
            Assert.Equal(ScanLens.Diplomacy, Bands.LensAt(1));
            Assert.Equal(ScanLens.Diplomacy, Bands.LensAt(2));
            Assert.Equal(ScanLens.Trade, Bands.LensAt(3));
            Assert.Equal(ScanLens.Trade, Bands.LensAt(6));
            Assert.Equal(ScanLens.Economy, Bands.LensAt(7));
            Assert.Equal(ScanLens.Economy, Bands.LensAt(10));
            Assert.Equal(ScanLens.System, Bands.LensAt(11));
            Assert.Equal(ScanLens.System, Bands.LensAt(13));
            Assert.Equal(ScanLens.SystemManagement, Bands.LensAt(14));
            Assert.Equal(ScanLens.Planet, Bands.LensAt(15));
        }

        [Fact]
        public void TheDiplomacyLensListsEmpiresAndNothingOnTheMap()
        {
            for (int level = 1; level <= 2; level++)
            {
                Assert.Equal(BandFidelity.Full, Bands.Shows(level, true, BandKind.Empires));
                Assert.Equal(BandFidelity.None, Bands.Shows(level, true, BandKind.Systems));
                Assert.Equal(BandFidelity.None, Bands.Shows(level, true, BandKind.Lanes));
                Assert.Equal(BandFidelity.None, Bands.Shows(level, true, BandKind.Planets));
            }
        }

        [Fact]
        public void EmpiresAreAScanKindOnly()
        {
            for (int level = Bands.FirstLevel; level <= Bands.LastLevel; level++)
            {
                Assert.Equal(BandFidelity.None, Bands.Shows(level, false, BandKind.Empires));
            }
        }

        [Fact]
        public void TradeAndEconomyCarrySystemsDotPlanetsAndLanes()
        {
            for (int level = 3; level <= 10; level++)
            {
                Assert.Equal(BandFidelity.Full, Bands.Shows(level, true, BandKind.Systems));
                Assert.Equal(BandFidelity.Dot, Bands.Shows(level, true, BandKind.Planets));
                Assert.Equal(BandFidelity.Full, Bands.Shows(level, true, BandKind.Lanes));
            }
        }

        [Fact]
        public void TheSystemLensNamesTheSystemsAndDrawsTheLanes()
        {
            for (int level = 11; level <= 13; level++)
            {
                Assert.Equal(BandFidelity.Name, Bands.Shows(level, true, BandKind.Systems));
                Assert.Equal(BandFidelity.Full, Bands.Shows(level, true, BandKind.Lanes));
                Assert.Equal(BandFidelity.None, Bands.Shows(level, true, BandKind.Planets));
            }
        }

        [Fact]
        public void NoLensDrawsTheStretchesOfSky()
        {
            for (int level = Bands.FirstLevel; level <= Bands.LastLevel; level++)
            {
                Assert.Equal(BandFidelity.None, Bands.Shows(level, true, BandKind.Constellations));
            }
        }

        [Fact]
        public void TheScannerFindsSystemsAndUnexploredLanesFromThree()
        {
            Assert.False(Bands.Scans(2, false, ScannerKeys.Systems));
            Assert.False(Bands.Scans(2, false, ScannerKeys.Unexplored));
            Assert.True(Bands.Scans(3, false, ScannerKeys.Systems));
            Assert.True(Bands.Scans(3, false, ScannerKeys.Unexplored));
        }

        [Fact]
        public void TheScannerFindsFleetsFromFiveInNormalViewAndNeverUnderALens()
        {
            Assert.False(Bands.Scans(4, false, ScannerKeys.Fleets));
            Assert.True(Bands.Scans(5, false, ScannerKeys.Fleets));
            for (int level = Bands.FirstLevel; level <= Bands.LastLevel; level++)
            {
                Assert.False(Bands.Scans(level, true, ScannerKeys.Fleets));
            }
        }

        [Fact]
        public void EverythingElseWaitsForTheFullNameplateAtSeven()
        {
            string[] rest = new string[]
            {
                ScannerKeys.Colonizable,
                ScannerKeys.Anomalies,
                ScannerKeys.Curiosities,
                ScannerKeys.Luxury,
                ScannerKeys.Strategic,
                ScannerKeys.Contested,
                ScannerKeys.Probes,
                ScannerKeys.Pins,
                ScannerKeys.Projectiles,
                ScannerKeys.Markers,
            };

            for (int i = 0; i < rest.Length; i++)
            {
                Assert.False(Bands.Scans(6, false, rest[i]), rest[i]);
                Assert.True(Bands.Scans(7, false, rest[i]), rest[i]);
            }
        }

        [Fact]
        public void CuriositiesAreNormalViewOnly()
        {
            for (int level = 7; level <= Bands.LastLevel; level++)
            {
                Assert.True(Bands.Scans(level, false, ScannerKeys.Curiosities));
            }

            // The scan dot prefab does not wire the curiosity circle - measured live, the field is
            // null - so no lens has one to find.
            for (int level = Bands.FirstLevel; level <= Bands.LastLevel; level++)
            {
                Assert.False(Bands.Scans(level, true, ScannerKeys.Curiosities));
            }
        }

        [Fact]
        public void TradeAndEconomyScanSystemsColonizableAndUnexplored()
        {
            for (int level = 3; level <= 10; level++)
            {
                Assert.True(Bands.Scans(level, true, ScannerKeys.Systems));
                Assert.True(Bands.Scans(level, true, ScannerKeys.Colonizable));
                Assert.True(Bands.Scans(level, true, ScannerKeys.Unexplored));
            }
        }

        [Fact]
        public void ContestedInfluenceIsTheEconomyLensOnly()
        {
            for (int level = Bands.FirstLevel; level <= Bands.LastLevel; level++)
            {
                Assert.Equal(
                    Bands.LensAt(level) == ScanLens.Economy,
                    Bands.Scans(level, true, ScannerKeys.Contested)
                );
            }
        }

        [Fact]
        public void TheSystemLensScansSystemsAndUnexploredOnly()
        {
            for (int level = 11; level <= 13; level++)
            {
                Assert.True(Bands.Scans(level, true, ScannerKeys.Systems));
                Assert.True(Bands.Scans(level, true, ScannerKeys.Unexplored));
                Assert.False(Bands.Scans(level, true, ScannerKeys.Colonizable));
            }
        }

        [Fact]
        public void TheDiplomacyLensAndThePagesBelowScanNothing()
        {
            int[] levels = new int[] { 1, 2, 14, 15 };
            for (int i = 0; i < levels.Length; i++)
            {
                for (int category = 0; category < ScannerKeys.Categories.Length; category++)
                {
                    Assert.False(
                        Bands.Scans(levels[i], true, ScannerKeys.Categories[category]),
                        levels[i] + " " + ScannerKeys.Categories[category]
                    );
                }
            }
        }

        [Fact]
        public void StepsInsideOneBandDrawTheSamePicture()
        {
            Assert.True(Bands.SameShape(7, 12, false));
            Assert.True(Bands.SameShape(1, 2, false));
            Assert.True(Bands.SameShape(3, 4, false));
            Assert.True(Bands.SameShape(13, 15, false));
            Assert.True(Bands.SameShape(3, 10, true));
        }

        [Fact]
        public void CrossingABandBoundaryChangesTheShape()
        {
            Assert.False(Bands.SameShape(2, 3, false));
            Assert.False(Bands.SameShape(4, 5, false));
            Assert.False(Bands.SameShape(6, 7, false));
            Assert.False(Bands.SameShape(12, 13, false));
            Assert.False(Bands.SameShape(2, 3, true));
        }

        [Fact]
        public void AShapeIsAskedOfTheLadderTheModeIsOn()
        {
            // Level 3 draws fleets under no lens and never under one, so the two ladders' rows at the
            // same level are not the same row.
            Assert.Equal(BandFidelity.None, Bands.Shows(5, true, BandKind.Fleets));
            Assert.Equal(BandFidelity.Full, Bands.Shows(5, false, BandKind.Fleets));
        }

        /// <summary>The minimum band a snap landing forces, per kind of target. These four numbers ARE
        /// the landing rule: a planet or anything hanging off one to the orbital view, a fleet to the
        /// lozenges, a system to the band that names them, and everything else the map draws out in the
        /// open to the band that draws the full nameplate beside it.</summary>
        [Fact]
        public void ALandingsMinimumBandIsWhereItsKindIsFirstDrawn()
        {
            Assert.Equal(13, Bands.LowestLevel(BandKind.Planets, false, BandFidelity.Full));
            Assert.Equal(7, Bands.LowestLevel(BandKind.Planets, false, BandFidelity.Dot));
            Assert.Equal(5, Bands.LowestLevel(BandKind.Fleets, false, BandFidelity.Full));
            Assert.Equal(3, Bands.LowestLevel(BandKind.Systems, false, BandFidelity.Name));
        }

        [Fact]
        public void AKindTheLadderNeverDrawsHasNoBandToForce()
        {
            // Nothing on the ordinary map is a row about an empire, and no lens draws a fleet.
            Assert.Equal(-1, Bands.LowestLevel(BandKind.Empires, false, BandFidelity.Name));
            Assert.Equal(-1, Bands.LowestLevel(BandKind.Fleets, true, BandFidelity.Name));
        }

        [Fact]
        public void AskingForNothingIsAnsweredByTheFurthestOutLevel()
        {
            Assert.Equal(
                Bands.FirstLevel,
                Bands.LowestLevel(BandKind.Planets, false, BandFidelity.None)
            );
        }

        /// <summary>A lens's own minimum is the lens ladder's, not the ordinary map's: the scan tree
        /// draws systems from the Trade lens and planet dots with them, which is a different pair of
        /// numbers from the ones the normal view answers with.</summary>
        [Fact]
        public void TheMinimumBandIsReadOffTheLadderTheModeIsOn()
        {
            Assert.Equal(3, Bands.LowestLevel(BandKind.Planets, true, BandFidelity.Dot));
            Assert.Equal(-1, Bands.LowestLevel(BandKind.Planets, true, BandFidelity.Full));
        }

        [Fact]
        public void AKeyNoBuildEverHadScansNothing()
        {
            Assert.False(Bands.Scans(9, false, "wormholes"));
            Assert.False(Bands.Scans(9, true, null));
        }
    }
}
