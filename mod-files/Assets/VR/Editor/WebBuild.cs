using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;

// Builds the game as a WebXR web page for the Quest browser. Run from the menu
// (Baldi VR > Build web VR) or with -executeMethod WebBuild.Build.
public static class WebBuild
{
    // Built into a staging folder, then copied over the served one only when the
    // build succeeded, so a page refreshed mid-build never gets half old, half new.
    // Debug builds go beside the real one, so the headset keeps the normal build.
    private static string Output => Debugging ? "../../Build/web-debug" : "../../Build/web";
    private static string Staging => Output + "-staging";

    private static bool Debugging => System.Environment.GetEnvironmentVariable("BALDI_DEBUG") == "1";

    [MenuItem("Baldi VR/Set up for web VR")]
    public static void Setup()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        PlayerSettings.productName = "Baldi VR (private)";
        PlayerSettings.WebGL.template = "PROJECT:WebXRFullView2020";
        // The WebXR page template's own settings. The Quest browser has WebXR
        // built in, so no polyfill is configured.
        PlayerSettings.SetTemplateCustomValue("DESCRIPTION", "Private Baldi's Basics Classic Remastered VR mod. All credit to Basically Games.");
        PlayerSettings.SetTemplateCustomValue("WEBXR_POLYFILL_CONFIG", "");
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.memorySize = 1024;
        // BALDI_DEBUG=1 makes a slower build whose errors say which line they came from.
        // A symbols file beside the build turns crash reports' function numbers into names.
        PlayerSettings.WebGL.debugSymbols = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace;

        // The game's URP shaders could not be extracted; the replacements in
        // Assets/Shader are written for the built-in render pipeline, so use it.
        UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset = null;
        for (int level = 0; level < QualitySettings.names.Length; level++)
        {
            QualitySettings.SetQualityLevel(level, false);
            QualitySettings.renderPipeline = null;
        }

        // Turn on the WebXR Export plug-in for WebGL.
        EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
        if (perTarget == null)
        {
            perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            Directory.CreateDirectory("Assets/XR");
            AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettings.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
        }
        if (!perTarget.HasSettingsForBuildTarget(BuildTargetGroup.WebGL)) perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.WebGL);
        if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.WebGL)) perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.WebGL);
        XRGeneralSettings web = perTarget.SettingsForBuildTarget(BuildTargetGroup.WebGL);
        web.InitManagerOnStart = true;
        XRPackageMetadataStore.AssignLoader(web.AssignedSettings, "WebXR.WebXRLoader", BuildTargetGroup.WebGL);

        // WebXR Export only packs its settings into the build when they are
        // registered here (opening its settings page in the editor normally does
        // that). Without them the page has no VR settings and entering VR fails.
        WebXR.WebXRSettings webXRSettings = AssetDatabase.LoadAssetAtPath<WebXR.WebXRSettings>("Assets/XR/Settings/Web XR Settings.asset");
        if (webXRSettings == null)
        {
            webXRSettings = ScriptableObject.CreateInstance<WebXR.WebXRSettings>();
            Directory.CreateDirectory("Assets/XR/Settings");
            AssetDatabase.CreateAsset(webXRSettings, "Assets/XR/Settings/Web XR Settings.asset");
        }
        // Its display mode needs URP; with the built-in pipeline it shows only purple.
        // Camera mode (VRRig's eye cameras) works with built-in.
        webXRSettings.DisableXRDisplaySubsystem = true;
        // Draw at the browser's own default size for the headset: sharper
        // (1.2) was too slow to play smoothly, and native resolution more so.
        webXRSettings.UseFramebufferScaleFactor = true;
        webXRSettings.UseNativeResolution = false;
        webXRSettings.FramebufferScaleFactor = 1.0f;
        EditorUtility.SetDirty(webXRSettings);
        EditorBuildSettings.AddConfigObject("WebXR.Settings", webXRSettings, true);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Baldi VR/Build web VR")]
    public static void Build()
    {
        Setup();
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (Directory.Exists(Staging)) Directory.Delete(Staging, true);
        Directory.CreateDirectory(Staging);
        BuildReport report = BuildPipeline.BuildPlayer(scenes, Staging, BuildTarget.WebGL, Debugging ? BuildOptions.Development : BuildOptions.None);
        if (report.summary.result == BuildResult.Succeeded)
        {
            CopyOver(Staging, Output);
        }
        Debug.Log($"Web VR build: {report.summary.result}, {report.summary.totalErrors} errors -> {Output}");
        if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    // Game files first and the page last, so the page never points at missing files.
    private static void CopyOver(string from, string to)
    {
        foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories).OrderBy(f => f.EndsWith("index.html") ? 1 : 0))
        {
            string target = Path.Combine(to, file.Substring(from.Length + 1));
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(file, target, true);
        }
    }
}
