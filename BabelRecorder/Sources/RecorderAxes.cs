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

    public enum TimeLineKinds {
        TimeLineFPS24, TimeLineFPS25, TimeLineFPS30, TimeLineMilliSeconds
    };

    public class RecorderTimeLineAxis : LinearAxis {

        RecorderChart ParentControl;
        public TimeLineKinds FPSKind;

        public RecorderTimeLineAxis(RecorderChart parent) {
            Position = AxisPosition.Bottom;
            ParentControl = parent;
            IsZoomEnabled = false;
            this.StringFormat = "mm:ss.msec";
            FPSKind = TimeLineKinds.TimeLineMilliSeconds;
        }

        public void SetTimeLineFormat(TimeLineKinds fps) {
            FPSKind = fps;
            switch (fps) {
                case TimeLineKinds.TimeLineFPS24:
                    this.StringFormat = "mm:ss.fa";
                    break;
                case TimeLineKinds.TimeLineFPS25:
                    this.StringFormat = "mm:ss.fb";
                    break;
                case TimeLineKinds.TimeLineFPS30:
                    this.StringFormat = "mm:ss.fc";
                    break;
                default: // TimeLineKinds.Milliseconds
                    this.StringFormat = "mm:ss.msec";
                    break;
            }         
        }

        public uint ConvertFPSToMilliseconds(uint fps) {
            switch (FPSKind) {
                case TimeLineKinds.TimeLineFPS24:
                    return (uint)((1000 * fps)/24);
                case TimeLineKinds.TimeLineFPS25:
                    return (uint)((1000 * fps) / 25);
                case TimeLineKinds.TimeLineFPS30:
                    return (uint)((1000 * fps) / 30);
                default: // TimeLineKinds.Milliseconds
                    break;
            }
            return fps;
        }

        public override void Pan(double delta) {
            base.Pan(delta);
            ParentControl.Pan();
        }

        public void ClearPan() {
            ViewMaximum = Double.NaN;
            ViewMinimum = Double.NaN;
        }

        public void ClearAxis() {
            ViewMaximum = Double.NaN;
            ViewMinimum = Double.NaN;
            this.DataMaximum = this.DataMinimum = this.ActualMaximum = this.ActualMinimum = double.NaN;
            Reset();
        }

        /// <summary>
        /// Converts a time span to a double.
        /// </summary>
        /// <param name="s">The time span.</param>
        /// <returns>A double value.</returns>
        public static double ToDouble(TimeSpan s) {
            //return s.TotalSeconds;
            return s.TotalMilliseconds;
        }

        /// <summary>
        /// Converts a double to a time span.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>A time span.</returns>
        public static TimeSpan ToTimeSpan(double value) {
            //return TimeSpan.FromSeconds(value);
            return TimeSpan.FromMilliseconds(value);
        }

        /// <summary>
        /// Gets the value from an axis coordinate, converts from double to the correct data type if necessary. e.g. DateTimeAxis returns the DateTime and CategoryAxis returns category strings.
        /// </summary>
        /// <param name="x">The coordinate.</param>
        /// <returns>The value.</returns>
        public override object GetValue(double x) {
            // return TimeSpan.FromSeconds(x);
            return TimeSpan.FromMilliseconds(x);
        }

        /// <summary>
        /// Formats the value.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <returns>The format value.</returns>
        public override string FormatValue(double x) {
            TimeSpan span = TimeSpan.FromSeconds(x);
            string s = this.ActualStringFormat ?? this.StringFormat;
            s = s.Replace("mm", span.Minutes.ToString("00"));
            s = s.Replace("ss", span.Seconds.ToString("00"));
            s = s.Replace("hh", span.Hours.ToString("00"));
            s = s.Replace("msec", span.Milliseconds.ToString("000"));
            s = s.Replace("fa", ((24*span.Milliseconds)/1000).ToString("0"));
            s = s.Replace("fb", ((25*span.Milliseconds)/1000).ToString("0"));
            s = s.Replace("fc", ((30*span.Milliseconds)/1000).ToString("0"));
            return s;
        }

        /// <summary>
        /// Calculates the actual interval.
        /// </summary>
        /// <param name="availableSize">Size of the available area.</param>
        /// <param name="maxIntervalSize">Maximum length of the intervals.</param>
        /// <returns>The calculate actual interval.</returns>
        protected override double CalculateActualInterval(double availableSize, double maxIntervalSize) {
            double range = Math.Abs(this.ActualMinimum - this.ActualMaximum);
            double interval = 1;
            var goodIntervals = new[] { 1.0, 5, 10, 30, 60, 120, 300, 600, 900, 1200, 1800, 3600 };

            int maxNumberOfIntervals = Math.Max((int)(availableSize / maxIntervalSize), 2);

            while (true) {
                if (range / interval < maxNumberOfIntervals) {
                    return interval;
                }

                double nextInterval = goodIntervals.FirstOrDefault(i => i > interval);
                if (Math.Abs(nextInterval) < double.Epsilon) {
                    nextInterval = interval * 2;
                }

                interval = nextInterval;
            }
        }
    }

    public class RecorderLeftAngularAxis : LinearAxis {

        RecorderChart ParentControl;

        public RecorderLeftAngularAxis(RecorderChart parent) {
            Position = AxisPosition.Left;
            ParentControl = parent;
            IsZoomEnabled = true;
            Key = "Angular";
            PositionTier = 0;
            Title = "Position";
        }

        /// <summary>
        /// Formats the value: x is degrees.fraction.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <returns>The format value.</returns>
        public override string FormatValue(double x) {
            // string info = String.Format("{0:F1}", x);
            string info;
            if (Double.IsNaN(x)) {
                info = "0:0.0";
            } else {
                int n = (int)(x / 360.0);
                int d, s, f = (int)(x % 360.0);
                if (x >= 0) {
                    d = f;
                    s = (int)(10.0 * (x - Math.Floor(x)));
                    if (s > 9) s = 9;
                } else {
                    f = -f;
                    d = 359 - f;
                    s = (int)(10.0 * (-x - Math.Floor(-x)));
                    if (s > 9) s = 9;
                    s = 9 - s;
                }
                info = String.Format("{0}:{1}.{2}", n, d, s);
                if (n == 0 && x < 0) {
                    info = '-' + info;
                }
            }
            return info;
        }
    }

    public class RecorderLeftSignalAxis : LinearAxis {

        RecorderChart ParentControl;

        public RecorderLeftSignalAxis(RecorderChart parent) {
            Position = AxisPosition.Left;
            ParentControl = parent;
            IsZoomEnabled = true;
            Key = "Signal";
            PositionTier = 1;
            Title = "Signal";
        }

        /// <summary>
        /// Formats the value: x is +/-1.0.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <returns>The format value.</returns>
        public override string FormatValue(double x) {
            // string info = String.Format("{0:F3}", x);
            string info;
            if (Double.IsNaN(x)) {
                info = "0.0";
            } else {
                x /= 32768.0;
                info = String.Format("{0:F3}", x);
            }
            return info;
        }
    }

    public class MyAngularTrackerHitResult : TrackerHitResult {
        RecorderChart ParentControl;

        public MyAngularTrackerHitResult(RecorderChart parent, Series series, DataPoint dp, ScreenPoint sp, object item = null, double index = -1, string text = null)
            : base(series,dp,sp,item,index,text) {
            ParentControl = parent;
        }

        public override string ToString() {
            if (this.Text != null) {
                return this.Text;
            }
            return this.Series.Title + "\n"
                + "Time:" + ParentControl.BottomAxis.FormatValue(this.DataPoint.X) + "\n"
                + "Angle:" + ParentControl.LeftAngularAxis.FormatValue(this.DataPoint.Y);          
        }
    }

    public class MySignalTrackerHitResult : TrackerHitResult {
        RecorderChart ParentControl;

        public MySignalTrackerHitResult(RecorderChart parent, Series series, DataPoint dp, ScreenPoint sp, object item = null, double index = -1, string text = null)
            : base(series, dp, sp, item, index, text) {
            ParentControl = parent;
        }

        public override string ToString() {
            if (this.Text != null) {
                return this.Text;
            }
            return this.Series.Title + "\n"
                + "Time:" + ParentControl.BottomAxis.FormatValue(this.DataPoint.X) + "\n"
                + "Signal:" + ParentControl.LeftSignalAxis.FormatValue(this.DataPoint.Y);
        }
    }

    public class RecorderAngularLineSeries : LineSeries {
        RecorderChart ParentControl;
        public int SeriesIndex;
        public DataCache ChartCache;

        public RecorderAngularLineSeries(RecorderChart parent,DataCache cache, int seriesIndex) {
            ParentControl = parent;
            YAxisKey = "Angular";
            MarkerType = OxyPlot.MarkerType.Circle;
        }

        public override TrackerHitResult GetNearestPoint(ScreenPoint point, bool interpolate) {
            if (interpolate) {
                // Cannot interpolate if there is no line
                if (this.ActualColor.IsInvisible() || this.StrokeThickness.Equals(0)) {
                    return null;
                }

                if (!this.CanTrackerInterpolatePoints) {
                    return null;
                }
            }

            if (interpolate && this.Smooth && this.SmoothedPoints != null) {
                return this.MyGetNearestInterpolatedPointInternal(this.SmoothedPoints, point);
            }

            if (interpolate && !this.CanTrackerInterpolatePoints) {
                return null;
            }

            if (interpolate) {
                return this.MyGetNearestInterpolatedPointInternal(this.ActualPoints, point);
            }

            return this.MyGetNearestPointInternal(this.ActualPoints, point);
        }
        protected TrackerHitResult MyGetNearestInterpolatedPointInternal(List<DataPoint> points, ScreenPoint point) {
            if (this.XAxis == null || this.YAxis == null || points == null) {
                return null;
            }

            var spn = default(ScreenPoint);
            var dpn = default(DataPoint);
            double index = -1;

            double minimumDistance = double.MaxValue;

            for (int i = 0; i + 1 < points.Count; i++) {
                var p1 = points[i];
                var p2 = points[i + 1];
                if (!this.IsValidPoint(p1) || !this.IsValidPoint(p2)) {
                    continue;
                }

                var sp1 = this.Transform(p1);
                var sp2 = this.Transform(p2);

                // Find the nearest point on the line segment.
                var spl = ScreenPointHelper.FindPointOnLine(point, sp1, sp2);

                if (ScreenPoint.IsUndefined(spl)) {
                    // P1 && P2 coincident
                    continue;
                }

                double l2 = (point - spl).LengthSquared;

                if (l2 < minimumDistance) {
                    double segmentLength = (sp2 - sp1).Length;
                    double u = segmentLength > 0 ? (spl - sp1).Length / segmentLength : 0;
                    dpn = new DataPoint(p1.X + (u * (p2.X - p1.X)), p1.Y + (u * (p2.Y - p1.Y)));
                    spn = spl;
                    minimumDistance = l2;
                    index = i + u;
                }
            }

            if (minimumDistance < double.MaxValue) {
                object item = this.GetItem((int)index);
                return new MyAngularTrackerHitResult(ParentControl,this, dpn, spn, item) { Index = index };
            }

            return null;
        }

        /// <summary>
        /// Gets the nearest point.
        /// </summary>
        /// <param name="points">The points (data coordinates).</param>
        /// <param name="point">The point (screen coordinates).</param>
        /// <returns>A <see cref="TrackerHitResult" /> if a point was found, <c>null</c> otherwise.</returns>
        protected TrackerHitResult MyGetNearestPointInternal(IEnumerable<DataPoint> points, ScreenPoint point) {
            var spn = default(ScreenPoint);
            var dpn = default(DataPoint);
            double index = -1;

            double minimumDistance = double.MaxValue;
            int i = 0;
            foreach (DataPoint p in points) {
                if (!this.IsValidPoint(p)) {
                    i++;
                    continue;
                }

                var sp = this.XAxis.Transform(p.X, p.Y, this.YAxis);
                double d2 = (sp - point).LengthSquared;

                if (d2 < minimumDistance) {
                    dpn = p;
                    spn = sp;
                    minimumDistance = d2;
                    index = i;
                }

                i++;
            }

            if (minimumDistance < double.MaxValue) {
                object item = this.GetItem((int)index);
                return new MyAngularTrackerHitResult(ParentControl,this, dpn, spn, item) { Index = index };
            }

            return null;
        }
    }

    public class RecorderSignalLineSeries : LineSeries {
        RecorderChart ParentControl;
        public int SeriesIndex;
        public DataCache ChartCache;

        public RecorderSignalLineSeries(RecorderChart parent, DataCache cache, int seriesIndex) {
            ParentControl = parent;
            YAxisKey = "Signal";
            MarkerType = OxyPlot.MarkerType.Circle;
        }

        public override TrackerHitResult GetNearestPoint(ScreenPoint point, bool interpolate) {
            if (interpolate) {
                // Cannot interpolate if there is no line
                if (this.ActualColor.IsInvisible() || this.StrokeThickness.Equals(0)) {
                    return null;
                }

                if (!this.CanTrackerInterpolatePoints) {
                    return null;
                }
            }

            if (interpolate && this.Smooth && this.SmoothedPoints != null) {
                return this.MyGetNearestInterpolatedPointInternal(this.SmoothedPoints, point);
            }

            if (interpolate && !this.CanTrackerInterpolatePoints) {
                return null;
            }

            if (interpolate) {
                return this.MyGetNearestInterpolatedPointInternal(this.ActualPoints, point);
            }

            return this.MyGetNearestPointInternal(this.ActualPoints, point);
        }
        protected TrackerHitResult MyGetNearestInterpolatedPointInternal(List<DataPoint> points, ScreenPoint point) {
            if (this.XAxis == null || this.YAxis == null || points == null) {
                return null;
            }

            var spn = default(ScreenPoint);
            var dpn = default(DataPoint);
            double index = -1;

            double minimumDistance = double.MaxValue;

            for (int i = 0; i + 1 < points.Count; i++) {
                var p1 = points[i];
                var p2 = points[i + 1];
                if (!this.IsValidPoint(p1) || !this.IsValidPoint(p2)) {
                    continue;
                }

                var sp1 = this.Transform(p1);
                var sp2 = this.Transform(p2);

                // Find the nearest point on the line segment.
                var spl = ScreenPointHelper.FindPointOnLine(point, sp1, sp2);

                if (ScreenPoint.IsUndefined(spl)) {
                    // P1 && P2 coincident
                    continue;
                }

                double l2 = (point - spl).LengthSquared;

                if (l2 < minimumDistance) {
                    double segmentLength = (sp2 - sp1).Length;
                    double u = segmentLength > 0 ? (spl - sp1).Length / segmentLength : 0;
                    dpn = new DataPoint(p1.X + (u * (p2.X - p1.X)), p1.Y + (u * (p2.Y - p1.Y)));
                    spn = spl;
                    minimumDistance = l2;
                    index = i + u;
                }
            }

            if (minimumDistance < double.MaxValue) {
                object item = this.GetItem((int)index);
                return new MySignalTrackerHitResult(ParentControl, this, dpn, spn, item) { Index = index };
            }

            return null;
        }

        /// <summary>
        /// Gets the nearest point.
        /// </summary>
        /// <param name="points">The points (data coordinates).</param>
        /// <param name="point">The point (screen coordinates).</param>
        /// <returns>A <see cref="TrackerHitResult" /> if a point was found, <c>null</c> otherwise.</returns>
        protected TrackerHitResult MyGetNearestPointInternal(IEnumerable<DataPoint> points, ScreenPoint point) {
            var spn = default(ScreenPoint);
            var dpn = default(DataPoint);
            double index = -1;

            double minimumDistance = double.MaxValue;
            int i = 0;
            foreach (DataPoint p in points) {
                if (!this.IsValidPoint(p)) {
                    i++;
                    continue;
                }

                var sp = this.XAxis.Transform(p.X, p.Y, this.YAxis);
                double d2 = (sp - point).LengthSquared;

                if (d2 < minimumDistance) {
                    dpn = p;
                    spn = sp;
                    minimumDistance = d2;
                    index = i;
                }

                i++;
            }

            if (minimumDistance < double.MaxValue) {
                object item = this.GetItem((int)index);
                return new MySignalTrackerHitResult(ParentControl, this, dpn, spn, item) { Index = index };
            }

            return null;
        }
    }
}
