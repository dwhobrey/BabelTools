using System;
using System.IO;
using System.Windows;
using System.Linq;
using System.Xml;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using Microsoft.Research.DynamicDataDisplay.PointMarkers;
using Babel.Resources;

namespace Babel.Core {

    public partial class ChartControl : ShellControl {

        static ChartControl() {
     //       DefaultStyleKeyProperty.OverrideMetadata(typeof(ChartControl),new FrameworkPropertyMetadata(typeof(ChartControl)));
        }

        public ChartControl() {
        }

        public ChartControl(string ownerShellId)
            : base(ownerShellId) {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public ChartControl(string ownerShellId, XmlNode node)
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

        public override string KindName { get { return "chart"; } }

        // On entry, node is the shell node.
        public override void Serializer(XmlNode node, bool isSerialize) {
            base.Serializer(node, isSerialize);
            XmlNode c = Project.GetChildNode(node, "chart");
            if (c == null) return;
            if (isSerialize) {
                Project.SetNodeAttributeValue(c, "someproperty", "?");
            } else {
                string v = Project.GetNodeAttributeValue(c, "someproperty","?");
            }
        }

        void ExamplePlot() {
            // Prepare data in arrays
            const int N = 1000;
            double[] x = new double[N];
            double[] y = new double[N];

            for (int i = 0; i < N; i++) {
                x[i] = i * 0.1;
                y[i] = Math.Sin(x[i]);
            }

            // Create data sources:
            var xDataSource = x.AsXDataSource();
            var yDataSource = y.AsYDataSource();

            CompositeDataSource compositeDataSource = xDataSource.Join(yDataSource);
            // adding graph to plotter
            plotter.AddLineGraph(compositeDataSource,
                Colors.Goldenrod,
                3,
                "Sine");

            // Force evertyhing plotted to be visible
            plotter.FitToView();
        }

        void OnLoaded(object sender, RoutedEventArgs e) {
            ExamplePlot();
        }

        // On entry, node is the shell node.
        [ShellDeserializer("chart")]
        public static ShellControl ChartShellDeserializer(string shellId, XmlNode node) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                return new ChartControl(shellId, node);
            }
            return (ShellControl)Application.Current.Dispatcher.Invoke(
                    new Func<ShellControl>(() => { return ChartShellDeserializer(shellId, node); })
                );
        }

        [ScriptFunction("chart", "Opens chart view.",
            typeof(Jint.Delegates.Func<String>))]
        public static string OpenChartShell() {
            if (Application.Current.Dispatcher.CheckAccess()) {
                Shell shell = Shell.OpenShell("Chart", 300, 500);
                string shellId = shell.ShellId;
                if (shell.MainControl == null) {
                    if (!shell.SetControl(new ChartControl(shellId))) {
                        return "Error: unable to create shell chart control.";
                    }
                }
                return shellId;
            }
            return (string)Application.Current.Dispatcher.Invoke(
                    new Func<string>(() => { return OpenChartShell(); })
                );
        }
    }
}

