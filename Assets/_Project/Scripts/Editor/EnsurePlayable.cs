#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CarMatch.Editor
{
    [InitializeOnLoad]
    static class EnsurePlayable
    {
        const string Boot = "Assets/_Project/Scenes/Boot.unity";

        static EnsurePlayable()
        {
            EditorApplication.delayCall += Pin;
        }

        [MenuItem("Car Match/Ensure Project Setup")]
        public static void Pin()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (PlayerSettings.companyName != "zfxgames")
                PlayerSettings.companyName = "zfxgames";
            if (PlayerSettings.productName != "Lot Loop")
                PlayerSettings.productName = "Lot Loop";
            const string id = "com.zfxgames.carmatch";
            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS) != id)
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, id);
            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone) != id)
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, id);
            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait)
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.defaultScreenWidth = 1080;
            PlayerSettings.defaultScreenHeight = 1920;

            var objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (objs != null && objs.Length > 0)
            {
                var so = new SerializedObject(objs[0]);
                var input = so.FindProperty("activeInputHandler");
                if (input != null && input.intValue != 1)
                {
                    input.intValue = 1;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        [MenuItem("Car Match/Play")]
        public static void Play()
        {
            Pin();
            ForcePortrait();
            if (EditorApplication.isPlaying) return;
            if (File.Exists(Boot) && EditorSceneManager.GetActiveScene().path != Boot)
                EditorSceneManager.OpenScene(Boot);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Car Match/Solve Levels")]
        public static void Solve()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Resources/levels.json");
            try
            {
                RuleChecks.RunAll(File.ReadAllText(path));
                Debug.Log("Lot Loop: match, bar, tunnel, booster, and solvability checks passed.");
            }
            catch (System.Exception e)
            {
                Debug.LogError("Lot Loop: " + e.Message);
            }
        }

        static void ForcePortrait()
        {
            var asm = typeof(EditorWindow).Assembly;
            var gvType = asm.GetType("UnityEditor.GameView");
            if (gvType == null) return;
            EditorWindow gv = null;
            var all = Resources.FindObjectsOfTypeAll(gvType);
            if (all != null && all.Length > 0) gv = all[0] as EditorWindow;
            if (gv == null) gv = EditorWindow.GetWindow(gvType, false, "Game", true);
            if (gv == null) return;
            try
            {
                int found = FindOrAddPortraitSize(asm);
                if (found < 0) return;
                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                var callback = gvType.GetMethod("SizeSelectionCallback", flags);
                if (callback != null) callback.Invoke(gv, new object[] { found, null });
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Lot Loop: could not pin the Game view to portrait. " + e.Message);
            }
        }

        static int FindOrAddPortraitSize(Assembly asm)
        {
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var sizeType = asm.GetType("UnityEditor.GameViewSize");
            var sizeTypeEnum = asm.GetType("UnityEditor.GameViewSizeType");
            if (sizesType == null || sizeType == null) return -1;
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance = singleton.GetProperty("instance").GetValue(null, null);
            var group = sizesType.GetProperty("currentGroup").GetValue(instance, null);
            var groupType = group.GetType();
            int count = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
            var getSize = groupType.GetMethod("GetGameViewSize");
            for (int i = 0; i < count; i++)
            {
                var size = getSize.Invoke(group, new object[] { i });
                var st = size.GetType();
                int w = (int)st.GetProperty("width").GetValue(size, null);
                int h = (int)st.GetProperty("height").GetValue(size, null);
                if (w == 1080 && h == 1920) return i;
            }
            var add = groupType.GetMethod("AddCustomSize");
            var ctor = sizeType.GetConstructor(new[] { sizeTypeEnum, typeof(int), typeof(int), typeof(string) });
            if (add == null || ctor == null) return -1;
            object fixedRes = System.Enum.ToObject(sizeTypeEnum, 1);
            var custom = ctor.Invoke(new[] { fixedRes, 1080, 1920, "Lot Loop iPhone" });
            add.Invoke(group, new[] { custom });
            return (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1;
        }
    }
}
#endif
