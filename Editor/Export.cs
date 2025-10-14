using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using Debug = UnityEngine.Debug;

namespace stationeers.modding.exporter
{
    public class Export
    {
        private readonly List<string> assetPaths;
        private readonly List<string> scenePaths;
        private readonly string modDirectory;
        private readonly string prefix;
        private readonly ExportSettings settings;
        private readonly string tempModDirectory;

        public Export(ExportSettings settings)
        {
            this.settings = settings;
            prefix = $"{settings.Name}-{settings.Version}";
            assetPaths = AssetUtility.GetAssets("t:prefab t:scriptableobject");
            scenePaths = AssetUtility.GetAssets("t:scene");
            tempModDirectory = Path.Combine("Temp", settings.Name);
            modDirectory = Path.Combine(settings.OutputDirectory, settings.Name);
        }

        public void SetAssetBundle(string assetPath, string variant = "assets")
        {
            var importer = AssetImporter.GetAtPath(assetPath);
            importer.assetBundleName = settings.Name;
            importer.assetBundleVariant = variant;
        }

        private void CopyAll(string sourceDirectory, string targetDirectory)
        {
            Directory.CreateDirectory(targetDirectory);

            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                var fileName = Path.GetFileName(file);
                File.Copy(file, Path.Combine(targetDirectory, fileName), true);
            }

            foreach (var subDirectory in Directory.GetDirectories(sourceDirectory))
            {
                var targetSubDirectory = Path.Combine(targetDirectory, Path.GetFileName(subDirectory));
                CopyAll(subDirectory, targetSubDirectory);
            }
        }

        private void CreateTempDirectory()
        {
            if (Directory.Exists(tempModDirectory))
                Directory.Delete(tempModDirectory, true);
            LogUtility.LogDebug($"Creating build directory: {tempModDirectory}");
            Directory.CreateDirectory(tempModDirectory);
        }

        private void ExportModAssemblies()
        {
            LogUtility.LogInfo("Exporting mod assemblies (" +  settings.Assemblies.Length + ")...");
            foreach (var asmDefPath in settings.Assemblies)
            {
                var json = File.ReadAllText(asmDefPath);
                var asmDef = JsonUtility.FromJson<AsmDef>(json);

                var modAsmPath = Path.Combine("Library", "ScriptAssemblies", $"{asmDef.name}.dll");
                var modPdbPath = Path.Combine("Library", "ScriptAssemblies", $"{asmDef.name}.pdb");

                if (!File.Exists(modAsmPath))
                {
                    LogUtility.LogError($"{asmDef.name}.dll not found: {modAsmPath}");
                    continue;
                }

                if (settings.IncludePdbs && !File.Exists(modPdbPath))
                {
                    LogUtility.LogError($"{asmDef.name}.pdb not found: {modPdbPath}");
                    continue;
                }

                LogUtility.LogDebug($" - {asmDef.name}");
                File.Copy(modAsmPath, Path.Combine(tempModDirectory, $"{asmDef.name}.dll"));

                if (settings.IncludePdbs)
                {
                    File.Copy(modPdbPath, Path.Combine(tempModDirectory, $"{asmDef.name}.pdb"));
                }
            }
        }

        private void ExportCopyAssets()
        {
            List<string> list = AssetDatabase.GetAllAssetPaths().Where(o => { /*LogUtility.LogDebug("Assetpath:" + o);*/ return o.StartsWith("Assets/GameData"); }).ToList();
            LogUtility.LogInfo("Exporting copy assets...");
            string gamedata = Path.Combine("Assets", "GameData");
            string about = Path.Combine("Assets", "About");
            var dir = new DirectoryInfo(gamedata);
            if (dir.Exists)
            {
                CopyDirectory(gamedata, Path.Combine(tempModDirectory,"GameData"), true);
            }
             dir = new DirectoryInfo(about);
            if (dir.Exists)
            {
                CopyDirectory(about, Path.Combine(tempModDirectory, "About"), true);
            }
        }

        static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                if(!file.FullName.EndsWith("meta"))
                {
                    file.CopyTo(targetFilePath);
                }
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }

        private void ExportModAssets()
        {
            assetPaths.ForEach(s => SetAssetBundle(s));
            scenePaths.ForEach(s => SetAssetBundle(s, "scenes"));

            var platform = BuildTarget.StandaloneWindows.ToString();
            var subDir = Path.Combine(tempModDirectory, platform);
            Directory.CreateDirectory(subDir);
            LogUtility.LogInfo($"Exporting assets for {platform} to: {subDir}");
            BuildPipeline.BuildAssetBundles(subDir, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows);
        }

        private void ExportAboutMetadata()
        {
            var modAbout = new ModAbout(
                settings.Name,
                settings.Author,
                settings.Description,
                settings.Version
            );

            //ModAbout.Save("Assets/About/About.xml", modAbout);
            ModAbout.Save(Path.Combine(tempModDirectory, "About/About.xml"), modAbout);
        }

        private void CopyToOutput()
        {
            try
            {
                if (Directory.Exists(modDirectory))
                    Directory.Delete(modDirectory, true);

                LogUtility.LogInfo($"Copying {tempModDirectory} => {modDirectory}");
                CopyAll(tempModDirectory, modDirectory);
                LogUtility.LogInfo($"Export completed: {modDirectory}");
            }
            catch (Exception e)
            {
                LogUtility.LogWarning("There was an issue while copying the mod to the output folder. ");
                LogUtility.LogWarning(e.Message);
            }
        }

        public void Run()
        {
            LogUtility.LogDebug($"Starting export of {settings.Name}");
            CreateTempDirectory();
            ExportModAssemblies();
            ExportCopyAssets();
            ExportModAssets();
            //ExportAboutMetadata();
            CopyToOutput();
        }

        public static void ExportMod(ExportSettings settings)
        {
            var exporter = new Export(settings);
            EditorUtility.SetDirty(settings);
            exporter.Run();
        }

        public static void RunGame(ExportSettings settings)
        {
            var patcher = new DevelopmentPatcher();

            string pathToStationeers = patcher.PathToStationeersExecutable(settings);
            if (!File.Exists(pathToStationeers))
            {
                throw new FileNotFoundException($"Game executable not found at \"{pathToStationeers}\". Make sure the Stationeers directory is configured correctly under export settings.");
            }

            Process.Start(pathToStationeers, settings.StationeersArguments ?? "");
        }
    }
}