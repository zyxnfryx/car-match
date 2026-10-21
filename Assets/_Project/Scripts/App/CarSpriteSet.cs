using UnityEngine;

namespace CarMatch
{
    // One catalog for the painted idle pairs and the eight open headings.
    // Slots stay empty until the editor sees the matching file under Art/Cars.
    // Headings is color-major: index = color * 8 + facing.
    public sealed class CarSpriteSet : ScriptableObject
    {
        public Sprite[] Open = new Sprite[Palette.Count];
        public Sprite[] Closed = new Sprite[Palette.Count];
        public Sprite[] Headings = new Sprite[Palette.Count * EyeState.FacingCount];
        public Sprite MascotIdle;
        public Sprite MascotHappy;
        public Sprite MascotCheer;

        public Sprite HeadingSprite(CarColor color, CarFacing facing)
        {
            int i = (int)color * EyeState.FacingCount + (int)facing;
            if (Headings == null || (uint)i >= (uint)Headings.Length) return null;
            return Headings[i];
        }

        public static CarSpriteSet Load()
        {
            return Resources.Load<CarSpriteSet>("CarSprites");
        }
    }
}
