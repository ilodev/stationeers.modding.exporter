using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;


namespace stationeers.modding.exporter
{

    public class ModMetadataEditorWindow : EditorWindow
    {
        private const string RelativeXmlPath = "Assets/About/About.xml";

        public enum ModSide
        {
            Unknown,
            Both,
            Client,
            Server,
        }

        private XDocument _document;

        private TextField _nameField;
        private TextField _authorField;
        private TextField _versionField;

        private TextField _descriptionField;
        private TextField _changeLogField;
        private TextField _inGameDescriptionField;

        private EnumField _modSideField;
        private TextField _workshopHandleField;
        private TextField _tagsField;

        private Label _statusLabel;

        private string AbsoluteXmlPath =>
            Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "..",
                    RelativeXmlPath));

        // =====================================================================
        // WINDOW
        // =====================================================================

        [MenuItem("Window/Stationeers Modding Tools/Mod Metadata Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<ModMetadataEditorWindow>();

            window.titleContent =
                new GUIContent("Mod Metadata");

            window.minSize =
                new Vector2(650, 650);
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;

            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            // =================================================================
            // TOOLBAR
            // =================================================================

            var toolbar = new Toolbar();

            var saveButton = new ToolbarButton(Save)
            {
                text = "Save"
            };

            var reloadButton = new ToolbarButton(Reload)
            {
                text = "Reload"
            };

            toolbar.Add(saveButton);
            toolbar.Add(reloadButton);

            root.Add(toolbar);

            // =================================================================
            // FILE PATH
            // =================================================================

            var pathLabel = new Label(RelativeXmlPath);

            pathLabel.style.unityFontStyleAndWeight =
                FontStyle.Italic;

            pathLabel.style.marginTop = 6;
            pathLabel.style.marginBottom = 8;

            root.Add(pathLabel);

            // =================================================================
            // CONTENT
            // =================================================================

            var scrollView = new ScrollView
            {
                style =
            {
                flexGrow = 1
            }
            };

            // -----------------------------------------------------------------
            // PROJECT SETTINGS
            // -----------------------------------------------------------------

            _nameField =
                CreateTextField("Name");

            _authorField =
                CreateTextField("Author / Company");

            _versionField =
                CreateTextField("Version");

            _nameField.tooltip =
                "Unity PlayerSettings.productName";

            _authorField.tooltip =
                "Unity PlayerSettings.companyName";

            _versionField.tooltip =
                "Unity PlayerSettings.bundleVersion";

            scrollView.Add(_nameField);
            scrollView.Add(_authorField);
            scrollView.Add(_versionField);

            AddSeparator(scrollView);

            // -----------------------------------------------------------------
            // DESCRIPTION
            // -----------------------------------------------------------------

            _descriptionField =
                CreateMultilineField(
                    "Description",
                    220);

            scrollView.Add(_descriptionField);

            AddSeparator(scrollView);

            // -----------------------------------------------------------------
            // CHANGE LOG
            // -----------------------------------------------------------------

            _changeLogField =
                CreateMultilineField(
                    "Change Log",
                    140);

            scrollView.Add(_changeLogField);

            AddSeparator(scrollView);

            // -----------------------------------------------------------------
            // IN-GAME DESCRIPTION
            // -----------------------------------------------------------------

            _inGameDescriptionField =
                CreateMultilineField(
                    "In-Game Description",
                    260);

            _inGameDescriptionField.tooltip =
                "Stored inside a CDATA section.";

            scrollView.Add(_inGameDescriptionField);

            AddSeparator(scrollView);

            // -----------------------------------------------------------------
            // MOD SIDE
            // -----------------------------------------------------------------

            _modSideField =
                new EnumField(
                    "Mod Side",
                    ModSide.Unknown);

            _modSideField.style.marginBottom = 5;

            _modSideField.tooltip =
                "Unknown / Both / Client / Server";

            scrollView.Add(_modSideField);

            // -----------------------------------------------------------------
            // WORKSHOP HANDLE
            // -----------------------------------------------------------------

            _workshopHandleField =
                CreateTextField("Workshop Handle");

            _workshopHandleField.tooltip =
                "Optional. If empty, <WorkshopHandle> will not be written.";

            scrollView.Add(_workshopHandleField);

            // -----------------------------------------------------------------
            // TAGS
            // -----------------------------------------------------------------

            _tagsField =
                CreateMultilineField(
                    "Tags",
                    100);

            _tagsField.tooltip =
                "One tag per line.";

            scrollView.Add(_tagsField);

            root.Add(scrollView);

            // =================================================================
            // STATUS
            // =================================================================

            _statusLabel = new Label();

            _statusLabel.style.marginTop = 6;

            root.Add(_statusLabel);

            // =================================================================
            // KEYBOARD SHORTCUTS
            // =================================================================

            root.RegisterCallback<KeyDownEvent>(
                evt =>
                {
                    if (evt.keyCode == KeyCode.S &&
                        (evt.ctrlKey || evt.commandKey))
                    {
                        Save();

                        evt.StopPropagation();
                    }
                });

            Initialize();
        }

        // =====================================================================
        // INITIALIZATION
        // =====================================================================

        private void Initialize()
        {
            LoadProjectSettings();

            if (!File.Exists(AbsoluteXmlPath))
            {
                ClearXmlFields();
                AskToCreateXml();
                return;
            }

            LoadXml();
        }

        // =====================================================================
        // PROJECT SETTINGS
        // =====================================================================

        private void LoadProjectSettings()
        {
            _nameField.value =
                PlayerSettings.productName;

            _authorField.value =
                PlayerSettings.companyName;

            _versionField.value =
                PlayerSettings.bundleVersion;
        }

        private void SaveProjectSettings()
        {
            PlayerSettings.productName =
                (_nameField.value ?? string.Empty).Trim();

            PlayerSettings.companyName =
                (_authorField.value ?? string.Empty).Trim();

            PlayerSettings.bundleVersion =
                (_versionField.value ?? string.Empty).Trim();
        }

        // =====================================================================
        // MISSING XML
        // =====================================================================

        private void AskToCreateXml()
        {
            _document = null;

            SetStatus(
                $"{RelativeXmlPath} does not exist.");

            bool create =
                EditorUtility.DisplayDialog(
                    "Create About.xml?",
                    $"{RelativeXmlPath} does not exist.\n\n" +
                    "Would you like to create it?",
                    "Create",
                    "Cancel");

            if (create)
            {
                CreateXml();
            }
        }

        private void CreateXml()
        {
            try
            {
                string directory =
                    Path.GetDirectoryName(AbsoluteXmlPath);

                if (!string.IsNullOrEmpty(directory) &&
                    !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                XNamespace xsd =
                    "http://www.w3.org/2001/XMLSchema";

                XNamespace xsi =
                    "http://www.w3.org/2001/XMLSchema-instance";

                _document =
                    new XDocument(
                        new XDeclaration(
                            "1.0",
                            "utf-8",
                            null),

                        new XElement(
                            "ModMetadata",

                            new XAttribute(
                                XNamespace.Xmlns + "xsd",
                                xsd),

                            new XAttribute(
                                XNamespace.Xmlns + "xsi",
                                xsi),

                            new XElement(
                                "Name",
                                PlayerSettings.productName),

                            new XElement(
                                "Author",
                                PlayerSettings.companyName),

                            new XElement(
                                "Version",
                                PlayerSettings.bundleVersion),

                            new XElement(
                                "Description",
                                string.Empty),

                            new XElement(
                                "ChangeLog",
                                string.Empty),

                            new XElement(
                                "InGameDescription",
                                new XCData(string.Empty)),

                            new XElement(
                                "ModSide",
                                ModSide.Unknown.ToString()),

                            // WorkshopHandle is intentionally NOT
                            // created when there is no value.

                            new XElement(
                                "Tags")
                        )
                    );

                SaveDocument();

                AssetDatabase.Refresh();

                LoadXml();

                SetStatus(
                    "Created Assets/About/About.xml.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);

                EditorUtility.DisplayDialog(
                    "Create About.xml Error",
                    exception.Message,
                    "OK");

                SetStatus(
                    "Failed to create About.xml.");
            }
        }

        // =====================================================================
        // LOADING
        // =====================================================================

        private void Reload()
        {
            LoadProjectSettings();

            if (!File.Exists(AbsoluteXmlPath))
            {
                ClearXmlFields();
                AskToCreateXml();
                return;
            }

            LoadXml();
        }

        private void LoadXml()
        {
            try
            {
                _document =
                    XDocument.Load(AbsoluteXmlPath);

                if (_document.Root == null ||
                    _document.Root.Name.LocalName != "ModMetadata")
                {
                    throw new Exception(
                        "The root XML element must be <ModMetadata>.");
                }

                XElement root =
                    _document.Root;

                // PlayerSettings is the source of truth for these.
                // Do not load these values from About.xml.
                LoadProjectSettings();

                _descriptionField.value =
                    GetValue(
                        root,
                        "Description");

                _changeLogField.value =
                    GetValue(
                        root,
                        "ChangeLog");

                _inGameDescriptionField.value =
                    GetValue(
                        root,
                        "InGameDescription");

                LoadModSide(root);

                // Missing WorkshopHandle = empty field.
                // There is deliberately no "0" fallback.
                _workshopHandleField.value =
                    GetValue(
                        root,
                        "WorkshopHandle");

                LoadTags(root);

                SetStatus("Loaded.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);

                EditorUtility.DisplayDialog(
                    "About.xml Error",
                    exception.Message,
                    "OK");

                SetStatus(
                    "Failed to load About.xml.");
            }
        }

        private void LoadModSide(
            XElement root)
        {
            string value =
                GetValue(
                    root,
                    "ModSide");

            if (Enum.TryParse(
                    value,
                    true,
                    out ModSide modSide))
            {
                _modSideField.value =
                    modSide;
            }
            else
            {
                _modSideField.value =
                    ModSide.Unknown;
            }
        }

        private void LoadTags(
            XElement root)
        {
            XElement tags =
                FindElement(
                    root,
                    "Tags");

            if (tags == null)
            {
                _tagsField.value =
                    string.Empty;

                return;
            }

            _tagsField.value =
                string.Join(
                    "\n",
                    tags
                        .Elements()
                        .Where(
                            x => x.Name.LocalName == "Tag")
                        .Select(
                            x => x.Value));
        }

        private void ClearXmlFields()
        {
            if (_descriptionField != null)
            {
                _descriptionField.value =
                    string.Empty;
            }

            if (_changeLogField != null)
            {
                _changeLogField.value =
                    string.Empty;
            }

            if (_inGameDescriptionField != null)
            {
                _inGameDescriptionField.value =
                    string.Empty;
            }

            if (_modSideField != null)
            {
                _modSideField.value =
                    ModSide.Unknown;
            }

            if (_workshopHandleField != null)
            {
                _workshopHandleField.value =
                    string.Empty;
            }

            if (_tagsField != null)
            {
                _tagsField.value =
                    string.Empty;
            }
        }

        // =====================================================================
        // SAVING
        // =====================================================================

        private void Save()
        {
            if (!File.Exists(AbsoluteXmlPath))
            {
                AskToCreateXml();

                if (!File.Exists(AbsoluteXmlPath))
                {
                    return;
                }
            }

            try
            {
                if (_document == null)
                {
                    _document =
                        XDocument.Load(
                            AbsoluteXmlPath);
                }

                if (_document.Root == null ||
                    _document.Root.Name.LocalName != "ModMetadata")
                {
                    throw new Exception(
                        "The root XML element must be <ModMetadata>.");
                }

                // -------------------------------------------------------------
                // SAVE UNITY PROJECT SETTINGS
                // -------------------------------------------------------------

                SaveProjectSettings();

                // -------------------------------------------------------------
                // SAVE XML
                // -------------------------------------------------------------

                XElement root =
                    _document.Root;

                // Mirror the Unity Project Settings into About.xml.
                SetValue(
                    root,
                    "Name",
                    PlayerSettings.productName);

                SetValue(
                    root,
                    "Author",
                    PlayerSettings.companyName);

                SetValue(
                    root,
                    "Version",
                    PlayerSettings.bundleVersion);

                SetValue(
                    root,
                    "Description",
                    NormalizeNewLines(
                        _descriptionField.value));

                SetValue(
                    root,
                    "ChangeLog",
                    NormalizeNewLines(
                        _changeLogField.value));

                SetCDataValue(
                    root,
                    "InGameDescription",
                    NormalizeNewLines(
                        _inGameDescriptionField.value));

                ModSide modSide =
                    _modSideField.value is ModSide value
                        ? value
                        : ModSide.Unknown;

                SetValue(
                    root,
                    "ModSide",
                    modSide.ToString());

                // Optional.
                // Empty value removes the element entirely.
                SetOptionalValue(
                    root,
                    "WorkshopHandle",
                    _workshopHandleField.value);

                SetTags(root);

                SaveDocument();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                SetStatus(
                    "Saved About.xml and Project Settings.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);

                EditorUtility.DisplayDialog(
                    "Save Error",
                    exception.Message,
                    "OK");

                SetStatus(
                    "Save failed.");
            }
        }

        private void SaveDocument()
        {
            if (_document == null)
            {
                throw new InvalidOperationException(
                    "There is no XML document to save.");
            }

            var settings =
                new XmlWriterSettings
                {
                    Encoding =
                        new UTF8Encoding(false),

                    Indent = true,

                    IndentChars = "  ",

                    NewLineChars = "\n",

                    NewLineHandling =
                        NewLineHandling.Replace
                };

            using XmlWriter writer =
                XmlWriter.Create(
                    AbsoluteXmlPath,
                    settings);

            _document.Save(writer);
        }

        // =====================================================================
        // TAGS
        // =====================================================================

        private void SetTags(
            XElement root)
        {
            XElement tags =
                FindElement(
                    root,
                    "Tags");

            if (tags == null)
            {
                tags =
                    new XElement(
                        root.Name.Namespace + "Tags");

                root.Add(tags);
            }

            tags.RemoveNodes();

            string[] values =
                (_tagsField.value ?? string.Empty)
                .Split(
                    new[]
                    {
                    '\r',
                    '\n'
                    },
                    StringSplitOptions.RemoveEmptyEntries);

            foreach (string value in values)
            {
                string tag =
                    value.Trim();

                if (string.IsNullOrEmpty(tag))
                {
                    continue;
                }

                tags.Add(
                    new XElement(
                        root.Name.Namespace + "Tag",
                        tag));
            }
        }

        // =====================================================================
        // XML HELPERS
        // =====================================================================

        private static XElement FindElement(
            XElement root,
            string elementName)
        {
            return root
                .Elements()
                .FirstOrDefault(
                    element =>
                        element.Name.LocalName ==
                        elementName);
        }

        private static string GetValue(
            XElement root,
            string elementName)
        {
            XElement element =
                FindElement(
                    root,
                    elementName);

            return element?.Value ??
                   string.Empty;
        }

        private static void SetValue(
            XElement root,
            string elementName,
            string value)
        {
            XElement element =
                FindElement(
                    root,
                    elementName);

            if (element == null)
            {
                element =
                    new XElement(
                        root.Name.Namespace +
                        elementName);

                root.Add(element);
            }

            element.Value =
                value ?? string.Empty;
        }

        /// <summary>
        /// Writes an element only when the supplied value is non-empty.
        /// If the value is empty, whitespace, or null, an existing element
        /// is removed from the XML.
        /// </summary>
        private static void SetOptionalValue(
            XElement root,
            string elementName,
            string value)
        {
            XElement element =
                FindElement(
                    root,
                    elementName);

            string trimmed =
                value?.Trim() ??
                string.Empty;

            if (string.IsNullOrEmpty(trimmed))
            {
                element?.Remove();
                return;
            }

            if (element == null)
            {
                element =
                    new XElement(
                        root.Name.Namespace +
                        elementName);

                root.Add(element);
            }

            element.Value =
                trimmed;
        }

        private static void SetCDataValue(
            XElement root,
            string elementName,
            string value)
        {
            XElement element =
                FindElement(
                    root,
                    elementName);

            if (element == null)
            {
                element =
                    new XElement(
                        root.Name.Namespace +
                        elementName);

                root.Add(element);
            }

            element.RemoveNodes();

            element.Add(
                new XCData(
                    value ?? string.Empty));
        }

        private static string NormalizeNewLines(
            string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\r\n", "\n")
                .Replace("\r", "\n");
        }

        // =====================================================================
        // UI HELPERS
        // =====================================================================

        private static TextField CreateTextField(
            string label)
        {
            var field =
                new TextField(label);

            field.style.marginBottom = 5;

            return field;
        }

        private static TextField CreateMultilineField(
            string label,
            float minHeight)
        {
            var field =
                new TextField(label)
                {
                    multiline = true
                };

            field.style.marginTop = 5;
            field.style.marginBottom = 10;

            VisualElement input =
                field.Q("unity-text-input");

            if (input != null)
            {
                input.style.minHeight =
                    minHeight;

                input.style.whiteSpace =
                    WhiteSpace.Normal;
            }

            return field;
        }

        private static void AddSeparator(
            VisualElement parent)
        {
            var separator =
                new VisualElement();

            separator.style.height = 1;

            separator.style.backgroundColor =
                new Color(
                    0.25f,
                    0.25f,
                    0.25f);

            separator.style.marginTop = 8;
            separator.style.marginBottom = 8;

            parent.Add(separator);
        }

        private void SetStatus(
            string message)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text =
                    message;
            }
        }
    }
}