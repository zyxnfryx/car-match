using UnityEngine;

namespace CarMatch
{
    // The painted mascot. Home shows idle. Coming home after a clear
    // plays happy, then cheer. Missing files fall back to SpriteKit.
    public static class MascotArt
    {
        public const string Folder = "Assets/_Project/Art/Mascot";

        public static string AssetPath(string name)
        {
            return Folder + "/" + name + ".png";
        }

        public static Sprite Idle => Load("mascot_idle", 0);
        public static Sprite Happy => Load("mascot_happy", 1);
        public static Sprite Cheer => Load("mascot_cheer", 2);

        static Sprite Load(string name, int slot)
        {
#if UNITY_EDITOR
            var direct = CarArt.LoadSprite(AssetPath(name));
            if (direct != null) return direct;
#endif
            var set = CarSpriteSet.Load();
            if (set == null) return null;
            if (slot == 0) return set.MascotIdle;
            if (slot == 1) return set.MascotHappy;
            return set.MascotCheer;
        }
    }
}
