namespace CarMatch
{
    // One glove at a time. Every tap lesson is forced (Oct 29): the lot locks to the glove's
    // target, so the player uses each taught tool once. Things that cannot be tapped yet get a
    // separate look-here hint (CoachHint) instead, which never blocks a tap. Completion is a flag the app persists.
    public enum CoachLesson
    {
        None = 0,
        MatchThree = 1,
        Covered = 2,
        SideLot = 3,
        Garage = 4,
        Undo = 5,
        Shuffle = 6,
        Magnet = 7,
        Super = 8,
        Underground = 9,
        ParkStrip = 10
    }

    public struct CoachWorld
    {
        public int Level;
        public Board Board;
        public int Moves;
        public bool CanShuffle;
        public bool CanMagnet;
        public bool CanSuper;
    }

    public struct CoachCue
    {
        public CoachLesson Lesson;
        public bool LockCars;
        public bool HasCar;
        public Tap Car;
        public string Booster;

        public bool IsBooster { get { return !string.IsNullOrEmpty(Booster); } }
    }

    public enum CoachHint
    {
        None = 0,
        Mystery = 1,
        SideLot = 2,
        LiftGate = 3
    }

    public struct HintCue
    {
        public CoachHint Hint;
        public Tap Spot;
        public int Garage;
        public string Label;
    }

    public static class LotCoach
    {
        // Canvas units. 140 is about 51pt on a tall Samsung portrait, 58pt on iPhone 18 Pro, 86pt on iPad (A16).
        public const float MinTapUnits = 140f;
        public const float GloveUnits = 168f;

        public const int UndoLevel = 3;
        public const int CoveredLevel = 4;
        public const int SideLevel = 5;
        public const int ShuffleLevel = 5;
        public const int GarageLevel = 6;
        public const int MagnetLevel = 7;
        public const int SuperLevel = 9;
        public const int UndergroundLevel = 21;

        public static string Key(CoachLesson lesson)
        {
            switch (lesson)
            {
                case CoachLesson.MatchThree: return "lotloop.coach.match3";
                case CoachLesson.Covered: return "lotloop.coach.covered";
                case CoachLesson.SideLot: return "lotloop.coach.side";
                case CoachLesson.Garage: return "lotloop.coach.garage";
                case CoachLesson.Undo: return "lotloop.coach.undo";
                case CoachLesson.Shuffle: return "lotloop.coach.shuffle";
                case CoachLesson.Magnet: return "lotloop.coach.magnet";
                case CoachLesson.Super: return "lotloop.coach.super";
                case CoachLesson.Underground: return "lotloop.coach.underground";
                case CoachLesson.ParkStrip: return "lotloop.coach.parkstrip";
                default: return "";
            }
        }

        public static void Complete(IFlagStore store, CoachLesson lesson)
        {
            if (store == null || lesson == CoachLesson.None) return;
            string key = Key(lesson);
            if (key.Length == 0 || store.Get(key)) return;
            store.Set(key);
        }

        public static bool Done(IFlagStore store, CoachLesson lesson)
        {
            if (store == null || lesson == CoachLesson.None) return false;
            return store.Get(Key(lesson));
        }

        public static CoachCue Cue(IFlagStore store, CoachWorld world)
        {
            if (store == null || world.Board == null) return default;
            Tap tap;
            if (world.Level == 1 && !Done(store, CoachLesson.MatchThree) && TryGuide(world.Board, out tap))
                return CarCue(CoachLesson.MatchThree, tap);
            // Covered / mystery cars cannot be tapped until a neighbor leaves: that is a hint now (see Hint).
            if (world.Level >= SideLevel && !Done(store, CoachLesson.SideLot) && TrySide(world.Board, out tap))
                return CarCue(CoachLesson.SideLot, tap);
            if (world.Level >= GarageLevel && !Done(store, CoachLesson.Garage) && TryTunnel(world.Board, out tap))
                return CarCue(CoachLesson.Garage, tap);
            if (world.Level >= UndergroundLevel && !Done(store, CoachLesson.Underground) && TryUnderground(world.Board, out tap))
                return CarCue(CoachLesson.Underground, tap);
            if (world.Level >= UndoLevel && world.Moves > 0 && !Done(store, CoachLesson.Undo) && FreeBoosters.IsFree(store, "Undo"))
                return Boost(CoachLesson.Undo, "Undo");
            if (world.Level >= ShuffleLevel && world.CanShuffle && !Done(store, CoachLesson.Shuffle) && FreeBoosters.IsFree(store, "Shuffle"))
                return Boost(CoachLesson.Shuffle, "Shuffle");
            if (world.Level >= MagnetLevel && world.CanMagnet && !Done(store, CoachLesson.Magnet) && FreeBoosters.IsFree(store, "Magnet"))
                return Boost(CoachLesson.Magnet, "Magnet");
            if (world.Level >= SuperLevel && world.CanSuper && !Done(store, CoachLesson.Super) && FreeBoosters.IsFree(store, "Super"))
                return Boost(CoachLesson.Super, "Super");
            return default;
        }

