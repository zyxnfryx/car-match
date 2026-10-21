using System.Collections.Generic;

namespace CarMatch
{
    // One check suite. EditMode tests and tools/SolveCheck both call these,
    // so a rule change is proved in the editor and from the command line.
    public static class RuleChecks
    {
        public static void MatchThreeClears()
        {
            var bar = new List<CarColor>();
            var first = MatchClear.Push(bar, CarColor.Coral);
            var second = MatchClear.Push(bar, CarColor.Coral);
            Expect(!first.Matched && !second.Matched && bar.Count == 2, "pair should sit in the bar");
            var third = MatchClear.Push(bar, CarColor.Coral);
            Expect(third.Matched && bar.Count == 0, "three coral should clear");
            MatchClear.Push(bar, CarColor.Teal);
            var mixed = MatchClear.Push(bar, CarColor.Coral);
            Expect(!mixed.Matched && bar.Count == 2, "different colors stay");
        }

        public static void SeventhCarFails()
        {
            var board = Tiny("[[\"coral\"],[\"coral\"],[\"amber\"],[\"amber\"],[\"lime\"],[\"lime\"],[\"violet\",\"teal\"]]");
            var history = new List<Board>();
            for (int i = 0; i < 7; i++)
            {
                var move = BoardRules.Play(board, history, new Tap(TapKind.Lane, i, 0));
                Expect(move.Accepted, "lane " + i + " should accept");
            }
            Expect(board.Failed && !board.Won, "the seventh car, a new color, fills the bar");
            Expect(board.Bar.Count == MatchClear.BarCap, "bar should be full");
            Expect(!BoardQuery.IsClear(board), "a teal is still behind the violet");
        }

        public static void MatchOnSeventhDoesNotFail()
        {
            var board = Tiny("[[\"coral\"],[\"coral\"],[\"coral\"],[\"amber\"],[\"amber\"],[\"lime\"],[\"lime\"]]");
            int[] order = { 0, 1, 3, 4, 5, 6, 2 };
            for (int i = 0; i < order.Length; i++)
            {
                var move = BoardRules.Play(board, null, new Tap(TapKind.Lane, order[i], 0));
                Expect(move.Accepted, "tap " + i + " should accept");
            }
            Expect(!board.Failed, "the seventh car completes a match, so the bar never stays full");
            Expect(board.Won, "that sequence also clears the lot");
            Expect(board.Bar.Count == 4, "two amber and two lime remain after coral clears");
        }

        public static void HiddenRevealsWhenUncovered()
        {
            var board = Tiny("[[\"coral\",\"?amber\",\"?lime\"]]");
            Expect(!FrontHidden(board, 0), "the exit car is uncovered on load");
            var covered = board.Lanes[0].Cells[1].Car;
            Expect(covered.Hidden && covered.Color == CarColor.Amber, "the car behind stays a '?'");
            var move = BoardRules.Play(board, null, new Tap(TapKind.Lane, 0, 0));
            Expect(move.Accepted && !board.Lanes[0].Cells[1].Car.Hidden, "amber reveals when the car in front leaves");
            Expect(board.Lanes[0].Cells[2].Car.Hidden, "lime stays hidden while amber still covers it");
        }

        public static void TunnelBlocksUntilEmpty()
        {
            var board = Tiny("[[\"tunnel:coral,coral,coral\",\"lime\",\"lime\",\"lime\"]]");
            Expect(board.Lanes[0].Cells[0].TunnelCount == 3, "tunnel shows 3");
            var taps = new List<Tap>();
            ExitPath.Collect(board, taps);
            Expect(taps.Count == 1 && taps[0].Cell == 0, "only the tunnel mouth is open");
            BoardRules.Play(board, null, taps[0]);
            Expect(board.Lanes[0].Cells[0].TunnelCount == 2, "count drops when a car leaves");
            Expect(!ExitPath.LaneOpen(board.Lanes[0], 1), "lime stays blocked");
            BoardRules.Play(board, null, new Tap(TapKind.Lane, 0, 0));
            BoardRules.Play(board, null, new Tap(TapKind.Lane, 0, 0));
            Expect(board.Lanes[0].Cells[0].Empty, "the tunnel is gone");
            Expect(ExitPath.LaneOpen(board.Lanes[0], 1), "lime can leave once the tunnel is empty");
            Expect(board.Bar.Count == 0, "three coral matched as they came out");
        }

