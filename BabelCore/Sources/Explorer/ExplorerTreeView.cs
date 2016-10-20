using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Generic;

namespace Babel.Core {
    /// <summary>
    /// This control displays a file system tree.
    /// </summary>
    public class ExplorerTreeView : TreeView {

        static bool EditingEnabled = true;
        static bool UnloadItemsOnCollapse = true;

            public delegate void RenameListenerDelegate(TreeViewItem selectedItem);


        /// <summary>
        /// This event is raised if error occurs while creating file system tree.
        /// </summary>
        public event EventHandler<ExplorerErrorEventArgs> ExplorerError;
        protected List<RenameListenerDelegate> RenameListeners;

        /// <summary>
        /// Invocator for <see cref="ExplorerError"/> event.
        /// </summary>
        /// <param name="e"></param>
        private void InvokeExplorerError(ExplorerErrorEventArgs e) {
            EventHandler<ExplorerErrorEventArgs> handler = ExplorerError;
            if (handler != null) handler(this, e);
        }

        public ExplorerTreeView() {
            RenameListeners = new List<RenameListenerDelegate>();
            AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(OnItemExpanded));
            AddHandler(TreeViewItem.CollapsedEvent, new RoutedEventHandler(OnItemCollapsed));
            InitExplorer();
        }

        public void AddRenameListener(RenameListenerDelegate d) {
            RenameListeners.Add(d);
        }

        protected void NotifyRenameListeners(TreeViewItem selectedItem) {
            foreach (RenameListenerDelegate d in RenameListeners) {
                d(selectedItem);
            }
        }

        private static childItem FindVisualChild<childItem>(DependencyObject obj)
            where childItem : DependencyObject {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++) {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is childItem)
                    return (childItem)child;
                else {
                    childItem childOfChild = FindVisualChild<childItem>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }

        public static DependencyObject FindVisualParent(DependencyObject obj, Type type) {
            DependencyObject o = obj;
            while (o != null && type != o.GetType()) {
                o = VisualTreeHelper.GetParent(o);
            }
            return o;
        }

        public static TreeView GetTree(TreeViewItem t) {
            while (t.Parent is TreeViewItem) t = t.Parent as TreeViewItem;
            return ItemsControl.ItemsControlFromItemContainer(t) as TreeView;
        }

        private static string Rename(TreeViewItem selectedItem,string oldName, string newName) {
            if (String.IsNullOrWhiteSpace(oldName) || String.IsNullOrWhiteSpace(newName)) return null;
            if (oldName.Equals(newName)) return null;
            ExplorerTreeView t = GetTree(selectedItem) as ExplorerTreeView;
            string newPath = null;
            if (t != null) {
                string filePath = t.GetSelectedPath();
                if (String.IsNullOrWhiteSpace(filePath)) return null;
                try {
                    string basePath = Path.GetDirectoryName(filePath);
                    string curName = Path.GetFileName(filePath);
                    if (!oldName.Equals(curName)) return null;
                    newPath = Path.Combine(basePath, newName);
                    Directory.Move(filePath, newPath);
                } catch (Exception) {
                    return null;
                }
            }
            return newPath;
        }

        private void OnMouseDoubleClick(object sender, RoutedEventArgs e) {
            if (EditingEnabled) {
                if (FindVisualParent((DependencyObject)e.OriginalSource, e.Source.GetType()) == e.Source) {
                    e.Handled = true;
                    TreeViewItem selectedItem = sender as TreeViewItem;
                    ContentPresenter myContentPresenter = FindVisualChild<ContentPresenter>(selectedItem);
                    DataTemplate myDataTemplate = myContentPresenter.ContentTemplate;
                    TextBlock tb = (TextBlock)myDataTemplate.FindName("tb", myContentPresenter);
                    tb.Visibility = Visibility.Collapsed;
                    TextBox etb = (TextBox)myDataTemplate.FindName("etb", myContentPresenter);
                    etb.Text = tb.Text;
                    etb.Visibility = Visibility.Visible;
                    etb.Focus();
                    etb.LostFocus += (o, f) => {
                        string newPath = Rename(selectedItem, tb.Text, etb.Text);
                        if (newPath!=null) {
                            tb.Text = etb.Text;
                            InitExplorer();
                            SetSelectedPath(newPath);
                            NotifyRenameListeners(selectedItem);
                        }
                        etb.Visibility = Visibility.Collapsed;
                        tb.Visibility = Visibility.Visible;
                    };
                }
            }
        }

