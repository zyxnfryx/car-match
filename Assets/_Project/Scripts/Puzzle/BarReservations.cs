using System.Collections.Generic;

namespace CarMatch
{
    // One bar slot as the player sees it. A car owns its slot from the moment it is
    // tapped (reserved), even while it is still driving there.
    public sealed class BarEntry
    {
        public CarColor Color;
        public bool Landed;
        // -1 while the car stays in the bar; otherwise the match-of-3 it clears with.
        public int Group = -1;
        public int Seq;
    }

    // The bar the player sees while several cars are in flight. The board itself is
    // updated at tap time (BoardRules.Play), so legality, matches and the fail check
    // are decided instantly and count reserved slots. This list only decides where each
    // car parks and when a matched set may pop: a set clears once all three of its cars
    // have landed, and sets clear in the order they were made.
    public sealed class BarReservations
    {
        public readonly List<BarEntry> Entries = new List<BarEntry>();
        int _seq;
        int _nextGroup;
        int _nextClear;

        public int InFlight
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Entries.Count; i++) if (!Entries[i].Landed) n++;
                return n;
            }
        }

        // True when nothing is driving and no matched set is waiting to pop.
        public bool Settled => InFlight == 0 && _nextClear == _nextGroup;

        public void Reset(IList<CarColor> bar)
        {
            Entries.Clear();
            _seq = 0;
            _nextGroup = 0;
            _nextClear = 0;
            if (bar == null) return;
            for (int i = 0; i < bar.Count; i++)
                Entries.Add(new BarEntry { Color = bar[i], Landed = true, Seq = _seq++ });
        }

        // Same grouping rule as MatchClear.InsertIndex, ignoring cars already promised to a set.
        public int InsertIndex(CarColor color)
        {
            for (int i = Entries.Count - 1; i >= 0; i--)
                if (Entries[i].Color == color && Entries[i].Group < 0) return i + 1;
            return Entries.Count;
        }

        // Call right after BoardRules.Play accepted the tap. 'matched' is MoveResult.Matched.
        public BarEntry Reserve(CarColor color, bool matched)
        {
            var e = new BarEntry { Color = color, Seq = _seq++ };
            Entries.Insert(InsertIndex(color), e);
            if (matched)
            {
                int g = _nextGroup++;
                int need = MatchClear.Set;
                for (int i = Entries.Count - 1; i >= 0 && need > 0; i--)
                    if (Entries[i].Color == color && Entries[i].Group < 0) { Entries[i].Group = g; need--; }
            }
            return e;
        }

        public int IndexOf(BarEntry e)
        {
            return Entries.IndexOf(e);
        }

        // Marks a car as parked and returns the sets that may pop now, in order
        // (each set is the three entries, already removed from Entries).
        public List<List<BarEntry>> Land(BarEntry e)
        {
            if (e != null) e.Landed = true;
            var ready = new List<List<BarEntry>>();
            while (_nextClear < _nextGroup)
            {
                var set = new List<BarEntry>(MatchClear.Set);
                bool all = true;
                for (int i = 0; i < Entries.Count; i++)
                {
                    if (Entries[i].Group != _nextClear) continue;
                    set.Add(Entries[i]);
                    if (!Entries[i].Landed) all = false;
                }
                if (!all) break;
                for (int i = 0; i < set.Count; i++) Entries.Remove(set[i]);
                ready.Add(set);
                _nextClear++;
            }
            return ready;
        }

        // Colors left in the bar once every pending set has popped (should equal Board.Bar).
        public List<CarColor> Remaining()
        {
            var list = new List<CarColor>(Entries.Count);
            for (int i = 0; i < Entries.Count; i++)
                if (Entries[i].Group < 0) list.Add(Entries[i].Color);
            return list;
        }
    }
}
