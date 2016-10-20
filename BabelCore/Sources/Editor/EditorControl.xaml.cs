using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.Win32;
using Babel.Resources;

namespace Babel.Core {
    public partial class EditorControl : ShellControl {

        public static readonly char[] PathChars = { ':', '/', '\\' };

        string currentFileName;

        static EditorControl() {
       //     DefaultStyleKeyProperty.OverrideMetadata(typeof(EditorControl),new FrameworkPropertyMetadata(typeof(EditorControl)));
        }

        public EditorControl() {
        }

        EditorControl(string ownerShellId,string fileName)
            : base(ownerShellId) {
            InitializeComponent();
            currentFileName = fileName;
            propertyGridComboBox.SelectedIndex = 2;
        }

        EditorControl(string ownerShellId,XmlNode node)
            : base(ownerShellId) {
            InitializeComponent();
            Serializer(node, false);
            propertyGridComboBox.SelectedIndex = 2;
        }

        public override void Init() {
            base.Init();
            if(currentFileName!=null)
                Dispatcher.Invoke((Action)(() => { LoadFile(); }));
        }

        public override void Close() {
            base.Close();
            if (textEditor.IsModified) {
                SaveFile(false);
            }
        }

        public override string KindName { get { return "editor"; } }

        // On entry, node is the shell node.
        public override void Serializer(XmlNode node, bool isSerialize) {
            base.Serializer(node,isSerialize);
            XmlNode c = Project.GetChildNode(node, "edit");
            if (c == null) return;
            if(isSerialize) {
                Project.SetNodeAttributeValue(c, "currentfilename", Settings.GetRelativePath(Project.GetProjectDir(), currentFileName));
            } else {
                string fileName = Project.GetNodeAttributeValue(c, "currentfilename", null);
                currentFileName = Settings.GetAbsolutePath(Project.GetProjectDir(), fileName);
                LoadFile();
            }
        }

        public void LoadFile() {
            if (currentFileName != null && File.Exists(currentFileName))
                textEditor.Load(currentFileName);
            string ext = Path.GetExtension(currentFileName);
            if (ext == null || ext == Project.ScriptExt) ext = ".js";
            textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(ext);
        }

        public void SaveFile(bool isSaveAs) {
            if (currentFileName == null||isSaveAs) {
                currentFileName = Settings.SaveFileNameFromUser(currentFileName, Project.ScriptExt, "Script");
                if (currentFileName == null) return;
            }
            textEditor.Save(currentFileName);
            Shell.ShellTitle(OwnerShellId,Path.GetFileNameWithoutExtension(currentFileName));
        }

        void openFileClick(object sender, RoutedEventArgs e) {
            string fileName = Settings.GetFileNameFromUser(true, Project.ScriptExt, "Script");
            if (fileName!=null) {
                currentFileName = fileName;
                LoadFile();
            }
        }

        void saveFileClick(object sender, EventArgs e) {
            SaveFile(false);
        }
        void saveAsFileClick(object sender, EventArgs e) {
            SaveFile(true);
        }

        void propertyGridComboBoxSelectionChanged(object sender, RoutedEventArgs e) {
            if (propertyGrid == null)
                return;
            switch (propertyGridComboBox.SelectedIndex) {
                case 0:
                    propertyGrid.SelectedObject = textEditor;
                    break;
                case 1:
                    propertyGrid.SelectedObject = textEditor.TextArea;
                    break;
                case 2:
                    propertyGrid.SelectedObject = textEditor.Options;
                    break;
            }
        }

        // On entry, node is the shell node.
        [ShellDeserializer("editor")]
        public static ShellControl EditorShellDeserializer(string shellId, XmlNode node) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                return new EditorControl(shellId, node);
            }
            return (ShellControl)Application.Current.Dispatcher.Invoke(
                    new Func<ShellControl>(() => { return EditorShellDeserializer(shellId, node); })
                );
        }

        [ScriptFunction("edit", "Opens script file for editing.",
            typeof(Jint.Delegates.Func<String, String>), "Script or file name.")]
        public static string OpenEditorShell(string scriptName="") {
            if (Application.Current.Dispatcher.CheckAccess()) {
                if (String.IsNullOrWhiteSpace(scriptName))
                    scriptName = Scripts.GenerateNewFileName("Script",Project.ScriptExt,Project.GetProjectScriptDir());
                string fileName = Scripts.GetScriptFilePath(scriptName);
                if (fileName == null) {
                    if (scriptName.IndexOfAny(PathChars) < 0)
                        fileName = Path.Combine(Project.GetProjectScriptDir(), scriptName + Project.ScriptExt);
                    else {
                        fileName = scriptName;
                        if (Directory.Exists(fileName)) {
                            scriptName = Scripts.GenerateNewFileName("Script", Project.ScriptExt, fileName);
                            fileName = Path.Combine(fileName, scriptName + Project.ScriptExt);
                        }
                    }
                }
                Shell shell = Shell.OpenShell(scriptName, 400, 400);
                string shellId = shell.ShellId;
                if (shell.MainControl == null) {
                    // TODO: update script cache - rehash when editor closed.
                    if (!shell.SetControl(new EditorControl(shellId, fileName))) {
                        return "Error: unable to create shell editor control.";
                    }
                }
                return shellId;
            }
            return (string)Application.Current.Dispatcher.Invoke(
                    new Func<string>(() => { return OpenEditorShell(scriptName); })
                );
        }   
    }
}