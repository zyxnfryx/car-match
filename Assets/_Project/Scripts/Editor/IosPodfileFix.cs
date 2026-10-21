#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;

// The resolver regenerates the Podfile on every export without a deployment-target override, so pods
// targeting iOS 10-13 break the archive. Append the same post_install hook Flock Five uses.
// Run `/opt/homebrew/bin/pod install` afterwards (system-ruby pod on the build Mac is broken).
public static class IosPodfileFix
{
    [PostProcessBuild(1000)]
    public static void OnPostprocessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        var podfile = Path.Combine(path, "Podfile");
        if (!File.Exists(podfile)) return;
        var text = File.ReadAllText(podfile);
        if (text.Contains("post_install")) return;
        File.AppendAllText(podfile, "\npost_install do |installer|\n  installer.pods_project.targets.each do |t|\n    t.build_configurations.each do |c|\n      c.build_settings['IPHONEOS_DEPLOYMENT_TARGET'] = '15.0'\n    end\n  end\nend\n");
    }
}
#endif
