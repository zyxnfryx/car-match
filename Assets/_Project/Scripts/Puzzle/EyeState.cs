namespace CarMatch
{
    // Shared by the board view and the tests. Open eyes mean the same thing
    // as a legal tap: ExitPath says this car can leave. The view asks again
    // after every move, undo, shuffle, and reveal, so it does not cache this.
    public static class EyeState
    {
        public static bool IsOpen(Board board, Tap tap)
        {
            return ExitPath.IsLegal(board, tap);
        }

        public static string SpriteFile(CarColor color, bool eyesOpen)
        {
            return "car_" + Token(color) + (eyesOpen ? "_open.png" : "_closed.png");
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