        // Forced lesson: only the glove's target takes a tap. A booster lesson blocks every car.
        public static bool Allows(CoachCue cue, Tap tap)
        {
            if (!cue.LockCars) return true;
            if (!cue.HasCar) return false;
            return cue.Car.Kind == tap.Kind && cue.Car.Index == tap.Index && cue.Car.Cell == tap.Cell;
        }

        public static bool AllowsBooster(CoachCue cue, string booster)
        {
            if (!cue.LockCars) return true;
            return cue.IsBooster && cue.Booster == booster;
        }

        public static string HintKey(CoachHint hint)
        {
            switch (hint)
            {
                case CoachHint.Mystery: return "lotloop.hint.mystery";
                case CoachHint.SideLot: return "lotloop.hint.side";
                case CoachHint.LiftGate: return "lotloop.hint.liftgate";
                default: return "";
            }
        }

        public static string HintLabel(CoachHint hint)
        {
            switch (hint)
            {
                case CoachHint.Mystery: return "Hides a car until a neighbor leaves";
                case CoachHint.SideLot: return "Opens when its spot is empty";
                case CoachHint.LiftGate: return "Opens when the spots on top are clear";
                default: return "";
            }
        }

        public static void CompleteHint(IFlagStore store, CoachHint hint)
        {
            if (store == null || hint == CoachHint.None) return;
            string key = HintKey(hint);
            if (!store.Get(key)) store.Set(key);
        }

        // Look-here hint on first sight of something that cannot be tapped yet. Never shown while
        // a forced lesson is running, never locks anything.
        public static HintCue Hint(IFlagStore store, CoachWorld world)
        {
            if (store == null || world.Board == null) return default;
            Tap tap;
            if (world.Level >= CoveredLevel && !store.Get(HintKey(CoachHint.Mystery)) && TryHidden(world.Board, out tap))
                return new HintCue { Hint = CoachHint.Mystery, Spot = tap, Label = HintLabel(CoachHint.Mystery) };
            if (world.Level >= SideLevel && !store.Get(HintKey(CoachHint.SideLot)) && TryClosedSide(world.Board, out tap))
                return new HintCue { Hint = CoachHint.SideLot, Spot = tap, Label = HintLabel(CoachHint.SideLot) };
            if (!store.Get(HintKey(CoachHint.LiftGate)) && world.Board.Garages != null)
            {
                for (int i = 0; i < world.Board.Garages.Count; i++)
                {
                    var g = world.Board.Garages[i];
                    if (g.Queue.Count == 0 || g.Cells.Count == 0) continue;
                    return new HintCue { Hint = CoachHint.LiftGate, Garage = i, Spot = new Tap(TapKind.Lane, g.Lane, g.Cells[0]), Label = HintLabel(CoachHint.LiftGate) };
                }
            }
            return default;
        }

        // Finger-down glove (tip at the bottom center) is rotated so the tip sits on the target
        // and the whole hand stays inside the screen. degrees: 0 points down, 90 points left.
        public static void PlaceGlove(UnityFreeVec target, float glove, UnityFreeVec min, UnityFreeVec max, float margin, out UnityFreeVec center, out float degrees)
        {
            float roomUp = max.Y - target.Y;
            float roomDown = target.Y - min.Y;
            float roomRight = max.X - target.X;
            float roomLeft = target.X - min.X;
            // Body sits in the roomiest direction. Tip offset from center, unrotated, is (0, -glove/2).
            var options = new VecOpt[4];
            options[0] = new VecOpt(roomUp, 0f, 0f, glove * 0.46f);
            options[1] = new VecOpt(roomDown, 180f, 0f, -glove * 0.46f);
            options[2] = new VecOpt(roomRight, 90f, glove * 0.46f, 0f);
            options[3] = new VecOpt(roomLeft, -90f, -glove * 0.46f, 0f);
            System.Array.Sort(options, delegate (VecOpt a, VecOpt b) { return b.Room.CompareTo(a.Room); });
            float half = glove * 0.5f;
            for (int i = 0; i < options.Length; i++)
            {
                var c = new UnityFreeVec(target.X + options[i].Dx, target.Y + options[i].Dy);
                if (Inside(c, half, min, max, margin))
                {
                    center = c;
                    degrees = options[i].Degrees;
                    return;
                }
            }
            var best = options[0];
            float x = target.X + best.Dx;
            float y = target.Y + best.Dy;
            float loX = min.X + margin + half;
            float hiX = max.X - margin - half;
            float loY = min.Y + margin + half;
            float hiY = max.Y - margin - half;
            if (loX > hiX) { float mid = (min.X + max.X) * 0.5f; loX = hiX = mid; }
            if (loY > hiY) { float mid = (min.Y + max.Y) * 0.5f; loY = hiY = mid; }
            if (x < loX) x = loX;
            if (x > hiX) x = hiX;
            if (y < loY) y = loY;
            if (y > hiY) y = hiY;
            center = new UnityFreeVec(x, y);
            degrees = best.Degrees;
        }

