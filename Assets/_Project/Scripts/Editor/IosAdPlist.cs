#if UNITY_EDITOR && UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace CarMatch.Editor
{
    // Kid-directed. Do not add an ATT prompt or an IDFA usage string.
    // Strip them if another SDK post-process wrote them. This build also
    // does not add SKAdNetworkItems.
    static class IosAdPlist
    {
        [PostProcessBuild(999)]
        static void OnPostprocess(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;
            if (root.values.ContainsKey("NSUserTrackingUsageDescription"))
                root.values.Remove("NSUserTrackingUsageDescription");
            if (root.values.ContainsKey("SKAdNetworkItems"))
                root.values.Remove("SKAdNetworkItems");
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.WriteToFile(plistPath);

            string projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            proj.AddBuildProperty(proj.GetUnityMainTargetGuid(), "OTHER_LDFLAGS", "-ObjC");
            proj.AddBuildProperty(proj.GetUnityFrameworkTargetGuid(), "OTHER_LDFLAGS", "-ObjC");
            proj.WriteToFile(projPath);
        }
    }
}
#endif
