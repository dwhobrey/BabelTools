using System;
using System.IO;
using System.Threading;
using System.Windows;
//using System.Linq;
using System.Xml;
using System.Windows.Controls;
//using System.Windows.Media;
//using System.Windows.Media.Imaging;
//using System.Windows.Shapes;
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using Microsoft.Research.DynamicDataDisplay.PointMarkers;
using Babel.Resources;

namespace Babel.Core {

    public partial class ScopeControl : ShellControl {

        // Three observable data sources. Observable data source contains
        // inside ObservableCollection. Modification of collection instantly modify
        // visual representation of graph. 
        ObservableDataSource<Point> source1 = null;
        ObservableDataSource<Point> source2 = null;
        ObservableDataSource<Point> source3 = null;

        static ScopeControl() {
     //       DefaultStyleKeyProperty.OverrideMetadata(typeof(ScopeControl),new FrameworkPropertyMetadata(typeof(ScopeControl)));
        }

        public ScopeControl() {
        }

        public ScopeControl(string ownerShellId)
            : base(ownerShellId) {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public ScopeControl(string ownerShellId, XmlNode node)
            : base(ownerShellId) {
            InitializeComponent();
            Serializer(node, false);
            Loaded += OnLoaded;
        }

        public override void Init() {
            base.Init();         
        }

        public override void Close() {
            base.Close();
        }

        public override string KindName { get { return "scope"; } }

        // On entry, node is the shell node.
        public override void Serializer(XmlNode node, bool isSerialize) {
            base.Serializer(node, isSerialize);
            XmlNode c = Project.GetChildNode(node, "scope");
            if (c == null) return;
            if (isSerialize) {
                Project.SetNodeAttributeValue(c, "someproperty", "?");
            } else {
                string v = Project.GetNodeAttributeValue(c, "someproperty","?");
            }
        }

        private void Simulation() {
            long counter = 0;
            double x, y1, y2, y3;
            Random random = new Random();
            while (!IsClosing) {
                x = ++counter;
                y1 = random.Next(0, 100);
                y2 = random.Next(0, 100);
                y3 = random.Next(0, 100);

                Point p1 = new Point(x, y1);
                Point p2 = new Point(x, y2);
                Point p3 = new Point(x, y3);

                source1.AppendAsync(Dispatcher, p1);
                source2.AppendAsync(Dispatcher, p2);
                source3.AppendAsync(Dispatcher, p3);

                Thread.Sleep(10); // Long-long time for computations...
            }
        }

        private void ExampleScopeStart() {
            // Create first source
            source1 = new ObservableDataSource<Point>();
            // Set identity mapping of point in collection to point on plot
            source1.SetXYMapping(p => p);

            // Create second source
            source2 = new ObservableDataSource<Point>();
            // Set identity mapping of point in collection to point on plot
            source2.SetXYMapping(p => p);

            // Create third source
            source3 = new ObservableDataSource<Point>();
            // Set identity mapping of point in collection to point on plot
            source3.SetXYMapping(p => p);

            // Add all three graphs. Colors are not specified and chosen random
            plotter.AddLineGraph(source1, 2, "Data row 1");
            plotter.AddLineGraph(source2, 2, "Data row 2");
            plotter.AddLineGraph(source3, 2, "Data row 3");

            // Start computation process in second thread
            Thread simThread = new Thread(new ThreadStart(Simulation));
            simThread.IsBackground = true;
            simThread.Start();
        }

        void OnLoaded(object sender, RoutedEventArgs e) {
            ExampleScopeStart();
        }

        // On entry, node is the shell node.
        [ShellDeserializer("scope")]
        public static ShellControl ScopeShellDeserializer(string shellId, XmlNode node) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                return new ScopeControl(shellId, node);
            }
            return (ShellControl)Application.Current.Dispatcher.Invoke(
                    new Func<ShellControl>(() => { return ScopeShellDeserializer(shellId, node); })
                );
        }

        [ScriptFunction("scope", "Opens scope view.",
            typeof(Jint.Delegates.Func<String>))]
        public static string OpenScopeShell() {
            if (Application.Current.Dispatcher.CheckAccess()) {
                Shell shell = Shell.OpenShell("Scope", 300, 500);
                string shellId = shell.ShellId;
                if (shell.MainControl == null) {
                    if (!shell.SetControl(new ScopeControl(shellId))) {
                        return "Error: unable to create shell scope control.";
                    }
                }
                return shellId;
            }
            return (string)Application.Current.Dispatcher.Invoke(
                    new Func<string>(() => { return OpenScopeShell(); })
                );
        }
    }
}

