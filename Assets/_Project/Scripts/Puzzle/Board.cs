using System.Collections.Generic;

namespace CarMatch
{
    public sealed class Slot
    {
        public bool Empty = true;
        public bool Tunnel;
        // Mystery box: a cardboard '?' box over a parked car. It blocks the lane and can't be tapped;
        // it breaks (Box = false) when a car from a neighboring cell is cleared in the bar.
        public bool Box;
        public Car Car;
        public List<Car> Queue;

        public int TunnelCount => Tunnel && Queue != null ? Queue.Count : 0;

        public Slot Clone()
        {
            var copy = new Slot
            {
                Empty = Empty,
                Tunnel = Tunnel,
                Box = Box,
                Car = Car
            };
            if (Queue != null)
            {
                copy.Queue = new List<Car>(Queue.Count);
                copy.Queue.AddRange(Queue);
            }
            return copy;
        }
    }

    public sealed class Lane
    {
        public readonly List<Slot> Cells = new List<Slot>();

        public Lane Clone()
        {
            var copy = new Lane();
            for (int i = 0; i < Cells.Count; i++)
                copy.Cells.Add(Cells[i].Clone());
            return copy;
        }
    }

    // A side lot feeds the exit through one lane cell. Index 0 of Queue is the car in front.
    public sealed class SideLot
    {
        public int Lane;
        public int Cell;
        public readonly List<Car> Queue = new List<Car>();

        public SideLot Clone()
        {
            var copy = new SideLot { Lane = Lane, Cell = Cell };
            copy.Queue.AddRange(Queue);
            return copy;
        }
    }

    // Cell 0 of each lane is the exit end, nearest the holding bar.
    public sealed class Board
    {
        public readonly List<Lane> Lanes = new List<Lane>();
        public readonly List<SideLot> Sides = new List<SideLot>();
        public readonly List<Garage> Garages = new List<Garage>();
        public readonly List<CarColor> Bar = new List<CarColor>();
        // Parallel to Bar: where each bar car came from (lane * 100 + cell), or -1 for side lots and tunnels' queues.
        public readonly List<int> BarFrom = new List<int>();
        // Cars moved out of the bar by Park 3 Cars. At most three. They stay until tapped back.
        public readonly List<CarColor> Parked = new List<CarColor>();
        public bool StripOpen;
        public bool Won;
        public bool Failed;

        public Board Clone()
        {
            var copy = new Board { Won = Won, Failed = Failed, StripOpen = StripOpen };
            for (int i = 0; i < Lanes.Count; i++)
                copy.Lanes.Add(Lanes[i].Clone());
            for (int i = 0; i < Sides.Count; i++)
                copy.Sides.Add(Sides[i].Clone());
            for (int i = 0; i < Garages.Count; i++)
                copy.Garages.Add(Garages[i].Clone());
            copy.Bar.AddRange(Bar);
            copy.BarFrom.AddRange(BarFrom);
            while (copy.BarFrom.Count < copy.Bar.Count) copy.BarFrom.Add(-1);
            copy.Parked.AddRange(Parked);
            return copy;
        }
    }
}
