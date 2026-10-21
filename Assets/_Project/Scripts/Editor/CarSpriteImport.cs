#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CarMatch.Editor
{
    // Dropping idle or heading PNGs into Art/Cars imports them as sprites
    // and points the player-build catalog at them.
    public sealed class CarSpriteImport : AssetPostprocessor
    {
        const string Folder = "Assets/_Project/Art/Cars/";

        void OnPreprocessTexture()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(Folder, System.StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!Touches(imported) && !Touches(deleted) && !Touches(moved) && !Touches(movedFrom))
                return;
            CarSpriteCatalog.Sync();
        }

        static bool Touches(string[] paths)
        {
            if (paths == null) return false;
            for (int i = 0; i < paths.Length; i++)
            {
                if (string.IsNullOrEmpty(paths[i])) continue;
                var path = paths[i].Replace('\\', '/');
                if (path.StartsWith(Folder, System.StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }

    [InitializeOnLoad]
    public static class CarSpriteCatalog
    {
        const string Catalog = "Assets/_Project/Resources/CarSprites.asset";

        static CarSpriteCatalog()
        {
            EditorApplication.delayCall += Sync;
        }

        public static void Sync()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var set = AssetDatabase.LoadAssetAtPath<CarSpriteSet>(Catalog);
            bool created = false;
            if (set == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources")) return;
                set = ScriptableObject.CreateInstance<CarSpriteSet>();
                AssetDatabase.CreateAsset(set, Catalog);
                created = true;
            }
            if (set.Open == null || set.Open.Length != Palette.Count)
                set.Open = new Sprite[Palette.Count];
            if (set.Closed == null || set.Closed.Length != Palette.Count)
                set.Closed = new Sprite[Palette.Count];
            int headingCount = Palette.Count * EyeState.FacingCount;
            if (set.Headings == null || set.Headings.Length != headingCount)
                set.Headings = new Sprite[headingCount];
            bool changed = created;
            for (int i = 0; i < Palette.Count; i++)
            {
                var color = (CarColor)i;
                var open = CarArt.LoadSprite(CarArt.AssetPath(color, true));
                var closed = CarArt.LoadSprite(CarArt.AssetPath(color, false));
                if (set.Open[i] != open)
                {
                    set.Open[i] = open;
                    changed = true;
                }
                if (set.Closed[i] != closed)
                {
                    set.Closed[i] = closed;
                    changed = true;
                }
                for (int f = 0; f < EyeState.FacingCount; f++)
                {
                    var facing = (CarFacing)f;
                    var heading = CarArt.LoadSprite(CarArt.HeadingPath(color, facing));
                    int index = i * EyeState.FacingCount + f;
                    if (set.Headings[index] == heading) continue;
                    set.Headings[index] = heading;
                    changed = true;
                }
            }
            if (!changed) return;
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(set);
        }
    }
}
#endif
