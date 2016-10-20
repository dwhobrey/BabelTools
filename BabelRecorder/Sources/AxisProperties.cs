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
using Babel.XLink;
using Babel.BabelProtocol;

using Xceed.Wpf.Toolkit;

namespace Babel.Recorder {

    public enum AxisKinds {
        Angular, Linear
    };

    public enum AxisStateKinds {
        AxisStateNotReady,
        AxisStateProblem,
        AxisStateRunning,
        AxisStateIniting,
        AxisStateNum
    };

    public class AxisProperties {

        public bool Active;
        public bool Show;
        public AxisKinds AxisKind;
        public int AxisId;
        public int ParamIndex;
        public int DeviceNumber;
        public int DeviceRank;
        public float MinusStop;
        public float PlusStop;
        public string DeviceId;
        public string Description;
        // Internal.
        public AxisStateKinds AxisState;
        public RecorderAngularLineSeries ActualSeries;
        public RecorderSignalLineSeries TargetSeries;
        public int SeriesIndex;
        public int DataCacheOffset;
        public int DataPVTOffset;
        public int AxisPortPropertiesIndex;

        public AxisProperties() {
            Active = false;
            Show = false;
            AxisKind = AxisKinds.Angular;
            AxisId = 0;
            SeriesIndex = 0;
            ParamIndex = 0;
            DeviceNumber = 0;
            DeviceRank = 0;
            MinusStop = 0.0f;
            MinusStop = 0.0f;
            DeviceId = "None";
            Description = "Axis 0";
            AxisState = AxisStateKinds.AxisStateNotReady;
            ActualSeries = null;
            TargetSeries = null;
            DataCacheOffset = 0;
            DataPVTOffset = 0;
            AxisPortPropertiesIndex = 0;
        }

        // Returns true if partially equal.
        // Does not compare Active or Show flags, or internal.
        public bool PartialCompare(AxisProperties a) {
            return AxisKind == a.AxisKind
                && AxisId == a.AxisId
                && ParamIndex == a.ParamIndex
                && DeviceNumber == a.DeviceNumber
                && DeviceRank == a.DeviceRank
                && MinusStop == a.MinusStop
                && PlusStop == a.PlusStop
                && DeviceId == a.DeviceId
                && Description == a.Description;
        }

        public bool WeakPartialCompare(AxisProperties a) {
            return AxisKind == a.AxisKind
                && AxisId == a.AxisId
                && ParamIndex == a.ParamIndex
                && DeviceNumber == a.DeviceNumber
                && DeviceRank == a.DeviceRank;
        }

        // Returns true if equal.
        // Does not compare internal.
        public bool Equals(AxisProperties a) {
            return Active == a.Active
                && Show == a.Show
                && AxisKind == a.AxisKind
                && AxisId == a.AxisId
                && ParamIndex == a.ParamIndex
                && DeviceNumber == a.DeviceNumber
                && DeviceRank == a.DeviceRank
                && MinusStop == a.MinusStop
                && PlusStop == a.PlusStop
                && DeviceId == a.DeviceId
                && Description == a.Description;
        }

        // Compares properties for differences using PartialCompare.
        // Also returns false if lists differ in length.
        // Returns true if partially equal.
        public static bool PartialCompareProperties(List<AxisProperties> a, List<AxisProperties> b) {
            if (a == null || b == null) return false;
            int num = a.Count;
            if (num != b.Count) return false;
            for (int k = 0; k < num; k++) {
                if (!a[k].PartialCompare(b[k])) return false;
            }
            return true;
        }

        public static bool WeakPartialCompareProperties(List<AxisProperties> a, List<AxisProperties> b) {
            if (a == null || b == null) return false;
            int num = a.Count;
            if (num != b.Count) return false;
            for (int k = 0; k < num; k++) {
                if (!a[k].WeakPartialCompare(b[k])) return false;
            }
            return true;
        }

        // Simply iterates over list for element with given id.
        // Returns element or null if not found.
        public static AxisProperties FindAxis(List<AxisProperties> ap, int axisId) {
            if (ap != null) foreach (AxisProperties a in ap) {
                    if (a.AxisId == axisId) return a;
                }
            return null;
        }

        public static AxisProperties FindAxis(List<AxisProperties> ap, string name) {
            if (ap != null && !String.IsNullOrWhiteSpace(name)) {
                int index = 0;
                while (index < name.Length && !Char.IsDigit(name[index])) ++index;
                if (index < name.Length) {
                    string id = name.Substring(index);
                    int k = 0;
                    if (int.TryParse(id, out k)) {
                        if (k > 0 && k <= 8) {
                            return FindAxis(ap, k);
                        }
                    }
                }
            }
            return null;
        }

        public static string ConvertToHeader(List<AxisProperties> ap) {
            string h = "time";
            foreach (AxisProperties a in ap) {
                h += ","
                    + a.AxisId
                    + ":" + a.AxisKind.ToString()
                    + ":" + a.ParamIndex
                    + ":" + a.DeviceNumber
                    + ":" + a.DeviceRank
                    ;
            }
            return h;
        }

