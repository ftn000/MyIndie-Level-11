using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

// Меню: Ogonok/Build WebGL. Batchmode: -executeMethod OgonokBuild.WebGL. Результат: Unity/Builds/WebGL (в .gitignore).
public static class OgonokBuild
{
    [MenuItem("Ogonok/Build WebGL")]
    public static void WebGL()
    {
        PlayerSettings.productName = "Ogonyok";
        PlayerSettings.companyName = "MyIndie";
        PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled; // сайты джемов часто не отдают нужные заголовки для gzip/brotli
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.dataCaching = false; PlayerSettings.WebGL.template = "PROJECT:Ogonyok"; // Assets/WebGLTemplates/Ogonyok: канвас на всё окно, без футера Unity
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Level1.unity" },
            locationPathName = "Builds/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };
        var rep = BuildPipeline.BuildPlayer(opts);
        Debug.Log($"Ogonok: WebGL build {rep.summary.result}, {rep.summary.totalSize / 1048576} MB, {rep.summary.totalTime}");
        if (Application.isBatchMode) EditorApplication.Exit(rep.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
