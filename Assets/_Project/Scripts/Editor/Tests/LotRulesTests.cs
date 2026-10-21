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
        public void Boosters_MagnetPullsASetOfThree()
        {
            RuleChecks.MagnetPullsASetOfThree();
        }

        [Test]
        public void Eyes_OpenOnlyWhenTheExitIsClear()
        {
            RuleChecks.OpenEyesOnlyOnAClearExit();
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
