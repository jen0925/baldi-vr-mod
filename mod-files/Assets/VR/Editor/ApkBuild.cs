using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;

// Builds the game as a Quest 2 app (an APK), for installing with SideQuest once
// the headset's Developer Mode is on. It uses Meta's own VR system (the Oculus XR
// plug-in) in place of the web page's WebXR. Run from the menu (Baldi VR > Build
// Quest APK) or with -executeMethod ApkBuild.Build.
public static class ApkBuild
{
    private const string Output = "../../Build/apk/BaldiVR.apk";

    [MenuItem("Baldi VR/Set up for Quest APK")]
    public static void Setup()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        PlayerSettings.productName = "Baldi's Basics Private";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.jen.baldivr");
        // The app's picture: the game's own launcher picture (from VR/Icon).
        AssetDatabase.ImportAsset("Assets/VR/Icon/AppIcon.png");
        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/VR/Icon/AppIcon.png");
        int sizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Android).Length;
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, Enumerable.Repeat(icon, sizes).ToArray());
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;

        // The same built-in render pipeline as the web build (see WebBuild).
        UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset = null;
        for (int level = 0; level < QualitySettings.names.Length; level++)
        {
            QualitySettings.SetQualityLevel(level, false);
            QualitySettings.renderPipeline = null;
        }

        // Turn on Meta's VR system for Android.
        EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
        if (perTarget == null)
        {
            perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            Directory.CreateDirectory("Assets/XR");
            AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettings.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
        }
        if (!perTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android)) perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
        if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android)) perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        XRGeneralSettings android = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
        android.InitManagerOnStart = true;
        XRPackageMetadataStore.AssignLoader(android.AssignedSettings, "Unity.XR.Oculus.OculusLoader", BuildTargetGroup.Android);

        // The game's hand-written shaders draw one eye at a time, so the headset
        // draws its two eyes one after the other (multi-pass).
        Unity.XR.Oculus.OculusSettings oculus = AssetDatabase.LoadAssetAtPath<Unity.XR.Oculus.OculusSettings>("Assets/XR/Settings/Oculus Settings.asset");
        if (oculus == null)
        {
            oculus = ScriptableObject.CreateInstance<Unity.XR.Oculus.OculusSettings>();
            Directory.CreateDirectory("Assets/XR/Settings");
            AssetDatabase.CreateAsset(oculus, "Assets/XR/Settings/Oculus Settings.asset");
        }
        oculus.m_StereoRenderingModeAndroid = Unity.XR.Oculus.OculusSettings.StereoRenderingModeAndroid.MultiPass;
        oculus.TargetQuest = false;
        oculus.TargetQuest2 = true;
        EditorUtility.SetDirty(oculus);
        EditorBuildSettings.AddConfigObject("Unity.XR.Oculus.Settings", oculus, true);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Baldi VR/Build Quest APK")]
    public static void Build()
    {
        Setup();
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        BuildReport report = BuildPipeline.BuildPlayer(scenes, Output, BuildTarget.Android, BuildOptions.None);
        Debug.Log($"Quest APK build: {report.summary.result}, {report.summary.totalErrors} errors -> {Output}");
        if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    // The loading-screen study app: the same game with LoadForever switched
    // on, as its own app ("Baldi Loading Screen") so it sits beside the real
    // one instead of replacing it. Everything is put back afterwards.
    [MenuItem("Baldi VR/Build loading screen study APK")]
    public static void BuildLoadForever()
    {
        Setup();
        string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
        string name = PlayerSettings.productName;
        string id = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
        const string output = "../../Build/apk/BaldiLoadingScreen.apk";
        BuildReport report;
        try
        {
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, (defines.Length > 0 ? defines + ";" : "") + "BALDI_LOAD_FOREVER");
            PlayerSettings.productName = "Baldi Loading Screen";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.jen.baldivr.loading");
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            report = BuildPipeline.BuildPlayer(scenes, output, BuildTarget.Android, BuildOptions.None);
        }
        finally
        {
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, defines);
            PlayerSettings.productName = name;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, id);
        }
        Debug.Log($"Loading screen study build: {report.summary.result}, {report.summary.totalErrors} errors -> {output}");
        if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
