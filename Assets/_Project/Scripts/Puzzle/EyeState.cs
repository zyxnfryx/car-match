namespace CarMatch
{
    // Eight compass headings. N is +Y on the board, E is +X. The painted
    // drive sprites use these tokens in the file name.
    public enum CarFacing : byte
    {
        N = 0,
        NE = 1,
        E = 2,
        SE = 3,
        S = 4,
        SW = 5,
        W = 6,
        NW = 7
    }

    // Shared by the board view and the tests. Open eyes mean the same thing
    // as a legal tap: ExitPath says this car can leave. The view asks again
    // after every move, undo, shuffle, and reveal, so it does not cache this.
    // Heading sprites are chosen here too, so the drive and the tests agree.
    public static class EyeState
    {
        public const int FacingCount = 8;

        public static bool IsOpen(Board board, Tap tap)
        {
            return ExitPath.IsLegal(board, tap);
        }

        public static string SpriteFile(CarColor color, bool eyesOpen)
        {
            return "car_" + Token(color) + (eyesOpen ? "_open.png" : "_closed.png");
        }

        public static string SpriteFile(CarColor color, CarFacing facing)
        {
            return "car_" + Token(color) + "_open_" + FacingToken(facing) + ".png";
        }

        // Nearest of N, NE, E, SE, S, SW, W, NW. +x is east and +y is north.
        // Each heading owns a 45° sector. The counterclockwise edge belongs
        // to the next heading. A zero vector has no heading.
        public static bool TryNearest(float x, float y, out CarFacing facing)
        {
            facing = CarFacing.N;
            if (float.IsNaN(x) || float.IsNaN(y) || (x == 0f && y == 0f)) return false;
            double deg = System.Math.Atan2(y, x) * (180.0 / System.Math.PI);
            double shifted = deg + 22.5;
            if (shifted < 0.0) shifted += 360.0;
            int index = (int)(shifted / 45.0) % FacingCount;
            if (index < 0) index += FacingCount;
            facing = FromEast(index);
            return true;
        }

        static CarFacing FromEast(int index)
        {
            switch (index)
            {
                case 0: return CarFacing.E;
                case 1: return CarFacing.NE;
                case 2: return CarFacing.N;
                case 3: return CarFacing.NW;
                case 4: return CarFacing.W;
                case 5: return CarFacing.SW;
                case 6: return CarFacing.S;
                default: return CarFacing.SE;
            }
        }

        public static string FacingToken(CarFacing facing)
        {
            switch (facing)
            {
                case CarFacing.N: return "N";
                case CarFacing.NE: return "NE";
                case CarFacing.E: return "E";
                case CarFacing.SE: return "SE";
                case CarFacing.S: return "S";
                case CarFacing.SW: return "SW";
                case CarFacing.W: return "W";
                default: return "NW";
            }
        }

        public static string Token(CarColor color)
        {
            switch (color)
            {
                case CarColor.Coral: return "coral";
                case CarColor.Amber: return "amber";
                case CarColor.Lime: return "lime";
                case CarColor.Teal: return "teal";
                case CarColor.Violet: return "violet";
                case CarColor.Rose: return "rose";
                case CarColor.Sky: return "sky";
                default: return "gold";
            }
        }
    }
}
