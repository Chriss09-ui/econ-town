using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EconTown.BuildTools
{
    public static class WebGLBuild
    {
        const string BundledChineseFontPath = "Assets/Resources/Fonts/NotoSansCJKsc-Regular.otf";

        [MenuItem("EconTown/Build/WebGL")]
        public static void PerformBuild()
        {
            string outputPath = GetCommandLineValue("-buildOutput");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                outputPath = Path.GetFullPath(Path.Combine(projectRoot, "..", "Builds", "WebGL"));
            }

            string[] scenes = ValidateBuildInputs();
            string stagingPath = outputPath + ".staging";
            string previousPath = outputPath + ".previous";
            RecreateDirectory(stagingPath);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = stagingPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"WebGL build failed: {report.summary.result} " +
                    $"({report.summary.totalErrors} errors, {report.summary.totalWarnings} warnings)."
                );
            }

            string cacheToken = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            ValidateAndStampBuild(stagingPath, cacheToken, report);
            PublishBuild(stagingPath, outputPath, previousPath);

            Debug.Log(
                $"WebGL build succeeded: {outputPath} " +
                $"({report.summary.totalSize} bytes, {report.summary.totalTime})."
            );
        }

        static string[] ValidateBuildInputs()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scenes found in Build Profiles.");

            Font bundledFont = AssetDatabase.LoadAssetAtPath<Font>(BundledChineseFontPath);
            if (bundledFont == null)
            {
                throw new InvalidOperationException(
                    $"Bundled Chinese font is missing or cannot be imported: {BundledChineseFontPath}"
                );
            }

            return scenes;
        }

        static void ValidateAndStampBuild(string buildPath, string cacheToken, BuildReport report)
        {
            string indexPath = Path.Combine(buildPath, "index.html");
            string buildDirectory = Path.Combine(buildPath, "Build");
            if (!File.Exists(indexPath) || !Directory.Exists(buildDirectory))
                throw new InvalidOperationException("WebGL output is incomplete: index.html or Build directory is missing.");

            string[] requiredPatterns = { "*.loader.js", "*.data*", "*.framework.js*", "*.wasm*" };
            string[] requiredFiles = requiredPatterns
                .Select(pattern => Directory.GetFiles(buildDirectory, pattern, SearchOption.TopDirectoryOnly)
                    .SingleOrDefault(path => new FileInfo(path).Length > 0))
                .ToArray();

            if (requiredFiles.Any(string.IsNullOrEmpty))
                throw new InvalidOperationException("WebGL output is incomplete: one or more loader/data/framework/wasm files are missing.");

            string html = File.ReadAllText(indexPath);
            foreach (string requiredFile in requiredFiles)
            {
                string fileName = Path.GetFileName(requiredFile);
                if (!html.Contains(fileName))
                    throw new InvalidOperationException($"WebGL index does not reference required asset: {fileName}");

                html = html.Replace(fileName, fileName + "?v=" + cacheToken);
            }
            File.WriteAllText(indexPath, html, new UTF8Encoding(false));

            long actualSize = Directory.EnumerateFiles(buildPath, "*", SearchOption.AllDirectories)
                .Sum(path => new FileInfo(path).Length);
            string buildInfo =
                $"BuiltAtUtc={DateTime.UtcNow:O}{Environment.NewLine}" +
                $"UnityVersion={Application.unityVersion}{Environment.NewLine}" +
                $"CacheToken={cacheToken}{Environment.NewLine}" +
                $"ReportedBytes={report.summary.totalSize}{Environment.NewLine}" +
                $"OutputBytes={actualSize}{Environment.NewLine}";
            File.WriteAllText(Path.Combine(buildPath, "build-info.txt"), buildInfo, new UTF8Encoding(false));
        }

        static void PublishBuild(string stagingPath, string outputPath, string previousPath)
        {
            if (Directory.Exists(previousPath))
                Directory.Delete(previousPath, true);

            bool movedCurrentBuild = false;
            try
            {
                if (Directory.Exists(outputPath))
                {
                    Directory.Move(outputPath, previousPath);
                    movedCurrentBuild = true;
                }

                Directory.Move(stagingPath, outputPath);
            }
            catch
            {
                if (movedCurrentBuild && !Directory.Exists(outputPath) && Directory.Exists(previousPath))
                    Directory.Move(previousPath, outputPath);
                throw;
            }
        }

        static void RecreateDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
            Directory.CreateDirectory(path);
        }

        private static string GetCommandLineValue(string argumentName)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], argumentName, StringComparison.Ordinal))
                {
                    return arguments[index + 1];
                }
            }

            return null;
        }
    }
}