        public static void SideLotWaitsForAClearPath()
        {
            var board = Tiny("[[\"coral\",\"coral\"]]", "[{\"lane\":0,\"at\":0,\"cars\":[\"lime\",\"lime\",\"lime\"]}]");
            Expect(!SideLotGate.IsOpen(board, 0), "the curb is blocked by the front car");
            BoardRules.Play(board, null, new Tap(TapKind.Lane, 0, 0));
            Expect(SideLotGate.IsOpen(board, 0), "the curb opens when the mouth cell is empty");
            var move = BoardRules.Play(board, null, new Tap(TapKind.Side, 0, 0));
            Expect(move.Accepted && board.Sides[0].Queue.Count == 2, "one side car left");
            Expect(board.Bar.Count == 2, "coral and lime are both in the bar");
        }

        public static void UndoRestoresThePreviousBoard()
        {
            var board = Tiny("[[\"coral\",\"teal\"],[\"teal\",\"coral\"]]");
            string before = BoardQuery.Key(board);
            var history = new List<Board>();
            BoardRules.Play(board, history, new Tap(TapKind.Lane, 0, 0));
            Expect(BoardQuery.Key(board) != before && history.Count == 1, "the tap changed the board");
            Board restored;
            Expect(BoosterKit.Undo(history, out restored), "undo should apply");
            Expect(BoardQuery.Key(restored) == before && history.Count == 0, "undo puts the car back");
        }

        public static void SuperUndoReturnsThreeCars()
        {
            var board = Tiny("[[\"coral\"],[\"teal\"],[\"amber\"],[\"lime\"]]");
            var history = new List<Board>();
            string start = BoardQuery.Key(board);
            for (int i = 0; i < 3; i++)
                BoardRules.Play(board, history, new Tap(TapKind.Lane, i, 0));
            Expect(board.Bar.Count == 3 && history.Count == 3, "three cars reached the bar");
            Board restored;
            Expect(BoosterKit.SuperUndo(history, out restored), "super undo should apply");
            Expect(history.Count == 0, "three snapshots were consumed");
            Expect(BoardQuery.Key(restored) == start, "all three cars are back on the lot");
            Expect(!restored.Failed && restored.Bar.Count == 0, "the bar is empty again");
        }

        public static void ContinueUndoesAFullBar()
        {
            var board = Tiny("[[\"coral\"],[\"coral\"],[\"amber\"],[\"amber\"],[\"lime\"],[\"lime\"],[\"violet\",\"teal\"]]");
            var history = new List<Board>();
            for (int i = 0; i < 7; i++)
                BoardRules.Play(board, history, new Tap(TapKind.Lane, i, 0));
            Expect(board.Failed && history.Count == 7, "the lot failed with history intact");
            Board restored;
            Expect(BoosterKit.SuperUndo(history, out restored), "continue uses super undo");
            Expect(!restored.Failed && restored.Bar.Count == 4, "three cars left the bar and the lot can continue");
        }

        public static void ShuffleKeepsColorCounts()
        {
            var board = Tiny("[[\"coral\",\"?coral\",\"teal\"],[\"teal\",\"amber\",\"amber\"]]");
            var before = BoardQuery.Histogram(board);
            int hiddenBefore = HiddenCount(board);
            string shape = Shape(board);
            var rng = new System.Random(2);
            Expect(BoosterKit.Shuffle(board, rng), "shuffle should apply");
            var after = BoardQuery.Histogram(board);
            for (int i = 0; i < before.Length; i++)
                Expect(before[i] == after[i], "color " + i + " count changed");
            Expect(Shape(board) == shape, "tunnels, slots, and hidden flags stay put");
            Expect(HiddenCount(board) == hiddenBefore, "hidden flags were not dropped");
            Expect(!board.Failed && !board.Won, "shuffle does not finish the lot");
        }

