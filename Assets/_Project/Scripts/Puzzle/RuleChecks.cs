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

        public static void BarKeepsColorsGrouped()
        {
            var bar = new List<CarColor>();
            MatchClear.Push(bar, CarColor.Coral);
            MatchClear.Push(bar, CarColor.Teal);
            MatchClear.Push(bar, CarColor.Lime);
            var r = MatchClear.Push(bar, CarColor.Coral);
            Expect(r.Index == 1, "second coral goes right after the first, got " + r.Index);
            Expect(bar.Count == 4 && bar[0] == CarColor.Coral && bar[1] == CarColor.Coral && bar[2] == CarColor.Teal && bar[3] == CarColor.Lime, "teal and lime slide right");
            var t = MatchClear.Push(bar, CarColor.Teal);
            Expect(t.Index == 3 && bar[3] == CarColor.Teal && bar[4] == CarColor.Lime, "teal joins teal");
            var amber = MatchClear.Push(bar, CarColor.Amber);
            Expect(amber.Index == 5 && bar[5] == CarColor.Amber, "a new color goes to the end");
            var third = MatchClear.Push(bar, CarColor.Coral);
            Expect(third.Matched && third.Index == 2 && third.ClearedFrom == 0, "three coral clear from slot 0");
            Expect(bar.Count == 4 && bar[0] == CarColor.Teal && bar[1] == CarColor.Teal && bar[2] == CarColor.Lime && bar[3] == CarColor.Amber, "the rest slide left");
            // Undo restores the exact grouped order.
            var board = Tiny("[[\"coral\",\"coral\"],[\"teal\"],[\"coral\"],[\"teal\",\"teal\"]]");
            var history = new List<Board>();
            BoardRules.Play(board, history, new Tap(TapKind.Lane, 0, 0));
            BoardRules.Play(board, history, new Tap(TapKind.Lane, 1, 0));
            BoardRules.Play(board, history, new Tap(TapKind.Lane, 2, 0));
            Expect(board.Bar.Count == 3 && board.Bar[0] == CarColor.Coral && board.Bar[1] == CarColor.Coral && board.Bar[2] == CarColor.Teal, "board bar groups coral");
            Board back;
            BoosterKit.Undo(history, out back);
            Expect(back.Bar.Count == 2 && back.Bar[0] == CarColor.Coral && back.Bar[1] == CarColor.Teal, "undo restores the bar order");
        }

        public static void ProgressionNeverSkips()
        {
            int saved = 0;
            for (int i = 0; i < 99; i++)
            {
                saved = Progress.AfterWin(saved, saved, 100);
                Expect(saved == i + 1, "win on level " + (i + 1) + " should open " + (i + 2) + ", got " + (saved + 1));
                // A second settle of the same win, or a stale win on an older level, changes nothing.
                Expect(Progress.AfterWin(saved, saved - 1, 100) == saved, "repeat win must not advance");
                Expect(Progress.AfterWin(saved, 0, 100) == saved, "replay of level 1 must not advance");
            }
            Expect(Progress.AfterWin(99, 99, 100) == 99, "the last level stays put");
            Expect(Progress.AfterWin(250, 3, 100) == 99, "out-of-range saves clamp");
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
            // Teal parks at the back so the '?' cars are covered from both ends.
            var board = Tiny("[[\"coral\",\"?amber\",\"?lime\",\"teal\"]]");
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
            Expect(taps.Count == 2 && taps[0].Cell == 0 && taps[1].Cell == 3, "the tunnel mouth and the back lime (out the rear) are open");
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

        // A shuffle that cannot change anything is unavailable: the button greys out,
        // Shuffle() refuses, and neither coins nor the free use are spent.
        public static void ShuffleRefusesWhenNothingCanChange()
        {
            var mono = Tiny("[[\"coral\",\"coral\"],[\"?coral\",\"coral\"]]");
            string key = BoardQuery.Key(mono);
            Expect(!BoosterKit.CanShuffle(mono), "one color left cannot be shuffled");
            Expect(!BoosterKit.Shuffle(mono, new System.Random(1)), "shuffle refuses a one-color lot");
            Expect(BoardQuery.Key(mono) == key, "a refused shuffle leaves the lot untouched");
            var single = Tiny("[[\"teal\"]]");
            Expect(!BoosterKit.CanShuffle(single), "one car cannot be shuffled");
            Expect(!BoosterKit.CanShuffle(null), "no board, no shuffle");

            var flags = new MemoryFlags();
            Expect(FreeBoosters.IsFree(flags, "Shuffle"), "shuffle starts with a free use");
            int purse = 1000;
            System.Func<int, bool> spend = c => { if (purse < c) return false; purse -= c; return true; };
            // The HUD only pays after a shuffle applied, mirroring CarMatchApp.OnShuffle.
            if (BoosterKit.CanShuffle(mono) && BoosterKit.Shuffle(mono, new System.Random(1)))
                FreeBoosters.Pay(flags, "Shuffle", Economy.ShuffleCost, spend);
            Expect(FreeBoosters.IsFree(flags, "Shuffle"), "the free use is kept");
            Expect(purse == 1000, "no coins charged");

            // Two colors: every shuffle actually changes the layout (never a wasted deal).
            for (int seed = 0; seed < 50; seed++)
            {
                var two = Tiny("[[\"coral\",\"teal\"]]");
                string k2 = BoardQuery.Key(two);
                Expect(BoosterKit.CanShuffle(two), "two colors can shuffle");
                Expect(BoosterKit.Shuffle(two, new System.Random(seed)), "shuffle applies");
                Expect(BoardQuery.Key(two) != k2, "seed " + seed + ": shuffle changed the layout");
            }
        }

        // Rapid taps: each tap applies to the board at once (the car leaves its spot at tap time),
        // so the car behind it is tappable on the very next tap, with no waiting on a drive.
        public static void RapidMultiTap()
        {
            var board = Tiny("[[\"coral\",\"teal\",\"amber\"],[\"coral\",\"teal\"],[\"coral\",\"amber\"],[\"teal\"]]");
            var history = new List<Board>();
            var legal = new LegalMoves();
            var bar = new BarReservations();
            bar.Reset(board.Bar);
            var flying = new List<BarEntry>();
            Tap[] taps =
            {
                new Tap(TapKind.Lane, 0, 0), new Tap(TapKind.Lane, 0, 1), new Tap(TapKind.Lane, 1, 0),
                new Tap(TapKind.Lane, 2, 0), new Tap(TapKind.Lane, 1, 1), new Tap(TapKind.Lane, 3, 0),
            };
            for (int i = 0; i < taps.Length; i++)
            {
                legal.Rebuild(board);
                Expect(legal.IsLegal(taps[i]), "tap " + i + " is legal immediately (no landing needed)");
                Expect(legal.IsLegal(taps[i]) == ExitPath.IsLegal(board, taps[i]), "cache agrees with ExitPath");
                var color = ExitPath.ColorOf(board, taps[i]);
                var move = BoardRules.Play(board, history, taps[i]);
                Expect(move.Accepted, "tap " + i + " accepted");
                flying.Add(bar.Reserve(color, move.Matched));
            }
            Expect(bar.InFlight == 6, "six cars in flight at once");
            Expect(history.Count == 6, "every tap is undoable");
            // Blocked taps are simply refused and do not disturb anything.
            legal.Rebuild(board);
            Expect(!legal.IsLegal(new Tap(TapKind.Lane, 2, 1)) || ExitPath.IsLegal(board, new Tap(TapKind.Lane, 2, 1)), "cache matches rules for blocked cars");
            // Two sets were made (coral x3, teal x3); the lot's cars are all gone except amber x2.
            Expect(BoardQuery.Count(board, CarColor.Coral) == 0 && BoardQuery.Count(board, CarColor.Teal) == 0, "coral and teal cleared logically");
            for (int i = flying.Count - 1; i >= 0; i--) bar.Land(flying[i]);
            Expect(bar.Settled, "everything landed and popped");
            ExpectSame(bar.Remaining(), board.Bar, "view bar equals board bar after rapid taps");
        }

        // Reserved slots: a set pops only when all three of its cars have parked, sets pop in
        // the order they were made, and arrivals out of order never break color grouping.
        public static void ReservedSlotsMatchInOrder()
        {
            var bar = new BarReservations();
            bar.Reset(new List<CarColor> { CarColor.Amber });
            var c1 = bar.Reserve(CarColor.Coral, false);
            var t1 = bar.Reserve(CarColor.Teal, false);
            var c2 = bar.Reserve(CarColor.Coral, false);
            Expect(bar.IndexOf(c2) == bar.IndexOf(c1) + 1, "second coral parks next to the first");
            var t2 = bar.Reserve(CarColor.Teal, false);
            var c3 = bar.Reserve(CarColor.Coral, true);   // coral set (group 0)
            var t3 = bar.Reserve(CarColor.Teal, true);    // teal set (group 1)
            Expect(c3.Group == 0 && c1.Group == 0 && c2.Group == 0, "three corals share set 0");
            Expect(t3.Group == 1 && t1.Group == 1 && t2.Group == 1, "three teals share set 1");
            var c4 = bar.Reserve(CarColor.Coral, false);
            Expect(c4.Group < 0, "a fourth coral starts a new group");
            Expect(bar.IndexOf(c4) == bar.Entries.Count - 1, "a coral after a promised set goes to the end");
            // Teal lands completely first, but must wait for the coral set (made first).
            Expect(bar.Land(t3).Count == 0, "teal 3 landed");
            Expect(bar.Land(t1).Count == 0, "teal 1 landed");
            Expect(bar.Land(t2).Count == 0, "teal set complete but waits for the coral set");
            Expect(bar.Land(c3).Count == 0, "coral 3 landed first, out of order");
            Expect(bar.Land(c1).Count == 0, "coral 1 landed");
            var popped = bar.Land(c2);
            Expect(popped.Count == 2, "coral set then teal set pop together, in order");
            Expect(popped[0][0].Color == CarColor.Coral && popped[1][0].Color == CarColor.Teal, "sets pop in the order they were made");
            Expect(!bar.Settled, "coral 4 still driving");
            bar.Land(c4);
            Expect(bar.Settled, "settled");
            ExpectSame(bar.Remaining(), new List<CarColor> { CarColor.Amber, CarColor.Coral }, "amber and the last coral remain");
        }

        // Fail check counts reserved slots: seven different colors tapped in a burst fail the lot
        // at the seventh tap, before any of them has parked, and further taps are refused.
        public static void ReservedSlotsCountTowardFail()
        {
            var board = Tiny("[[\"coral\"],[\"rose\"],[\"amber\"],[\"gold\"],[\"lime\"],[\"teal\"],[\"sky\"],[\"violet\"]]");
            var history = new List<Board>();
            var bar = new BarReservations();
            bar.Reset(board.Bar);
            for (int i = 0; i < 7; i++)
            {
                var tap = new Tap(TapKind.Lane, i, 0);
                var move = BoardRules.Play(board, history, tap);
                Expect(move.Accepted, "burst tap " + i + " accepted");
                bar.Reserve(board.Bar[board.Bar.Count - 1], move.Matched);
                Expect(move.Failed == (i == 6), "only the seventh reserved slot fails the lot");
            }
            Expect(bar.InFlight == 7, "all seven still driving");
            Expect(board.Failed, "the lot failed on reserved slots");
            Expect(!ExitPath.IsLegal(board, new Tap(TapKind.Lane, 7, 0)), "no taps after the bar is full");
        }

        static void ExpectSame(IList<CarColor> a, IList<CarColor> b, string what)
        {
            bool same = a.Count == b.Count;
            for (int i = 0; same && i < a.Count; i++) same = a[i] == b[i];
            Expect(same, what);
        }

        static Board Garaged(string lanes, string garages)
        {
            string json = "{\"levels\":[{\"id\":\"t\",\"title\":\"T\",\"lanes\":" + lanes + ",\"garages\":" + garages + "}]}";
            return LevelJson.Parse(json)[0].Board;
        }

        // Underground garage: the hatch under spots (0,1)-(0,2) opens only when both are empty,
        // then queued cars come up hidden into those spots; the lot is not clear until it is empty.
        public static void GarageRefillsWhenSpotsClear()
        {
            var board = Garaged("[[\"teal\",\"coral\",\"amber\",\"lime\"],[\"coral\",\"coral\"]]",
                "[{\"lane\":0,\"cells\":[1,2],\"cars\":[\"amber\",\"amber\",\"teal\"]}]");
            var g = board.Garages[0];
            Expect(g.Queue.Count == 3, "three cars wait underground");
            Expect(!BoardQuery.IsClear(board), "garage cars count");
            Expect(BoardQuery.Count(board, CarColor.Amber) == 3, "amber counted underground too");
            BoardRules.Play(board, null, new Tap(TapKind.Lane, 0, 0)); // teal (front)
            BoardRules.Play(board, null, new Tap(TapKind.Lane, 0, 1)); // coral leaves spot 1
            Expect(g.Queue.Count == 3 && g.Opens == 0, "hatch stays shut while spot 2 is taken");
            BoardRules.Play(board, null, new Tap(TapKind.Lane, 0, 2)); // amber leaves spot 2
            Expect(g.Opens == 1 && g.Queue.Count == 1, "hatch opened and two cars came up");
            var up = board.Lanes[0].Cells[1];
            Expect(!up.Empty && up.Car.Color == CarColor.Amber, "first queued car parks on the first spot");
            Expect(!up.Car.Hidden, "the arrival at the open front is revealed");
            Expect(board.Lanes[0].Cells[2].Car.Hidden, "the one behind stays a '?' (lime still parks behind it)");
            var r = LotSolve.Search(board);
            Expect(r.Outlook == LotSolve.Outlook.Winnable, "the solver plays through the garage");
            var copy = board.Clone();
            Expect(copy.Garages.Count == 1 && copy.Garages[0].Queue.Count == 1, "clone keeps the garage");
            Expect(BoardQuery.Key(copy) == BoardQuery.Key(board), "key includes the garage");
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

        // Alive idle: awake trucks look around, blink, bounce and puff; blocked ones only breathe.
        // The loop is pure view state: ticking it never changes legality, and a tap is accepted
        // on the very same frame (no lock, no wait for an idle beat to finish).
        public static void IdleLoopNeverBlocksInput()
        {
            var board = Tiny("[[\"teal\",\"teal\",\"coral\"],[\"amber\",\"coral\",\"amber\"]]");
            var legal = new LegalMoves();
            legal.Rebuild(board);
            var idles = new AliveIdle[6];
            for (int l = 0, k = 0; l < 2; l++)
                for (int c = 0; c < 3; c++, k++)
                    idles[k] = AliveIdle.Create((uint)(k + 1), legal.IsLegal(new Tap(TapKind.Lane, l, c)));
            int puffs = 0, calmPuffs = 0, blinks = 0, squints = 0, wide = 0;
            var seen = new bool[5];
            float maxCalm = 0f, maxAwake = 0f;
            var prev = new IdleEyes[6];
            var prevHop = new float[6]; int hops = 0;
            for (int f = 0; f < 60 * 30; f++)
            {
                for (int k = 0; k < idles.Length; k++)
                {
                    idles[k].Tick(1f / 60f);
                    var a = idles[k];
                    float dev = a.HopY;
                    Expect(a.HopY >= 0f && System.Math.Abs(a.ScaleY - 1f) < 0.03f, "sputter stays subtle");
                    if (a.Eyes == IdleEyes.Blink || a.Eyes == IdleEyes.Squint) Expect(a.HopY == 0f && a.JitterX == 0f, "no hop while the eyes blink or squint");
                    if (a.Awake && a.HopY > 0f && prevHop[k] == 0f) hops++;
                    prevHop[k] = a.HopY;
                    if (a.Awake)
                    {
                        if (a.PuffNow) puffs++;
                        if (dev > maxAwake) maxAwake = dev;
                        seen[(int)AliveIdle.Classify(a.Look, a.LookUp, a.LookDown)] = true;
                        if (a.Eyes == IdleEyes.Squint && prev[k] != IdleEyes.Squint) squints++;
                        if (a.Eyes == IdleEyes.Wide) wide++;
                    }
                    else
                    {
                        if (a.PuffNow) calmPuffs++;
                        if (dev > maxCalm) maxCalm = dev;
                        Expect(a.Look == 0f && a.LookUp == 0f && a.LookDown == 0f, "blocked trucks keep still eyes");
                    }
                    if (a.Eyes == IdleEyes.Blink && prev[k] != IdleEyes.Blink) blinks++;
                    prev[k] = a.Eyes;
                }
                // Legality is untouched by the idle loop, every frame.
                if (f % 97 == 0)
                    for (int l = 0; l < 2; l++)
                        for (int c = 0; c < 3; c++)
                        {
                            var t = new Tap(TapKind.Lane, l, c);
                            Expect(legal.IsLegal(t) == ExitPath.IsLegal(board, t), "idle never changes legality");
                        }
            }
            Expect(puffs >= 4 * 30 / 3 - 4, "awake trucks puff every 1.5-3 s (" + puffs + ")");
            Expect(calmPuffs == 0, "blocked trucks never puff");
            Expect(seen[0] && seen[1] && seen[2] && seen[3] && seen[4], "looks ahead, left, right, at the player and at the bar");
            Expect(blinks >= 6 * 30 / 5, "blinks every 2-5 s (" + blinks + ")");
            Expect(squints > 0 && wide > 0, "happy squints and wide eye contact happen");
            Expect(maxAwake > 0.008f && maxAwake <= AliveIdle.HopMax + 1e-4f, "awake sputter hops are small (" + maxAwake + ")");
            Expect(maxCalm == 0f, "sleeping trucks never hop (" + maxCalm + ")");
            Expect(hops > 4 * 30 * 2, "awake trucks sputter often (" + hops + " hops)");
            var a0 = AliveIdle.Create(1, true); var a1 = AliveIdle.Create(2, true);
            bool diff = false;
            for (int f = 0; f < 60; f++) { a0.Tick(1f / 60f); a1.Tick(1f / 60f); if (System.Math.Abs(a0.HopY - a1.HopY) > 1e-4f) diff = true; }
            Expect(diff, "trucks sputter out of sync");
            // A tap mid-idle (mid-blink, mid-look) is accepted immediately.
            var back = new Tap(TapKind.Lane, 0, 2);
            Expect(legal.IsLegal(back), "the back coral is tappable while idling");
            var move = BoardRules.Play(board, null, back);
            Expect(move.Accepted && board.Bar.Count == 1, "the tap lands on the same frame");
            legal.Rebuild(board);
            Expect(legal.IsLegal(new Tap(TapKind.Lane, 0, 1)), "the next car is tappable right away");
            idles[4].SetAwake(true);
            idles[4].Tick(1f / 60f);
            Expect(idles[4].Awake, "a newly unblocked truck wakes into the alive loop");
            // Live-eye layers: pupils never leave the travel ellipse; faced-away headings use baked frames.
            for (int h = 0; h < 16; h++)
            {
                var lay = EyeLayout.For(h);
                Expect(lay.Valid == (h < 6 || h > 10), "eye layers exist except h06-h10 (h" + h + ")");
                if (!lay.Valid) continue;
                for (int k = 0; k < 64; k++)
                {
                    float ang = k * 0.0982f, mag = 0.5f + (k % 4) * 0.5f;
                    float ox, oy;
                    lay.PupilOffset((float)System.Math.Cos(ang) * mag, (float)System.Math.Sin(ang) * mag, out ox, out oy);
                    // A zero travel axis (side headings) means the pupil must not move on that axis at all.
                    if (lay.TravelX <= 0f) Expect(ox == 0f, "no horizontal pupil travel when the art allows none");
                    if (lay.TravelY <= 0f) Expect(oy == 0f, "no vertical pupil travel when the art allows none");
                    float e = (lay.TravelX > 0f ? (ox * ox) / (lay.TravelX * lay.TravelX) : 0f)
                        + (lay.TravelY > 0f ? (oy * oy) / (lay.TravelY * lay.TravelY) : 0f);
                    Expect(e <= 1.0001f, "pupil stays inside the travel ellipse");
                }
            }
        }

        // Car Match rule: a car may leave out either end of its lane. The back car of a
        // stacked lane is awake and tappable (rear exit onto the ring road); middle cars are not.
        public static void BackOfLaneCanExit()
        {
            var board = Tiny("[[\"teal\",\"teal\",\"coral\"],[\"amber\",\"coral\",\"amber\"]]");
            var back = new Tap(TapKind.Lane, 0, 2);
            Expect(ExitPath.IsLegal(board, back), "the back car with open road behind it can leave");
            Expect(EyeState.IsOpen(board, back), "and it is drawn awake");
            Expect(ExitPath.ExitsBack(board, back), "it drives out the rear");
            Expect(!ExitPath.ExitsBack(board, new Tap(TapKind.Lane, 0, 0)), "the front car uses the front");
            Expect(!ExitPath.IsLegal(board, new Tap(TapKind.Lane, 0, 1)), "a middle car is blocked both ways");
            Expect(!ExitPath.IsLegal(board, new Tap(TapKind.Lane, 1, 1)), "the middle coral is blocked both ways");
            var taps = new List<Tap>();
            ExitPath.Collect(board, taps);
            Expect(taps.Count == 4, "front and back of both lanes are offered");
            var legal = new LegalMoves();
            legal.Rebuild(board);
            for (int l = 0; l < board.Lanes.Count; l++)
                for (int c = 0; c < board.Lanes[l].Cells.Count; c++)
                {
                    var t = new Tap(TapKind.Lane, l, c);
                    Expect(legal.IsLegal(t) == ExitPath.IsLegal(board, t), "cache matches the rule at " + l + "," + c);
                }
            var move = BoardRules.Play(board, null, back);
            Expect(move.Accepted && board.Bar.Count == 1 && board.Bar[0] == CarColor.Coral, "the rear exit reaches the bar");
            Expect(ExitPath.IsLegal(board, new Tap(TapKind.Lane, 0, 1)), "the middle teal is now the back car");
            BoardRules.Play(board, null, new Tap(TapKind.Lane, 1, 2));
            Expect(ExitPath.IsLegal(board, new Tap(TapKind.Lane, 1, 1)), "coral opens from the rear");
            var m2 = BoardRules.Play(board, null, new Tap(TapKind.Lane, 1, 1));
            Expect(m2.Accepted && board.Bar.Count == 3 && board.Bar[0] == CarColor.Coral && board.Bar[1] == CarColor.Coral, "two coral grouped, amber after");
        }

        public static void OpenEyesOnlyOnAClearExit()
        {
            // Gold parks at the back so teal is covered from both ends.
            var stacked = Tiny("[[\"coral\",\"teal\",\"gold\"]]");
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

            var covered = Tiny("[[\"coral\",\"?amber\",\"gold\"]]");
            Expect(covered.Lanes[0].Cells[1].Car.Hidden, "amber starts hidden");
            Expect(!EyeState.IsOpen(covered, new Tap(TapKind.Lane, 0, 1)), "a covered car is closed");
            BoardRules.Play(covered, null, new Tap(TapKind.Lane, 0, 0));
            Expect(!covered.Lanes[0].Cells[1].Car.Hidden, "amber is revealed when the cell in front empties");
            Expect(EyeState.IsOpen(covered, new Tap(TapKind.Lane, 0, 1)), "the revealed car is open");

            var tube = Tiny("[[\"tunnel:coral,coral\",\"lime\",\"gold\"]]");
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
            Expect(EyeState.IsOpen(mix, new Tap(TapKind.Lane, 0, 2)), "the back of the lane is open out the rear");
            Expect(EyeState.IsOpen(mix, new Tap(TapKind.Lane, 1, 0)), "an open lane stays open after a shuffle");
            Expect(!EyeState.IsOpen(mix, new Tap(TapKind.Lane, 9, 0)), "a missing lane is closed");
            Expect(!EyeState.IsOpen(mix, new Tap(TapKind.Side, 0, 0)), "a missing side lot is closed");
            Expect(!EyeState.IsOpen(null, front), "a missing board is closed");

            var failed = Tiny("[[\"coral\"],[\"coral\"],[\"amber\"],[\"amber\"],[\"lime\"],[\"lime\"],[\"violet\",\"teal\",\"gold\"]]");
            var failHistory = new List<Board>();
            for (int i = 0; i < 7; i++)
                BoardRules.Play(failed, failHistory, new Tap(TapKind.Lane, i, 0));
            Expect(failed.Failed, "the bar filled");
            Expect(!EyeState.IsOpen(failed, new Tap(TapKind.Lane, 6, 1)), "a failed lot closes the leftover car");
            Board continued;
            Expect(BoosterKit.SuperUndo(failHistory, out continued), "continue restores a snapshot");
            Expect(EyeState.IsOpen(continued, new Tap(TapKind.Lane, 6, 0)), "the restored mouth is open");
            Expect(!EyeState.IsOpen(continued, new Tap(TapKind.Lane, 6, 1)), "the car behind the restored mouth stays closed");

            string[] art = { "red", "yellow", "green", "teal", "purple", "red", "blue", "yellow" };
            var distinct = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < art.Length; i++)
            {
                var color = (CarColor)i;
                distinct.Add(art[i]);
                Expect(EyeState.Token(color) == art[i], color + " maps to " + art[i]);
                Expect(EyeState.SpriteFile(color, true) == "car_" + art[i] + "_open.png", color + " open file");
                Expect(EyeState.SpriteFile(color, false) == "car_" + art[i] + "_closed.png", color + " closed file");
            }
            Expect(distinct.Count == 6, "the eight game colors share six painted sets");
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
            Expect(dirs.Length == EyeState.FacingCount, "eight headings");
            for (int c = 0; c < Palette.Count; c++)
            {
                var color = (CarColor)c;
                for (int d = 0; d < dirs.Length; d++)
                {
                    var file = EyeState.SpriteFile(color, (CarFacing)d);
                    Expect(file == "car_" + EyeState.Token(color) + "_open_" + dirs[d] + ".png", file);
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
            BarKeepsColorsGrouped();
            ProgressionNeverSkips();
            SeventhCarFails();
            MatchOnSeventhDoesNotFail();
            HiddenRevealsWhenUncovered();
            TunnelBlocksUntilEmpty();
            SideLotWaitsForAClearPath();
            UndoRestoresThePreviousBoard();
            SuperUndoReturnsThreeCars();
            ContinueUndoesAFullBar();
            ShuffleKeepsColorCounts();
            ShuffleRefusesWhenNothingCanChange();
            RapidMultiTap();
            GarageRefillsWhenSpotsClear();
            BackOfLaneCanExit();
            IdleLoopNeverBlocksInput();
            ReservedSlotsMatchInOrder();
            ReservedSlotsCountTowardFail();
            MagnetPullsASetOfThree();
            OpenEyesOnlyOnAClearExit();
            HeadingUsesTheNearestDirection();
            WinAndFailPayTheSame();
            LivesSpendAndRegen();
            AllLevelsSolvable(json);
            BoostersOnEveryLevel(json);
            LevelsAreUnique(json);
            EachBoosterOnceFree();
            LevelsForGapCheck.Clear();
            foreach (var d in LevelJson.Parse(json)) LevelsForGapCheck.Add(d.Board);
            MagnetClosesLaneGaps();
            SuperClearsASet();
            MysteryBoxBreaksNextToAMatch();
            SuperHardEveryTenth(json);
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

        // Shuffle, Super Undo and Magnet on every level, at the start and after a few moves:
        // color totals (board + bar) stay whole sets, Super puts cars back, a failed booster leaves the board untouched.
        public static void BoostersOnEveryLevel(string json)
        {
            var rng = new System.Random(7);
            var defs = LevelJson.Parse(json);
            var taps = new List<Tap>(16);
            foreach (var def in defs)
            {
                for (int moves = 0; moves <= 4; moves += 2)
                {
                    var board = def.Board.Clone();
                    var history = new List<Board>();
                    for (int m = 0; m < moves && !board.Won && !board.Failed; m++)
                    {
                        taps.Clear();
                        ExitPath.Collect(board, taps);
                        if (taps.Count == 0) break;
                        BoardRules.Play(board, history, taps[rng.Next(taps.Count)]);
                    }
                    if (board.Won || board.Failed) continue;
                    string tag = "level " + def.Number + " after " + moves + ": ";
                    int[] before = Totals(board);

                    var sh = board.Clone();
                    if (BoosterKit.Shuffle(sh, rng))
                    {
                        int[] after = Totals(sh);
                        for (int i = 0; i < after.Length; i++)
                            Ensure(after[i] == before[i], tag + "shuffle changed color counts");
                        Ensure(BoardQuery.Total(sh) == BoardQuery.Total(board), tag + "shuffle changed car count");
                    }

                    var mg = board.Clone();
                    string key = BoardQuery.Key(mg);
                    if (BoosterKit.Magnet(mg))
                    {
                        Ensure(BoardQuery.Total(mg) == BoardQuery.Total(board) - MatchClear.Set, tag + "magnet did not pull exactly three");
                        int[] after = Totals(mg);
                        for (int i = 0; i < after.Length; i++)
                            Ensure(after[i] % MatchClear.Set == before[i] % MatchClear.Set, tag + "magnet broke a color set");
                        Ensure(!mg.Won || (BoardQuery.IsClear(mg) && mg.Bar.Count == 0), tag + "magnet won with cars left");
                    }
                    else Ensure(BoardQuery.Key(mg) == key, tag + "failed magnet changed the board");

                    var sp = board.Clone();
                    CarColor sc; int sb;
                    if (BoosterKit.Super(sp, null, out sc, out sb))
                    {
                        Ensure(BoardQuery.Total(sp) == BoardQuery.Total(board) - (MatchClear.Set - sb), tag + "super removed the wrong number of lot cars");
                        int[] after = Totals(sp);
                        for (int i = 0; i < after.Length; i++)
                            Ensure(after[i] % MatchClear.Set == before[i] % MatchClear.Set, tag + "super broke a color set");
                    }

                    if (history.Count > 0)
                    {
                        var hist = new List<Board>(history);
                        Board restored;
                        Ensure(BoosterKit.SuperUndo(hist, out restored), tag + "super refused");
                        int back = System.Math.Min(3, history.Count);
                        Ensure(hist.Count == history.Count - back, tag + "super dropped the wrong number of snapshots");
                        Ensure(BoardQuery.Total(restored) >= BoardQuery.Total(board), tag + "super did not return cars");
                    }
                }
            }
        }

        static int[] Totals(Board board)
        {
            var n = BoardQuery.Histogram(board);
            for (int i = 0; i < board.Bar.Count; i++) n[(int)board.Bar[i]]++;
            return n;
        }

        static void Ensure(bool ok, string message)
        {
            if (!ok) throw new System.InvalidOperationException(message);
        }

        // No two levels may share the same layout and colors.
        public static void LevelsAreUnique(string json)
        {
            var seen = new Dictionary<string, int>();
            foreach (var def in LevelJson.Parse(json))
            {
                string key = BoardQuery.Key(def.Board);
                int other;
                if (seen.TryGetValue(key, out other))
                    throw new System.InvalidOperationException("level " + def.Number + " repeats level " + other);
                seen[key] = def.Number;
            }
        }

        // Every booster is free exactly once, then costs its normal price; a fresh store (existing player) starts free.
        public static void EachBoosterOnceFree()
        {
            var store = new MemoryFlags();
            int[] costs = { Economy.UndoCost, Economy.ShuffleCost, Economy.SuperUndoCost, Economy.MagnetCost };
            for (int i = 0; i < FreeBoosters.Names.Length; i++)
            {
                string name = FreeBoosters.Names[i];
                int coins = 0;
                int charged = -1;
                System.Func<int, bool> spend = delegate (int c) { if (coins < c) return false; coins -= c; charged = c; return true; };
                Expect(FreeBoosters.IsFree(store, name), name + " starts free");
                Expect(FreeBoosters.Pay(store, name, costs[i], spend) && charged < 0, name + " first use is free with 0 coins");
                Expect(!FreeBoosters.IsFree(store, name), name + " free use is spent");
                Expect(!FreeBoosters.Pay(store, name, costs[i], spend), name + " second use needs coins");
                coins = costs[i];
                Expect(FreeBoosters.Pay(store, name, costs[i], spend) && charged == costs[i] && coins == 0, name + " then charges the normal price");
                Expect(FreeBoosters.Key(name).StartsWith("lotloop."), name + " key is under lotloop.*");
            }
        }

        // Car Match keeps parked cars where they are after a car leaves; the magnet does the same.
        public static void MagnetClosesLaneGaps()
        {
            var board = Tiny("[[\"teal\",\"coral\",\"amber\",\"teal\"],[\"coral\"],[\"coral\",\"amber\"]]");
            Expect(BoosterKit.Magnet(board), "magnet should find coral");
            var lane = board.Lanes[0].Cells;
            Expect(!lane[0].Empty && lane[0].Car.Color == CarColor.Teal, "front teal stays put");
            Expect(lane[1].Empty, "the pulled coral leaves an empty bay, like a normal send");
            Expect(!lane[2].Empty && lane[2].Car.Color == CarColor.Amber, "amber stays parked");
            Expect(!lane[3].Empty && lane[3].Car.Color == CarColor.Teal, "rear teal stays parked");
        }

        // Super clears a set: uses matching bar cars first, then lot cars, and never refills the lot.
        public static void SuperClearsASet()
        {
            var board = Tiny("[[\"coral\",\"teal\"],[\"amber\",\"teal\"],[\"teal\",\"amber\",\"amber\"]]");
            BoardRules.Play(board, null, new Tap(TapKind.Lane, 0, 0));
            BoardRules.Play(board, null, new Tap(TapKind.Lane, 1, 0));
            int before = BoardQuery.Total(board);
            var pulled = new List<int[]>();
            CarColor color; int fromBar;
            Expect(BoosterKit.Super(board, pulled, out color, out fromBar), "super applies");
            Expect(color == CarColor.Amber || color == CarColor.Coral, "super picks a color waiting in the bar");
            Expect(BoardQuery.Total(board) == before - (MatchClear.Set - fromBar), "super only removes cars");
            Expect(MatchClear.Count(board.Bar, color) == 0, "the bar car of that color is used up");
            var lone = Tiny("[[\"coral\",\"teal\"]]");
            CarColor c2; int b2;
            Expect(!BoosterKit.Super(lone, null, out c2, out b2), "no set possible, super refuses");
        }

        public static List<Board> LevelsForGapCheck = new List<Board>();

        // A box blocks its lane until a car from a neighboring cell is cleared in a match; then its car shows.
        public static void MysteryBoxBreaksNextToAMatch()
        {
            var board = Tiny("[[\"coral\",\"box:teal\",\"teal\"],[\"coral\",\"amber\"],[\"coral\",\"teal\"],[\"amber\",\"amber\"]]");
            Expect(!ExitPath.IsLegal(board, new Tap(TapKind.Lane, 0, 1)), "a box can't be tapped");
            BoardRules.Play(board, null, new Tap(TapKind.Lane, 0, 0));
            Expect(board.Lanes[0].Cells[1].Box, "one coral in the bar is not a clear");
            BoardRules.Play(board, null, new Tap(TapKind.Lane, 1, 0));
            var m = BoardRules.Play(board, null, new Tap(TapKind.Lane, 2, 0));
            Expect(m.Matched, "three coral clear");
            Expect(!board.Lanes[0].Cells[1].Box, "the box next to the cleared coral broke");
            Expect(ExitPath.IsLegal(board, new Tap(TapKind.Lane, 0, 1)), "the revealed teal can leave");
            var far = Tiny("[[\"coral\"],[\"coral\"],[\"coral\"],[\"amber\",\"amber\",\"box:lime\"]]");
            for (int i = 0; i < 3; i++) BoardRules.Play(far, null, new Tap(TapKind.Lane, i, 0));
            Expect(far.Lanes[3].Cells[2].Box, "a box away from the match stays shut");
            Expect(LotSolve.Search(Tiny("[[\"coral\",\"box:teal\",\"teal\"],[\"coral\",\"teal\"],[\"coral\"]]")).Outlook == LotSolve.Outlook.Winnable, "the solver opens boxes");
        }

        public static void SuperHardEveryTenth(string json)
        {
            foreach (var d in LevelJson.Parse(json))
                Expect(d.SuperHard == (d.Number % 10 == 0), "level " + d.Number + " super hard flag");
            Expect(Economy.WinPayFor(true) == 100 && Economy.WinPayFor(false) == 20, "super hard pays 100, others 20");
        }
    }
}
