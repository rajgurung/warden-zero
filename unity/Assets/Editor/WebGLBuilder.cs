using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WardenZero.EditorTools
{
    // Builds the WebGL player to Build/WebGL. Batch mode:
    //   -executeMethod WardenZero.EditorTools.WebGLBuilder.Build
    // Settings favour a small download that loads from any static file server
    // (gzip with the JS decompression fallback, so no Content-Encoding headers are needed).
    public static class WebGLBuilder
    {
        const string OutputDir = "Build/WebGL";

        [MenuItem("Warden Zero/Build WebGL")]
        public static void Build()
        {
            var target = NamedBuildTarget.WebGL;
            PlayerSettings.companyName = "Warden Zero";
            PlayerSettings.productName = "Warden Zero";
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.High);
            PlayerSettings.SetIl2CppCodeGeneration(target, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetIl2CppCompilerConfiguration(target, Il2CppCompilerConfiguration.Release);
            UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.DiskSize;

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[WebGLBuilder] No scenes in build settings. Run SceneBuilder.Build first.");
                Exit(1);
                return;
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            var summary = report.summary;
            long bytes = Directory.Exists(OutputDir)
                ? Directory.GetFiles(OutputDir, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length)
                : 0;
            Debug.Log($"[WebGLBuilder] Result: {summary.result}, errors: {summary.totalErrors}, " +
                      $"time: {summary.totalTime}, output: {OutputDir} ({bytes / 1024f / 1024f:0.00} MB on disk)");
            Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static void Exit(int code)
        {
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