        public static void MagnetPullsASetOfThree()
        {
            var board = Tiny("[[\"coral\",\"teal\"],[\"coral\"],[\"coral\",\"amber\"]]");
            Expect(board.Bar.Count == 0, "bar starts empty");
            Expect(BoosterKit.Magnet(board), "magnet should find coral");
            Expect(BoardQuery.Count(board, CarColor.Coral) == 0, "three coral left the lot");
            Expect(BoardQuery.Count(board, CarColor.Teal) == 1, "teal stays");
            Expect(BoardQuery.Count(board, CarColor.Amber) == 1, "amber stays");
            Expect(board.Bar.Count == 0, "a magnet set does not fill the bar");
            Expect(!board.Won, "other cars remain");

            var shortBoard = Tiny("[[\"coral\",\"coral\",\"teal\"]]");
            string key = BoardQuery.Key(shortBoard);
            Expect(!BoosterKit.Magnet(shortBoard), "two coral is not a set");
            Expect(BoardQuery.Key(shortBoard) == key, "a refused magnet changes nothing");
        }

        public static void WinAndFailPayTheSame()
        {
            Expect(Economy.Pay(true) == 20 && Economy.Pay(false) == 20, "wins and fails both pay 20");
            Expect(Economy.UndoCost == 300 && Economy.ShuffleCost == 300, "undo and shuffle cost 300");
            Expect(Economy.SuperUndoCost == 900 && Economy.MagnetCost == 600, "super undo is 900 and magnet is 600");
            var wallet = Economy.NewWallet();
            Expect(wallet.Coins == Economy.StartCoins && wallet.Lives == Economy.MaxLives, "a new purse is full");
            Expect(Economy.TrySpend(ref wallet, Economy.UndoCost), "undo is affordable at the start");
            Expect(wallet.Coins == Economy.StartCoins - Economy.UndoCost, "coins dropped by the undo price");
            Economy.Grant(ref wallet, Economy.Pay(true));
            Economy.Grant(ref wallet, Economy.Pay(false));
            Expect(wallet.Coins == Economy.StartCoins - Economy.UndoCost + 40, "both pays landed");
        }

        public static void LivesSpendAndRegen()
        {
            var wallet = Economy.NewWallet();
            long now = 1_000_000;
            Expect(Economy.TrySpendLife(ref wallet, now), "a full purse can spend a life");
            Expect(wallet.Lives == Economy.MaxLives - 1, "one life is gone");
            Expect(Economy.SecondsUntilLife(wallet, now) == Economy.LifeRegenSeconds, "the next life is on the regen clock");
            Economy.Tick(ref wallet, now + Economy.LifeRegenSeconds);
            Expect(wallet.Lives == Economy.MaxLives && wallet.NextLifeUnix == 0, "one interval refills the last life");

            wallet.Lives = 0;
            wallet.NextLifeUnix = now + 10;
            Economy.Tick(ref wallet, now + 10 + Economy.LifeRegenSeconds);
            Expect(wallet.Lives == 2, "two intervals return two lives");
            Expect(!Economy.TrySpend(ref wallet, 999999), "the purse cannot go negative");
        }

