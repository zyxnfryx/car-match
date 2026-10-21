using System.Collections.Generic;

namespace CarMatch
{
    // One kit for the four boosters. The HUD spends coins only after these
    // return true, and the EditMode tests call the same methods.
    public static class BoosterKit
    {
        public static bool Undo(IList<Board> history, out Board restored)
        {
            restored = null;
            if (history == null || history.Count == 0) return false;
            int i = history.Count - 1;
            restored = history[i];
            history.RemoveAt(i);
            return true;
        }

        // Puts back up to three tapped cars by restoring the snapshot from
        // three accepted moves ago (fewer if the player has not moved that much).
        public static bool SuperUndo(IList<Board> history, out Board restored)
        {
            restored = null;
            if (history == null || history.Count == 0) return false;
            int drop = history.Count >= 3 ? 3 : history.Count;
            int i = history.Count - drop;
            restored = history[i];
            while (history.Count > i)
                history.RemoveAt(history.Count - 1);
            return true;
        }

        // True only if a shuffle can produce a different board: at least two cars of
        // two different colors. With one color left (or one car) the HUD greys the
        // button and nothing is charged.
        public static bool CanShuffle(Board board)
        {
            if (board == null || board.Won || board.Failed) return false;
            var paints = new List<Paint>(32);
            var colors = new List<CarColor>(32);
            Gather(board, paints, colors);
            for (int i = 1; i < colors.Count; i++)
                if (colors[i] != colors[0]) return true;
            return false;
        }

        public static bool Shuffle(Board board, System.Random rng)
        {
            if (rng == null || !CanShuffle(board)) return false;
            var paints = new List<Paint>(32);
            var colors = new List<CarColor>(32);
            Gather(board, paints, colors);
            var original = colors.ToArray();
            for (int i = colors.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = colors[i];
                colors[i] = colors[j];
                colors[j] = tmp;
            }
            // Never spend a shuffle on an identical layout: if the deal came out the same,
            // swap the first car with the first one of a different color.
            bool same = true;
            for (int i = 0; i < colors.Count && same; i++) same = colors[i] == original[i];
            if (same)
                for (int j = 1; j < colors.Count; j++)
                    if (colors[j] != colors[0]) { var tmp = colors[0]; colors[0] = colors[j]; colors[j] = tmp; break; }
            for (int i = 0; i < paints.Count; i++)
                PaintColor(board, paints[i], colors[i]);
            HiddenReveal.Sweep(board);
            return true;
        }

        // Pulls three cars of one color off the board. They clear as a set
        // and do not take holding-bar slots. Fronts go first.
        public static bool Magnet(Board board)
        {
            return Magnet(board, null, null);
        }

        // pulled: (lane, cell) of lane cars the magnet took; moves: (lane, from, to) of cars that slid up to close gaps.
        public static bool Magnet(Board board, List<int[]> pulled, List<int[]> moves)
        {
            if (board == null || board.Won || board.Failed) return false;
            var hist = BoardQuery.Histogram(board);
            int best = 2;
            int pick = -1;
            for (int i = 0; i < hist.Length; i++)
            {
                if (hist[i] <= best) continue;
                best = hist[i];
                pick = i;
            }
            if (pick < 0) return false;
            var color = (CarColor)pick;
            int need = MatchClear.Set;
            for (int l = 0; l < board.Lanes.Count && need > 0; l++)
            {
                var cells = board.Lanes[l].Cells;
                for (int c = 0; c < cells.Count && need > 0; c++)
                {
                    var slot = cells[c];
                    if (slot.Tunnel)
                    {
                        int got = PullQueue(slot.Queue, color, need);
                        need -= got;
                        TunnelSpawn.CloseIfEmpty(slot);
                        continue;
                    }
                    if (slot.Empty || slot.Car.Color != color) continue;
                    slot.Empty = true;
                    slot.Box = false;
                    if (pulled != null) pulled.Add(new[] { l, c });
                    need--;
                }
            }
            for (int s = 0; s < board.Sides.Count && need > 0; s++)
                need -= PullQueue(board.Sides[s].Queue, color, need);
            if (need != 0) return false;
            // Car Match keeps parked cars where they are (ref video): no sliding after a magnet, same as a normal send.
            HiddenReveal.Sweep(board);
            if (BoardQuery.IsClear(board))
                board.Won = true;
            return true;
        }

        // Closes holes between parked cars in a lane: cars behind a hole slide toward the exit until they
        // touch the car ahead. Leading empty cells (normal after cars leave) stay; tunnels never move.
        public static void CloseGaps(Board board, List<int[]> moves)
        {
            for (int l = 0; l < board.Lanes.Count; l++)
            {
                var cells = board.Lanes[l].Cells;
                int c = 0;
                while (c < cells.Count)
                {
                    int segEnd = c;
                    while (segEnd < cells.Count && !cells[segEnd].Tunnel) segEnd++;
                    int first = c;
                    while (first < segEnd && cells[first].Empty) first++;
                    int write = first;
                    for (int read = first; read < segEnd; read++)
                    {
                        if (cells[read].Empty) continue;
                        if (read != write)
                        {
                            cells[write] = cells[read];
                            cells[read] = new Slot { Empty = true };
                            if (moves != null) moves.Add(new[] { l, read, write });
                        }
                        write++;
                    }
                    c = segEnd + 1;
                }
            }
        }

