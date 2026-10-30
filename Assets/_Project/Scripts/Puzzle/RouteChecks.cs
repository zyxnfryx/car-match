using System.Collections.Generic;

namespace CarMatch
{
    // Oct 29 playtest: no drive may pass over a parked car. Plays every level with seeded random
    // legal taps and checks each exit route, and each Undo return route, against the board.
    public static class RouteChecks
    {
        public static int NoPathOverlapsOccupiedCell(string json, int seeds = 3, int maxMoves = 40)
        {
            var levels = LevelJson.Parse(json);
            var taps = new List<Tap>(32);
            var steps = new List<DriveStep>(16);
            int checkedRoutes = 0;
            for (int li = 0; li < levels.Count; li++)
            {
                for (int seed = 0; seed < seeds; seed++)
                {
                    var rng = new System.Random(li * 7919 + seed);
                    var board = levels[li].Board.Clone();
                    var history = new List<Board>();
                    for (int m = 0; m < maxMoves && !board.Won && !board.Failed; m++)
                    {
                        ExitPath.Collect(board, taps);
                        var onLot = new List<Tap>();
                        for (int t = 0; t < taps.Count; t++)
                        {
                            var tap = taps[t];
                            if (tap.Kind == TapKind.Parked) continue;
                            onLot.Add(tap);
                            Expect(DriveRoute.Exit(board, tap, steps), levels[li].Id + " legal tap has no exit route");
                            int ownLane = tap.Kind == TapKind.Side ? board.Sides[tap.Index].Lane : tap.Index;
                            int ownCell = tap.Kind == TapKind.Side ? -99 : tap.Cell;
                            string why;
                            Expect(DriveRoute.Clear(board, steps, ownLane, ownCell, out why), levels[li].Id + " exit route: " + why);
                            Expect(steps[steps.Count - 1].Cell == DriveRoute.MouthCell, levels[li].Id + " exit ends at the mouth");
                            for (int s = 1; s < steps.Count; s++)
                                Expect(steps[s].Cell <= steps[s - 1].Cell, levels[li].Id + " exit route backs up");
                            checkedRoutes++;
                        }
                        if (onLot.Count == 0) break;
                        var pick = onLot[rng.Next(onLot.Count)];
                        var move = BoardRules.Play(board, history, pick);
                        Expect(move.Accepted, levels[li].Id + " legal tap refused");
                        if (pick.Kind != TapKind.Lane) continue;
                        var restored = history[history.Count - 1];
                        var slot = restored.Lanes[pick.Index].Cells[pick.Cell];
                        if (slot.Tunnel) continue;
                        bool fromTop;
                        Expect(DriveRoute.Return(restored, pick.Index, pick.Cell, steps, out fromTop), levels[li].Id + " undo has no way back");
                        string whyBack;
                        Expect(DriveRoute.Clear(restored, steps, pick.Index, pick.Cell, out whyBack), levels[li].Id + " return route: " + whyBack);
                        var last = steps[steps.Count - 1];
                        Expect(last.Lane == pick.Index && last.Cell == pick.Cell, levels[li].Id + " return ends on the spot");
                        checkedRoutes++;
                    }
                }
            }
            return checkedRoutes;
        }

        // A spot with a car parked behind it and none in front can only be reached through the mouth;
        // a spot boxed in on every side has no way back at all; an empty back row is reached from the top.
        public static void ReturnRoutesNeverCrossCars()
        {
            var board = new Board();
            for (int l = 0; l < 3; l++)
            {
                var lane = new Lane();
                for (int c = 0; c < 3; c++) lane.Cells.Add(new Slot { Empty = false, Car = new Car(CarColor.Teal, false) });
                board.Lanes.Add(lane);
            }
            var steps = new List<DriveStep>();
            bool fromTop;
            // (1,1) boxed in by cars everywhere: no route.
            Expect(!DriveRoute.Return(board, 1, 1, steps, out fromTop), "boxed-in spot has no route");
            // Empty the back row of lane 1: comes in from the top.
            board.Lanes[1].Cells[2].Empty = true;
            Expect(DriveRoute.Return(board, 1, 1, steps, out fromTop) && fromTop, "empty back row: in from the top");
            string why;
            Expect(DriveRoute.Clear(board, steps, 1, 1, out why), "top route is clear: " + why);
            Expect(steps[0].Cell == 3 && steps[steps.Count - 1].Cell == 1, "top route starts above the lane and ends on the spot");
            // Empty the front of lane 0 and the cell beside the spot: comes in through the mouth, sideways.
            board.Lanes[0].Cells[0].Empty = true;
            board.Lanes[0].Cells[1].Empty = true;
            Expect(DriveRoute.Return(board, 1, 1, steps, out fromTop) && !fromTop, "front path wins over the top");
            Expect(DriveRoute.Clear(board, steps, 1, 1, out why), "mouth route is clear: " + why);
            Expect(steps[0].Cell == DriveRoute.MouthCell && steps[0].Lane == 0, "enters through lane 0's mouth");
        }

        static void Expect(bool ok, string message)
        {
            if (!ok) throw new System.InvalidOperationException(message);
        }
    }
}
