using System;
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

    public class Motion {

        public const string TAG = "Motion";

        RecorderControl Recorder;
        private AxesController Controller;

        public Motion(RecorderControl recorder) {
            Recorder = recorder;
        }

        public void InitializeControls() {
            Controller = Recorder.Controller;
            Recorder.RecorderAction.SelectionChanged += new SelectionChangedEventHandler(RecorderAction_SelectionChanged);
            Recorder.RecorderAction.SelectedIndex = 0;
            Recorder.FramesPerSecond.SelectionChanged += new SelectionChangedEventHandler(FramesPerSecond_SelectionChanged);
            Recorder.FramesPerSecond.SelectedIndex = 0;
            Recorder.GotoRPM.ValueChanged += new RoutedPropertyChangedEventHandler<object>(HomeRPM_ValueChanged);

            Recorder.ZeroButton.Click += new RoutedEventHandler(Zero_Click);
            Recorder.HomeButton.Click += new RoutedEventHandler(Home_Click);

            Recorder.ReverseTimeLineButton.Click += new RoutedEventHandler(Reverse_TimeLine_Click);
            Recorder.PlayTimeLineButton.Click += new RoutedEventHandler(Play_TimeLine_Click);
            Recorder.RecordTimeLineButton.Click += new RoutedEventHandler(Record_TimeLine_Click);
            Recorder.PauseTimeLineButton.Click += new RoutedEventHandler(Pause_TimeLine_Click);
            Recorder.StopTimeLineButton.Click += new RoutedEventHandler(Stop_TimeLine_Click);
            Recorder.GotoButton.Click += new RoutedEventHandler(Goto_Click);
            Recorder.PrimeTimeLineButton.Click += new RoutedEventHandler(Prime_TimeLine_Click);

            Recorder.LockButton.Click += new RoutedEventHandler(Lock_Click);

            for (int k = 1; k <= 8; k++) {
                CheckBox c;
                c = Recorder.FindName("AxisShow" + k.ToString()) as CheckBox;
                if (c != null) {
                    c.Checked += new RoutedEventHandler(AxisShow_CheckBox);
                    c.Unchecked += new RoutedEventHandler(AxisShow_CheckBox);
                }
                c = Recorder.FindName("AxisActive" + k.ToString()) as CheckBox;
                if (c != null) {
                    c.Checked += new RoutedEventHandler(AxisActive_CheckBox);
                    c.Unchecked += new RoutedEventHandler(AxisActive_CheckBox);
                }
                Button b = Recorder.FindName("AxisState" + k.ToString()) as Button;
                if (b != null) {
                    b.Click += new RoutedEventHandler(AxisState_Click);
                }
            }
            for (int k = 1; k <= 4; k++) {
                Button b = Recorder.FindName("TriggerState" + k.ToString()) as Button;
                if (b != null) {
                    b.Click += new RoutedEventHandler(TriggerState_Click);
                }
            }
        }

        public void SerializeMotion(XmlNode node, bool isSerialize) {
            XmlNode axes = Project.GetChildNode(node, "axes");
            if (axes == null) return;
            int k;
            for (k = 1; k <= 8; k++) {
                string id = k.ToString();
                XmlNode a = Project.GetChildNode(axes, "axis", id);
                if (a != null) {
                    CheckBox cbe = Recorder.FindName("AxisActive" + id) as CheckBox;
                    CheckBox cbs = Recorder.FindName("AxisShow" + id) as CheckBox;
                    if (isSerialize) {
                        Project.SetNodeAttributeValue(a, "active", (cbe == null) ? "true" : cbe.IsChecked.Value.ToString());
                        Project.SetNodeAttributeValue(a, "show", (cbs == null) ? "true" : cbs.IsChecked.Value.ToString());
                    } else {
                        bool result = false;
                        Boolean.TryParse(Project.GetNodeAttributeValue(a, "active", "false"), out result);
                        if (cbe != null) cbe.IsChecked = result;
                        Boolean.TryParse(Project.GetNodeAttributeValue(a, "show", "false"), out result);
                        if (cbs != null) cbs.IsChecked = result;
                    }
                }
            }
            DecimalUpDown dudg = Recorder.FindName("TimeLineGoto") as DecimalUpDown;
            DecimalUpDown duds = Recorder.FindName("TimeLineSpeed") as DecimalUpDown;
            DecimalUpDown dudr = Recorder.FindName("GotoRPM") as DecimalUpDown;
            Slider s = Recorder.FindName("TimeLineSlider") as Slider;
            TextBox hp = Recorder.FindName("GotoPosition") as TextBox;
            ComboBox zd = Recorder.FindName("ZoomDirection") as ComboBox;
            ComboBox sm = Recorder.FindName("RecorderAction") as ComboBox;
            ComboBox fps = Recorder.FindName("FramesPerSecond") as ComboBox;
            if (isSerialize) {
                Project.SetNodeAttributeValue(axes, "goto", (dudg == null) ? "0.0" : dudg.Text);
                Project.SetNodeAttributeValue(axes, "speed", (duds == null) ? "1.0" : duds.Text);
                Project.SetNodeAttributeValue(axes, "rpm", (dudr == null) ? "50.0" : dudr.Text);
                Project.SetNodeAttributeValue(axes, "slider", (s == null) ? "0.0" : s.Value.ToString());
                Project.SetNodeAttributeValue(axes, "gotoposition", (hp == null) ? "0.0" : hp.Text);
                Project.SetNodeAttributeValue(axes, "zoomdirection", (zd == null) ? "X axis" : zd.Text);
                Project.SetNodeAttributeValue(axes, "recorderaction", (sm == null) ? "Idle" : sm.Text);
                Project.SetNodeAttributeValue(axes, "framespersecond", (fps == null) ? "24" : fps.Text);
            } else {
                if (dudg != null) dudg.Text = Project.GetNodeAttributeValue(axes, "goto", "0.0");
                if (dudr != null) dudr.Text = Project.GetNodeAttributeValue(axes, "rpm", "50.0");
                if (duds != null) duds.Text = Project.GetNodeAttributeValue(axes, "speed", "1.0");
                if (s != null) {
                    string v = Project.GetNodeAttributeValue(axes, "slider", "0.0");
                    double n = 0;
                    Double.TryParse(v, out n);
                    if (n < 0.0) n = 0.0;
                    if (n > 1.0) n = 1.0;
                    s.Value = n;
                }
                if (hp != null) hp.Text = Project.GetNodeAttributeValue(axes, "gotoposition", "0.0");
                if (zd != null) zd.Text = Project.GetNodeAttributeValue(axes, "zoomdirection", "X axis");
                if (sm != null) sm.Text = Project.GetNodeAttributeValue(axes, "recorderaction", "Idle");
                if (fps != null) fps.Text = Project.GetNodeAttributeValue(axes, "framespersecond", "24");
            }
        }


        public int GetHomeRPM() {
            DecimalUpDown dudr = Recorder.FindName("GotoRPM") as DecimalUpDown;
            if (dudr != null) return (int)dudr.Value.Value;
            return 50;
        }

        public float GetGotoPosition() {
            DecimalUpDown dudr = Recorder.FindName("GotoPosition") as DecimalUpDown;
            if (dudr != null) return (float)dudr.Value.Value;
            return (float)0.0;
        }

        public void UpdateTriggerInfoState() {
            for (int k = 1; k <= 4; k++) {
                TriggerProperties tp = TriggerProperties.FindTrigger(Recorder.TriggerOptions, k);
                string id = k.ToString();
                Button b = Recorder.FindName("TriggerState" + id) as Button;
                if (b != null) {
                    b.Visibility = ((tp == null) ? Visibility.Collapsed : Visibility.Visible);
                }
            }
        }

        void TriggerState_Click(object sender, RoutedEventArgs e) {
            Button b = sender as Button;
            if (b != null) {
                TriggerProperties tp = TriggerProperties.FindTrigger(Recorder.TriggerOptions, b.Name);
                if (tp != null) {
                    //Toggle trigger state. Send to all devices in deviceMask.
                    if(Controller!=null)
                        Controller.ToggleTriggerOutput(tp);
                }
            }
        }

        public void UpdateAxisInfoState() {
            for (int k = 1; k <= 8; k++) {
                AxisProperties ap = AxisProperties.FindAxis(Recorder.AxisOptions, k);
                string id = k.ToString();
                StackPanel sp = Recorder.FindName("AxisInfo" + id) as StackPanel;
                if (sp != null) {
                    sp.Visibility = ((ap == null) ? Visibility.Collapsed : Visibility.Visible);
                }
            }
        }

        void AxisState_Click(object sender, RoutedEventArgs e) {
            Button b = sender as Button;
            if (b != null) {
                AxisProperties ap = AxisProperties.FindAxis(Recorder.AxisOptions, b.Name);
                if (ap != null) {
                    Controller.IssueCommand(ap.AxisPortPropertiesIndex, AxesController.AXIS_CMD_RESET, 1, 
                                (byte)ap.DeviceRank, 0);
                }
            }
        }

        void AxisShow_CheckBox(object sender, RoutedEventArgs e) {
            CheckBox c = sender as CheckBox;
            if (c != null) {
                AxisProperties ap = AxisProperties.FindAxis(Recorder.AxisOptions, c.Name);
                if (ap != null) {
                    ap.Show = (c.IsChecked == true);
                    Recorder.RecorderScope.SetAxisVisibility(ap);
                }
            }
        }
        void AxisActive_CheckBox(object sender, RoutedEventArgs e) {
            CheckBox c = sender as CheckBox;
            if (c != null) {
                AxisProperties ap = AxisProperties.FindAxis(Recorder.AxisOptions, c.Name);
                if (ap != null) {
                    ap.Active = (c.IsChecked == true);
                }
            }
        }

        // This just sets the desired next action.
        // Pressing Prime will activate this mode.
        void RecorderAction_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (Recorder.RecorderAction.SelectedIndex < 0) {
                Recorder.RecorderAction.SelectedIndex = 0;
            }
        }

        public void UpdateFPS() {
            if (Recorder.FramesPerSecond.SelectedIndex < 0) {
                Recorder.FramesPerSecond.SelectedIndex = 0;
            }
            Recorder.RecorderScope.UpdateFPS((TimeLineKinds)Recorder.FramesPerSecond.SelectedIndex);
        }

        void FramesPerSecond_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (Recorder.FramesPerSecond.SelectedIndex < 0) {
                Recorder.FramesPerSecond.SelectedIndex = 0;
            }
            UpdateFPS();
        }

        // Prime Axis Controller for next action.
        void Prime_TimeLine_Click(object sender, RoutedEventArgs e) {
            if (!Recorder.SetMode()) {
                Settings.BugReport("Axes not ready.");
            }
        }

        void Reverse_TimeLine_Click(object sender, RoutedEventArgs e) {
            if (Controller != null && Controller.AxesReady) {
                //TODO Reverse timeline.
            } else {
                Settings.BugReport("Axes not ready.");
            }
        }
        void Play_TimeLine_Click(object sender, RoutedEventArgs e) {
            if (Controller != null && Controller.AxesReady 
                    && (
                    (Recorder.RecorderMode == RecorderModeKinds.RecorderModePlayStandby)
                    || (Recorder.RecorderMode == RecorderModeKinds.RecorderModePlayStopped)
                    )
                ) {
                Controller.IssueSetModeCommandToAll((byte)RecorderModeKinds.RecorderModePlaying, 0);
            } else {
                Settings.BugReport("Axes not primed.");
            }
        }
        void Record_TimeLine_Click(object sender, RoutedEventArgs e) {
            if (Controller != null && Controller.AxesReady 
                    && (
                    (Recorder.RecorderMode == RecorderModeKinds.RecorderModeRecordStandby)
                    || (Recorder.RecorderMode == RecorderModeKinds.RecorderModeRecordStopped)
                    )
                ) {
                Controller.IssueSetModeCommandToAll((byte)RecorderModeKinds.RecorderModeRecording, 0);
            } else {
                Settings.BugReport("Axes not primed.");
            }

        }
        void Stop_TimeLine_Click(object sender, RoutedEventArgs e) {
            if (Controller != null) {
                Controller.IssueStopAction();
            }
        }
        void Pause_TimeLine_Click(object sender, RoutedEventArgs e) {
            // TODO Pause timeline.
            Stop_TimeLine_Click(sender, e);
        }
        void Lock_Click(object sender, RoutedEventArgs e) {
            LockBox a = new LockBox(Recorder.LockPassword.Text);
            a.Show();
        }
        void Goto_Click(object sender, RoutedEventArgs e) {
            if (Controller != null && Controller.AxesReady) {
                if (Recorder.RecorderMode == RecorderModeKinds.RecorderModeIdle) {
                    int pos = (int)GetGotoPosition();
                    Controller.IssueCommandToAll(AxesController.AXIS_CMD_GOTO, 2, (byte)0xff, pos);
                } else {
                    Settings.BugReport("Must be in Idle mode to Goto.");
                }
            } else {
                Settings.BugReport("Axes not ready.");
            }
        }
        void Home_Click(object sender, RoutedEventArgs e) {
            if (Controller != null && Controller.AxesReady) {
                if (Recorder.RecorderMode == RecorderModeKinds.RecorderModeIdle) {
                    Controller.IssueCommandToAll(AxesController.AXIS_CMD_HOME, 2, (byte)0xff, 0);
                } else {
                    Settings.BugReport("Must be in Idle mode to Home.");
                }
            } else {
                Settings.BugReport("Axes not ready.");
            }
        }
        public void UpdateHomeRPM() {     
            if (Application.Current.Dispatcher.CheckAccess()) {
                if (Controller != null) {
                    int rpm = GetHomeRPM();
                    Controller.IssueCommandToAll(AxesController.AXIS_CMD_HOME_RPM, 2, (byte)0xff, rpm);
                }
            } else {
                Application.Current.Dispatcher.Invoke((Action)(() => { UpdateHomeRPM(); }));
            }
        }
        void HomeRPM_ValueChanged(object sender, RoutedEventArgs e) {
            UpdateHomeRPM();
        }
        void Zero_Click(object sender, RoutedEventArgs e) {
            if (Controller != null && Controller.AxesReady) {
                if (Recorder.RecorderMode == RecorderModeKinds.RecorderModeIdle) {
                    Controller.IssueCommandToAll(AxesController.AXIS_CMD_ZERO, 1, (byte)0xff, 0);
                } else {
                    Settings.BugReport("Must be in Idle mode to Zero.");
                }
            } else {
                Settings.BugReport("Axes not ready.");
            }
        }
    }
}
