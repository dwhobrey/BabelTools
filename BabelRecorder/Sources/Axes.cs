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

    public class Axes {

        RecorderControl Recorder;

        public Axes(RecorderControl recorder) {
            Recorder = recorder;
        }

        public void InitializeControls() {
            UpdateDeviceIds();
        }

        public void PostInit() {
        }

        public void SerializeAxes(XmlNode node, bool isSerialize) {
            XmlNode axes = Project.GetChildNode(node, "axes");
            if (axes == null) return;
            int k;
            for (k = 1; k <= 4; k++) {
                string id = k.ToString();
                XmlNode a = Project.GetChildNode(axes, "device", id);
                if (a != null) {
                    ComboBox di = Recorder.FindName("DeviceId" + id) as ComboBox;
                    if (isSerialize) {
                        Project.SetNodeAttributeValue(a, "id", (di == null) ? "None" : di.Text);
                    } else {
                        if (di != null) di.Text = Project.GetNodeAttributeValue(a, "id", "None");
                    }
                }
            }
            for (k = 1; k <= 8; k++) {
                string id = k.ToString();
                XmlNode a = Project.GetChildNode(axes, "axis", id);
                if (a != null) {
                    CheckBox cbe = Recorder.FindName("AxisEnable" + id) as CheckBox;
                    ComboBox cb = Recorder.FindName("AxisKind" + id) as ComboBox;
                    IntegerUpDown iudad = Recorder.FindName("AxisDevice" + id) as IntegerUpDown;
                    IntegerUpDown iud = Recorder.FindName("AxisIndex" + id) as IntegerUpDown;
                    IntegerUpDown iudh = Recorder.FindName("AxisDeviceRank" + id) as IntegerUpDown;
                    IntegerUpDown iudma = Recorder.FindName("AxisMinusStopMajor" + id) as IntegerUpDown;
                    IntegerUpDown iudmb = Recorder.FindName("AxisMinusStopMinor" + id) as IntegerUpDown;
                    IntegerUpDown iudpa = Recorder.FindName("AxisPlusStopMajor" + id) as IntegerUpDown;
                    IntegerUpDown iudpb = Recorder.FindName("AxisPlusStopMinor" + id) as IntegerUpDown;
                    TextBox tb = Recorder.FindName("AxisDescription" + id) as TextBox;
                    if (isSerialize) {
                        Project.SetNodeAttributeValue(a, "enable", (cbe == null) ? "false" : cbe.IsChecked.Value.ToString());
                        Project.SetNodeAttributeValue(a, "kind", (cb == null) ? "Angular" : cb.Text);
                        Project.SetNodeAttributeValue(a, "device", (iudad == null) ? "1" : iudad.Text);
                        Project.SetNodeAttributeValue(a, "index", (iud == null) ? ("36") : iud.Text);
                        Project.SetNodeAttributeValue(a, "rank", (iudh == null) ? "0" : iudh.Text);
                        Project.SetNodeAttributeValue(a, "minusstopmajor", (iudma == null) ? "0" : iudma.Text);
                        Project.SetNodeAttributeValue(a, "minusstopminor", (iudmb == null) ? "0" : iudmb.Text);
                        Project.SetNodeAttributeValue(a, "plusstopmajor", (iudpa == null) ? "0" : iudpa.Text);
                        Project.SetNodeAttributeValue(a, "plusstopminor", (iudpb == null) ? "0" : iudpb.Text);
                        Project.SetNodeAttributeValue(a, "description", (tb == null) ? id : tb.Text);
                    } else {
                        bool result = false;
                        Boolean.TryParse(Project.GetNodeAttributeValue(a, "enable", "false"), out result);
                        if (cbe != null) cbe.IsChecked = result;
                        if (cb != null) cb.Text = Project.GetNodeAttributeValue(a, "kind", "Angular");
                        if (iudad != null) iudad.Text = Project.GetNodeAttributeValue(a, "device", "1");
                        if (iud != null) iud.Text = Project.GetNodeAttributeValue(a, "index", ("36"));
                        if (iudh != null) iudh.Text = Project.GetNodeAttributeValue(a, "rank", "0");

                        if (iudma != null) iudma.Text = Project.GetNodeAttributeValue(a, "minusstopmajor", "0");
                        if (iudmb != null) iudmb.Text = Project.GetNodeAttributeValue(a, "minusstopminor", "0");
                        if (iudpa != null) iudpa.Text = Project.GetNodeAttributeValue(a, "plusstopmajor", "0");
                        if (iudpb != null) iudpb.Text = Project.GetNodeAttributeValue(a, "description", "0");

                        if (tb != null) tb.Text = Project.GetNodeAttributeValue(a, "description", id);
                    }
                }
            }
        }

        public void UpdateDeviceIds() {
            if (Application.Current.Dispatcher.CheckAccess()) {
                List<string> currentDeviceIds = LinkManager.Manager.GetDeviceIds();
                for (int k = 1; k <= 4; k++) {
                    string id = k.ToString();
                    ComboBox di = Recorder.FindName("DeviceId" + id) as ComboBox;
                    if (di != null) {
                        di.Items.Clear();
                        di.Items.Add("None");
                        foreach (string d in currentDeviceIds) {
                            di.Items.Add(d);
                        }
                    }
                }
            } else {
                Application.Current.Dispatcher.Invoke((Action)(() => { UpdateDeviceIds(); }));
            }
        }
    }
}
