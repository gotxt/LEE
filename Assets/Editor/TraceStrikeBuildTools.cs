#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NHN.TraceStrike.Editor
{
    public static class TraceStrikeBuildTools
    {
        [MenuItem("Trace Strike/Configure Mobile Project")]
        public static void ConfigureProject()
        {
            PlayerSettings.companyName = "NHN Challenge Team";
            PlayerSettings.productName = "Trace Strike";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.nhnchallenge.tracestrike");
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.runInBackground = false;
            PlayerSettings.use32BitDisplayBuffer = true;
            AssetDatabase.SaveAssets();
            Debug.Log("Trace Strike mobile project settings configured.");
        }

        [MenuItem("Trace Strike/Build Android APK")]
        public static void BuildAndroidApk()
        {
            ConfigureProject();
            string outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Android"));
            Directory.CreateDirectory(outputDirectory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = Path.Combine(outputDirectory, "TraceStrike.apk"),
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException("Android build failed: " + report.summary.result);
            }

            Debug.Log("APK created at: " + options.locationPathName);
        }

        [MenuItem("Trace Strike/Configure Desktop Project")]
        public static void ConfigureDesktopProject()
        {
            PlayerSettings.companyName = "NHN Challenge Team";
            PlayerSettings.productName = "Trace Strike";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.runInBackground = false;
            PlayerSettings.use32BitDisplayBuffer = true;
            AssetDatabase.SaveAssets();
            Debug.Log("Trace Strike desktop 16:9 project settings configured.");
        }

        [MenuItem("Trace Strike/Build Windows 16:9")]
        public static void BuildWindows16By9()
        {
            ConfigureDesktopProject();
            string outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Windows"));
            Directory.CreateDirectory(outputDirectory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = Path.Combine(outputDirectory, "TraceStrike.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException("Windows 16:9 build failed: " + report.summary.result);
            }

            Debug.Log("Windows 16:9 build created at: " + options.locationPathName);
        }
    }
}
#endif
