using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Batch entry: Unity -batchmode -quit -buildTarget iOS -executeMethod IosBuild.Run
// Env: CM_BUILD_NUMBER (CFBundleVersion, default 1), CM_XCODE_PATH (export dir, required).
public static class IosBuild
{
    public static void Run()
    {
        var ios = NamedBuildTarget.iOS;
        PlayerSettings.SetApplicationIdentifier(ios, "com.zfxgames.carmatch");
        PlayerSettings.iOS.buildNumber = Env("CM_BUILD_NUMBER", "1");
        PlayerSettings.iOS.appleDeveloperTeamID = "23K8T866FT";
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.iOS.requiresFullScreen = true;
        AssetDatabase.Refresh();
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/AppIcon.png");
        if (icon == null) throw new Exception("missing Assets/_Project/Art/AppIcon.png");
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Env("CM_XCODE_PATH", null),
            target = BuildTarget.iOS,
            options = BuildOptions.None
        });
        Debug.Log("IOSBUILD result=" + r.summary.result + " errors=" + r.summary.totalErrors);
        EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    static string Env(string k, string fallback)
    {
        var v = Environment.GetEnvironmentVariable(k);
        if (!string.IsNullOrEmpty(v)) return v;
        if (fallback != null) return fallback;
        throw new Exception("missing " + k);
    }
}
