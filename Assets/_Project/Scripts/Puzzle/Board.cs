using System.Collections.Generic;

namespace CarMatch
{
    public sealed class Slot
    {
        public bool Empty = true;
        public bool Tunnel;
        public Car Car;
        public List<Car> Queue;

        public int TunnelCount => Tunnel && Queue != null ? Queue.Count : 0;

        public Slot Clone()
        {
            var copy = new Slot
            {
                Empty = Empty,
                Tunnel = Tunnel,
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
        public readonly List<CarColor> Bar = new List<CarColor>();
        public bool Won;
        public bool Failed;

        public Board Clone()
        {
            var copy = new Board { Won = Won, Failed = Failed };
            for (int i = 0; i < Lanes.Count; i++)
                copy.Lanes.Add(Lanes[i].Clone());
            for (int i = 0; i < Sides.Count; i++)
                copy.Sides.Add(Sides[i].Clone());
            copy.Bar.AddRange(Bar);
            return copy;
        }
    }
}