        // Super: clears one set of three automatically. Prefers the color already waiting in the bar (those bar cars
        // count toward the set), else the color with the most cars on the lot. Cars are taken from the lot front-first.
        // pulled gets (lane, cell) of lane cars taken; barTaken gets how many bar cars of that color were used.
        public static bool Super(Board board, List<int[]> pulled, out CarColor color, out int barTaken)
        {
            color = CarColor.Coral;
            barTaken = 0;
            if (board == null || board.Won || board.Failed) return false;
            var hist = BoardQuery.Histogram(board);
            int pick = -1, bestBar = 0;
            for (int i = 0; i < hist.Length; i++)
            {
                int inBar = MatchClear.Count(board.Bar, (CarColor)i);
                if (inBar == 0 || inBar + hist[i] < MatchClear.Set) continue;
                if (inBar > bestBar) { bestBar = inBar; pick = i; }
            }
            if (pick < 0)
            {
                int best = MatchClear.Set - 1;
                for (int i = 0; i < hist.Length; i++)
                    if (hist[i] > best) { best = hist[i]; pick = i; }
            }
            if (pick < 0) return false;
            color = (CarColor)pick;
            int fromBar = System.Math.Min(MatchClear.Set - 1, MatchClear.Count(board.Bar, color));
            if (hist[pick] < MatchClear.Set - fromBar) return false;
            var trial = board.Clone();
            if (!PullFromLot(trial, color, MatchClear.Set - fromBar, null)) return false;
            PullFromLot(board, color, MatchClear.Set - fromBar, pulled);
            for (int k = 0; k < fromBar; k++)
            {
                int at = board.Bar.LastIndexOf(color);
                board.Bar.RemoveAt(at);
                if (at < board.BarFrom.Count) board.BarFrom.RemoveAt(at);
            }
            barTaken = fromBar;
            HiddenReveal.Sweep(board);
            if (BoardQuery.IsClear(board)) board.Won = true;
            return true;
        }

        static bool PullFromLot(Board board, CarColor color, int need, List<int[]> pulled)
        {
            // Front cells first across lanes, so the cars nearest the exit go.
            int depth = 0;
            for (int l = 0; l < board.Lanes.Count; l++) depth = System.Math.Max(depth, board.Lanes[l].Cells.Count);
            for (int c = 0; c < depth && need > 0; c++)
                for (int l = 0; l < board.Lanes.Count && need > 0; l++)
                {
                    var cells = board.Lanes[l].Cells;
                    if (c >= cells.Count) continue;
                    var slot = cells[c];
                    if (slot.Tunnel)
                    {
                        need -= PullQueue(slot.Queue, color, need);
                        TunnelSpawn.CloseIfEmpty(slot);
                        continue;
                    }
                    if (slot.Empty || slot.Car.Color != color) continue;
                    slot.Empty = true;
                    slot.Box = false;
                    if (pulled != null) pulled.Add(new[] { l, c });
                    need--;
                }
            for (int s = 0; s < board.Sides.Count && need > 0; s++)
                need -= PullQueue(board.Sides[s].Queue, color, need);
            return need == 0;
        }

        struct Paint
        {
            public int Kind;
            public int A;
            public int B;
            public int C;
        }

        static void Gather(Board board, List<Paint> paints, List<CarColor> colors)
        {
            for (int l = 0; l < board.Lanes.Count; l++)
            {
                var cells = board.Lanes[l].Cells;
                for (int c = 0; c < cells.Count; c++)
                {
                    var slot = cells[c];
                    if (slot.Tunnel && slot.Queue != null)
                    {
                        for (int q = 0; q < slot.Queue.Count; q++)
                        {
                            paints.Add(new Paint { Kind = 1, A = l, B = c, C = q });
                            colors.Add(slot.Queue[q].Color);
                        }
                    }
                    else if (!slot.Empty)
                    {
                        paints.Add(new Paint { Kind = 0, A = l, B = c });
                        colors.Add(slot.Car.Color);
                    }
                }
            }
            for (int s = 0; s < board.Sides.Count; s++)
            {
                var queue = board.Sides[s].Queue;
                for (int q = 0; q < queue.Count; q++)
                {
                    paints.Add(new Paint { Kind = 2, A = s, C = q });
                    colors.Add(queue[q].Color);
                }
            }
        }

        static void PaintColor(Board board, Paint paint, CarColor color)
        {
            if (paint.Kind == 0)
            {
                var slot = board.Lanes[paint.A].Cells[paint.B];
                var car = slot.Car;
                car.Color = color;
                slot.Car = car;
                return;
            }
            if (paint.Kind == 1)
            {
                var queue = board.Lanes[paint.A].Cells[paint.B].Queue;
                var car = queue[paint.C];
                car.Color = color;
                queue[paint.C] = car;
                return;
            }
            var side = board.Sides[paint.A].Queue;
            var parked = side[paint.C];
            parked.Color = color;
            side[paint.C] = parked;
        }

        static int PullQueue(List<Car> queue, CarColor color, int need)
        {
            if (queue == null || need <= 0) return 0;
            int got = 0;
            for (int i = 0; i < queue.Count && got < need;)
            {
                if (queue[i].Color != color)
                {
                    i++;
                    continue;
                }
                queue.RemoveAt(i);
                got++;
            }
            return got;
        }
    }
}
