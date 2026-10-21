using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Batch: Unity -batchmode -quit -buildTarget WebGL -executeMethod CliBuild.BuildWebGL
// Env: CM_WEBGL_PATH (output folder, default Builds/WebGL).
public static class CliBuild
{
    public static void BuildWebGL()
    {
        PlayerSettings.WebGL.initialMemorySize = 512;   // MB
        PlayerSettings.WebGL.maximumMemorySize = 3072;  // MB (3 GB)
        PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled; // plain python http.server
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.dataCaching = false;
        PlayerSettings.WebGL.template = "PROJECT:LotLoop"; // portrait 390x844 letterboxed
        PlayerSettings.defaultWebScreenWidth = 390;
        PlayerSettings.defaultWebScreenHeight = 844;
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var outPath = Environment.GetEnvironmentVariable("CM_WEBGL_PATH");
        if (string.IsNullOrEmpty(outPath)) outPath = "Builds/WebGL";
        var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });
        Debug.Log("WEBGLBUILD result=" + r.summary.result + " errors=" + r.summary.totalErrors);
        EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