        static bool Inside(UnityFreeVec c, float half, UnityFreeVec min, UnityFreeVec max, float margin)
        {
            return c.X - half >= min.X + margin && c.X + half <= max.X - margin
                && c.Y - half >= min.Y + margin && c.Y + half <= max.Y - margin;
        }

        struct VecOpt
        {
            public float Room;
            public float Degrees;
            public float Dx;
            public float Dy;
            public VecOpt(float room, float degrees, float dx, float dy)
            {
                Room = room;
                Degrees = degrees;
                Dx = dx;
                Dy = dy;
            }
        }

        static CoachCue CarCue(CoachLesson lesson, Tap tap)
        {
            return new CoachCue { Lesson = lesson, LockCars = true, HasCar = true, Car = tap };
        }

        static CoachCue Boost(CoachLesson lesson, string name)
        {
            return new CoachCue { Lesson = lesson, LockCars = true, Booster = name };
        }

        static bool TryGuide(Board board, out Tap tap)
        {
            // Lane 0's corals leave in order: the nearest one that can already reach the bar.
            if (TryColor(board, 0, CarColor.Coral, out tap)) return true;
            tap = default;
            return false;
        }

        static bool TryColor(Board board, int lane, CarColor color, out Tap tap)
        {
            tap = default;
            if (board == null || lane < 0 || lane >= board.Lanes.Count) return false;
            var cells = board.Lanes[lane].Cells;
            for (int c = 0; c < cells.Count; c++)
            {
                var slot = cells[c];
                if (slot.Empty || slot.Tunnel || slot.Box) continue;
                if (slot.Car.Color != color || slot.Car.Hidden) continue;
                var candidate = new Tap(TapKind.Lane, lane, c);
                if (!ExitPath.IsLegal(board, candidate)) continue;
                tap = candidate;
                return true;
            }
            return false;
        }

        static bool TryHidden(Board board, out Tap tap)
        {
            tap = default;
            for (int l = 0; l < board.Lanes.Count; l++)
            {
                var cells = board.Lanes[l].Cells;
                for (int c = 0; c < cells.Count; c++)
                {
                    var slot = cells[c];
                    if (slot.Empty || slot.Tunnel || slot.Box) continue;
                    if (!slot.Car.Hidden) continue;
                    tap = new Tap(TapKind.Lane, l, c);
                    return true;
                }
            }
            return false;
        }

        static bool TrySide(Board board, out Tap tap)
        {
            tap = default;
            for (int s = 0; s < board.Sides.Count; s++)
            {
                if (board.Sides[s].Queue.Count == 0) continue;
                var candidate = new Tap(TapKind.Side, s, board.Sides[s].Cell);
                if (!ExitPath.IsLegal(board, candidate)) continue;
                tap = candidate;
                return true;
            }
            return false;
        }

        static bool TryClosedSide(Board board, out Tap tap)
        {
            tap = default;
            for (int s = 0; s < board.Sides.Count; s++)
            {
                if (board.Sides[s].Queue.Count == 0) continue;
                var candidate = new Tap(TapKind.Side, s, board.Sides[s].Cell);
                if (ExitPath.IsLegal(board, candidate)) continue;
                tap = candidate;
                return true;
            }
            return false;
        }

        static bool TryTunnel(Board board, out Tap tap)
        {
            tap = default;
            for (int l = 0; l < board.Lanes.Count; l++)
            {
                var cells = board.Lanes[l].Cells;
                for (int c = 0; c < cells.Count; c++)
                {
                    if (!cells[c].Tunnel || cells[c].TunnelCount <= 0) continue;
                    var candidate = new Tap(TapKind.Lane, l, c);
                    if (!ExitPath.IsLegal(board, candidate)) continue;
                    tap = candidate;
                    return true;
                }
            }
            return false;
        }

        // The glove sits on the first stall of the lift, which is the thing the player clears.
        static bool TryUnderground(Board board, out Tap tap)
        {
            tap = default;
            if (board.Garages == null || board.Garages.Count == 0) return false;
            var g = board.Garages[0];
            if (g.Cells.Count == 0) return false;
            // The player clears a stall on top of the lift; teach the first one that can leave now.
            for (int i = 0; i < g.Cells.Count; i++)
            {
                var candidate = new Tap(TapKind.Lane, g.Lane, g.Cells[i]);
                if (!ExitPath.IsLegal(board, candidate)) continue;
                tap = candidate;
                return true;
            }
            return false;
        }
    }

    // Plain point so the puzzle assembly stays free of UnityEngine.
    public struct UnityFreeVec
    {
        public float X;
        public float Y;
        public UnityFreeVec(float x, float y) { X = x; Y = y; }
    }
}