        public static List<AxisProperties> ConvertToProperties(string header) {
            List<AxisProperties> ap = new List<AxisProperties>();
            char[] axesSeps = { ',' };
            char[] propsSeps = { ':' };
            bool isFirst = true;
            if (!String.IsNullOrWhiteSpace(header)) {
                string[] axesAry = header.Split(axesSeps);
                foreach (string s in axesAry) {
                    if (isFirst) { // Skip time field.
                        isFirst = false;
                        continue;
                    }
                    AxisProperties p = new AxisProperties();
                    if (!String.IsNullOrWhiteSpace(s)) {
                        string[] propsAry = s.Split(propsSeps);
                        int v, k = 0, n = propsAry.Length;
                        if (k < n) {
                            if (int.TryParse(propsAry[k++], out v))
                                p.AxisId = v;
                        }
                        if (k < n) {
                            try {
                                p.AxisKind = (AxisKinds)Enum.Parse(typeof(AxisKinds), propsAry[k++]);
                            } catch (Exception) {
                                p.AxisKind = AxisKinds.Angular;
                            }
                        }
                        if (k < n) {
                            if (int.TryParse(propsAry[k++], out v))
                                p.ParamIndex = v;
                        }
                        if (k < n) {
                            if (int.TryParse(propsAry[k++], out v))
                                p.DeviceNumber = v;
                        }
                        if (k < n) {
                            if (int.TryParse(propsAry[k++], out v))
                                p.DeviceRank = v;
                        }
                        p.Description = "Axis " + p.AxisId;

                    }
                    ap.Add(p);
                }
            }
            return ap;
        }

        // Collects together properties of enabled and valid axes.
        public static List<AxisProperties> CollectProperties(RecorderControl recorder) {
            int k, n, i;
            List<string> devices = new List<string>();
            for (k = 1; k <= 4; k++) {
                string id = k.ToString();
                ComboBox di = recorder.FindName("DeviceId" + id) as ComboBox;
                if (di != null && !String.IsNullOrWhiteSpace(di.Text)) devices.Add(di.Text);
                else devices.Add("None");
            }
            List<AxisProperties> properties = new List<AxisProperties>();
            for (k = 1; k <= 8; k++) {
                string id = k.ToString();
                CheckBox cba = recorder.FindName("AxisActive" + id) as CheckBox;
                CheckBox cbs = recorder.FindName("AxisShow" + id) as CheckBox;
                CheckBox cbe = recorder.FindName("AxisEnable" + id) as CheckBox;
                ComboBox cb = recorder.FindName("AxisKind" + id) as ComboBox;
                TextBox tb = recorder.FindName("AxisDescription" + id) as TextBox;
                IntegerUpDown iudad = recorder.FindName("AxisDevice" + id) as IntegerUpDown;
                IntegerUpDown iud = recorder.FindName("AxisIndex" + id) as IntegerUpDown;
                IntegerUpDown iudh = recorder.FindName("AxisDeviceRank" + id) as IntegerUpDown;
                IntegerUpDown iudma = recorder.FindName("AxisMinusStopMajor" + id) as IntegerUpDown;
                IntegerUpDown iudmb = recorder.FindName("AxisMinusStopMinor" + id) as IntegerUpDown;
                IntegerUpDown iudpa = recorder.FindName("AxisPlusStopMajor" + id) as IntegerUpDown;
                IntegerUpDown iudpb = recorder.FindName("AxisPlusStopMinor" + id) as IntegerUpDown;
                if (cbe == null || !cbe.IsChecked.Value) continue;
                n = (iudad == null) ? 1 : iudad.Value.Value;
                i = (iud == null) ? 0 : iud.Value.Value;
                if (i < 1 || n < 1 || n > 4 || devices[n - 1].Equals("None")) continue;
                /* Debug: could have multiple axes on same device.
                bool inUse = false;
                for (int j = 0; j < properties.Count; j++) {
                    if (properties[j].DeviceNumber == n) {
                        inUse = true;
                        break;
                    }
                }
                if (inUse) continue;
                */
                AxisProperties ap = new AxisProperties();
                ap.DeviceNumber = n;
                ap.DeviceId = devices[n - 1];
                ap.ParamIndex = i;
                ap.DeviceRank = (iudh == null) ? 0 : iudh.Value.Value;
                ap.AxisId = k;
                ap.SeriesIndex = 2 * (k - 1);
                ap.Show = (cbs == null) ? false : cbs.IsChecked.Value;
                ap.Active = (cba == null) ? false : cba.IsChecked.Value;
                try {
                    if (cb == null || String.IsNullOrWhiteSpace(cb.Text)) ap.AxisKind = AxisKinds.Angular;
                    else ap.AxisKind = (AxisKinds)Enum.Parse(typeof(AxisKinds), cb.Text);
                } catch (Exception) {
                    ap.AxisKind = AxisKinds.Angular;
                }
                int minusmajor = (iudma == null) ? 0 : iudma.Value.Value;
                int minusminor = (iudmb == null) ? 0 : iudmb.Value.Value;
                int plusmajor = (iudpa == null) ? 0 : iudpa.Value.Value;
                int plusminor = (iudpb == null) ? 0 : iudpb.Value.Value;
                if (ap.AxisKind == AxisKinds.Angular) {
                    ap.MinusStop = minusmajor + ((float)minusminor / 360.0f);
                    ap.PlusStop = plusmajor + ((float)plusminor / 360.0f);
                } else {
                    ap.MinusStop = minusmajor + ((float)minusminor / 1000.0f);
                    ap.PlusStop = plusmajor + ((float)plusminor / 1000.0f);
                }
                string s = tb.Text;
                if (String.IsNullOrWhiteSpace(s)) s = "Axis " + id;
                ap.Description = s;
                properties.Add(ap);
            }
            return properties;
        }
    }
}
