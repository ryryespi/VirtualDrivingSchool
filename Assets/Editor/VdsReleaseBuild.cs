using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class VdsReleaseBuild
{
    const string TriggerPath = "Builds/Release/START_BUILD.txt";
    const string StatusPath = "Builds/Release/build-status.txt";
    const string ReportPath = "Builds/Release/build-report.txt";
    const string ExePath = "Builds/Release/VirtualDrivingSchool.exe";
    const string Scene = "Assets/Scenes/DrivingSchool_Main.unity";

    static VdsReleaseBuild()
    {
        EditorApplication.update += CheckTrigger;
    }

    static void CheckTrigger()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        if (EditorApplication.isPlaying)
            return;
        if (!File.Exists(TriggerPath))
            return;

        try
        {
            File.Delete(TriggerPath);
        }
        catch
        {
            return;
        }

        Build();
    }

    [MenuItem("Tools/Virtual Driving School/Build Windows Release Player")]
    public static void BuildMenu()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Virtual Driving School", "Exit Play mode before building.", "OK");
            return;
        }

        var report = Build();
        EditorUtility.DisplayDialog("Virtual Driving School", report, "OK");
    }
    public static string Build()
    {
        Directory.CreateDirectory("Builds/Release");
        File.WriteAllText(StatusPath, "building " + System.DateTime.Now.ToString("o"));

        PlayerSettings.productName = "Virtual Driving School";
        PlayerSettings.companyName = "ryryespi";
        PlayerSettings.bundleVersion = "1.0.0";
        PlayerSettings.runInBackground = true;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultIsNativeResolution = true;
        PlayerSettings.visibleInBackground = true;
        PlayerSettings.allowFullscreenSwitch = true;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[]
        {
            UnityEngine.Rendering.GraphicsDeviceType.Direct3D11,
            UnityEngine.Rendering.GraphicsDeviceType.Direct3D12
        });

        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.allowDebugging = false;
        EditorUserBuildSettings.connectProfiler = false;

        var options = new BuildPlayerOptions
        {
            scenes = new[] { Scene },
            locationPathName = ExePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        var sb = new StringBuilder();
        sb.AppendLine("result=" + report.summary.result);
        sb.AppendLine("errors=" + report.summary.totalErrors);
        sb.AppendLine("warnings=" + report.summary.totalWarnings);
        sb.AppendLine("size=" + report.summary.totalSize);
        sb.AppendLine("output=" + report.summary.outputPath);
        sb.AppendLine("time=" + report.summary.totalTime);
        foreach (var step in report.steps)
        {
            foreach (var msg in step.messages)
            {
                if (msg.type == LogType.Error || msg.type == LogType.Exception ||
                    msg.type == LogType.Assert || msg.type == LogType.Warning)
                {
                    sb.AppendLine("[" + step.name + "] " + msg.type + ": " + msg.content);
                }
            }
        }

        File.WriteAllText(ReportPath, sb.ToString());
        File.WriteAllText(StatusPath, report.summary.result.ToString());
        return sb.ToString();
    }
}