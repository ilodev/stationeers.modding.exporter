using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

namespace stationeers.modding.exporter
{
    /// <summary>
    /// Utility for launching Stationeers from within the Unity Editor.
    /// </summary>
    /// <remarks>
    /// This class supports multiple launch strategies and remembers a user-selected executable path.
    /// <para><b>Launch order</b></para>
    /// <list type="number">
    /// <item><description>Override exe path (if valid)</description></item>
    /// <item><description>Steam URI (Steam resolves install/update/etc.)</description></item>
    /// <item><description>Steam library scan (heuristic, registry-free)</description></item>
    /// <item><description>itch.io scan (default install dir under %APPDATA%)</description></item>
    /// <item><description>User prompt to locate the exe once</description></item>
    /// </list>
    /// </remarks>
    public static class StationeersRunner
    {
        private const string AppId = "544550"; // Stationeers Steam AppID
        private const string ExeName = "rocketstation.exe";

        /// <summary>
        /// Attempts to launch Stationeers only if autorun has been enabled in EditorPrefs.
        /// </summary>
        /// <remarks>
        /// This is intended to be called by other tooling (e.g., after exporting/mod building).
        /// </remarks>
        public static void TryRunStationeers()
        {            
            if (!StationeersExporterUserPreferences.RunnerEnabled)
                return;

            RunStationeers();
        }

        /// <summary>
        /// Returns a clean string with the arguments to run the game.
        /// </summary>
        private static string GetLaunchArgs()
        {
            return StationeersExporterUserPreferences.RunnerArguments?.Trim();
        }

        /// <summary>
        /// Launches Stationeers using the configured fallback chain.
        /// </summary>
        /// <remarks>
        /// This method tries (in order):
        /// override exe path, 
        /// Steam URI, 
        /// Steam install scan, 
        /// itch.io scan, 
        /// if still failed, prompts the user to find the executable.
        /// </remarks>
        [MenuItem("Tools/Stationeers/Launch game")]
        public static void RunStationeers()
        {
            string overridePath = StationeersExporterUserPreferences.RunnerExeOverride;
            if (TryLaunchExe(overridePath))
                return;

            if (TryOpenSteamUri())
                return;

            var steamExe = FindSteamExe();
            if (TryLaunchExe(steamExe))
                return;

            var itchExe = FindItchExe();
            if (TryLaunchExe(itchExe))
                return;

            var picked = EditorUtility.OpenFilePanel($"Locate Stationeers ({ExeName})", "", "exe");
            if (IsValidExePath(picked))
            {
                StationeersExporterUserPreferences.RunnerExeOverride = picked;
                if (TryLaunchExe(picked))
                    return;
            }

            EditorUtility.DisplayDialog(
                "Stationeers",
                "Could not find Stationeers.\n\nInstall it via Steam or itch.io, or set an override path by selecting rocketstation.exe.",
                "OK");
        }

