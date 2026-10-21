using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CarMatch
{
    // Painted cars live in Assets/_Project/Art/Cars as car_<color>_open.png and
    // car_<color>_closed.png. Editor play reads those files directly. A player
    // build uses the CarSprites catalog, which the importer fills when the
    // files exist. Anything missing falls back to the code-drawn car.
    public static class CarArt
    {
        public const string Folder = "Assets/_Project/Art/Cars";

        public static string AssetPath(CarColor color, bool eyesOpen)
        {
            return Folder + "/" + EyeState.SpriteFile(color, eyesOpen);
        }

        public static Sprite For(CarColor color, bool eyesOpen)
        {
            var painted = Painted(color, eyesOpen);
            if (painted != null) return painted;
            return SpriteKit.Car(color, eyesOpen);
        }

        public static Sprite Painted(CarColor color, bool eyesOpen)
        {
#if UNITY_EDITOR
            var direct = LoadSprite(AssetPath(color, eyesOpen));
            if (direct != null) return direct;
#endif
            var set = CarSpriteSet.Load();
            if (set == null) return null;
            var list = eyesOpen ? set.Open : set.Closed;
            int i = (int)color;
            if (list == null || (uint)i >= (uint)list.Length) return null;
            return list[i];
        }

#if UNITY_EDITOR
        public static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null) return null;
            for (int i = 0; i < assets.Length; i++)
            {
                var found = assets[i] as Sprite;
                if (found != null) return found;
            }
            return null;
        }
#endif
    }
}
