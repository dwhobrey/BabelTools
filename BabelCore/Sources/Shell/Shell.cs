using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using Jint.Native;
using Babel.Resources;

namespace Babel.Core {

    // When creating custom shell types from a xml configuration node.
    public delegate ShellControl ShellDeserializerDelegate(string shellId, XmlNode node);

    // Used for indicating custom shell deserializers during assembly search.
    public class ShellDeserializerAttribute : Attribute {
        public string Name;
        public ShellDeserializerAttribute(string shellKindName) {
            Name = shellKindName;
        }
    }

    public sealed class Shell {

        public static readonly string ShellNamePrefix = "Shell";
        public static readonly string CurrentShellId = "0";
        public static readonly string ConsoleShellId = "1";
        public static readonly string LogShellId = "2";
        public static readonly int NumReservedShellIds = 2;

        static BitmapImage ShellIcon = new BitmapImage(
            new Uri("pack://application:,,,/BabelResources;component/images/babelfish32.png", UriKind.Absolute));

        static SortedDictionary<string, ShellDeserializerDelegate> ShellDeserializers = new SortedDictionary<string, ShellDeserializerDelegate>();

        static Hashtable Shells = Hashtable.Synchronized(new Hashtable());
        static int ShellXPos, ShellYPos;
        static int ShellIdCount;

        public string ShellId;
        public string ShellKind;
        public string WorkingDir;
        public MdiChild ShellWindow;
        public ShellControl MainControl;
        public InterruptSource Interrupter;

        private WindowState OpenWindowState;

        bool IsClosing;

        public static string GetNewShellId() {
            int n;
            string id = null;
            while (true) {
                try {
                    do {
                        n = ++ShellIdCount;
                        id = n.ToString();
                    } while (Shells.ContainsKey(id));
                    break;
                } catch (Exception) {
                }
            }
            return id;
        }

        // Search for shell either with ShellId or Title.
        public static Shell GetShell(string id) {
            if (String.IsNullOrWhiteSpace(id) || id.Equals(CurrentShellId))
                return ScriptEngine.GetCurrentShell();
            Shell s = null;
            while (true) {
                try {
                    s = (Shell)(Shells[id]);
                    if (s != null) break;
                    foreach (Shell t in Shells.Values) {
                        if (id.Equals(t.Title)) {
                            return t;
                        }
                    }
                    break;
                } catch (Exception) {
                }
            }
            return s;
        }

        static void RemoveShell(Shell shell) {
            if ((shell != null) && !String.IsNullOrWhiteSpace(shell.ShellId)) {
                try {
                    if (Shells.ContainsKey(shell.ShellId)) {
                        Shells.Remove(shell.ShellId);
                    }
                } catch (Exception) {
                }
            }
        }

        static void AddShell(Shell shell) {
            if ((shell != null) && !String.IsNullOrWhiteSpace(shell.ShellId)) {
                try {
                    if (Shells.ContainsKey(shell.ShellId)) {
                        Shell s = (Shell)Shells[shell.ShellId];
                        if (s.Equals(shell)) return;
                        s.Close();
                    }
                    Shells[shell.ShellId] = shell;
                } catch (Exception) {
                }
            }
        }

        public override string ToString() {
            try {
                if (MainControl != null) {
                    if (MainControl.Dispatcher.CheckAccess()) {
                        return ShellId + ",'" + ShellWindow.Title + "'";
                    }
                    return MainControl.Dispatcher.Invoke(new Func<string>(() => { return ToString(); }));
                }
            } catch (Exception) {
            }
            return "BabelFish";
        }

        Shell(string shellId, MdiChild shellWindow) {
            IsClosing = false;
            ShellId = shellId;
            ShellWindow = shellWindow;
            OpenWindowState = WindowState.Normal;
            MainControl = null;
            Interrupter = new InterruptSource();
        }

        public void Close() {
            if (IsClosing) return;
            IsClosing = true;
            Interrupter.GenerateInterrupt();
            Modules.CloseShell(ShellId);
            if (MainControl != null) {
                MainControl.Close();
                MainControl = null;
            }
            RemoveShell(this);
            MainWindow.CloseChild(ShellWindow);
            MainWindow.UpdateStatus("Shell#" + ShellId, null);
        }

        private void Shell_OnClose(object sender, RoutedEventArgs e) {
            Close();
        }

        void Clear() {
            if (MainControl.Dispatcher.CheckAccess()) {
                MainControl.Clear();
            } else {
                MainControl.Dispatcher.Invoke((Action)(() => { Clear(); }));
            }
        }

