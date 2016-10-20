using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;
using Babel.Resources;

namespace Babel.Core {

    public partial class MainWindow : Window {

        static int[] DefaultDisplay = { 100, 100, 350, 300 };

        string OriginalTitle;

        public static StatusInformationBar Status;

        MdiContainer ContainerWindow {
            get {
                return FindName("ContainerWindowId") as MdiContainer;
            }
        }

        void SetHeader(string header) {
            TextBlock tb = FindName("Header") as TextBlock;
            Title = header;
            tb.Text = header;
        }

        internal void SetTitle(string title) {
            // OriginalTitle = Settings.AppName + (String.IsNullOrWhiteSpace(title) ? "" : " : " + title);
            OriginalTitle = (String.IsNullOrWhiteSpace(title) ? Settings.AppName : title);
            SetHeader(OriginalTitle);
        }

        public static void Serializer(XmlNode node, bool isSerialize) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                MainWindow mw = (MainWindow)Application.Current.MainWindow;
                XmlNode c = Project.GetChildNode(node, "display");
                if (c == null) return;
                if (isSerialize) {
                    Project.SetNodeAttributeValue(c, "top", (int)mw.Top);
                    Project.SetNodeAttributeValue(c, "left", (int)mw.Left);
                    Project.SetNodeAttributeValue(c, "width", (int)mw.Width);
                    Project.SetNodeAttributeValue(c, "height", (int)mw.Height);
                } else {
                    mw.Top = Project.GetNodeAttributeValue(c, "top", DefaultDisplay[0]);
                    mw.Left = Project.GetNodeAttributeValue(c, "left", DefaultDisplay[1]);
                    mw.Width = Project.GetNodeAttributeValue(c, "width", DefaultDisplay[2]);
                    mw.Height = Project.GetNodeAttributeValue(c, "height", DefaultDisplay[3]);
                }
            } else {
                Application.Current.Dispatcher.Invoke((Action)(() => { Serializer(node, isSerialize); }));
            }
        }

        void SaveDisplayPosition() {
            int[] position = new int[4];
            position[0] = (int)Top;
            position[1] = (int)Left;
            position[2] = (int)Width;
            position[3] = (int)Height;
            Settings.SaveRegistryList("Display", position);
        }

        void SetDisplayPosition() {
            int[] d;
            List<string> a = Settings.GetRegistryList("Display");
            if (a.Count == 4) {
                d = new int[4];
                int k = 0;
                foreach (string s in a) {
                    int v;
                    try {
                        v = Convert.ToInt32(s);
                    } catch (Exception) {
                        v = DefaultDisplay[k];
                    }
                    if (v < 50 || v > 2000) v = 200;
                    d[k++] = v;
                }
            } else {
                d = DefaultDisplay;
            }
            Top = d[0];
            Left = d[1];
            Width = d[2];
            Height = d[3];
        }

        internal void RefreshRecentProjects() {
            MenuItem menu = FindName("RecentMenu") as MenuItem;
            menu.Items.Clear();
            MenuItem mi;
            List<string> p = Project.GetRecentProjects();
            foreach (string s in p) {
                mi = new MenuItem { Header = Path.GetFileNameWithoutExtension(s) };
                mi.Click += (o, e) => Recent_Click(o, e, s);
                menu.Items.Add(mi);
            }
        }

        internal void RefreshProjectKinds() {
            MenuItem menu = FindName("ProjectKindMenu") as MenuItem;
            menu.Items.Clear();
            MenuItem mi;
            List<string> p = Project.GetProjectKinds();
            foreach (string s in p) {
                mi = new MenuItem { Header = s };
                mi.Click += (o, e) => ProjectKind_Click(o, e, s);
                menu.Items.Add(mi);
            }
        }

        public MainWindow() {
            InitializeComponent();
            SourceInitialized += (s, a) => { // We need to initialize the following once main window is ready.
                SetTitle(null);
                SetDisplayPosition();
                RefreshRecentProjects();
                RefreshProjectKinds();
                OriginalTitle = Title;
                MdiContainer cw = ContainerWindow;
                cw.MdiChildTitleChanged += Container_MdiChildTitleChanged;
                DockPanel dp = FindName("ChildButtons") as DockPanel;
                dp.Children.Add(cw.GetChildButtons());
                Status = new StatusInformationBar(StatusBox);
                Modules.StartApp();
            };
        }

        public static void UpdateStatus(string key, string text) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                Status.UpdateStatus(key,text);
            } else {
                Application.Current.Dispatcher.Invoke((Action)(() => { UpdateStatus(key, text); }));
            }
        }

        void SetMdiTitle() {
            MdiContainer cw = ContainerWindow;
            string t = OriginalTitle;
            if (cw.ActiveMdiChild != null && cw.ActiveMdiChild.WindowState == WindowState.Maximized) {
                t += " - [" + cw.ActiveMdiChild.Title + "]";
            }
            SetHeader(t);
        }

        void Container_MdiChildTitleChanged(object sender, RoutedEventArgs e) {
            SetMdiTitle();
        }

        public static void RefreshTitle() {
            if (Application.Current.Dispatcher.CheckAccess()) {
                MainWindow mw = (MainWindow)Application.Current.MainWindow;
                mw.SetMdiTitle();
            } else {
                Application.Current.Dispatcher.Invoke((Action)(() => { RefreshTitle(); }));
            }
        }

        void On_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            DragMove();
        }

        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);
            SaveDisplayPosition();
            Project.SaveRecentProjects();
            Status.Close();
        }

        public static void CloseChild(MdiChild child) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                if (child != null) {
                    child.ForceClose();
                }
            } else {
                Application.Current.Dispatcher.Invoke((Action)(() => { CloseChild(child); }));
            }
        }

        public static void SetActiveChild(MdiChild child) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                if (child != null) {
                    MainWindow mw = (MainWindow)Application.Current.MainWindow;
                    mw.ContainerWindow.ActiveMdiChild = child;
                }
            } else {
                Application.Current.Dispatcher.Invoke((Action)(() => { SetActiveChild(child); }));
            }
        }

        public static void AddChild(MdiChild child, WindowState windowState) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                if (child != null) {
                    MainWindow mw = (MainWindow)Application.Current.MainWindow;
                    MdiChild curActive = mw.ContainerWindow.ActiveMdiChild;
                    mw.ContainerWindow.Children.Add(child);
                    if (curActive != null && windowState!=WindowState.Maximized) {
                        mw.ContainerWindow.ActiveMdiChild = curActive;
                    }                
                }
            } else {
                Application.Current.Dispatcher.Invoke((Action)(() => { AddChild(child,windowState); }));
            }
        }

        public static void CloseChildren(bool closeConsole) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                MainWindow mw = (MainWindow)Application.Current.MainWindow;
                if (mw != null) {
                    int k, n = mw.ContainerWindow.Children.Count;
                    while (((k = mw.ContainerWindow.Children.Count) != 0) && (n-- > 0)) {
                        try {
                            MdiChild c = mw.ContainerWindow.Children[0];
                            if (c != null) {
                                if (c.Name.Equals(Shell.ShellNamePrefix + Shell.ConsoleShellId)) {
                                    if (!closeConsole) {
                                        if (k < 2) return;
                                        c = mw.ContainerWindow.Children[1];
                                        if (c == null) continue;
                                    }
                                }
                                c.ForceClose();
                            }
                        } catch (Exception) {
                        }
                    }
                }
            } else {
                Application.Current.Dispatcher.Invoke((Action)(() => { CloseChildren(closeConsole); }));
            }
        }

        void CloseAll_Click(object sender, RoutedEventArgs e) {
            CloseChildren(false);
        }

        void Save_Project_Click(object sender, RoutedEventArgs e) {
            Modules.SaveProject();
        }

        void Close_Project_Click(object sender, RoutedEventArgs e) {
            Modules.SaveProject();
            Modules.CloseProject();
            SetTitle(null);
        }

        void Exit_Click(object sender, RoutedEventArgs e) {
            Modules.EndApp();
            Close();
        }

        void Open_Project_Click(object sender, RoutedEventArgs e) {
            Modules.OpenProject(null, null, false);
        }

        void ProjectKind_Click(object sender, RoutedEventArgs e, string projectKind) {
            // TODO: set project kind.
            Modules.OpenProject(null, projectKind, true);
        }

        void Recent_Click(object sender, RoutedEventArgs e, string fileName) {
            Modules.OpenProject(fileName, null, false);
        }

        void Minimize_Click(object sender, RoutedEventArgs e) {
            this.WindowState = WindowState.Minimized;
        }
        void Maximize_Click(object sender, RoutedEventArgs e) {
            this.WindowState = (this.WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }
        void Cascade_Click(object sender, RoutedEventArgs e) {
            ContainerWindow.MdiLayout = MdiLayout.Cascade;
        }
        void Horizontally_Click(object sender, RoutedEventArgs e) {
            ContainerWindow.MdiLayout = MdiLayout.TileHorizontal;
        }
        void Vertically_Click(object sender, RoutedEventArgs e) {
            ContainerWindow.MdiLayout = MdiLayout.TileVertical;
        }
        public static void ChangeLayout(MdiLayout layoutPattern) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                MainWindow mw = (MainWindow)Application.Current.MainWindow;
                mw.ContainerWindow.MdiLayout = layoutPattern;
            } else {
                Application.Current.Dispatcher.Invoke((Action)(() => { ChangeLayout(layoutPattern); }));
            }
        }

        void About_Click(object sender, RoutedEventArgs e) {
            AboutBox a = new AboutBox(Assembly.GetEntryAssembly());
            a.Show();
        }
        void License_Click(object sender, RoutedEventArgs e) {
            LicenseBox a = new LicenseBox();
            a.Show();
        }
        void Guide_Click(object sender, RoutedEventArgs e) {
            Help h = new Help();
            h.Show();
        }
    }
}