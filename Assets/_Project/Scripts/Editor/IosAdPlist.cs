#if UNITY_EDITOR && UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace CarMatch.Editor
{
    // Same iOS ad settings as keno's Info.plist: ATT text + the Unity/LevelPlay SKAdNetwork list
    // (77 IDs, ironSource su67r6k2v3 + Unity, from https://skan.mz.unity3d.com/v3/partner/skadnetworks.plist.json).
    // Add IDs for any extra mediation adapters you enable.
    static class IosAdPlist
    {
        const string Att = "This lets Lot Loop show more relevant ads when you choose to watch an ad to continue a lot.";
        static readonly string[] Skan =
        {
            "su67r6k2v3.skadnetwork",
            "5tjdwbrq8w.skadnetwork",
            "e5fvkxwrpn.skadnetwork",
            "9rd848q2bz.skadnetwork",
            "v9wttpbfk9.skadnetwork",
            "9nlqeag3gk.skadnetwork",
            "5f5u5tfb26.skadnetwork",
            "glqzh8vgby.skadnetwork",
            "3qy4746246.skadnetwork",
            "4dzt52r2t5.skadnetwork",
            "prcb7njmu6.skadnetwork",
            "8s468mfl3y.skadnetwork",
            "hs6bdukanm.skadnetwork",
            "zmvfpc5aq8.skadnetwork",
            "f73kdq92p3.skadnetwork",
            "n9x2a789qt.skadnetwork",
            "zq492l623r.skadnetwork",
            "feyaarzu9v.skadnetwork",
            "424m5254lk.skadnetwork",
            "44jx6755aq.skadnetwork",
            "klf5c3l5u5.skadnetwork",
            "32z4fx6l9h.skadnetwork",
            "ppxm28t8ap.skadnetwork",
            "488r3q3dtq.skadnetwork",
            "2fnua5tdw4.skadnetwork",
            "77y3x8wds4.skadnetwork",
            "wg4vff78zm.skadnetwork",
            "ydx93a7ass.skadnetwork",
            "578prtvx9j.skadnetwork",
            "2u9pt9hc89.skadnetwork",
            "m8dbw4sv7c.skadnetwork",
            "c6k4g5qg8m.skadnetwork",
            "tl55sbb4fm.skadnetwork",
            "av6w8kgt66.skadnetwork",
            "294l99pt4k.skadnetwork",
            "wzmmz9fp6w.skadnetwork",
            "mp6xlyr22a.skadnetwork",
            "p78axxw29g.skadnetwork",
            "5l3tpt7t6e.skadnetwork",
            "kbd757ywx3.skadnetwork",
            "4468km3ulz.skadnetwork",
            "mqn7fxpca7.skadnetwork",
            "mlmmfzh3r3.skadnetwork",
            "vhf287vqwu.skadnetwork",
            "5lm9lj6jb7.skadnetwork",
            "cstr6suwn9.skadnetwork",
            "v72qych5uu.skadnetwork",
            "4fzdc2evr5.skadnetwork",
            "f7s53z58qe.skadnetwork",
            "97r2b46745.skadnetwork",
            "w9q455wk68.skadnetwork",
            "9t245vhmpl.skadnetwork",
            "pwa73g5rt2.skadnetwork",
            "g6gcrrvk4p.skadnetwork",
            "v79kvwwj4g.skadnetwork",
            "s39g8k73mm.skadnetwork",
            "3rd42ekr43.skadnetwork",
            "k674qkevps.skadnetwork",
            "a8cz6cu7e5.skadnetwork",
            "22mmun2rn5.skadnetwork",
            "f38h382jlk.skadnetwork",
            "4pfyvq9l8r.skadnetwork",
            "5a6flpkh64.skadnetwork",
            "238da6jt44.skadnetwork",
            "4w7y6s5ca2.skadnetwork",
            "k6y4y55b64.skadnetwork",
            "t38b2kh725.skadnetwork",
            "7ug5zh24hu.skadnetwork",
            "3sh42y64q3.skadnetwork",
            "6yxyv74ff7.skadnetwork",
            "yclnxrl5pm.skadnetwork",
            "x44k69ngh6.skadnetwork",
            "a2p9lx4jpn.skadnetwork",
            "uw77j35x4d.skadnetwork",
            "mj797d8u6f.skadnetwork",
            "xga6mpmplv.skadnetwork",
            "lr83yxwka7.skadnetwork",
        };

        [PostProcessBuild(100)]
        static void OnPostprocess(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;
            root.SetString("NSUserTrackingUsageDescription", Att);
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            var items = root.CreateArray("SKAdNetworkItems");
            foreach (var id in Skan) items.AddDict().SetString("SKAdNetworkIdentifier", id);
            plist.WriteToFile(plistPath);

            // LevelPlay needs -ObjC, as keno's App target has.
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