        public void SetActive() {
            if (MainControl.Dispatcher.CheckAccess()) {
                MainWindow.SetActiveChild(ShellWindow);
            } else {
                MainControl.Dispatcher.Invoke((Action)(() => { SetActive(); }));
            }
        }

        bool Sleep(int milliseconds) {
            bool wasCancelled = false;
            if (IsClosing) return true;
            try {
                wasCancelled = Interrupter.GetToken.WaitHandle.WaitOne(milliseconds);
            } catch (Exception) {
                wasCancelled = true;
            }
            return wasCancelled;
        }

        private void OnMdiChild_Loaded(object sender, RoutedEventArgs e) {
            if (OpenWindowState == WindowState.Maximized)
                ShellWindow.Focused = true;
            ShellWindow.WindowState = OpenWindowState;
        }

        public bool SetControl(ShellControl c) {
            if (c == null) {
                Close();
                return false;
            }
            MainControl = c;
            ShellWindow.Content = c;
            c.Init();
            return true;
        }

        public string Title {
            get {
                if (MainControl == null) return null;
                if (MainControl.Dispatcher.CheckAccess()) {
                    string s = ShellWindow.Title;
                    if (!String.IsNullOrWhiteSpace(s) && s.IndexOf(':') > 0) {
                        return s.Substring(1 + s.IndexOf(':')).Trim();
                    }
                    return s;
                }
                return MainControl.Dispatcher.Invoke(new Func<string>(() => { return Title; }));
            }
            set {
                ShellWindow.Title = "#" + ShellId + ":" + value;
            }
        }

        public static Shell OpenShell(string title,
            int width = 300, int height = 200, int x = -1, int y = -1, int windowState = 0,
            string shellId = "", string workingDir = "") {
            if (Application.Current.Dispatcher.CheckAccess()) {
                if (String.IsNullOrWhiteSpace(shellId)) shellId = GetNewShellId();
                else if (Shells.ContainsKey(shellId)) return GetShell(shellId);
                if (String.IsNullOrWhiteSpace(title)) title = "Shell" + shellId;
                bool isConsole = (shellId == ConsoleShellId);
                if (x < 0) {
                    ShellXPos = (ShellXPos + 10) % 150;
                    x = ShellXPos;
                }
                if (y < 0) {
                    ShellYPos = (ShellYPos + 10) % 100;
                    y = ShellYPos;
                    Shell c = GetShell(ConsoleShellId);
                    if (c != null) {
                        MdiChild m = c.ShellWindow;
                        y += (int)m.Height + (int)m.Position.Y;
                    }
                }
                if (x > 2000) x = ShellXPos;
                if (y > 2000) y = ShellYPos;
                if (width < 50 || width > 2000) width = 300;
                if (height < 50 || height > 2000) height = 200;
                MainWindow mw = (MainWindow)Application.Current.MainWindow;
                MdiChild shellWindow = new MdiChild {
                    Style = mw.Resources["MdiChildStyle"] as Style,
                    Name = ShellNamePrefix + shellId,
                    Icon = ShellIcon,
                    Width = (double)width,
                    Height = (double)height,
                    Position = new Point(x, y),
                    ShowClose = !isConsole,
                    ShowIcon = isConsole,
                    MinimizeBox = true,
                    MaximizeBox = true
                };
                Shell shell = new Shell(shellId, shellWindow);
                shell.Title = title;
                shell.WorkingDir = workingDir;
                shell.OpenWindowState = (windowState == 0) ? WindowState.Normal : ((windowState == 1) ? WindowState.Minimized : WindowState.Maximized);
                AddShell(shell);
                shellWindow.Closed += new RoutedEventHandler(shell.Shell_OnClose);
                shellWindow.Loaded += shell.OnMdiChild_Loaded;
                MainWindow.AddChild(shellWindow, shell.OpenWindowState);
                Modules.OpenShell(shellId);
                return shell;
            }
            return (Shell)Application.Current.Dispatcher.Invoke(
                    new Func<Shell>(() => { return OpenShell(title, width, height, x, y, windowState, shellId, workingDir); })
                );
        }

