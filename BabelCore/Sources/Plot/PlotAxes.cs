using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Xml;
using System.Windows.Controls;

using Babel.Resources;

using System.ComponentModel;
using System.Diagnostics;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace Babel.Core {

    public class BottomLinearAxis : LinearAxis {

        PlotControl ParentControl;

        public BottomLinearAxis(PlotControl parent) {
            Position = AxisPosition.Bottom;
            ParentControl = parent;
            IsZoomEnabled = false;
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
            this.DataMaximum = this.DataMinimum = this.ActualMaximum = this.ActualMinimum = double.NaN;
            Reset();
        }

    }

    public class LeftLinearAxis : LinearAxis {

        PlotControl ParentControl;

        public LeftLinearAxis(PlotControl parent) {
            Position = AxisPosition.Left;
            ParentControl = parent;
            IsZoomEnabled = true;
        }

    }

    public class MapLineSeries : LineSeries {
        public bool Normalise;
        public bool AutoNormalise;
        public int SeriesIndex;
        public double Offset;
        public double Scale;
        public double MinValue;
        public double MaxValue;
        public double RangeDiff;
        public DataCache Cache;

        public MapLineSeries(DataCache cache,int seriesIndex) {
            Cache = cache;
            SeriesIndex = seriesIndex;
            Normalise = false;
            AutoNormalise = false;
            this.MarkerType = OxyPlot.MarkerType.Circle;
            Offset = 0.0;
            Scale = 1.0;
            ResetMinMax();
        }

        public void ResetMinMax() {
            MinValue = 0.0;
            MaxValue = 1.0;
            RangeDiff = 1.0;
        }

        public void RefreshMinMax() {
            ResetMinMax();
        }

        public void SetNormalise(bool isOn, double min = 0.0, double max = 0.0) {
            Normalise = isOn;
            if (isOn) {
                MinValue = min;
                MaxValue = max;
                RangeDiff = max - min;
                if (RangeDiff == 0.0) RangeDiff = 1.0;
            }
        }

        public double Map(double v) {
            if (AutoNormalise) {
                v -= Cache.MinValues[SeriesIndex];
                v /= Cache.RangeValues[SeriesIndex];
            } else {
                v -= MinValue;
                v /= RangeDiff;
            }
            return Offset + (Scale * v);
        }

        public double Inverse(double v) {
            v -= Offset;
            if(Scale!=0.0) v /= Scale;
            if (AutoNormalise) {
                v *= Cache.RangeValues[SeriesIndex];
                v += Cache.MinValues[SeriesIndex];
            } else  if (Normalise) {
                v *= RangeDiff;
                v += MinValue;
            }
            return v;
        }
    }
}
