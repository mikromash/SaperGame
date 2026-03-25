using System.IO;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript
{
    public static void BuildWindows()
    {
        string buildDirectory = Path.Combine("Builds", "Windows");
        Directory.CreateDirectory(buildDirectory);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[]
            {
                "Assets/Scenes/SampleScene.unity"
            },
            locationPathName = Path.Combine(buildDirectory, "test-ai-coop.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new System.Exception("Unity build failed: " + report.summary.result);
        }
    }

    public static void PublishRelayServer()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string serverProjectPath = Path.Combine(projectRoot, "RelayServer", "RelayServer.csproj");
        string publishDirectory = Path.Combine(projectRoot, "Builds", "RelayServer");

        Directory.CreateDirectory(publishDirectory);

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"publish \"{serverProjectPath}\" -c Release -o \"{publishDirectory}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using Process process = Process.Start(startInfo);
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            string error = process.StandardError.ReadToEnd();
            throw new System.Exception("Relay server publish failed: " + error);
        }
    }
}
