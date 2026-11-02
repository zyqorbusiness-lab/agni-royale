using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Batchmode helpers for Agni Royale (Zyqor Studio).
//   Unity -batchmode -nographics -projectPath <proj> -executeMethod AgniBuild.ConfigureAndroid -quit -logFile <log>
//   Unity -batchmode -nographics -projectPath <proj> -executeMethod AgniBuild.BuildAndroidApk -quit -logFile <log> [-mono] [-out <apk>]
public static class AgniBuild
{
    const string Package = "com.zyqorstudio.agniroyale";

    [MenuItem("Agni Royale/Configure Android settings")]
    public static void ConfigureAndroid() { Configure(!HasArg("-mono")); }

    static bool HasArg(string a) => Environment.GetCommandLineArgs().Contains(a);
    static string ArgVal(string a, string def)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, a);
        return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
    }

    static void Configure(bool il2cpp)
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        PlayerSettings.companyName = "Zyqor Studio";
        PlayerSettings.productName = "Agni Royale";
        var android = NamedBuildTarget.Android;
        PlayerSettings.SetApplicationIdentifier(android, Package);

        // Landscape only (both landscape directions).
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        // No Unity splash / logo (Unity 6 Personal allows this).
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;

        if (il2cpp)
        {
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // Smaller generated C++ = much less build scratch disk.
            PlayerSettings.SetIl2CppCodeGeneration(android, Il2CppCodeGeneration.OptimizeSize);
        }
        else
        {
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.Mono2x);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7;
        }
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
        PlayerSettings.Android.forceInternetPermission = true; // multiplayer (NGO/UTP) + LAN room discovery
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        // Android-safe rendering + presentation (the LOOT worker switched the editor to Linux for a test build;
        // these are pinned here so every APK is built the same way whatever the editor last did).
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan, UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.Android.fullscreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.Android.useCustomKeystore = false; // same (debug) signing key as every earlier test APK, so it installs over them
        // Version: -agniVersion 0.2.0 -agniVersionCode 2 (otherwise left as they are)
        var ver = ArgVal("-agniVersion", null); if (!string.IsNullOrEmpty(ver)) PlayerSettings.bundleVersion = ver;
        var vc = ArgVal("-agniVersionCode", null); if (int.TryParse(vc, out int vci) && vci > 0) PlayerSettings.Android.bundleVersionCode = vci;
        EditorUserBuildSettings.buildAppBundle = false;
        AssetDatabase.SaveAssets();
        Debug.Log("[AgniBuild] configured Android: " + (il2cpp ? "IL2CPP ARM64" : "Mono ARMv7") + " version=" + PlayerSettings.bundleVersion + " code=" + PlayerSettings.Android.bundleVersionCode + " splash=" + PlayerSettings.SplashScreen.show + "/" + PlayerSettings.SplashScreen.showUnityLogo + " apis=" + string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android)));
    }

    [MenuItem("Agni Royale/Build test APK")]
    public static void BuildAndroidApk()
    {
        Configure(!HasArg("-mono"));
        string outPath = ArgVal("-out", Path.GetFullPath("../builds/AgniRoyale_test.apk"));
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
        if (!File.Exists(AgniWorldBuilder.ScenePath)) AgniWorldBuilder.BuildAll();
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None
        };
        BuildReport r = BuildPipeline.BuildPlayer(opts);
        Debug.Log($"[AgniBuild] result={r.summary.result} size={r.summary.totalSize} errors={r.summary.totalErrors} out={outPath}");
        if (Application.isBatchMode) EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