        // On serialize, node is the shell node.
        public void Serializer(XmlNode node) {
            if (node == null) return;
            Project.SetNodeAttributeValue(node, "name", ShellId);
            Project.SetNodeAttributeValue(node, "controlkind", MainControl.KindName);
            Project.SetNodeAttributeValue(node, "title", Title);
            Project.SetNodeAttributeValue(node, "width", (int)ShellWindow.Width);
            Project.SetNodeAttributeValue(node, "height", (int)ShellWindow.Height);
            Project.SetNodeAttributeValue(node, "x", (int)ShellWindow.Position.X);
            Project.SetNodeAttributeValue(node, "y", (int)ShellWindow.Position.Y);
            WindowState ws = ShellWindow.WindowState;
            int windowState = (ws == WindowState.Normal) ? 0 : ((ws == WindowState.Minimized) ? 1 : 2);
            Project.SetNodeAttributeValue(node, "windowstate", windowState);
            Project.SetNodeAttributeValue(node, "workingdir", WorkingDir);
        }

        public static Shell OpenShell(XmlNode node) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                if (node == null) return null;
                string id = Project.GetNodeAttributeValue(node, "name", "");
                string title = Project.GetNodeAttributeValue(node, "title", "");
                int width = Convert.ToInt32(Project.GetNodeAttributeValue(node, "width", "300"));
                int height = Convert.ToInt32(Project.GetNodeAttributeValue(node, "height", "200"));
                int x = Convert.ToInt32(Project.GetNodeAttributeValue(node, "x", "-1"));
                int y = Convert.ToInt32(Project.GetNodeAttributeValue(node, "y", "-1"));
                int windowState = Convert.ToInt32(Project.GetNodeAttributeValue(node, "windowstate", "0"));
                string workingDir = Project.GetNodeAttributeValue(node, "workingdir", Project.GetProjectDir());
                return Shell.OpenShell(title, width, height, x, y, windowState, id, workingDir);
            }
            return (Shell)Application.Current.Dispatcher.Invoke(
                    new Func<Shell>(() => { return OpenShell(node); })
                );
        }

        public static void SaveProject() {
            XmlNode parent = Project.GetNode(Project.ProjectRootNodeName + "/shells");
            if (parent != null) {
                parent.RemoveAll();
                try {
                    foreach (string id in Shells.Keys) {
                        Shell s = GetShell(id);
                        if (s != null) {
                            if (s.MainControl.Dispatcher.CheckAccess()) {
                                XmlNode node = Project.GetChildNode(parent, "shell", id);
                                s.Serializer(node);
                                s.MainControl.Serializer(node, true);
                            } else {
                                s.MainControl.Dispatcher.Invoke((Action)(() => { s.MainControl.Serializer(parent, true); }));
                            }
                        }
                    }
                } catch (Exception) {
                }
            }
        }

        public static void ConfigProject() {
            ShellXPos = 0;
            ShellYPos = 0;
            ShellIdCount = NumReservedShellIds;
            Shells.Clear();
        }

        public static void StartProject() {
            XmlNodeList nodeList = Project.GetNodes(Project.ProjectRootNodeName + "/shells/*");
            foreach (XmlNode p in nodeList) {
                Shell shell = OpenShell(p);
                if (shell.MainControl == null) {
                    string controlKind = Project.GetNodeAttributeValue(p, "controlkind", null);
                    ShellDeserializerDelegate d = GetShellDeserializer(controlKind);
                    if (d != null) {
                        if (!shell.SetControl(d(shell.ShellId, p))) {
                            // return "Error: unable to create shell control.";
                        }
                    }
                }
            }
            Shell s = GetShell(ConsoleShellId);
            if (s != null) s.SetActive();
        }

        public static void CloseProject() {
            MainWindow.CloseChildren(true);
        }

        public static string Write(string id, string text, bool onPromptLine, bool onNewLine) {
            if (!String.IsNullOrWhiteSpace(id)) {
                Shell s = GetShell(id);
                try {
                    if (s != null && s.MainControl != null) {
                        if (s.MainControl.Dispatcher.CheckAccess()) {
                            s.MainControl.Write(text, onPromptLine, onNewLine);
                            return "";
                        } else {
                            return (string)s.MainControl.Dispatcher.Invoke(new Func<string>(()
                                => { return Write(s.ShellId, text, onPromptLine, onNewLine); }));
                        }
                    }
                } catch (Exception) {
                    return "";
                }
            }
            return "Error: invalid shell id.";
        }

        public static string Write(string text) {
            return Write(ConsoleShellId, text, false, false);
        }
        public static string WriteLine(string text) {
            return Write(ConsoleShellId, text + "\n", false, false);
        }

        [ScriptFunction("write", "Writes text to shell.",
            typeof(Jint.Delegates.Func<String, String, String>),
            "Id of shell for output.", "Text string to display.")]
        public static string Write(string id, string text) {
            return Write(id, text, true, false);
        }

        [ScriptFunction("read", "Read text from shell.",
            typeof(Jint.Delegates.Func<String, String>),
            "Id of shell for input.")]
        public static string Read(string id) {
            return ""; //TODO: Read text from shell.
        }

        [ScriptFunction("cls", "Clear shell window.",
            typeof(Jint.Delegates.Func<String, String>),
            "Id of shell to clear, or '*' to clear all shells.")]
        public static string Clear(string id = "*") {
            if (String.IsNullOrWhiteSpace(id)) id = "*";
            if (id.Equals("*")) {
                try {
                    foreach (Shell s in Shells.Values) {
                        //  if (!s.ShellId.Equals(ConsoleShellId)) 
                        s.Clear();
                    }
                } catch (Exception) {
                }
            } else {
                Shell s = GetShell(id);
                if (s != null) s.Clear();
            }
            return "";
        }

        [ScriptFunction("exit", "Exit shell window.",
            typeof(Jint.Delegates.Func<String, String>),
            "Id of shell to exit, or '*' for all.")]
        public static string Exit(string id = "*") {
            if (String.IsNullOrWhiteSpace(id)) id = "*";
            if (id.Equals("*")) {
                MainWindow.CloseChildren(false);
                return "";
            }
            Shell s = GetShell(id);
            if ((s != null) && (!s.ShellId.Equals(ConsoleShellId))) {
                if (s.MainControl.Dispatcher.CheckAccess()) {
                    s.Close();
                } else {
                    s.MainControl.Dispatcher.Invoke(new Func<string>(() => { return Exit(s.ShellId); }));
                }
            }
            return "";
        }

        [ScriptFunction("title", "Set or get shell title.",
            typeof(Jint.Delegates.Func<String, String, String>),
            "Id of shell.", "Optional new title.")]
        public static string ShellTitle(string id = "", string title = "") {
            Shell s = GetShell(id);
            if (s != null) {
                if (s.MainControl.Dispatcher.CheckAccess()) {
                    if (!String.IsNullOrWhiteSpace(title) && !s.ShellId.Equals(ConsoleShellId)) {
                        s.Title = title;
                    }
                    return s.Title;
                } else {
                    return s.MainControl.Dispatcher.Invoke(new Func<string>(() => { return ShellTitle(s.ShellId, title); }));
                }
            }
            return "Error: invalid shell id.";
        }

        [ScriptFunction("position", "Set shell position.",
            typeof(Jint.Delegates.Func<String, Int32, Int32, Point>),
            "Id of shell. Note: to keep old value use a negative value.",
            "x coordinate of top left corner.", "y coordinate of top left corner.")]
        public static Point Position(string id = "", Int32 x = -1, Int32 y = -1) {
            Point p = new Point(); bool update = false;
            Shell s = GetShell(id);
            if (s != null) {
                if (s.MainControl.Dispatcher.CheckAccess()) {
                    if (x < 0) {
                        p.X = s.ShellWindow.Position.X;
                    } else {
                        p.X = (double)x;
                        update = true;
                    }
                    if (y < 0) {
                        p.Y = s.ShellWindow.Position.Y;
                    } else {
                        p.Y = (double)y;
                        update = true;
                    }
                    if (update) s.ShellWindow.Position = p;
                } else {
                    p = (Point)s.MainControl.Dispatcher.Invoke(new Func<Point>(()
                        => { return Position(s.ShellId, x, y); }));
                }
            }
            return p;
        }

        [ScriptFunction("size", "Set shell size.",
            typeof(Jint.Delegates.Func<String, Int32, Int32, Size>),
            "Id of shell. Note: to keep old value use a negative value.", "Width.", "Height.")]
        public static Size Size(string id="", Int32 width = -1, Int32 height = -1) {
            Size a = new Size();
            Shell s = GetShell(id);
            if (s != null) {
                if (s.MainControl.Dispatcher.CheckAccess()) {
                    if (width < 0) {
                        a.Width = s.ShellWindow.Width;
                    } else {
                        a.Width = (double)width;
                        s.ShellWindow.Width = a.Width;
                    }
                    if (height < 0) {
                        a.Height = s.ShellWindow.Height;
                    } else {
                        a.Height = (double)height;
                        s.ShellWindow.Height = a.Height;
                    }
                } else {
                    a = (Size)s.MainControl.Dispatcher.Invoke(new Func<Size>(()
                        => { return Size(s.ShellId, width, height); }));
                }
            }
            return a;
        }

        [ScriptFunction("shell", "Fetches shell Id for a given Title or Id.",
            typeof(Jint.Delegates.Func<String,String>),
            "Id or Title of shell to search for. Returns a list of the current shells if null (the default).")]
        public static string ListShells(string shellIdOrTitle=null) {
            string result = "";
            try {
                if (shellIdOrTitle == null) {
                    foreach (Shell s in Shells.Values) {
                        result += s.ToString() + "\n";
                    }
                } else {
                    Shell s = GetShell(shellIdOrTitle);
                    if (s != null) {
                        return s.ShellId;
                    }
                }
            } catch (Exception) {

            }
            return result;
        }

        [ScriptFunction("layout", "Rearrange shell windows.",
            typeof(Jint.Delegates.Func<String, String>),
            "Layout pattern:'Cascade','Horizontal','Vertical'.")]
        public static string Layout(string layoutPattern) {
            if (!String.IsNullOrWhiteSpace(layoutPattern)) {
                MdiLayout p = MdiLayout.Cascade;
                switch (layoutPattern.ToUpper()[0]) {
                    case 'C': p = MdiLayout.Cascade; break;
                    case 'H': p = MdiLayout.TileHorizontal; break;
                    case 'V': p = MdiLayout.TileVertical; break;
                    default: return "";
                }
                MainWindow.ChangeLayout(p);
            }
            return "";
        }

        [ScriptFunction("workingdir", "Get or set the current working directory for relative file paths.",
            typeof(Jint.Delegates.Func<String, String, String>),
            "Shell id.", "Optional new working directory.")]
        public static string GetWorkingDir(string id="", string newDir = "") {
            Shell s = GetShell(id);
            if (s != null) {
                if (s.MainControl.Dispatcher.CheckAccess()) {
                    if (!String.IsNullOrWhiteSpace(newDir)) {
                        s.WorkingDir = newDir;
                    }
                    if (String.IsNullOrWhiteSpace(s.WorkingDir)) return "";
                    return s.WorkingDir;
                } else {
                    s.MainControl.Dispatcher.Invoke(new Func<string>(() => { return GetWorkingDir(s.ShellId, newDir); }));
                }
            }
            return "";
        }

        [ScriptFunction("find", "Returns the full path for file name.",
            typeof(Jint.Delegates.Func<String, String, String>), "Shell id.", "File name.")]
        public static string FindFile(string id, string fileName) {
            Shell s = GetShell(id);
            if (s != null) {
                if (s.MainControl.Dispatcher.CheckAccess()) {
                    try {
                        string f = Path.Combine(s.WorkingDir, fileName);
                        if (File.Exists(f)) return f;
                    } catch (Exception) {
                    }
                    string p = Settings.FindFileOnPath(Project.GetProjectPaths(), fileName);
                    if (p != null) return p;
                } else {
                    s.MainControl.Dispatcher.Invoke(new Func<string>(() => { return FindFile(s.ShellId, fileName); }));
                }
            }
            return "Error: file not found.";
        }

        internal static void ScanTypeForShellDeserializers(Type targetType) {
            foreach (MethodInfo info in targetType.GetMethods(BindingFlags.Public | BindingFlags.Static)) {
                foreach (Attribute attr in Attribute.GetCustomAttributes(info)) {
                    if (attr.GetType() == typeof(ShellDeserializerAttribute)) {
                        try {
                            ShellDeserializerAttribute shellAttr = (ShellDeserializerAttribute)attr;
                            string name = shellAttr.Name;
                            if (ShellDeserializers.ContainsKey(name)) {
                                ShellDeserializers.Remove(name);
                            }
                            ShellDeserializerDelegate d = (ShellDeserializerDelegate)info.CreateDelegate(typeof(ShellDeserializerDelegate));
                            ShellDeserializers.Add(name, d);
                        } catch (Exception e) {
                            Settings.BugReport("Problem with shell deserializer: " + e.Message);
                        }
                    }
                }
            }
        }

        public static ShellDeserializerDelegate GetShellDeserializer(string shellKindName) {
            if (String.IsNullOrWhiteSpace(shellKindName)) return null;
            try {
                return ShellDeserializers[shellKindName];
            } catch (Exception) {
            }
            return null;
        }
    }
}
