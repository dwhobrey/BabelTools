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

    public enum TriggerStateKinds {
        TriggerStateSilent,
        TriggerStateFired,
        TriggerStateNum
    };

    public class TriggerProperties {

        public bool ActiveHighInput;
        public bool ActiveHighOutput;
        public bool PlayInput;
        public bool PlayOutput;
        public bool RecordInput;
        public bool RecordOutput;
        public bool SignalInput;
        public bool SignalOutput;
        public uint DelaySeconds;
        public uint DelayFrames;
        public int InputDeviceId;
        public int InputBitMask;
        public int OutputDeviceMask;
        public int OutputBitMask;
        public float PulseLength;
        public float PulseCycle;
        public int PulseRepeat;
        public string Description;

        public int TriggerId;
        // Internal.
        public bool InputEnabled;
        public bool OutputEnabled;
        public bool IsOn;
        public bool IsPrimedOnly;
        public int NumLeft;
        public uint ActionTime;
        public AxisPortProperties AxisPort;

        public TriggerProperties() {

            ActiveHighInput = false;
            ActiveHighOutput = false;
            PlayInput = false;
            PlayOutput = false;
            RecordInput = false;
            RecordOutput = false;
            SignalInput = false;
            SignalOutput = false;
            DelaySeconds = 0;
            DelayFrames = 0;
            InputDeviceId = 0;
            InputBitMask = 0;
            OutputDeviceMask = 0;
            OutputBitMask = 0;
            PulseLength = 0.0f;
            PulseCycle = 0.0f;
            PulseRepeat = 0;
            Description = "Trigger 0";
            TriggerId = 0;
            //
            InputEnabled = false;
            OutputEnabled = false;
            IsOn = false;
            IsPrimedOnly = false;
            NumLeft = 0;
            ActionTime = 0;
            AxisPort = null;
        }

        // Returns true if partially equal.
        // Does not compare internal.
        public bool PartialCompare(TriggerProperties t) {
            return ActiveHighInput == t.ActiveHighInput
                && ActiveHighOutput == t.ActiveHighOutput
                && PlayInput == t.PlayInput
                && PlayOutput == t.PlayOutput
                && RecordInput == t.RecordInput
                && RecordOutput == t.RecordOutput
                && SignalInput == t.SignalInput
                && SignalOutput == t.SignalOutput
                && DelaySeconds == t.DelaySeconds
                && DelayFrames == t.DelayFrames
                && InputDeviceId == t.InputDeviceId
                && InputBitMask == t.InputBitMask
                && OutputDeviceMask == t.OutputDeviceMask
                && OutputBitMask == t.OutputBitMask
                && PulseLength == t.PulseLength
                && PulseCycle == t.PulseCycle
                && PulseRepeat == t.PulseRepeat
                && TriggerId == t.TriggerId;
        }

        // Returns true if equal.
        // Does not compare internal.
        public bool Equals(TriggerProperties t) {
            return PartialCompare(t)
                && Description == t.Description;
        }

        // Compares properties for differences using PartialCompare.
        // Also returns false if lists differ in length.
        // Returns true if partially equal.
        public static bool PartialCompareProperties(List<TriggerProperties> a, List<TriggerProperties> b) {
            if (a == null || b == null) return false;
            int num = a.Count;
            if (num != b.Count) return false;
            for (int k = 0; k < num; k++) {
                if (!a[k].PartialCompare(b[k])) return false;
            }
            return true;
        }

        // Simply iterates over list for element with given id.
        // Returns element or null if not found.
        public static TriggerProperties FindTrigger(List<TriggerProperties> tp, int triggerId) {
            if (tp != null) foreach (TriggerProperties t in tp) {
                    if (t.TriggerId == triggerId) return t;
                }
            return null;
        }

        public static TriggerProperties FindTrigger(List<TriggerProperties> tp, string name) {
            if (tp != null && !String.IsNullOrWhiteSpace(name)) {
                int index = 0;
                while (index < name.Length && !Char.IsDigit(name[index])) ++index;
                if (index < name.Length) {
                    string id = name.Substring(index);
                    int k = 0;
                    if (int.TryParse(id, out k)) {
                        if (k > 0 && k <= 8) {
                            return FindTrigger(tp, k);
                        }
                    }
                }
            }
            return null;
        }

        // Collects together properties of enabled and valid triggers.
        public static List<TriggerProperties> CollectProperties(RecorderControl recorder) {
            int k;
            List<TriggerProperties> properties = new List<TriggerProperties>();
            for (k = 1; k <= 4; k++) {
                string id = k.ToString();
                CheckBox cbe = recorder.FindName("TriggerEnable" + id) as CheckBox;
                CheckBox cbahi = recorder.FindName("TriggerActiveHighInput" + id) as CheckBox;
                CheckBox cbaho = recorder.FindName("TriggerActiveHighOutput" + id) as CheckBox;
                CheckBox cbpi = recorder.FindName("TriggerPlayInput" + id) as CheckBox;
                CheckBox cbpo = recorder.FindName("TriggerPlayOutput" + id) as CheckBox;
                CheckBox cbri = recorder.FindName("TriggerRecordInput" + id) as CheckBox;
                CheckBox cbro = recorder.FindName("TriggerRecordOutput" + id) as CheckBox;
                CheckBox cbsi = recorder.FindName("TriggerSignalInput" + id) as CheckBox;
                CheckBox cbso = recorder.FindName("TriggerSignalOutput" + id) as CheckBox;
                IntegerUpDown iudds = recorder.FindName("TriggerDelaySeconds" + id) as IntegerUpDown;
                IntegerUpDown iuddf = recorder.FindName("TriggerDelayFrames" + id) as IntegerUpDown;
                IntegerUpDown iudidm = recorder.FindName("TriggerInputDeviceId" + id) as IntegerUpDown;
                IntegerUpDown iudibm = recorder.FindName("TriggerInputBitMask" + id) as IntegerUpDown;
                IntegerUpDown iudodm = recorder.FindName("TriggerOutputDeviceMask" + id) as IntegerUpDown;
                IntegerUpDown iudobm = recorder.FindName("TriggerOutputBitMask" + id) as IntegerUpDown;
                DecimalUpDown dudl = recorder.FindName("TriggerPulseLength" + id) as DecimalUpDown;
                DecimalUpDown dudc = recorder.FindName("TriggerPulseCycle" + id) as DecimalUpDown;
                IntegerUpDown iudr = recorder.FindName("TriggerRepeat" + id) as IntegerUpDown;
                TextBox tb = recorder.FindName("TriggerDescription" + id) as TextBox;
                //
                if (cbe == null || !cbe.IsChecked.Value) continue;
                TriggerProperties tp = new TriggerProperties();
                tp.TriggerId = k;
                tp.ActiveHighInput = (cbahi == null) ? false : cbahi.IsChecked.Value;
                tp.ActiveHighOutput = (cbaho == null) ? false : cbaho.IsChecked.Value;
                tp.PlayInput = (cbpi == null) ? false : cbpi.IsChecked.Value;
                tp.PlayOutput = (cbpo == null) ? false : cbpo.IsChecked.Value;
                tp.RecordInput = (cbri == null) ? false : cbri.IsChecked.Value;
                tp.RecordOutput = (cbro == null) ? false : cbro.IsChecked.Value;
                tp.SignalInput = (cbsi == null) ? false : cbsi.IsChecked.Value;
                tp.SignalOutput = (cbso == null) ? false : cbso.IsChecked.Value;
                tp.DelaySeconds = (uint)((iudds == null) ? 0 : iudds.Value.Value);
                tp.DelayFrames = (uint)((iuddf == null) ? 0 : iuddf.Value.Value);
                tp.InputDeviceId = (iudidm == null) ? 0 : iudidm.Value.Value;
                tp.InputBitMask = (iudibm == null) ? 0 : iudibm.Value.Value;
                tp.OutputDeviceMask = (iudodm == null) ? 0 : iudodm.Value.Value;
                tp.OutputBitMask = (iudobm == null) ? 0 : iudobm.Value.Value;
                tp.PulseLength = (dudl == null) ? 0.0f : (float)(dudl.Value.Value);
                tp.PulseCycle = (dudc == null) ? 0.0f : (float)(dudc.Value.Value);
                tp.PulseRepeat = (iudr == null) ? 0 : iudr.Value.Value;
                string s = tb.Text;
                if (String.IsNullOrWhiteSpace(s)) s = "Trigger " + id;
                tp.Description = s;
                tp.InputEnabled = tp.PlayInput || tp.RecordInput || tp.SignalInput;
                tp.OutputEnabled = tp.PlayOutput || tp.RecordOutput || tp.SignalOutput;
                if (tp.SignalInput) {
                    foreach (AxisPortProperties app in recorder.Controller.AxisPorts) {
                        if (app.DeviceNumber == tp.InputDeviceId) {
                            tp.AxisPort = app;
                            break;
                        }
                    }
                    if (tp.AxisPort == null) continue;
                }
                if (tp.InputEnabled &&tp.OutputEnabled) // Note: must have defined an input & output.
                    properties.Add(tp);
            }
            return properties;
        }
    }
}