        public static void OpenEyesOnlyOnAClearExit()
        {
            var stacked = Tiny("[[\"coral\",\"teal\"]]");
            var front = new Tap(TapKind.Lane, 0, 0);
            var behind = new Tap(TapKind.Lane, 0, 1);
            Expect(EyeState.IsOpen(stacked, front), "the mouth car is open");
            Expect(!EyeState.IsOpen(stacked, behind), "the car behind it is closed");
            var history = new List<Board>();
            BoardRules.Play(stacked, history, front);
            Expect(!EyeState.IsOpen(stacked, front), "the empty mouth is not a car");
            Expect(EyeState.IsOpen(stacked, behind), "teal opens after the car in front leaves");
            Board restored;
            Expect(BoosterKit.Undo(history, out restored), "undo should apply");
            Expect(EyeState.IsOpen(restored, front), "undo opens the mouth again");
            Expect(!EyeState.IsOpen(restored, behind), "undo closes the car that is covered again");

            var covered = Tiny("[[\"coral\",\"?amber\"]]");
            Expect(covered.Lanes[0].Cells[1].Car.Hidden, "amber starts hidden");
            Expect(!EyeState.IsOpen(covered, new Tap(TapKind.Lane, 0, 1)), "a covered car is closed");
            BoardRules.Play(covered, null, new Tap(TapKind.Lane, 0, 0));
            Expect(!covered.Lanes[0].Cells[1].Car.Hidden, "amber is revealed when the cell in front empties");
            Expect(EyeState.IsOpen(covered, new Tap(TapKind.Lane, 0, 1)), "the revealed car is open");

            var tube = Tiny("[[\"tunnel:coral,coral\",\"lime\"]]");
            Expect(EyeState.IsOpen(tube, new Tap(TapKind.Lane, 0, 0)), "a tunnel mouth with a clear exit is open");
            Expect(!EyeState.IsOpen(tube, new Tap(TapKind.Lane, 0, 1)), "lime behind the tunnel is closed");
            BoardRules.Play(tube, null, new Tap(TapKind.Lane, 0, 0));
            BoardRules.Play(tube, null, new Tap(TapKind.Lane, 0, 0));
            Expect(tube.Lanes[0].Cells[0].Empty, "the tunnel is gone");
            Expect(EyeState.IsOpen(tube, new Tap(TapKind.Lane, 0, 1)), "lime opens once the tunnel has left");

            var side = Tiny("[[\"coral\"]]", "[{\"lane\":0,\"at\":0,\"cars\":[\"?lime\"]}]");
            var curb = new Tap(TapKind.Side, 0, 0);
            Expect(!EyeState.IsOpen(side, curb), "the side lot is closed while its cell is full");
            Expect(side.Sides[0].Queue[0].Hidden, "the side mouth stays covered until it can exit");
            BoardRules.Play(side, null, new Tap(TapKind.Lane, 0, 0));
            Expect(!side.Sides[0].Queue[0].Hidden, "the side mouth reveals when the path opens");
            Expect(EyeState.IsOpen(side, curb), "the side lot is open once the path is clear");

            var mix = Tiny("[[\"coral\",\"teal\",\"amber\"],[\"lime\"]]");
            Expect(BoosterKit.Shuffle(mix, new System.Random(3)), "shuffle should apply");
            Expect(EyeState.IsOpen(mix, new Tap(TapKind.Lane, 0, 0)), "a shuffle leaves the mouth open");
            Expect(!EyeState.IsOpen(mix, new Tap(TapKind.Lane, 0, 1)), "a shuffle leaves a blocked slot closed");
            Expect(!EyeState.IsOpen(mix, new Tap(TapKind.Lane, 0, 2)), "the back of the lane stays closed");
            Expect(EyeState.IsOpen(mix, new Tap(TapKind.Lane, 1, 0)), "an open lane stays open after a shuffle");
            Expect(!EyeState.IsOpen(mix, new Tap(TapKind.Lane, 9, 0)), "a missing lane is closed");
            Expect(!EyeState.IsOpen(mix, new Tap(TapKind.Side, 0, 0)), "a missing side lot is closed");
            Expect(!EyeState.IsOpen(null, front), "a missing board is closed");

            var failed = Tiny("[[\"coral\"],[\"coral\"],[\"amber\"],[\"amber\"],[\"lime\"],[\"lime\"],[\"violet\",\"teal\"]]");
            var failHistory = new List<Board>();
            for (int i = 0; i < 7; i++)
                BoardRules.Play(failed, failHistory, new Tap(TapKind.Lane, i, 0));
            Expect(failed.Failed, "the bar filled");
            Expect(!EyeState.IsOpen(failed, new Tap(TapKind.Lane, 6, 1)), "a failed lot closes the leftover car");
            Board continued;
            Expect(BoosterKit.SuperUndo(failHistory, out continued), "continue restores a snapshot");
            Expect(EyeState.IsOpen(continued, new Tap(TapKind.Lane, 6, 0)), "the restored mouth is open");
            Expect(!EyeState.IsOpen(continued, new Tap(TapKind.Lane, 6, 1)), "the car behind the restored mouth stays closed");

            string[] names = { "coral", "amber", "lime", "teal", "violet", "rose", "sky", "gold" };
            for (int i = 0; i < names.Length; i++)
            {
                var color = (CarColor)i;
                Expect(EyeState.SpriteFile(color, true) == "car_" + names[i] + "_open.png", names[i] + " open file");
                Expect(EyeState.SpriteFile(color, false) == "car_" + names[i] + "_closed.png", names[i] + " closed file");
            }
        }

