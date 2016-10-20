using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using System.Xml;
using System.Linq;
using System.Windows.Controls;

using Babel.Core;
using Babel.BabelProtocol;

using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace Babel.Recorder {

    // Simply for holding point details in task Q.
    public class RecorderAxisPoint {
        public bool IsTarget;
        public bool UpdateSeries;
        public bool SeriesResetRequired;
        public AxisPortProperties SeriesAxisPortPorperties;
        public List<double> AxisPoint;
        public RecorderAxisPoint() {
            UpdateSeries = true;
        }
        public RecorderAxisPoint(AxisPortProperties axisPortPorperties,List<double> point,
                bool isTarget, bool updateSeries, bool seriesResetRequired) {
            IsTarget = isTarget;
            UpdateSeries = updateSeries;
            SeriesResetRequired = seriesResetRequired;
            SeriesAxisPortPorperties = axisPortPorperties;
            AxisPoint = point;
        }
    }

    public class RecorderChart : IComparer<DataPoint> {

        public const String TAG = "RecorderChart";

        public const int POINTS_PER_PLOT = 200;

        public int NumPointsPerPlot;
        public DataCache ChartCache;

        bool IsClosing;
        bool IsYZoom;
        int CurrentRelativeChartCacheIndex;
        public RecorderTimeLineAxis BottomAxis;
        public RecorderLeftAngularAxis LeftAngularAxis;
        public RecorderLeftSignalAxis LeftSignalAxis;
        string CurrentChartImageFileName;
        OxyPlot.Wpf.Plot ChartPlotter;
        PlotModel ChartPlotModel;
        RecorderControl Recorder;
        public LinkedBlockingCollection<RecorderAxisPoint> AxisPointTaskQueue;
        Thread PointSchedulerTask;

        public RecorderChart(RecorderControl recorder) {
            Recorder = recorder;
            ChartPlotter = null;
            IsClosing = false;
            IsYZoom = false;
            NumPointsPerPlot = POINTS_PER_PLOT;
            CurrentRelativeChartCacheIndex = 0;
            CurrentChartImageFileName = null;
            ChartCache = new DataCache(recorder.ShellId, false, true, true, 0);
            BottomAxis = new RecorderTimeLineAxis(this);
            LeftAngularAxis = new RecorderLeftAngularAxis(this);
            LeftSignalAxis = new RecorderLeftSignalAxis(this);
            AxisPointTaskQueue = new LinkedBlockingCollection<RecorderAxisPoint>();
            PointSchedulerTask = new Thread(new ThreadStart(AxisPointScheduler));
            PointSchedulerTask.Name = "AxisPointSchedulerThread:" + recorder.ShellId;
            PointSchedulerTask.Priority = ThreadPriority.Normal;
            PointSchedulerTask.Start();
        }

        public void InitializeControls() {
            CompositionTarget.Rendering += CompositionTarget_Rendering;
            ChartPlotter = Recorder.plotter;
            ChartPlotModel = new PlotModel();
            ChartPlotter.Model = ChartPlotModel;
            ChartPlotModel.Axes.Add(BottomAxis);
            ChartPlotModel.Axes.Add(LeftAngularAxis);
            ChartPlotModel.Axes.Add(LeftSignalAxis);
            Recorder.ZoomDirection.SelectionChanged += new SelectionChangedEventHandler(ZoomDirection_SelectionChanged);
            Recorder.ZoomDirection.SelectedIndex = 0;
            Recorder.SaveImageButton.Click += new RoutedEventHandler(SaveImage_Click);
        }

        public void Close() {
            IsClosing = true;
            if (PointSchedulerTask != null) {
                Primitives.Interrupt(PointSchedulerTask);
                PointSchedulerTask = null;
            }
            ResetCache();
            ResetSeries();
            ChartPlotModel = null;
            ChartPlotter = null;
            BottomAxis = null;
            LeftAngularAxis = null;
            LeftSignalAxis = null;
            ChartCache = null;
        }

        public void SetPointsPerPlot(int numPoints) {
            if (numPoints < POINTS_PER_PLOT)
                NumPointsPerPlot = POINTS_PER_PLOT;
            else
                NumPointsPerPlot = numPoints;
        }

        public void ResetSeries(int numPointsPerPlot=0) {
            int numSeries = ChartPlotModel.Series.Count;
            BottomAxis.ClearAxis();
            for (int i = 0; i < numSeries; i++) {
                LineSeries s = ChartPlotModel.Series[i] as LineSeries;
                s.Points.Clear();
            }
            ChartPlotModel.ResetAllAxes();
            SetPointsPerPlot(numPointsPerPlot);
        }

        public void ResetCache() {
            CurrentRelativeChartCacheIndex = 0;
            if (ChartCache != null) {
                ChartCache.Reset();
            }
        }

        public uint ConvertFPSToMilliseconds(uint fps) {
            if (BottomAxis != null) 
                return BottomAxis.ConvertFPSToMilliseconds(fps);
            return 0;
        }

        public void UpdateFPS(TimeLineKinds fps) {
            BottomAxis.SetTimeLineFormat(fps);
        }

        public void SetAxisVisibility(AxisProperties ap) {
            if (ap != null) {
                Series s = null;
                try {
                    s = ap.ActualSeries;
                    if (s != null) s.IsVisible = ap.Show;
                } catch (Exception) { }
                try {
                    s = ap.TargetSeries;
                    if (s != null) s.IsVisible = ap.Show;
                } catch (Exception) { }
            }
        }

        public void SetAxisVisibility(int axisId) {
            SetAxisVisibility(AxisProperties.FindAxis(Recorder.AxisOptions, axisId));
        }

        // This is called every time axis options change.
        public void SetupSeriesConfiguration() {
            ResetCache();
            ResetSeries();
            ChartPlotModel.Series.Clear();
            foreach (AxisProperties ap in Recorder.AxisOptions) {
                // Note: series are installed in reverse order so that actual draws on top of signal.
                RecorderSignalLineSeries st = new RecorderSignalLineSeries(this,ChartCache, ap.SeriesIndex + 1);
                st.LineStyle = LineStyle.Solid;
                st.Title = ap.Description + " signal";
                ChartPlotModel.Series.Add(st);
                ap.TargetSeries = st;
                //
                RecorderAngularLineSeries sa = new RecorderAngularLineSeries(this,ChartCache, ap.SeriesIndex + 0);
                sa.LineStyle = LineStyle.Solid;
                sa.Title = ap.Description + " position";
                ChartPlotModel.Series.Add(sa);
                ap.ActualSeries = sa;
                //
                SetAxisVisibility(ap);
            }
        }

        public int Compare(DataPoint a, DataPoint b) {
            double d = a.X - b.X;
            if (d > 0.0) return 1;
            if (d < 0.0) return -1;
            return 0;
        }

        private void UpdateSeries(LineSeries s, double x, double y) {
            if (s.Points.Count >= NumPointsPerPlot) {
                if(s.Points.Count>0)
                    s.Points.RemoveAt(0);
            }
            DataPoint dp = new DataPoint(x/1000.0, y);
            int pos = s.Points.BinarySearch(dp, this);
            if (pos >= 0) {
                s.Points[pos] = dp;
            } else {
                pos = ~pos;
                if (pos >= s.Points.Count)
                    s.Points.Add(dp);
                else
                    s.Points.Insert(pos, dp);
            }
        }

        // When playing cache count = total size = CurrentRelativeChartCacheIndex, 
        // whereas CurrentPlayIndex is somewhere inbetween. 
        // Need to show AP series up to that point.
        // When playing, updates ActualPos (AP) series only, otherwise both.
        // Uses NaN's to mask out unused series points.
        private void UpdateSeries() {
            try {
                while (!IsClosing && ChartPlotter != null && (AxisPointTaskQueue.Size()>0)) {
                    RecorderAxisPoint rap = AxisPointTaskQueue.Take();
                    if (rap != null && !IsClosing) {
                        AxisPortProperties ap = rap.SeriesAxisPortPorperties;
                        List<double> p = rap.AxisPoint;
                        bool isTarget = rap.IsTarget;
                        if (rap.SeriesResetRequired) {
                            ResetSeries();
                        }
                        foreach (AxisProperties a in ap.AxesOnPort) {
                            if (rap.UpdateSeries) {
                                if (isTarget) {
                                    if (a.TargetSeries != null) {
                                        try {
                                            double yt = p[a.DataPVTOffset + 1];
                                            if (!Double.IsNaN(yt)) {
                                                UpdateSeries(a.TargetSeries, p[0], yt);
                                            }
                                        } catch (Exception) {
                                            // Index out of range.
                                        }
                                    }
                                } else {
                                    if (a.ActualSeries != null) {
                                        try {
                                            double ya = p[a.DataPVTOffset];
                                            if (!Double.IsNaN(ya)) {
                                                UpdateSeries(a.ActualSeries, p[0], ya);
                                            }
                                        } catch (Exception) {
                                            // Index out of range.
                                        }
                                    }
                                }
                            }
                            if (!isTarget) {
                                try {
                                    Recorder.UpdateAxisPosition(a.AxisId, p[a.DataPVTOffset]);
                                } catch (Exception) {
                                    // Index out of range.
                                }
                            }
                        }
                    }
                }
            } catch (ThreadInterruptedException) {
            } catch (Exception e) {
                if (IsClosing) return;
                // ignore.
                Log.d(TAG, "UpdateSeries exception:" + e.Message);
            }
        }

        // Simply for handling chart update asynchornously at lower priority.
        private void AxisPointScheduler() {
            while (!IsClosing) {
                try {
                    while (!IsClosing && ChartPlotter != null) {
                        AxisPointTaskQueue.WaitWhileEmpty(200);
                        if (!IsClosing) {
                            ChartPlotter.Dispatcher.Invoke((Action)(() => { UpdateSeries(); }));
                        }
                    }
                } catch (ThreadInterruptedException e) {
                    if (IsClosing) break;
                    Log.i(TAG, "AxisPointScheduler interrupt exception:" + e.Message);
                } catch (Exception e) {
                    if (IsClosing) break;
                    // ignore.
                    Log.d(TAG, "AxisPointScheduler exception:" + e.Message);
                }
            }
        }

        public void RefreshChart(int numPointsPerPlot) {
            if (ChartCache != null) {
                int k = ChartCache.FindRelativeXCoordinateInCache(BottomAxis.ActualMinimum);
                if (k < 0) k = ~k;
                ResetSeries(numPointsPerPlot);
                CurrentRelativeChartCacheIndex = k + NumPointsPerPlot;
                if (CurrentRelativeChartCacheIndex > ChartCache.RelativeCount)
                    CurrentRelativeChartCacheIndex = ChartCache.RelativeCount;
                if (k < ChartCache.RelativeCount) {
                    ChartCache.RefreshMinMax();
                    foreach (AxisProperties ap in Recorder.AxisOptions) { 
                        RecorderSignalLineSeries s = ap.TargetSeries;
                        for (int i = k; i < CurrentRelativeChartCacheIndex; i++) {
                            List<double> p = ChartCache.RelativeGetAt(i);
                            if (p == null || (p.Count <= ap.DataCacheOffset + 1)) break;
                            // Check if NaN, skip series point if so.
                            double d = p[ap.DataCacheOffset+1];
                            if (Double.IsNaN(d)) continue;
                            s.Points.Add(new DataPoint(p[0]/1000.0, d));
                        }
                    }
                }
            }
        }

        public void RenewChart() {
            if (ChartCache != null) {
                BottomAxis.ClearAxis();
                int n = ChartCache.AbsoluteCount;
                if (n > 0) { 
                    List<double> p = ChartCache.AbsoluteGetAt(n-1);
                    if(p!=null&&p.Count>0) {
                        double v = p[0] / 1000.0;
                        BottomAxis.AbsoluteMaximum = v;
                    }
                }
                RefreshChart(n);
            }
        }

        public void Pan() {
            if (ChartCache != null) {
                int k = ChartCache.FindRelativeXCoordinateInCache(BottomAxis.ActualMaximum);
                if (k >= ChartCache.RelativeCount || (~k) >= ChartCache.RelativeCount)
                    BottomAxis.ClearPan();
                RefreshChart(NumPointsPerPlot);
            }
        }

        void ZoomDirection_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (Recorder.ZoomDirection.SelectedIndex < 0) {
                Recorder.ZoomDirection.SelectedIndex = 0;
            }
            SetZoom(Recorder.ZoomDirection.SelectedIndex != 0);
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e) {
            if (ChartPlotter != null)
                ChartPlotter.InvalidatePlot(true);
        }

        public void SetZoom(bool isY) {
            if (LeftSignalAxis == null || LeftAngularAxis == null || BottomAxis == null) return;
            IsYZoom = isY;
            if (IsYZoom) {
                LeftSignalAxis.IsZoomEnabled = true;
                LeftAngularAxis.IsZoomEnabled = true;
                BottomAxis.IsZoomEnabled = false;
            } else {
                LeftSignalAxis.IsZoomEnabled = false;
                LeftAngularAxis.IsZoomEnabled = false;
                BottomAxis.IsZoomEnabled = true;
            }
        }

        void SaveImage_Click(object sender, RoutedEventArgs e) {
            CurrentChartImageFileName = Settings.SaveFileNameFromUser(CurrentChartImageFileName, ".png", "Image");
            if (CurrentChartImageFileName == null) return;
            OxyPlot.WindowsForms.PngExporter.Export(ChartPlotModel, CurrentChartImageFileName, 1000, 707);
        }
    }
}