        private static void OnMouseRightButtonDown(object sender, RoutedEventArgs e) {
            ((TreeViewItem)sender).IsSelected = true;
            e.Handled = true;
        }

        /// <summary>
        /// Generates <see cref="TreeViewItem"/> for directory info.
        /// </summary>
        /// <param name="directory"></param>
        /// <returns></returns>
        private TreeViewItem GenerateDirectoryNode(DirectoryInfo directory) {
            var item = new TreeViewItem {
                Tag = directory,
                Header = directory.Name
            };
            item.Items.Add("*");
            item.MouseRightButtonDown += OnMouseRightButtonDown;
            item.MouseDoubleClick += OnMouseDoubleClick;
            return item;
        }

        private TreeViewItem GenerateFileNode(FileInfo file) {
            var item = new TreeViewItem {
                Tag = file,
                Header = file.Name
            };
            item.Items.Add("*");
            item.MouseRightButtonDown += OnMouseRightButtonDown;
            item.MouseDoubleClick += OnMouseDoubleClick;
            return item;
        }

        /// <summary>
        /// Generates <see cref="TreeViewItem"/> for drive info.
        /// </summary>
        /// <param name="drive"></param>
        /// <returns></returns>
        private TreeViewItem GenerateDriveNode(DriveInfo drive) {
            var item = new TreeViewItem {
                Tag = drive,
                Header = drive.ToString()
            };
            item.Items.Add("*");
            item.MouseRightButtonDown += OnMouseRightButtonDown;
            return item;
        }

        /// <summary>
        /// Populates tree with initial drive nodes. 
        /// </summary>
        public void InitExplorer() {
            while (Items.Count > 0) {
                Items.RemoveAt(0);
            }
            foreach (var drive in DriveInfo.GetDrives()) {
                Items.Add(GenerateDriveNode(drive));
            }
        }

        private void ExpandItem(TreeViewItem item) {
            item.Items.Clear();
            DirectoryInfo dir;
            if (item.Tag is DriveInfo) {
                var drive = (DriveInfo)item.Tag;
                dir = drive.RootDirectory;
            } else if (item.Tag is DirectoryInfo) {
                dir = (DirectoryInfo)item.Tag;
            } else return;
            try {
                foreach (var subDir in dir.GetDirectories()) {
                    item.Items.Add(GenerateDirectoryNode(subDir));
                }
                foreach (var subFile in dir.GetFiles()) {
                    item.Items.Add(GenerateFileNode(subFile));
                }
            } catch (Exception ex) {
                InvokeExplorerError(new ExplorerErrorEventArgs(ex));
            }
        }

