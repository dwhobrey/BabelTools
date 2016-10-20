using System;
using System.IO;
using System.Windows;
using System.Xml;
using System.Windows.Controls;
using Babel.Resources;

namespace Babel.Core {

    public partial class ExplorerControl : ShellControl {

        string currentFilePath;

        static ExplorerControl() {
  //          DefaultStyleKeyProperty.OverrideMetadata(typeof(ExplorerControl),new FrameworkPropertyMetadata(typeof(ExplorerControl)));
        }

        public ExplorerControl() {
        }

        public ExplorerControl(string ownerShellId, string filePath)
            : base(ownerShellId) {
            InitializeComponent();
            currentFilePath = filePath;
            Loaded += OnLoaded;
            Explorer.SelectedItemChanged += OnSelectedItemChanged;
        }

        public ExplorerControl(string ownerShellId, XmlNode node)
            : base(ownerShellId) {
            InitializeComponent();
            Serializer(node, false);
            Loaded += OnLoaded;
            Explorer.SelectedItemChanged += OnSelectedItemChanged;
        }

        public override void Init() {
            base.Init();
            if (currentFilePath != null)
                Dispatcher.Invoke((Action)(() => { SetSelectedPath(currentFilePath); }));
        }

        public override void Close() {
            base.Close();
        }

        public void SetSelectedPath(string filePath) {
            if (String.IsNullOrWhiteSpace(filePath)) filePath = "";
            currentFilePath = filePath;
            CurrentPath.Text = currentFilePath;
            Explorer.SetSelectedPath(currentFilePath);
        }

        public override string KindName { get { return "explorer"; } }

        // On entry, node is the shell node.
        public override void Serializer(XmlNode node, bool isSerialize) {
            base.Serializer(node, isSerialize);
            XmlNode c = Project.GetChildNode(node, "explore");
            if (c == null) return;
            if (isSerialize) {
                currentFilePath = Explorer.GetSelectedPath();
                CurrentPath.Text = currentFilePath;
                Project.SetNodeAttributeValue(c, "currentfilepath", Settings.GetRelativePath(Project.GetProjectDir(), currentFilePath));
            } else {
                string filePath = Project.GetNodeAttributeValue(c, "currentfilepath","");
                SetSelectedPath(Settings.GetAbsolutePath(Project.GetProjectDir(), filePath));
            }
        }

        private void OnSelectedItemChanged(object sender, RoutedEventArgs e) {
            currentFilePath = Explorer.GetSelectedPath();
            CurrentPath.Text = currentFilePath;
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            CurrentPath.Text = currentFilePath;
            Explorer.SetSelectedPath(currentFilePath);
        }

        private void Explorer_ExplorerError(object sender, ExplorerErrorEventArgs e) {
            MessageDialog.Show(e.Exception.Message);
        }

        private void Update_Click(object sender, RoutedEventArgs e) {
            currentFilePath = CurrentPath.Text;
            Explorer.SetSelectedPath(currentFilePath);
        }

        private void Explorer_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
            TreeViewItem selectedItem = Explorer.SelectedItem as TreeViewItem;
            if (selectedItem != null && selectedItem.Tag is FileSystemInfo) {
                FileSystemInfo info = selectedItem.Tag as FileSystemInfo;
                if ((info.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                    Explorer.ContextMenu = Explorer.Resources["FolderContext"] as System.Windows.Controls.ContextMenu;
                else
                    Explorer.ContextMenu = Explorer.Resources["FileContext"] as System.Windows.Controls.ContextMenu;
            }
        }

        private void EditFile_Click(object sender, RoutedEventArgs e) {
            EditorControl.OpenEditorShell(currentFilePath);
        }

        private void NewFile_Click(object sender, RoutedEventArgs e) {
            string filePath = currentFilePath;
            bool isDir = Directory.Exists(currentFilePath);
            if (isDir||File.Exists(filePath)) {
                try {
                    string parentDir = isDir ? currentFilePath : Path.GetDirectoryName(filePath);
                    string name = Scripts.GenerateNewFileName("Script", Project.ScriptExt, parentDir);
                    filePath = Path.Combine(parentDir, name + Project.ScriptExt);
                } catch (Exception) {
                }
                try {
                    FileInfo info = new FileInfo(filePath);
                    using (StreamWriter sw = info.CreateText()) {
                        Explorer.InsertFile(info);
                        SetSelectedPath(filePath);
                    }
                } catch (Exception) {
                }
            }
        }

        private void DeleteFile_Click(object sender, RoutedEventArgs e) {
            try {
                File.Delete(currentFilePath);
                Explorer.DeleteSelected();
                currentFilePath = Explorer.GetSelectedPath();
                CurrentPath.Text = currentFilePath;
            } catch (Exception) {
            }
        }

        private void NewFolder_Click(object sender, RoutedEventArgs e) {
            try {
                string name = Settings.GenerateNewFolderName("Folder",currentFilePath);
                string folder = Path.Combine(currentFilePath, name);
                DirectoryInfo info = Directory.CreateDirectory(folder);
                Explorer.InsertDirectory(info);
                SetSelectedPath(folder);
            } catch (Exception) {
            }
        }

        private void DeleteFolder_Click(object sender, RoutedEventArgs e) {
            try {
                Directory.Delete(currentFilePath,true);
                Explorer.DeleteSelected();
                currentFilePath = Explorer.GetSelectedPath();
                CurrentPath.Text = currentFilePath;
            } catch (Exception) {
            }
        }

        // On entry, node is the shell node.
        [ShellDeserializer("explorer")]
        public static ShellControl ExplorerShellDeserializer(string shellId, XmlNode node) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                return new ExplorerControl(shellId, node);
            }
            return (ShellControl)Application.Current.Dispatcher.Invoke(
                    new Func<ShellControl>(() => { return ExplorerShellDeserializer(shellId,node); })
                );
        }

        [ScriptFunction("explorer", "Opens file browser.",
            typeof(Jint.Delegates.Func<String, String>), "Optional directory.")]
        public static string OpenExplorerShell(string filePath = "") {
            if (Application.Current.Dispatcher.CheckAccess()) {
                Shell shell = Shell.OpenShell("Explorer", 300, 500);
                string shellId = shell.ShellId;
                if (shell.MainControl == null) {
                    if (String.IsNullOrWhiteSpace(filePath)) filePath = Project.GetProjectDir();
                    if (!shell.SetControl(new ExplorerControl(shellId, filePath))) {
                        return "Error: unable to create shell explorer control.";
                    }
                }
                return shellId;
            }
            return (string)Application.Current.Dispatcher.Invoke(
                    new Func<string>(() => { return OpenExplorerShell(filePath); })
                );
        }
    }
}

