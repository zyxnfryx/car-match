#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

// Post-build: home-screen name and export-compliance flag for every iOS export.
public static class IosInfoPlist
{
    [PostProcessBuild(100)]
    public static void OnPostprocessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        var plistPath = Path.Combine(path, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetString("CFBundleDisplayName", "Lot Loop");
        plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        // Portrait-only on iPad needs full screen (no Split View), or App Store validation rejects it.
        plist.root.SetBoolean("UIRequiresFullScreen", true);
        plist.WriteToFile(plistPath);
    }
}
#endif