        /// <summary>
        /// Occurs when tree node is expanded.
        /// Reloads node sub-folders, if required.
        /// May raise <see cref="ExplorerError"/> on some IO exceptions.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnItemExpanded(object sender, RoutedEventArgs e) {
            var item = (TreeViewItem)e.OriginalSource;
            if (UnloadItemsOnCollapse || !HasSubFolders(item)) 
                ExpandItem(item);
        }

        private void CollapseItem(TreeViewItem item) {
            item.Items.Clear();
            item.Items.Add("*");
        }

        /// <summary>
        /// Occurs when tree node is collapsed.
        /// Unloads node sub-folders, if UnloadItemsOnCollapse is set to True.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnItemCollapsed(object sender, RoutedEventArgs e) {
            if (UnloadItemsOnCollapse) {
                var item = (TreeViewItem)e.OriginalSource;
                CollapseItem(item);
            }
        }

        /// <summary>
        /// Checks whether specified <see cref="TreeViewItem"/> has any real sub-folder nodes.
        /// </summary>
        /// <param name="item">Node to check.</param>
        /// <returns></returns>
        private static bool HasSubFolders(TreeViewItem item) {
            if (item.Items.Count == 0) {
                return false;
            }
            var firstItem = item.Items[0] as TreeViewItem;
            return firstItem != null;
        }

        /// <summary>
        /// Compares old <see cref="SelectedPath"/> value with the specified one 
        /// and desides whether tree view selection has to be updated or not, if you apply this new value.
        /// </summary>
        /// <param name="newPath">New <see cref="SelectedPath"/> value.</param>
        /// <returns></returns>
        private bool IsSelectionUpdateRequired(String newPath) {
            if (String.IsNullOrWhiteSpace(newPath)) {
                return true;
            }
            var selectedPath = GetSelectedPath();
            if (String.IsNullOrWhiteSpace(selectedPath)) {
                return true;
            }
            return !Path.GetFullPath(newPath).Equals(Path.GetFullPath(selectedPath));
        }

        public void SetSelectedPath(String desiredPath) {
            if (String.IsNullOrWhiteSpace(desiredPath)) return;
            string[] split = Path.GetFullPath(desiredPath).Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },StringSplitOptions.RemoveEmptyEntries);
            string driveName = new DriveInfo(split[0]).Name.ToLower();
            TreeViewItem curItem = null;
            foreach (TreeViewItem driveItem in Items) {
                DriveInfo info = (DriveInfo)driveItem.Tag;
                if (driveName == info.Name.ToLower()) {
                    curItem = driveItem;
                    break;
                }
            }
            if(curItem==null) return;
            if(!curItem.IsExpanded)
                curItem.IsExpanded = true;
            int index=1;
            while(index<split.Length) {
                string name = split[index++].ToLower();
                TreeViewItem nextItem = null;
                foreach (TreeViewItem item in curItem.Items) {
                    FileSystemInfo info = (FileSystemInfo)item.Tag;
                    if (info.Name.ToLower().Equals(name)) {
                        nextItem = item;
                        break;
                    }
                }
                if(nextItem==null) break;
                if (index == split.Length) {
                    nextItem.IsSelected = true;
                    break;
                }
                if (!(nextItem.Tag is DirectoryInfo)) break;
                if (!nextItem.IsExpanded) {
                    nextItem.IsExpanded = true;
                }
                curItem = nextItem;
            }
        }

        public string GetItemPath(TreeViewItem item) {
            if (item == null) return null;
            if (item.Tag is DriveInfo) return ((DriveInfo)item.Tag).RootDirectory.FullName;
            if (item.Tag is DirectoryInfo) return ((DirectoryInfo)item.Tag).FullName;
            if (item.Tag is FileInfo) return ((FileInfo)item.Tag).FullName;
            return null;
        }

        /// <summary>
        /// Returns full path of the selected node.
        /// </summary>
        /// <returns></returns>
        public String GetSelectedPath() {
            return GetItemPath(SelectedItem as TreeViewItem);
        }

        public void InsertDirectory(DirectoryInfo info) {
            TreeViewItem item = SelectedItem as TreeViewItem;
            if (item == null) return;
            if (item.Tag is FileInfo) {
                item = item.Parent as TreeViewItem;
            }
            TreeViewItem a = GenerateDirectoryNode(info);
            item.Items.Add(a);
            a.IsSelected = true;
            a.Focus();
        }

        public void InsertFile(FileInfo info) {
            TreeViewItem item = SelectedItem as TreeViewItem;
            if (item == null) return;
            if (item.Tag is FileInfo) {
                item = item.Parent as TreeViewItem;
            }
            TreeViewItem a = GenerateFileNode(info);
            item.Items.Add(a);
            a.IsSelected = true;
            a.Focus();
        }

        public void DeleteSelected() {
            TreeViewItem item = SelectedItem as TreeViewItem;
            if (item == null) return;
            if (item.Tag is DirectoryInfo || item.Tag is FileInfo) {
                ItemCollection items;
                TreeViewItem parent = item.Parent as TreeViewItem;
                if (parent != null) {
                    items = parent.Items;
                    int index = items.IndexOf(item);
                    items.Remove(item);
                    if (index > 0 || items.Count > 0) {
                        if (index > 0) --index;
                        parent = items.GetItemAt(index) as TreeViewItem;
                    }
                    if (parent != null) {
                        parent.IsSelected = true;
                        parent.Focus();
                    }
                }
            }
        }
    }
}