        public static void HeadingUsesTheNearestDirection()
        {
            CarFacing facing;
            Expect(!EyeState.TryNearest(0f, 0f, out facing), "a zero move has no heading");
            Expect(EyeState.TryNearest(0f, 1f, out facing) && facing == CarFacing.N, "up is N");
            Expect(EyeState.TryNearest(1f, 1f, out facing) && facing == CarFacing.NE, "up-right is NE");
            Expect(EyeState.TryNearest(1f, 0f, out facing) && facing == CarFacing.E, "right is E");
            Expect(EyeState.TryNearest(1f, -1f, out facing) && facing == CarFacing.SE, "down-right is SE");
            Expect(EyeState.TryNearest(0f, -1f, out facing) && facing == CarFacing.S, "down is S");
            Expect(EyeState.TryNearest(-1f, -1f, out facing) && facing == CarFacing.SW, "down-left is SW");
            Expect(EyeState.TryNearest(-1f, 0f, out facing) && facing == CarFacing.W, "left is W");
            Expect(EyeState.TryNearest(-1f, 1f, out facing) && facing == CarFacing.NW, "up-left is NW");
            Expect(FacingAt(20f) == CarFacing.E, "20 degrees stays east");
            Expect(FacingAt(25f) == CarFacing.NE, "25 degrees is northeast");
            Expect(FacingAt(-20f) == CarFacing.E, "20 degrees south of east stays east");
            Expect(FacingAt(-25f) == CarFacing.SE, "25 degrees south of east is southeast");
            Expect(FacingAt(90f) == CarFacing.N, "90 degrees is north");
            Expect(FacingAt(180f) == CarFacing.W, "180 degrees is west");
            Expect(FacingAt(-90f) == CarFacing.S, "-90 degrees is south");
            Expect(FacingAt(135f) == CarFacing.NW, "135 degrees is northwest");
            Expect(FacingAt(-135f) == CarFacing.SW, "-135 degrees is southwest");
            Expect(FacingAt(45f) == CarFacing.NE, "45 degrees is northeast");
            Expect(FacingAt(-45f) == CarFacing.SE, "-45 degrees is southeast");

            string[] dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            string[] names = { "coral", "amber", "lime", "teal", "violet", "rose", "sky", "gold" };
            Expect(dirs.Length == EyeState.FacingCount, "eight headings");
            for (int c = 0; c < names.Length; c++)
            {
                var color = (CarColor)c;
                for (int d = 0; d < dirs.Length; d++)
                {
                    var file = EyeState.SpriteFile(color, (CarFacing)d);
                    Expect(file == "car_" + names[c] + "_open_" + dirs[d] + ".png", file);
                }
            }
        }

