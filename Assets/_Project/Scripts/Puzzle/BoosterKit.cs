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

        public static bool Shuffle(Board board, System.Random rng)
        {
            if (board == null || board.Won || board.Failed || rng == null) return false;
            var paints = new List<Paint>(32);
            var colors = new List<CarColor>(32);
            Gather(board, paints, colors);
            if (colors.Count < 2) return false;
            for (int i = colors.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = colors[i];
                colors[i] = colors[j];
                colors[j] = tmp;
            }
            for (int i = 0; i < paints.Count; i++)
                PaintColor(board, paints[i], colors[i]);
            HiddenReveal.Sweep(board);
            return true;
        }

        // Pulls three cars of one color off the board. They clear as a set
        // and do not take holding-bar slots. Fronts go first.
        public static bool Magnet(Board board)
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
                    need--;
                }
            }
            for (int s = 0; s < board.Sides.Count && need > 0; s++)
                need -= PullQueue(board.Sides[s].Queue, color, need);
            if (need != 0) return false;
            HiddenReveal.Sweep(board);
            if (BoardQuery.IsClear(board))
                board.Won = true;
            return true;
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
