using System.Collections.Generic;

namespace CarMatch
{
    // Park 3 Cars. The last cars in the bar (the right-hand end) move into a
    // 3-spot strip under the bar and stay visible. Nothing returns by itself:
    // the player taps a parked car, and only when the bar has room. If that
    // arrival completes a set of three, it clears like any other insert.
    // A second continue with 1 or 2 spots left parks only that many (the bar
    // un-fails). A full strip refuses before the ad, for VIP too.
    public static class ParkStrip
    {
        public const int Cap = 3;

        public static Tap ContinueTap
        {
            get { return new Tap(TapKind.Parked, 0, -1); }
        }

        public static bool CanReceive(Board board)
        {
            if (board == null || board.Won) return false;
            if (board.Parked.Count >= Cap || board.Bar.Count == 0) return false;
            return board.Failed || board.Bar.Count >= MatchClear.BarCap;
        }

        public static int Room(Board board)
        {
            if (board == null) return 0;
            int room = Cap - board.Parked.Count;
            return room < 0 ? 0 : room;
        }

        public static bool Legal(Board board, Tap tap)
        {
            if (board == null || board.Won || tap.Kind != TapKind.Parked) return false;
            if (tap.Cell < 0) return CanReceive(board);
            if (board.Failed) return false;
            if ((uint)tap.Cell >= (uint)board.Parked.Count) return false;
            return board.Bar.Count < MatchClear.BarCap;
        }

        // How many cars the next continue will move. Zero when the strip is full.
        public static int TakeCount(Board board)
        {
            if (!CanReceive(board)) return 0;
            int n = Room(board);
            if (n > 3) n = 3;
            if (n > board.Bar.Count) n = board.Bar.Count;
            return n;
        }

        public static MoveResult Apply(Board board, Tap tap)
        {
            if (!Legal(board, tap)) return new MoveResult();
            if (tap.Cell < 0) return MoveToStrip(board);
            return ReturnToBar(board, tap.Cell);
        }

        static MoveResult MoveToStrip(Board board)
        {
            int n = TakeCount(board);
            var moved = new List<CarColor>(n);
            for (int i = 0; i < n; i++)
            {
                int last = board.Bar.Count - 1;
                moved.Add(board.Bar[last]);
                board.Bar.RemoveAt(last);
                if (board.BarFrom.Count > last) board.BarFrom.RemoveAt(last);
            }
            moved.Reverse();
            board.Parked.AddRange(moved);
            board.StripOpen = true;
            board.Failed = false;
            return new MoveResult { Accepted = true };
        }

        static MoveResult ReturnToBar(Board board, int index)
        {
            var color = board.Parked[index];
            board.Parked.RemoveAt(index);
            while (board.BarFrom.Count < board.Bar.Count) board.BarFrom.Add(-1);
            int insertAt = MatchClear.InsertIndex(board.Bar, color);
            var clear = MatchClear.Push(board.Bar, color);
            board.BarFrom.Insert(insertAt, -1);
            if (clear.Matched && clear.ClearedFrom >= 0)
            {
                var from = board.BarFrom.GetRange(clear.ClearedFrom, MatchClear.Set);
                board.BarFrom.RemoveRange(clear.ClearedFrom, MatchClear.Set);
                MysteryBox.BreakNear(board, from, null);
            }
            HiddenReveal.Sweep(board);
            bool won = BoardQuery.IsClear(board);
            bool failed = !won && clear.Overflow;
            board.Won = won;
            board.Failed = failed;
            return new MoveResult
            {
                Accepted = true,
                Matched = clear.Matched,
                BarIndex = clear.Index,
                ClearedFrom = clear.ClearedFrom,
                Won = won,
                Failed = failed
            };
        }
    }

    // What the Park 3 Cars button may show. VIP skips the ad. No fill, or a full strip, stays shut.
    public enum ContinueFace
    {
        Ad = 0,
        Crown = 1,
        Unavailable = 2
    }

    public static class ContinueOffer
    {
        public static ContinueFace Face(bool vip, bool adReady, bool stripHasRoom)
        {
            if (!stripHasRoom) return ContinueFace.Unavailable;
            if (vip) return ContinueFace.Crown;
            if (!adReady) return ContinueFace.Unavailable;
            return ContinueFace.Ad;
        }

        public static bool CanPress(ContinueFace face)
        {
            return face != ContinueFace.Unavailable;
        }
    }
}
