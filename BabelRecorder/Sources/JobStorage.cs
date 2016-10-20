using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using System.Xml;
using System.Windows.Controls;

using Babel.Resources;
using Babel.Core;
using Babel.BabelProtocol;

namespace Babel.Recorder {

    public class JobStorage {

        public const string TAG = "JobStorage";

        public const string RecordingFileExt = ".rec";

        RecorderControl Recorder;
        public string CurrentFilePath;
        private TextBox CurrentPath;
        private ExplorerTreeView Explorer;
        private AxesController Controller;

        public JobStorage(RecorderControl recorder) {
            Recorder = recorder;
            CurrentFilePath = Project.GetProjectDir();
        }

        public MenuItem GetContextMenuItem(string contextName, string itemName) {
            System.Windows.Controls.ContextMenu cm = Explorer.Resources[contextName] as System.Windows.Controls.ContextMenu;
            return LogicalTreeHelper.FindLogicalNode(cm, itemName) as MenuItem;
        }

        public void InitializeControls() {
            CurrentPath = Recorder.CurrentPath;
            CurrentPath.TextChanged += OnCurrentPathChanged;
            Controller = Recorder.Controller;
            Explorer = Recorder.Explorer;
            Explorer.SelectedItemChanged += OnSelectedItemChanged;
            Explorer.ExplorerError += OnExplorerError;
            Explorer.AddRenameListener(RenameListener);
            Recorder.Loaded += OnLoaded;

            Recorder.ReadRecordingButton.Click += new RoutedEventHandler(ReadRecording_Click);
            Recorder.WriteRecordingButton.Click += new RoutedEventHandler(WriteRecording_Click);

            GetContextMenuItem("FileContext","LoadRecordingItem").Click += new RoutedEventHandler(LoadRecording_Click);
            GetContextMenuItem("FileContext","SaveRecordingItem").Click += new RoutedEventHandler(SaveRecording_Click);
            GetContextMenuItem("FileContext","NewRecordingItem").Click += new RoutedEventHandler(NewRecording_Click);
            GetContextMenuItem("FileContext","DeleteFileButton").Click += new RoutedEventHandler(DeleteFile_Click);
            GetContextMenuItem("FolderContext","NewRecordingFolderButton").Click += new RoutedEventHandler(NewRecording_Click);
            GetContextMenuItem("FolderContext","NewFolderButton").Click += new RoutedEventHandler(NewFolder_Click);
            GetContextMenuItem("FolderContext","DeleteFolderButton").Click += new RoutedEventHandler(DeleteFolder_Click);
        }

        public void SerializeJobStorage(XmlNode node, bool isSerialize) {
            XmlNode job = Project.GetChildNode(node, "jobstorage");
            if (job == null) return;
            if (isSerialize) {
                CurrentFilePath = Explorer.GetSelectedPath();
                CurrentPath.Text = CurrentFilePath;
                string relPath = Settings.GetRelativePath(Project.GetProjectDir(), CurrentFilePath);
                Project.SetNodeAttributeValue(job, "path", relPath);
            } else {
                string filePath = Project.GetNodeAttributeValue(job, "path", "");
                SetSelectedPath(Settings.GetAbsolutePath(Project.GetProjectDir(), filePath));
            }
        }

        public void SetSelectedPath(string filePath) {
            if (String.IsNullOrWhiteSpace(filePath)) filePath = "";
            CurrentFilePath = filePath;
            CurrentPath.Text = CurrentFilePath;
            Explorer.SetSelectedPath(CurrentFilePath);
        }

        public void RenameListener(TreeViewItem selectedItem) {
            if (selectedItem != null) {
                CurrentFilePath = Explorer.GetSelectedPath();
                CurrentPath.Text = CurrentFilePath;
            }
        }

        private void SetTitle() {
            string name = Path.GetFileNameWithoutExtension(CurrentFilePath);
            Shell s = Shell.GetShell(Recorder.ShellId);
            if (s != null) {
                s.Title = "Recorder/" + name;
                MainWindow.RefreshTitle();
            }
        }

