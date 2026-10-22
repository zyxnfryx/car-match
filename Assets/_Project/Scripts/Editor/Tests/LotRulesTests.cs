using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CarMatch.Tests
{
    public class LotRulesTests
    {
        static string Json()
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Resources/levels.json"));
        }

        [Test]
        public void MatchLogic_ThreeOfAColorClear()
        {
            RuleChecks.MatchThreeClears();
        }

        [Test]
        public void Bar_InsertsAfterSameColorAndSlides()
        {
            RuleChecks.BarKeepsColorsGrouped();
        }

        [Test]
        public void Progression_WinOpensExactlyTheNextLevel()
        {
            RuleChecks.ProgressionNeverSkips();
        }

        [Test]
        public void BarOverflow_SeventhCarFails()
        {
            RuleChecks.SeventhCarFails();
        }

        [Test]
        public void BarOverflow_MatchOnTheSeventhDoesNotFail()
        {
            RuleChecks.MatchOnSeventhDoesNotFail();
        }

        [Test]
        public void HiddenCars_RevealWhenUncovered()
        {
            RuleChecks.HiddenRevealsWhenUncovered();
        }

        [Test]
        public void Tunnels_BlockUntilTheQueueIsEmpty()
        {
            RuleChecks.TunnelBlocksUntilEmpty();
        }

        [Test]
        public void SideLots_WaitForAClearPath()
        {
            RuleChecks.SideLotWaitsForAClearPath();
        }

        [Test]
        public void Boosters_UndoRestoresTheBoard()
        {
            RuleChecks.UndoRestoresThePreviousBoard();
        }

        [Test]
        public void Boosters_SuperUndoReturnsThreeCars()
        {
            RuleChecks.SuperUndoReturnsThreeCars();
        }

        [Test]
        public void Boosters_ContinueClearsAFullBar()
        {
            RuleChecks.ContinueUndoesAFullBar();
        }

        [Test]
        public void Boosters_ShuffleKeepsColorCounts()
        {
            RuleChecks.ShuffleKeepsColorCounts();
        }

        [Test]
        public void Boosters_ShuffleRefusesWhenNothingCanChange()
        {
            RuleChecks.ShuffleRefusesWhenNothingCanChange();
        }

        [Test]
        public void Lanes_BackOfLaneCanExit()
        {
            RuleChecks.BackOfLaneCanExit();
        }

        [Test]
        public void Taps_RapidMultiTap()
        {
            RuleChecks.RapidMultiTap();
        }

        [Test]
        public void Taps_ReservedSlotsMatchInOrder()
        {
            RuleChecks.ReservedSlotsMatchInOrder();
        }

        [Test]
        public void Taps_ReservedSlotsCountTowardFail()
        {
            RuleChecks.ReservedSlotsCountTowardFail();
        }

        [Test]
        public void Boosters_MagnetPullsASetOfThree()
        {
            RuleChecks.MagnetPullsASetOfThree();
        }

        [Test]
        public void Boosters_ShuffleSuperMagnetOnEveryLevel()
        {
            RuleChecks.BoostersOnEveryLevel(Json());
        }

        [Test]
        public void Levels_NoTwoShareALayout()
        {
            RuleChecks.LevelsAreUnique(Json());
        }

        [Test]
        public void Boosters_EachIsFreeOnceThenPriced()
        {
            RuleChecks.EachBoosterOnceFree();
        }

        [Test]
        public void Boosters_MagnetLeavesParkedCarsInPlace()
        {
            RuleChecks.MagnetClosesLaneGaps();
        }

        [Test]
        public void MysteryBoxes_BreakNextToAMatch()
        {
            RuleChecks.MysteryBoxBreaksNextToAMatch();
        }

        [Test]
        public void Levels_EveryTenthIsSuperHard()
        {
            RuleChecks.SuperHardEveryTenth(Json());
        }

        [Test]
        public void Boosters_SuperClearsASet()
        {
            RuleChecks.SuperClearsASet();
        }

        [Test]
        public void Eyes_OpenOnlyWhenTheExitIsClear()
        {
            RuleChecks.OpenEyesOnlyOnAClearExit();
        }

        [Test]
        public void Headings_FollowTheNearestDirection()
        {
            RuleChecks.HeadingUsesTheNearestDirection();
        }

        [Test]
        public void Economy_WinsAndFailsPayTheSame()
        {
            RuleChecks.WinAndFailPayTheSame();
        }

        [Test]
        public void Economy_LivesSpendAndRegen()
        {
            RuleChecks.LivesSpendAndRegen();
        }

        [Test]
        public void Levels_AreSolvable()
        {
            RuleChecks.AllLevelsSolvable(Json());
        }
    }
}
