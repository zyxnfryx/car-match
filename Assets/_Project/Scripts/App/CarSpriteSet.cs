using UnityEngine;

namespace CarMatch
{
    // One catalog for the painted open/closed pairs. Slots stay empty until
    // the editor sees the matching file under Art/Cars.
    public sealed class CarSpriteSet : ScriptableObject
    {
        public Sprite[] Open = new Sprite[Palette.Count];
        public Sprite[] Closed = new Sprite[Palette.Count];

        public static CarSpriteSet Load()
        {
            return Resources.Load<CarSpriteSet>("CarSprites");
        }
    }
}
