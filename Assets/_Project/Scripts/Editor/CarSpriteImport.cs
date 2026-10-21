#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CarMatch.Editor
{
    // Cars and the mascot import as sprites: centered pivot, no mips,
    // uncompressed so the baked shadow stays clean.
    public sealed class CarSpriteImport : AssetPostprocessor
    {
        const string Cars = "Assets/_Project/Art/Cars/";
        const string Mascot = "Assets/_Project/Art/Mascot/";
        // Painted UI, the 16-heading drive frames and the eye-open frames load from Resources.
        static readonly string[] Extra =
        {
            "Assets/_Project/Resources/UI/",
            "Assets/_Project/Resources/Cars16/",
            "Assets/_Project/Resources/CarEyes/"
        };

        static bool IsExtra(string path)
        {
            for (int i = 0; i < Extra.Length; i++)
                if (path.StartsWith(Extra[i], System.StringComparison.Ordinal)) return true;
            return false;
        }

        void OnPreprocessTexture()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(Cars, System.StringComparison.Ordinal) &&
                !path.StartsWith(Mascot, System.StringComparison.Ordinal) &&
                !IsExtra(path))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.streamingMipmaps = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.spritePixelsToUnits = 100f;
            // 3D buttons and pills slice: keep the rounded ends and bevel, stretch the middle.
            var file = System.IO.Path.GetFileNameWithoutExtension(path);
            if (path.StartsWith(Extra[0], System.StringComparison.Ordinal) &&
                (file.StartsWith("btn_", System.StringComparison.Ordinal) || file == "pill"))
            {
                float b = file == "btn_booster" ? 70f : file == "pill" ? 56f : file == "btn_navy" ? 60f : 96f;
                importer.spriteBorder = new Vector4(b, b * 0.8f, b, b * 0.8f);
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            settings.mipmapEnabled = false;
            settings.alphaIsTransparency = true;
            settings.sRGBTexture = true;
            settings.npotScale = TextureImporterNPOTScale.None;
            settings.filterMode = FilterMode.Bilinear;
            settings.wrapMode = TextureWrapMode.Clamp;
            importer.SetTextureSettings(settings);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;

            Uncompressed(importer, "DefaultTexturePlatform", false);
            Uncompressed(importer, "Standalone", true);
            Uncompressed(importer, "iOS", true);
            Uncompressed(importer, "Android", true);
            Uncompressed(importer, "WebGL", true);
        }

        static void Uncompressed(TextureImporter importer, string platform, bool overridden)
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            settings.overridden = overridden;
            settings.maxTextureSize = 2048;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            settings.crunchedCompression = false;
            settings.format = TextureImporterFormat.RGBA32;
            settings.compressionQuality = 100;
            importer.SetPlatformTextureSettings(settings);
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
                if (path.StartsWith(Cars, System.StringComparison.Ordinal)) return true;
                if (path.StartsWith(Mascot, System.StringComparison.Ordinal)) return true;
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
            var idle = CarArt.LoadSprite(MascotArt.AssetPath("mascot_idle"));
            var happy = CarArt.LoadSprite(MascotArt.AssetPath("mascot_happy"));
            var cheer = CarArt.LoadSprite(MascotArt.AssetPath("mascot_cheer"));
            if (set.MascotIdle != idle)
            {
                set.MascotIdle = idle;
                changed = true;
            }
            if (set.MascotHappy != happy)
            {
                set.MascotHappy = happy;
                changed = true;
            }
            if (set.MascotCheer != cheer)
            {
                set.MascotCheer = cheer;
                changed = true;
            }
            if (!changed) return;
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(set);
        }
    }
}
#endif