        public static void AllLevelsSolvable(string json)
        {
            var levels = LevelJson.Parse(json);
            Expect(levels.Count >= 10, "need at least 10 hand-authored levels, found " + levels.Count);
            bool anyHidden = false;
            bool anyTunnel = false;
            bool anySide = false;
            for (int i = 0; i < levels.Count; i++)
            {
                var level = levels[i];
                Expect(level.Number == i + 1, "level numbers follow the file");
                Expect(!string.IsNullOrEmpty(level.Id) && !string.IsNullOrEmpty(level.Title), "level " + level.Number + " needs an id and title");
                var hist = BoardQuery.Histogram(level.Board);
                int cars = 0;
                for (int c = 0; c < hist.Length; c++)
                {
                    cars += hist[c];
                    Expect(hist[c] % MatchClear.Set == 0, level.Id + " color " + (CarColor)c + " is not a multiple of 3 (" + hist[c] + ")");
                }
                Expect(cars > 0 && cars % MatchClear.Set == 0, level.Id + " has no matching set of cars");
                if (BoardQuery.HasHidden(level.Board)) anyHidden = true;
                if (BoardQuery.HasTunnel(level.Board)) anyTunnel = true;
                if (level.Board.Sides.Count > 0) anySide = true;
                var report = LotSolve.Search(level.Board, 200000);
                Expect(report.Outlook == LotSolve.Outlook.Winnable,
                    level.Id + " outlook " + report.Outlook + " nodes " + report.Nodes + " moves " + report.Moves);
            }
            Expect(anyHidden, "the pack needs a hidden '?' car");
            Expect(anyTunnel, "the pack needs a tunnel");
            Expect(anySide, "the pack needs a side lot");
        }

        public static void RunAll(string json)
        {
            MatchThreeClears();
            SeventhCarFails();
            MatchOnSeventhDoesNotFail();
            HiddenRevealsWhenUncovered();
            TunnelBlocksUntilEmpty();
            SideLotWaitsForAClearPath();
            UndoRestoresThePreviousBoard();
            SuperUndoReturnsThreeCars();
            ContinueUndoesAFullBar();
            ShuffleKeepsColorCounts();
            MagnetPullsASetOfThree();
            OpenEyesOnlyOnAClearExit();
            HeadingUsesTheNearestDirection();
            WinAndFailPayTheSame();
            LivesSpendAndRegen();
            AllLevelsSolvable(json);
        }

        static Board Tiny(string lanes, string sides = null)
        {
            string sideJson = sides ?? "[]";
            string json = "{\"levels\":[{\"id\":\"t\",\"title\":\"T\",\"lanes\":" + lanes + ",\"sides\":" + sideJson + "}]}";
            return LevelJson.Parse(json)[0].Board;
        }

        static bool FrontHidden(Board board, int lane)
        {
            var slot = board.Lanes[lane].Cells[0];
            return !slot.Empty && !slot.Tunnel && slot.Car.Hidden;
        }

        static int HiddenCount(Board board)
        {
            int n = 0;
            BoardQuery.Each(board, delegate (Car car) { if (car.Hidden) n++; });
            return n;
        }

        static string Shape(Board board)
        {
            var sb = new System.Text.StringBuilder();
            for (int l = 0; l < board.Lanes.Count; l++)
            {
                var cells = board.Lanes[l].Cells;
                for (int c = 0; c < cells.Count; c++)
                {
                    var slot = cells[c];
                    if (slot.Tunnel) sb.Append('T').Append(slot.TunnelCount);
                    else if (slot.Empty) sb.Append('_');
                    else sb.Append(slot.Car.Hidden ? '?' : 'c');
                }
                sb.Append('|');
            }
            return sb.ToString();
        }

        static CarFacing FacingAt(float degrees)
        {
            double rad = degrees * (System.Math.PI / 180.0);
            CarFacing facing;
            bool ok = EyeState.TryNearest((float)System.Math.Cos(rad), (float)System.Math.Sin(rad), out facing);
            if (!ok) throw new System.InvalidOperationException("no heading at " + degrees);
            return facing;
        }

        static void Expect(bool ok, string message)
        {
            if (!ok) throw new System.InvalidOperationException(message);
        }
    }
}
