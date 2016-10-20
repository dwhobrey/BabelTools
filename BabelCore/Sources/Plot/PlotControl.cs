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

using System.ComponentModel;
using System.Diagnostics;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace Babel.Core {

    public partial class PlotControl : ShellControl {

        public const String TAG = "PlotControl";

        static PlotControl() {
     //       DefaultStyleKeyProperty.OverrideMetadata(typeof(ScopeControl),new FrameworkPropertyMetadata(typeof(ScopeControl)));
        }

        bool IsRealTime;
        bool IsYZoom;
        bool IsGrid;
        bool IsNormalise;
        int NumSeries;
        int NumPointsPerPlot;
        int CurrentRelativeCacheIndex;
        DataCache PointCache;
        BottomLinearAxis BottomAxis;
        LeftLinearAxis LeftAxis;
        MapLineSeries MovingSeries;
        DataPoint MoveLastPosition;
        string BaseTitle;
        String CurrentFileName;
        public Thread Task;
        Dictionary<int, string> SeriesNames;

        public PlotControl() {
        }

        public PlotControl(string ownerShellId)
            : base(ownerShellId) {
            InitializeComponent();
            SetupPlot();
        }

        public PlotControl(string ownerShellId, XmlNode node)
            : base(ownerShellId) {
            InitializeComponent();
            SetupPlot();
            Serializer(node, false);
        }

        // On entry, node is the shell node.
        public override void Serializer(XmlNode node, bool isSerialize) {
            base.Serializer(node, isSerialize);
            XmlNode c = Project.GetChildNode(node, "plot");
            if (c == null) return;
            if (isSerialize) {
                // Serialize series names.
                XmlNode names = Project.GetChildNode(c, "seriesnames");
                if (names != null) {
                    names.RemoveAll();
                    foreach (KeyValuePair<int, string> seriesLabel in SeriesNames) {
                        XmlNode seriesNode = Project.GetChildNode(names, "series", seriesLabel.Key.ToString());
                        if (seriesNode != null) {
                            Project.SetNodeAttributeValue(seriesNode, "label", seriesLabel.Value);
                        }
                    }
                }
                // Serialize plot titles.
                XmlNode titles = Project.GetChildNode(c, "titles");
                if (titles != null) {
                    Project.SetNodeAttributeValue(titles, "heading", BaseTitle);
                    Project.SetNodeAttributeValue(titles, "yaxis", LeftAxis.Title);
                    Project.SetNodeAttributeValue(titles, "xaxis", BottomAxis.Title);
                }
                // Serialize data cache.
                if (PointCache!=null) {
                    PointCache.Serializer(c, true);
                }
            } else {
                XmlNodeList nodeList = Project.GetNodes(c, "seriesnames/*");
                if (nodeList != null) {
                    // Fetch series names:
                    foreach (XmlNode p in nodeList) {
                        string seriesIndex = Project.GetNodeAttributeValue(p, "name", "");
                        if (!String.IsNullOrWhiteSpace(seriesIndex)) {
                            string seriesName = Project.GetNodeAttributeValue(p, "label", "");
                            if (!String.IsNullOrWhiteSpace(seriesIndex)
                                && !String.IsNullOrWhiteSpace(seriesName)) {
                                 SetSeriesName(Convert.ToInt16(seriesIndex), seriesName);
                            }
                        }
                    }
                }
                // Fetch plot titles.
                XmlNode titles = Project.GetChildNode(c, "titles");
                if (titles != null) {
                    string heading = Project.GetNodeAttributeValue(titles, "heading", "Plot");
                    string yaxis = Project.GetNodeAttributeValue(titles, "yaxis", "Y");
                    string xaxis = Project.GetNodeAttributeValue(titles, "xaxis", "X");
                    SetTitles(heading, yaxis, xaxis);
                }
                // Fetch data cache.
                PointCache = DataCache.Deserializer(c);
            }
        }

        public override string KindName { get { return "plot"; } }

        public PlotModel PlotModel {
            get {
                return plotter.Model;
            }
            set {
                plotter.Model = value;
            }
        }

        private string CurrentDateAndTime() {
            return DateTime.Now.ToString(", d MMM yyyy, H:mm:ss");
        }

        private void SetupPlot() {
            CompositionTarget.Rendering += CompositionTarget_Rendering;
            PlotModel = new PlotModel();
            BottomAxis = new BottomLinearAxis(this);
            PlotModel.Axes.Add(BottomAxis);
            LeftAxis = new LeftLinearAxis(this);
            PlotModel.Axes.Add(LeftAxis);
            PlotModel.MouseDown += PlotMouseDown;
            PlotModel.MouseMove += PlotMouseMove;
            PlotModel.MouseUp += PlotMouseUp;
            SeriesNames = new Dictionary<int, string>();
            IsRealTime = true;
            IsYZoom = true;
            IsGrid = false;
            IsNormalise = false;
            NumSeries = 0;
            NumPointsPerPlot = 200;
            CurrentRelativeCacheIndex = 0;
            CurrentFileName = null;
            BaseTitle="";
            MovingSeries = null;
            PointCache = null;
            Task = new Thread(new ThreadStart(Run));
            Task.Name = "PlotServiceThread:" + OwnerShellId;
            Task.Priority = ThreadPriority.Normal; // XXX was AboveNormal.
            Task.Start();
        }

        public void SetCache(DataCache dc) {
            PointCache = dc;
        }

        public DataCache GetCache() {
            return PointCache;
        }

        public void SetTitles(string title, string yAxis, string xAxis) {
            BaseTitle = title;
            PlotModel.Title = title + CurrentDateAndTime();
            BottomAxis.Title = xAxis;
            LeftAxis.Title = yAxis;
        }

        public void SetSeriesName(int index, string name) {
            if (index >= 0 && index < 16) {
                try {
                    if (String.IsNullOrWhiteSpace(name)) name = "";
                    SeriesNames[index] = name;
                    if (PlotModel.Series.Count > index) {
                        MapLineSeries s = PlotModel.Series[index] as MapLineSeries;
                        s.Title = name;
                    }
                } catch (Exception) { }
            }
        }

        public void SetSeries(int numSeries) {
            NumSeries = numSeries;
            for (int i = 0; i < NumSeries; i++) {
                MapLineSeries s = new MapLineSeries(PointCache, i + 1);
                s.LineStyle = LineStyle.Solid;
                string t=null;
                SeriesNames.TryGetValue(i, out t);
                if (t == null) t = "Series " + (1 + i);
                s.Title = t;
                PlotModel.Series.Add(s);
            }
        }

        public override void Init() {
            base.Init();
        }

        public void ResetSeries() {
            for (int i = 0; i < NumSeries; i++) {
                MapLineSeries s = PlotModel.Series[i] as MapLineSeries;
                s.Points.Clear();
                s.ResetMinMax();
            }
        }

        public void ResetCache() {
            CurrentRelativeCacheIndex = 0;
            if (PointCache != null) {
                PointCache.Reset();
            }
            ResetSeries();
        }

        public override void Close() {
            base.Close();
            if (Task != null) {
                Primitives.Interrupt(Task);
                Task = null;
            }
            if (PointCache != null) {
                PointCache.Close();
                PointCache = null;
            }
            PlotModel = null;
            BottomAxis = null;
            LeftAxis = null;
            MovingSeries = null;
            SeriesNames = null;
        }

        public void RefreshPoints() {
            if (PointCache != null) {
                int k = PointCache.FindRelativeXCoordinateInCache(BottomAxis.ActualMinimum);
                if (k < 0) k = ~k;
                ResetSeries();
                CurrentRelativeCacheIndex = k + NumPointsPerPlot;
                if (CurrentRelativeCacheIndex > PointCache.RelativeCount) CurrentRelativeCacheIndex = PointCache.RelativeCount;
                if (k < PointCache.RelativeCount) {
                    List<double> q = PointCache.RelativeGetAt(k);
                    if (q != null) {
                        int maxDataSeries = q.Count - 1;
                        if (q == null) return;
                        if (NumSeries == 0) SetSeries(maxDataSeries);
                        int maxNumSeries = NumSeries < maxDataSeries ? NumSeries : maxDataSeries;
                        PointCache.RefreshMinMax();
                        for (int h = 0; h < maxNumSeries; h++) {
                            MapLineSeries s = PlotModel.Series[h] as MapLineSeries;
                            s.RefreshMinMax();
                            for (int i = k; i < CurrentRelativeCacheIndex; i++) {
                                List<double> p = PointCache.RelativeGetAt(i);
                                if (p == null) return;
                                s.Points.Add(new DataPoint(p[0], s.Map(p[1 + h])));
                            }
                        }
                    }
                }
            }
        }

        public void Pan() {
            if (PointCache != null) {
                int k = PointCache.FindRelativeXCoordinateInCache(BottomAxis.ActualMaximum);
                IsRealTime = (k >= PointCache.RelativeCount || (~k) >= PointCache.RelativeCount);
                if (IsRealTime) BottomAxis.ClearPan();
                RefreshPoints();
            }
        }

        public void AddPoint(List<double> p) {
            if (PointCache != null) {
                PointCache.AddPoint(p);
                StatusMessage.Header = "Status: #Points=" + PointCache.RelativeCount;
            }
        }

        public void UpdatePlot() {
            if (PointCache != null) {
                if (Dispatcher.CheckAccess()) {
                    while (IsRealTime && PointCache.RelativeCount > CurrentRelativeCacheIndex) {
                        List<double> p = PointCache.RelativeGetAt(CurrentRelativeCacheIndex++);
                        if (p == null) break;
                        int maxDataSeries = p.Count - 1;
                        if (NumSeries == 0) SetSeries(maxDataSeries);
                        int numPlotableSeries = NumSeries < maxDataSeries ? NumSeries : maxDataSeries;
                        for (int h = 0; h < numPlotableSeries; h++) {
                            MapLineSeries s = PlotModel.Series[h] as MapLineSeries;
                            if (s.Points.Count >= NumPointsPerPlot) {
                                s.Points.RemoveAt(0);
                            }
                            s.Points.Add(new DataPoint(p[0], s.Map(p[1 + h])));
                        }
                    }
                } else {
                    Dispatcher.Invoke((Action)(() => { UpdatePlot(); }));
                }
            }
        }

        public void GenerateTestPoint() {
            if (PointCache != null) {
                if (NumSeries == 0) SetSeries(3);
                if (NumSeries > 0) {
                    List<double> p = new List<double>(1 + NumSeries);
                    double x = PointCache.AbsoluteCount > 0 ? (PointCache.AbsoluteGetAt(PointCache.AbsoluteCount - 1)[0] + 1) : 0;
                    p.Add(x);
                    for (int i = 0; i < NumSeries; i++) {
                        double y = 0;
                        int m = 80;
                        for (int j = 0; j < m; j++)
                            y += Math.Cos(20.0 * i + 0.001 * x * j * j);
                        y /= m;
                        y += (i * 6);
                        p.Add(y);
                    }
                    AddPoint(p);
                    UpdatePlot();
                }
            }
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e) {
          //  this.GenerateTestPoint();
            plotter.InvalidatePlot(true);
        }

        private void PlotMouseDown(object sender, OxyMouseEventArgs e) {
           // if (e.ChangedButton == OxyMouseButton.Left) {
                MovingSeries = PlotModel.GetSeriesFromPoint(e.Position) as MapLineSeries;
                MoveLastPosition = BottomAxis.InverseTransform(e.Position.X,e.Position.Y,LeftAxis);
                //e.Handled = true;
           // }
        }

        private void PlotMouseMove(object sender, OxyMouseEventArgs e) {
            if (MovingSeries != null) {
                DataPoint p = BottomAxis.InverseTransform(e.Position.X, e.Position.Y, LeftAxis);
                MovingSeries.Offset += (p.Y - MoveLastPosition.Y);
                MoveLastPosition = p;
                RefreshPoints();
                PlotModel.InvalidatePlot(false);
               // e.Handled = true;
            }
        }

        private void PlotMouseUp(object sender, OxyMouseEventArgs e) {
            if (MovingSeries != null) {
                MovingSeries = null;
               // e.Handled = true;
            }
        }

        void ToggleRealTime() {
            IsRealTime = !IsRealTime;
            if (IsRealTime) BottomAxis.ClearPan();
            RefreshPoints();
        }
        void SetZoom(bool isY) {
            IsYZoom = isY;
            if (IsYZoom) {
                LeftAxis.IsZoomEnabled = true;
                BottomAxis.IsZoomEnabled = false;
            } else {
                LeftAxis.IsZoomEnabled = false;
                BottomAxis.IsZoomEnabled = true;
            }
        }
        void ToggleNormalise() {
            IsNormalise = !IsNormalise;
            for (int i = 0; i < NumSeries; i++) {
                MapLineSeries s = PlotModel.Series[i] as MapLineSeries;
                s.AutoNormalise = IsNormalise;
            }
            RefreshPoints();
        }

        void ToggleGrid() {
            IsGrid = !IsGrid;
            if (IsGrid) {
                LeftAxis.MajorGridlineStyle = LineStyle.Solid;
                LeftAxis.MinorGridlineStyle = LineStyle.Dot;
                BottomAxis.MajorGridlineStyle = LineStyle.Solid;
                BottomAxis.MinorGridlineStyle = LineStyle.Dot;
            } else {
                LeftAxis.MajorGridlineStyle = LineStyle.None;
                LeftAxis.MinorGridlineStyle = LineStyle.None;
                BottomAxis.MajorGridlineStyle = LineStyle.None;
                BottomAxis.MinorGridlineStyle = LineStyle.None;
            }
        }
        void SaveToFile() {
            CurrentFileName = Settings.SaveFileNameFromUser(CurrentFileName, ".png", "Image");
            if (CurrentFileName == null) return;
            OxyPlot.WindowsForms.PngExporter.Export(PlotModel, CurrentFileName, 1000, 707);
        }

        void Realtime_Click(object sender, RoutedEventArgs e) {
            ToggleRealTime();
        }
        void Normalise_Click(object sender, RoutedEventArgs e) {
            ToggleNormalise();
        }

        void ZoomToggle_Click(object sender, RoutedEventArgs e) {
            SetZoom(!IsYZoom);
        }

        void GridToggle_Click(object sender, RoutedEventArgs e) {
            ToggleGrid();
        }

        void SaveImage_Click(object sender, RoutedEventArgs e) {
            SaveToFile();
        }

        void ClearCache_Click(object sender, RoutedEventArgs e) {
            ResetCache();
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            switch (e.Key) {
                case Key.C:
                    ResetCache();
                    break;
                case Key.G:
                    ToggleGrid();
                    break;
                case Key.N:
                    ToggleNormalise();
                    break;
                case Key.R:
                case Key.P:
                    ToggleRealTime();
                    break;
                case Key.S:
                    SaveToFile();
                    break;
                case Key.X:
                    SetZoom(false);
                    break;
                case Key.Y:
                    SetZoom(true);
                    break;
                case Key.Z:
                    SetZoom(!IsYZoom);
                    break;
                default:
                    base.OnKeyDown(e);
                    return;
            }
            e.Handled = true;
            return;
        }

        public void Run() {
            while (!IsClosing) {
                try {
                    if (PointCache != null) {
                        while (!IsClosing) {
                            while (!IsClosing && IsRealTime && PointCache.RelativeCount > CurrentRelativeCacheIndex) {
                                UpdatePlot();
                            }
                            PointCache.WaitForData();
                        }
                    } else {
                        Thread.Sleep(1000);
                    }
                } catch (ThreadInterruptedException) {
                    break;
                } catch (Exception e) {
                    // ignore.
                    Log.d(TAG, "PlotServiceThread exception:" + e.Message);
                }
            }
        }

        // On entry, node is the shell node.
        [ShellDeserializer("plot")]
        public static ShellControl PlotShellDeserializer(string shellId, XmlNode node) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                return new PlotControl(shellId, node);
            }
            return (ShellControl)Application.Current.Dispatcher.Invoke(
                    new Func<ShellControl>(() => { return PlotShellDeserializer(shellId, node); })
                );
        }

        public static string OpenPlotShell() {
            if (Application.Current.Dispatcher.CheckAccess()) {
                Shell shell = Shell.OpenShell("Plot", 300, 500);
                string shellId = shell.ShellId;
                if (shell.MainControl == null) {
                    if (!shell.SetControl(new PlotControl(shellId))) {
                        return "Error: unable to create shell plot control.";
                    }
                }
                return shellId;
            }
            return (string)Application.Current.Dispatcher.Invoke(
                    new Func<string>(() => { return OpenPlotShell(); })
                );
        }

        [ScriptFunction("plotfile", "Opens plot view of data file. Returns plot Id.",
            typeof(Jint.Delegates.Func<String, String>),
            "Data file name.")]
        public static string PlotDataFile(string dataFileName) {
            if (!String.IsNullOrWhiteSpace(dataFileName)) {
                string id = PlotControl.OpenPlotShell();
                Shell s = Shell.GetShell(id);
                if (s != null) {
                    PlotControl p = s.MainControl as PlotControl;
                    if (p != null) {
                        DataCache d = new DataCache(dataFileName);
                        string header = "";
                        d.LoadFromFile(dataFileName, out header);
                        p.NumPointsPerPlot = d.AbsoluteCount;
                        p.SetCache(d);
                        return id;
                    }
                    s.Close();
                    return "Error: unable to open plot control.";
                }
                return "Error: unable to open plot shell.";
            }
            return "Error: bad file name.";
        }
    }
}