        public void ClearTitle() {
            Shell s = Shell.GetShell(Recorder.ShellId);
            if (s != null) {
                s.Title = "Recorder";
                MainWindow.RefreshTitle();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            CurrentPath.Text = CurrentFilePath;
            SetSelectedPath(CurrentFilePath);
        }

        private void OnExplorerError(object sender, ExplorerErrorEventArgs e) {
            Settings.BugReport(e.Exception.Message);
        }

        private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
            CurrentFilePath = Explorer.GetSelectedPath();
            CurrentPath.Text = CurrentFilePath;
            TreeViewItem selectedItem = Explorer.SelectedItem as TreeViewItem;
            if (selectedItem != null && selectedItem.Tag is FileSystemInfo) {
                FileSystemInfo info = selectedItem.Tag as FileSystemInfo;
                if ((info.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                    Explorer.ContextMenu = Explorer.Resources["FolderContext"] as System.Windows.Controls.ContextMenu;
                else
                    Explorer.ContextMenu = Explorer.Resources["FileContext"] as System.Windows.Controls.ContextMenu;
            }
        }

        private void OnCurrentPathChanged(object sender, TextChangedEventArgs args) {
            // User must press Read/Write/New for title to change.
            ClearTitle();
        }

        private void ReadRecording_Click(object sender, RoutedEventArgs e) {
            CurrentFilePath = CurrentPath.Text;
            SetSelectedPath(CurrentFilePath);
            LoadRecording_Click(sender, e);
        }
        private void WriteRecording_Click(object sender, RoutedEventArgs e) {
            CurrentFilePath = CurrentPath.Text;
            SetSelectedPath(CurrentFilePath);
            SaveRecording_Click(sender,e);
        }

        public string LoadRecording() {
            if (String.IsNullOrWhiteSpace(CurrentFilePath)) {
                return "Error:First select a file name from JobStorage.";
            }
            Recorder.Controller.PVTCache.Reset(); // TODO: Future: could append recordings.
            string header = "";
            string s = Controller.PVTCache.LoadFromFile(CurrentFilePath, out header);
            if (!s.StartsWith("Error:")) {              
                List<AxisProperties> apAry = AxisProperties.ConvertToProperties(header);
                if (!AxisProperties.WeakPartialCompareProperties(apAry, Recorder.AxisOptions)) {
                    s="Warning: read file " 
                        + CurrentFilePath
                        + "\nIs not compatible with current axis options.";
                } else {
                    s="Read:" + CurrentFilePath;
                }
                SetTitle();
            }
            return s;
        }

        private void LoadRecording_Click(object sender, RoutedEventArgs e) {
            string s="";
            if (Recorder.RecorderMode == RecorderModeKinds.RecorderModeIdle) {
                s = LoadRecording();           
            } else {
                s = "Error: Must be in Idle mode to read.";
            }
            if (s.StartsWith("Error:")||s.StartsWith("Warning:")) {
                Settings.BugReport(s);
            }
        }

        private void SaveRecording_Click(object sender, RoutedEventArgs e) {
            if (String.IsNullOrWhiteSpace(CurrentFilePath)) {
                Settings.BugReport("Select a file name first from JobStorage.");
                return;
            }
            if ((Recorder.RecorderMode == RecorderModeKinds.RecorderModeIdle)
                || (Recorder.RecorderMode == RecorderModeKinds.RecorderModeIdleStopped)
                || (Recorder.RecorderMode == RecorderModeKinds.RecorderModeRecordStopped)) {
                if (Controller.PVTCache != null) {
                    string header = AxisProperties.ConvertToHeader(Recorder.AxisOptions);
                    string s = Controller.PVTCache.DumpToFile(CurrentFilePath, true, header);
                    if (s.StartsWith("Error:")) {
                        Settings.BugReport(s);
                    } else {
                        Settings.BugReport("Saved:" + CurrentFilePath);
                    }
                }
            } else {
                Settings.BugReport("Must be in Idle or Stopped mode to write.");
            }
        }

        private void NewRecording_Click(object sender, RoutedEventArgs e) {
            if (Recorder.RecorderMode == RecorderModeKinds.RecorderModeIdle) {
                string filePath = CurrentFilePath;
                bool isDir = Directory.Exists(CurrentFilePath);
                if (isDir || File.Exists(filePath)) {
                    try {
                        string parentDir = isDir ? CurrentFilePath : Path.GetDirectoryName(filePath);
                        string name = Scripts.GenerateNewFileName("Recording", RecordingFileExt, parentDir);
                        filePath = Path.Combine(parentDir, name + RecordingFileExt);
                    } catch (Exception) {
                    }
                    try {
                        FileInfo info = new FileInfo(filePath);
                        using (StreamWriter sw = info.CreateText()) {
                            Explorer.InsertFile(info);
                            SetSelectedPath(filePath);
                            SetTitle();
                        }
                    } catch (Exception) {
                    }
                }
            } else {
                Settings.BugReport("Must be in Idle mode to create new recording.");
            }
        }

        private void DeleteFile_Click(object sender, RoutedEventArgs e) {
            try {
                File.Delete(CurrentFilePath);
                Explorer.DeleteSelected();
                CurrentFilePath = Explorer.GetSelectedPath();
                CurrentPath.Text = CurrentFilePath;
            } catch (Exception) {
            }
        }

        private void NewFolder_Click(object sender, RoutedEventArgs e) {
            try {
                string name = Settings.GenerateNewFolderName("Folder", CurrentFilePath);
                string folder = Path.Combine(CurrentFilePath, name);
                DirectoryInfo info = Directory.CreateDirectory(folder);
                Explorer.InsertDirectory(info);
                SetSelectedPath(folder);
            } catch (Exception) {
            }
        }

        private void DeleteFolder_Click(object sender, RoutedEventArgs e) {
            try {
                Directory.Delete(CurrentFilePath, true);
                Explorer.DeleteSelected();
                CurrentFilePath = Explorer.GetSelectedPath();
                CurrentPath.Text = CurrentFilePath;
            } catch (Exception) {
            }
        }

    }
}
