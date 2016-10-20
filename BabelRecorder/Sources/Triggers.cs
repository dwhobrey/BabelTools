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
using Babel.BabelProtocol;

using Xceed.Wpf.Toolkit;

namespace Babel.Recorder {

    public class Triggers {

        RecorderControl Recorder;

        public Triggers(RecorderControl recorder) {
            Recorder = recorder;
        }

        public void InitializeControls() {
            Recorder.TriggerTestButton1.Click += new RoutedEventHandler(TestButton_Click);
            Recorder.TriggerTestButton2.Click += new RoutedEventHandler(TestButton_Click);
            Recorder.TriggerTestButton3.Click += new RoutedEventHandler(TestButton_Click);
            Recorder.TriggerTestButton4.Click += new RoutedEventHandler(TestButton_Click);
        }

        public void SerializeTriggers(XmlNode node, bool isSerialize) {
            XmlNode triggers = Project.GetChildNode(node, "triggers");
            if (triggers == null) return;
            int k;
            for (k = 1; k <= 4; k++) {
                string id = k.ToString();
                XmlNode a = Project.GetChildNode(triggers, "trigger", id);
                if (a != null) {
                    CheckBox cbe = Recorder.FindName("TriggerEnable" + id) as CheckBox;
                    CheckBox cbahi = Recorder.FindName("TriggerActiveHighInput" + id) as CheckBox;
                    CheckBox cbaho = Recorder.FindName("TriggerActiveHighOutput" + id) as CheckBox;
                    CheckBox cbpi = Recorder.FindName("TriggerPlayInput" + id) as CheckBox;
                    CheckBox cbpo = Recorder.FindName("TriggerPlayOutput" + id) as CheckBox;
                    CheckBox cbri = Recorder.FindName("TriggerRecordInput" + id) as CheckBox;
                    CheckBox cbro = Recorder.FindName("TriggerRecordOutput" + id) as CheckBox;
                    CheckBox cbsi = Recorder.FindName("TriggerSignalInput" + id) as CheckBox;
                    CheckBox cbso = Recorder.FindName("TriggerSignalOutput" + id) as CheckBox;
                    IntegerUpDown iudds = Recorder.FindName("TriggerDelaySeconds" + id) as IntegerUpDown;
                    IntegerUpDown iuddf = Recorder.FindName("TriggerDelayFrames" + id) as IntegerUpDown;
                    IntegerUpDown iudidm = Recorder.FindName("TriggerInputDeviceId" + id) as IntegerUpDown;
                    IntegerUpDown iudibm = Recorder.FindName("TriggerInputBitMask" + id) as IntegerUpDown;
                    IntegerUpDown iudodm = Recorder.FindName("TriggerOutputDeviceMask" + id) as IntegerUpDown;
                    IntegerUpDown iudobm = Recorder.FindName("TriggerOutputBitMask" + id) as IntegerUpDown;
                    DecimalUpDown dudl = Recorder.FindName("TriggerPulseLength" + id) as DecimalUpDown;
                    DecimalUpDown dudc = Recorder.FindName("TriggerPulseCycle" + id) as DecimalUpDown;
                    IntegerUpDown iudr = Recorder.FindName("TriggerRepeat" + id) as IntegerUpDown;
                    TextBox tb = Recorder.FindName("TriggerDescription" + id) as TextBox;
                    if (isSerialize) {
                        Project.SetNodeAttributeValue(a, "enable", (cbe == null) ? "false" : cbe.IsChecked.Value.ToString());
                        Project.SetNodeAttributeValue(a, "activehighinput", (cbahi == null) ? "false" : cbahi.IsChecked.Value.ToString());
                        Project.SetNodeAttributeValue(a, "activehighoutput", (cbaho == null) ? "false" : cbaho.IsChecked.Value.ToString());
                        Project.SetNodeAttributeValue(a, "playinput", (cbpi == null) ? "false" : cbpi.IsChecked.Value.ToString());
                        Project.SetNodeAttributeValue(a, "playoutput", (cbpo == null) ? "false" : cbpo.IsChecked.Value.ToString());
                        Project.SetNodeAttributeValue(a, "recordinput", (cbri == null) ? "false" : cbri.IsChecked.Value.ToString());
                        Project.SetNodeAttributeValue(a, "recordoutput", (cbro == null) ? "false" : cbro.IsChecked.Value.ToString());
                        Project.SetNodeAttributeValue(a, "signalinput", (cbsi == null) ? "false" : cbsi.IsChecked.Value.ToString());
                        Project.SetNodeAttributeValue(a, "signaloutput", (cbso == null) ? "false" : cbso.IsChecked.Value.ToString());
                        Project.SetNodeAttributeValue(a, "inputdeviceid", (iudidm == null) ? "1" : iudidm.Text);
                        Project.SetNodeAttributeValue(a, "inputbitmask", (iudibm == null) ? "1" : iudibm.Text);
                        Project.SetNodeAttributeValue(a, "outputdevicemask", (iudodm == null) ? "1" : iudodm.Text);
                        Project.SetNodeAttributeValue(a, "outputbitmask", (iudobm == null) ? "1" : iudobm.Text);
                        Project.SetNodeAttributeValue(a, "delayseconds", (iudds == null) ? "0" : iudds.Text);
                        Project.SetNodeAttributeValue(a, "delayframes", (iuddf == null) ? "0" : iuddf.Text);
                        Project.SetNodeAttributeValue(a, "pulselength", (dudl == null) ? "1.0" : dudl.Text);
                        Project.SetNodeAttributeValue(a, "repeat", (iudr == null) ? "1" : iudr.Text);
                        Project.SetNodeAttributeValue(a, "cycle", (dudc == null) ? "0.0" : dudc.Text);
                        Project.SetNodeAttributeValue(a, "description", (tb == null) ? id : tb.Text);
                    } else {
                        bool result = false;
                        Boolean.TryParse(Project.GetNodeAttributeValue(a, "enable", "false"), out result);
                        if (cbe != null) cbe.IsChecked = result;
                        Boolean.TryParse(Project.GetNodeAttributeValue(a, "activehighinput", "false"), out result);
                        if (cbahi != null) cbahi.IsChecked = result;
                        Boolean.TryParse(Project.GetNodeAttributeValue(a, "activehighoutput", "false"), out result);
                        if (cbaho != null) cbaho.IsChecked = result;
                        Boolean.TryParse(Project.GetNodeAttributeValue(a, "playinput", "false"), out result);
                        if (cbpi != null) cbpi.IsChecked = result;
                        Boolean.TryParse(Project.GetNodeAttributeValue(a, "playoutput", "false"), out result);
                        if (cbpo != null) cbpo.IsChecked = result;
                        Boolean.TryParse(Project.GetNodeAttributeValue(a, "recordinput", "false"), out result);
                        if (cbri != null) cbri.IsChecked = result;
                        Boolean.TryParse(Project.GetNodeAttributeValue(a, "recordoutput", "false"), out result);
                        if (cbro != null) cbro.IsChecked = result;
                        Boolean.TryParse(Project.GetNodeAttributeValue(a, "signalinput", "false"), out result);
                        if (cbsi != null) cbsi.IsChecked = result;
                        Boolean.TryParse(Project.GetNodeAttributeValue(a, "signaloutput", "false"), out result);
                        if (cbso != null) cbso.IsChecked = result;
                        if (iudidm != null) iudidm.Text = Project.GetNodeAttributeValue(a, "inputdeviceid", "1");
                        if (iudibm != null) iudibm.Text = Project.GetNodeAttributeValue(a, "inputbitmask", "1");
                        if (iudodm != null) iudodm.Text = Project.GetNodeAttributeValue(a, "outputdevicemask", "1");
                        if (iudobm != null) iudobm.Text = Project.GetNodeAttributeValue(a, "outputbitmask", "1");
                        if (iudds != null) iudds.Text = Project.GetNodeAttributeValue(a, "delayseconds", "0");
                        if (iuddf != null) iuddf.Text = Project.GetNodeAttributeValue(a, "delayframes", "0");
                        if (dudl != null) dudl.Text = Project.GetNodeAttributeValue(a, "pulselength", "1.0");
                        if (iudr != null) iudr.Text = Project.GetNodeAttributeValue(a, "repeat", "1");
                        if (dudc != null) dudc.Text = Project.GetNodeAttributeValue(a, "cycle", "0.0");
                        if (tb != null) tb.Text = Project.GetNodeAttributeValue(a, "description", id);
                    }
                }
            }
        }
        void TestButton_Click(object sender, RoutedEventArgs e) {
            // TODO Test trigger.
        }
    }
}
