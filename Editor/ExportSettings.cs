using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace stationeers.modding.exporter
{
    /// <summary>
    ///     Stores the exporter's settings.
    /// </summary>
    /// 

    [Flags]
    public enum ContentType { assemblies = 2, prefabs = 4, scenes = 8 }

    public class ExportSettings : ScriptableObject
    {
        [SerializeField] private string _name;

        [SerializeField] private string _author;

        [SerializeField] private string _description;

        [SerializeField] private string _version;

        [SerializeField] private string _tags;

        [SerializeField] private string _workshophandle;

        [SerializeField] private string _outputDirectory;

        [SerializeField] private string _stationeersDirectory;

        [SerializeField] private string _stationeersArguments;

        [SerializeField] private string[] _assemblies = new string[] { };

        [SerializeField] private string[] _artifacts = new string[] { };

        [SerializeField] private ContentType _contentTypes = ContentType.assemblies | ContentType.prefabs | ContentType.scenes;

        [SerializeField] private bool _includePdbs;

        [SerializeField] private bool _waitForDebugger;

        /// <summary>
        ///     The Mod's name.
        /// </summary>
        public string Name
        {
            get => _name;
            set => _name = value;
        }

        /// <summary>
        ///     The Mod's author.
        /// </summary>
        public string Author
        {
            get => _author;
            set => _author = value;
        }

        /// <summary>
        ///     The Mod's description.
        /// </summary>
        public string Description
        {
            get => _description;
            set => _description = value;
        }

        /// <summary>
        ///     The Mod's version.
        /// </summary>
        public string Version
        {
            get => _version;
            set => _version = value;
        }

        /// <summary>
        ///     The Mod's Tags.
        /// </summary>
        public string Tags
        {
            get => _tags;
            set => _tags = value;
        }

        /// <summary>
        ///     The Mod's Steam Workshop Handle.
        /// </summary>
        public string WorkshopHandle
        {
            get => _workshophandle;
            set => _workshophandle = value;
        }


        /// <summary>
        ///     The directory to which the Mod will be exported.
        /// </summary>
        public string[] Assemblies
        {
            get => _assemblies;
            set => _assemblies = value ?? new string[0];
        }
        public string[] Artifacts
        {
            get => _artifacts;
            set => _artifacts = value ?? new string[0];
        }

        public string OutputDirectory { get => _outputDirectory; set => _outputDirectory = value; }

        public string StationeersDirectory { get => _stationeersDirectory; set => _stationeersDirectory = value; }

        public string StationeersArguments { get => _stationeersArguments; set => _stationeersArguments = value; }

        public ContentType ContentTypes { get => _contentTypes; set => _contentTypes = value; }

        public bool IncludePdbs { get => _includePdbs; set => _includePdbs = value; }

        public bool WaitForDebugger { get => _waitForDebugger; set => _waitForDebugger = value; }


        public void AddAssembly(string assemblyName)
        {
            _assemblies.Append(assemblyName);
        }

        private void OnEnable()
        {
            // Set default name if empty
            if (string.IsNullOrEmpty(_name))
            {
                // Get the folder name of the Unity project
                string projectFolderName = Path.GetFileName(Path.GetDirectoryName(Application.dataPath));
                _name = projectFolderName;
            }

            // Set default author if empty
            if (string.IsNullOrEmpty(_author))
            {
                // Try to use Unity's company name or username
                string companyName = Application.companyName;

                // EditorPrefs stores the user name under this key in the Editor
                string editorUserName = EditorPrefs.GetString("unity.username", "");
                if (!string.IsNullOrEmpty(editorUserName))
                    _author = editorUserName;
                else
                    _author = companyName;
            }

            // Set default author if empty
            if (string.IsNullOrEmpty(_version))
            {
                _version = "1.0.0";
            }

            // Description, needs to be filled up by the user.

        }

    }
}