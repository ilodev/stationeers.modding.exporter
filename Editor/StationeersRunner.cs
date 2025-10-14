// StationeersRunner.cs
// Unity 2022.3.7f1 (Editor only, ASCII)
// Launches Stationeers from Steam or itch.io, with manual override fallback.

/*
It will try (in order):
steam://rungameid/544550
Direct Steam install (…/steamapps/common/Stationeers/rocketstation.exe)
itch.io default installs (%APPDATA%\itch\apps\**\rocketstation.exe)
If not found, it’ll prompt the user to pick rocketstation.exe once and remember it.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{
    public static class StationeersRunner
    {
        private const string AppId = "544550"; // Stationeers (Steam) — steamdb confirms
        private const string ExeName = "rocketstation.exe";
        private const string PrefExeOverride = "StationeersRunner.ExeOverride";

        [MenuItem("Tools/Stationeers/Run")]
        public static void RunStationeers()
        {
            // 1) Try Steam URI
            if (TryOpenSteamUri())
                return;

            // 2) Try direct EXE via Steam library scan
            if (TryLaunch(FindSteamExe()))
                return;

            // 3) Try direct EXE via itch.io app installs
            if (TryLaunch(FindItchExe()))
                return;

            // 4) Try manual override from EditorPrefs
            var pref = EditorPrefs.GetString(PrefExeOverride, string.Empty);
            if (TryLaunch(ValidateExe(pref)))
                return;

            // 5) Prompt user to pick the executable once, then save it
            string picked = EditorUtility.OpenFilePanel("Locate Stationeers (rocketstation.exe)", "", "exe");
            if (!string.IsNullOrEmpty(picked) && Path.GetFileName(picked).Equals(ExeName, StringComparison.OrdinalIgnoreCase))
            {
                EditorPrefs.SetString(PrefExeOverride, picked);
                if (TryLaunch(picked)) return;
            }

            EditorUtility.DisplayDialog("Stationeers", "Could not find Stationeers. Please install it via Steam or itch.io, or set an override path.", "OK");
        }

        [MenuItem("Tools/Stationeers/Clear Manual Path")]
        private static void ClearOverride()
        {
            EditorPrefs.DeleteKey(PrefExeOverride);
            UnityEngine.Debug.Log("[StationeersRunner] Cleared manual path override.");
        }

        // --- Steam URI ---

        private static bool TryOpenSteamUri()
        {
            try
            {
                // This lets Steam resolve the install and handle arguments, updates, etc.
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"steam://rungameid/{AppId}",
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[StationeersRunner] Steam URI failed: " + ex.Message);
                return false;
            }
        }

        // --- Direct EXE launch ---

        private static bool TryLaunch(string exePath)
        {
            exePath = ValidateExe(exePath);
            if (string.IsNullOrEmpty(exePath)) return false;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = Path.GetDirectoryName(exePath),
                    UseShellExecute = true
                    // Arguments = "" // add CLI args if you want
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

        private static string ValidateExe(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!File.Exists(path)) return null;
            if (!Path.GetFileName(path).Equals(ExeName, StringComparison.OrdinalIgnoreCase)) return null;
            return path;
        }

        // --- Steam install discovery ---

        private static string FindSteamExe()
        {
            try
            {
                // Common Steam base paths (registry-free heuristic)
                var candidates = new List<string>();

                // Steam base (default)
                string progx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (!string.IsNullOrEmpty(progx86))
                    candidates.Add(Path.Combine(progx86, "Steam"));

                // Steam config libraryfolders.vdf (newer location)
                foreach (var steamRoot in candidates)
                {
                    var configVdf = Path.Combine(steamRoot, "config", "libraryfolders.vdf");
                    var steamapps = Path.Combine(steamRoot, "steamapps");
                    foreach (var lib in EnumerateSteamLibraries(configVdf, steamapps))
                    {
                        var exe = Path.Combine(lib, "common", "Stationeers", ExeName);
                        if (File.Exists(exe)) return exe;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[StationeersRunner] Steam scan failed: " + ex.Message);
            }
            return null;
        }

        private static IEnumerable<string> EnumerateSteamLibraries(string configVdf, string defaultSteamapps)
        {
            var libs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (Directory.Exists(defaultSteamapps))
                libs.Add(defaultSteamapps);

            // Parse libraryfolders.vdf (key/value-ish)
            if (File.Exists(configVdf))
            {
                string text = File.ReadAllText(configVdf);
                // Match lines like: "path" "D:\\SteamLibrary"
                var rx = new Regex("\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                foreach (Match m in rx.Matches(text))
                {
                    var path = m.Groups[1].Value.Replace(@"\\", @"\");
                    var steamapps = Path.Combine(path, "steamapps");
                    if (Directory.Exists(steamapps))
                        libs.Add(steamapps);
                }
            }

            return libs;
        }

        // --- itch.io install discovery ---

        private static string FindItchExe()
        {
            try
            {
                // Common default: %APPDATA%/itch/apps/**/rocketstation.exe
                string roam = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); // %APPDATA%
                var baseDir = Path.Combine(roam, "itch", "apps");
                if (Directory.Exists(baseDir))
                {
                    var exe = FindFileRecursive(baseDir, ExeName, depthLimit: 4);
                    if (!string.IsNullOrEmpty(exe)) return exe;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[StationeersRunner] itch scan failed: " + ex.Message);
            }
            return null;
        }

        private static string FindFileRecursive(string root, string fileName, int depthLimit)
        {
            try
            {
                foreach (var f in Directory.EnumerateFiles(root, fileName, SearchOption.TopDirectoryOnly))
                    return f;

                if (depthLimit <= 0) return null;

                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    var r = FindFileRecursive(dir, fileName, depthLimit - 1);
                    if (!string.IsNullOrEmpty(r)) return r;
                }
            }
            catch { /* ignore permission issues */ }
            return null;
        }
    }
}