        /// <summary>
        /// Attempts to launch Stationeers via the Steam protocol URI.
        /// </summary>
        /// <returns><c>true</c> if the URI open attempt was initiated; otherwise <c>false</c>.</returns>
        /// <remarks>
        /// Steam will resolve installation location, updates, and launch behavior.
        /// Note: <see cref="Process.Start(ProcessStartInfo)"/> may return null on some platforms.
        /// </remarks>
        private static bool TryOpenSteamUri()
        {
            try
            {
                var args = GetLaunchArgs();

                // steam://rungameid/<appid>//<launch options>
                var uri = string.IsNullOrWhiteSpace(args)
                    ? $"steam://rungameid/{AppId}"
                    : $"steam://rungameid/{AppId}//{Uri.EscapeDataString(args)}";

                var psi = new ProcessStartInfo
                {
                    FileName = uri,
                    UseShellExecute = true
                };

                var process = Process.Start(psi);
                UnityEngine.Debug.Log(process != null
                    ? $"[StationeersRunner] Steam URI launch requested (pid={process.Id}). Args='{args}'"
                    : $"[StationeersRunner] Steam URI launch requested. Args='{args}'");

                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[StationeersRunner] Steam URI failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Attempts to launch Stationeers by directly starting a validated executable path.
        /// </summary>
        /// <param name="exePath">Absolute path to <c>rocketstation.exe</c>.</param>
        /// <returns><c>true</c> if a launch was initiated; otherwise <c>false</c>.</returns>
        private static bool TryLaunchExe(string exePath)
        {
            if (!IsValidExePath(exePath))
            {
                UnityEngine.Debug.Log($"[StationeersRunner] invalid exe path: {exePath}");
                return false;
            }

            try
            {   
                var args = GetLaunchArgs();
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = Path.GetDirectoryName(exePath),
                    UseShellExecute = true,
                    Arguments = string.IsNullOrWhiteSpace(args) ? "" : args
                };

                Process.Start(psi);
                UnityEngine.Debug.Log("[StationeersRunner] Launched: " + exePath);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[StationeersRunner] Failed to launch: " + exePath + "\n" + ex);
                return false;
            }
        }

        /// <summary>
        /// Validates that the given path points to an existing <c>rocketstation.exe</c>.
        /// </summary>
        /// <param name="path">Candidate path.</param>
        /// <returns><c>true</c> if the path exists and matches <see cref="ExeName"/>; otherwise <c>false</c>.</returns>
        private static bool IsValidExePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (!File.Exists(path))
                return false;

            return Path.GetFileName(path).Equals(ExeName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Attempts to locate <c>rocketstation.exe</c> in common Steam library locations.
        /// </summary>
        /// <returns>The full path to the exe if found; otherwise <c>null</c>.</returns>
        /// <remarks>
        /// This is a registry-free heuristic:
        /// <list type="bullet">
        /// <item><description>Starts from <c>%ProgramFiles(x86)%\Steam</c></description></item>
        /// <item><description>Reads <c>config\libraryfolders.vdf</c> to discover additional libraries</description></item>
        /// <item><description>Checks <c>steamapps\common\Stationeers\rocketstation.exe</c> for each library</description></item>
        /// </list>
        /// </remarks>
        private static string FindSteamExe()
        {
            try
            {
                var steamRoots = new List<string>();

                var progx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (!string.IsNullOrEmpty(progx86))
                    steamRoots.Add(Path.Combine(progx86, "Steam"));

                foreach (var steamRoot in steamRoots)
                {
                    var configVdf = Path.Combine(steamRoot, "config", "libraryfolders.vdf");
                    var defaultSteamapps = Path.Combine(steamRoot, "steamapps");

                    foreach (var steamapps in EnumerateSteamLibraries(configVdf, defaultSteamapps))
                    {
                        var exe = Path.Combine(steamapps, "common", "Stationeers", ExeName);
                        if (File.Exists(exe))
                            return exe;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[StationeersRunner] Steam scan failed: " + ex.Message);
            }

            return null;
        }

        /// <summary>
        /// Enumerates Steam library <c>steamapps</c> folders from the default path plus <c>libraryfolders.vdf</c>.
        /// </summary>
        /// <param name="configVdf">Full path to <c>libraryfolders.vdf</c>.</param>
        /// <param name="defaultSteamapps">Default <c>steamapps</c> folder.</param>
        /// <returns>Distinct <c>steamapps</c> directories that exist.</returns>
        private static IEnumerable<string> EnumerateSteamLibraries(string configVdf, string defaultSteamapps)
        {
            var libs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (Directory.Exists(defaultSteamapps))
                libs.Add(defaultSteamapps);

            if (File.Exists(configVdf))
            {
                var text = File.ReadAllText(configVdf);

                // Matches lines like: "path" "D:\\SteamLibrary"
                var rx = new Regex("\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);

                foreach (Match m in rx.Matches(text))
                {
                    var libraryRoot = m.Groups[1].Value.Replace(@"\\", @"\");
                    var steamapps = Path.Combine(libraryRoot, "steamapps");

                    if (Directory.Exists(steamapps))
                        libs.Add(steamapps);
                }
            }

            return libs;
        }

        /// <summary>
        /// Attempts to locate <c>rocketstation.exe</c> in common itch.io installation folders.
        /// </summary>
        /// <returns>The full path to the exe if found; otherwise <c>null</c>.</returns>
        /// <remarks>
        /// Searches under: <c>%APPDATA%\itch\apps</c> with a shallow recursion depth.
        /// </remarks>
        private static string FindItchExe()
        {
            try
            {
                var roam = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); // %APPDATA%
                var baseDir = Path.Combine(roam, "itch", "apps");

                if (!Directory.Exists(baseDir))
                    return null;

                var exe = FindFileRecursive(baseDir, ExeName, depthLimit: 4);
                return string.IsNullOrEmpty(exe) ? null : exe;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[StationeersRunner] itch scan failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Recursively searches for a file name under a root directory with a recursion depth limit.
        /// </summary>
        /// <param name="root">Root folder to begin searching.</param>
        /// <param name="fileName">File name to find (no path).</param>
        /// <param name="depthLimit">Maximum directory depth to recurse into.</param>
        /// <returns>The first matching full path found; otherwise <c>null</c>.</returns>
        private static string FindFileRecursive(string root, string fileName, int depthLimit)
        {
            try
            {
                foreach (var f in Directory.EnumerateFiles(root, fileName, SearchOption.TopDirectoryOnly))
                    return f;

                if (depthLimit <= 0)
                    return null;

                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    var r = FindFileRecursive(dir, fileName, depthLimit - 1);
                    if (!string.IsNullOrEmpty(r))
                        return r;
                }
            }
            catch
            {
                // Ignore permission issues, broken symlinks, etc.
            }

            return null;
        }
    }
}