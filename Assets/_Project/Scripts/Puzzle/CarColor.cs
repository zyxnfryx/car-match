namespace CarMatch
{
    public enum CarColor : byte
    {
        Coral = 0,
        Amber = 1,
        Lime = 2,
        Teal = 3,
        Violet = 4,
        Rose = 5,
        Sky = 6,
        Gold = 7
    }

    public static class Palette
    {
        public const int Count = 8;

        public static bool TryParse(string name, out CarColor color)
        {
            color = default;
            if (string.IsNullOrEmpty(name)) return false;
            switch (name)
            {
                case "coral": color = CarColor.Coral; return true;
                case "amber": color = CarColor.Amber; return true;
                case "lime": color = CarColor.Lime; return true;
                case "teal": color = CarColor.Teal; return true;
                case "violet": color = CarColor.Violet; return true;
                case "rose": color = CarColor.Rose; return true;
                case "sky": color = CarColor.Sky; return true;
                case "gold": color = CarColor.Gold; return true;
                default: return false;
            }
        }
    }

    public struct Car
    {
        public CarColor Color;
        public bool Hidden;

        public Car(CarColor color, bool hidden)
        {
            Color = color;
            Hidden = hidden;
        }
    }
}
