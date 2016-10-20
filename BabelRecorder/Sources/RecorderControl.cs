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

using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
//using Xceed.Wpf.Toolkit;

namespace Babel.Recorder {

    public enum RecorderActionKinds {
        RecorderActionIdle, RecorderActionPlay, RecorderActionRecord, RecorderActionNum
    };

    public enum RecorderModeKinds {
        RecorderModeLocalOnly, RecorderModeIdle,
        RecorderModeIdleStopped,
        RecorderModePlayStandby, RecorderModePlaying,
        RecorderModeRecordStandby, RecorderModeRecording,
        RecorderModePlayStopped, RecorderModeRecordStopped,
        RecorderModeNum
    };

    public partial class RecorderControl : ShellControl {

        public const String TAG = "RecorderControl";

        bool CheckAxisPending;

        public RecorderModeKinds RecorderMode;
        public RecorderModeKinds LastRecorderMode;
        public byte CurrentSystemModeId;
        public byte LastSystemModeId;
        public AxesController Controller;
        public RecorderChart RecorderScope;
        public Options OptionsTab;
        public Axes AxesTab;
        public Triggers TriggersTab;
        public JobStorage JobStorageTab;
        public Motion MotionTab;
        public List<AxisProperties> AxisOptions;
        public List<TriggerProperties> TriggerOptions;

        string[] RecorderModeMessages = { 
             "Local\nOnly", 
             "Idle", "Idle\nStopped", 
             "Play", "Playing", 
             "Record", "Recording", 
             "Play\nStopped", "Record\nStopped"
        };
        Brush[] RecorderModeBrushes = { 
            Brushes.White, 
            Brushes.Yellow, Brushes.Blue,
            Brushes.Orange, Brushes.Green,
            Brushes.Orange, Brushes.Red,
            Brushes.Blue, Brushes.Blue
        };

        /* For Status report each AxisStatus mapped to 2 bits to overlap SystemStatus:
	        SystemStateNotReady=0, // When homing etc.
	        SystemStateProblem=1,  // When error.
	        SystemStateRunning=2,  // When ok.
	        SystemStateIniting=3,  // When initing.
            8*2-bits stored in top word of status.
        */
        Brush[] AxisStateBrushes = { 
            Brushes.Orange, 
            Brushes.Red, 
            Brushes.Cyan, 
            Brushes.Orange 
        };

        Brush[] TriggerStateBrushes = { 
            Brushes.Blue, Brushes.Red
        };

        static RecorderControl() {
            // DefaultStyleKeyProperty.OverrideMetadata(typeof(ScopeControl),new FrameworkPropertyMetadata(typeof(ScopeControl)));
        }

        public override string KindName { get { return "recorder"; } }

        private void PreInit() {
            CheckAxisPending = true;
            AxisOptions = null;
            TriggerOptions = null;
            RecorderMode = RecorderModeKinds.RecorderModeLocalOnly;
            LastRecorderMode = RecorderModeKinds.RecorderModeIdle;
            CurrentSystemModeId = 0;
            LastSystemModeId = 0xff;
            OptionsTab = new Options(this);
            AxesTab = new Axes(this);
            TriggersTab = new Triggers(this);
            JobStorageTab = new JobStorage(this);
            MotionTab = new Motion(this);
            RecorderScope = new RecorderChart(this);
            Controller = new AxesController(this);
        }

        private void PostInit() {
            LinkManager.Manager.AddListener(this, ComponentEventListener);
            MotionTab.UpdateFPS();
            CheckAxesConfiguration();
            CheckTriggerConfiguration();
            MotionTab.UpdateHomeRPM();
            SetMode();
            string s = JobStorageTab.LoadRecording();
            if (s.StartsWith("Error:") || s.StartsWith("Warning:")) {
                if (!String.IsNullOrWhiteSpace(JobStorageTab.CurrentFilePath)) {
                    // Settings.BugReport(s);
                }
            }
        }

        private void InitializeControls() {
            OptionsTab.InitializeControls();
            AxesTab.InitializeControls();
            TriggersTab.InitializeControls();
            JobStorageTab.InitializeControls();
            MotionTab.InitializeControls();
            RecorderScope.InitializeControls();

            UpdateMode((byte)RecorderMode, 0);
            UpdateAxisStates();
            UpdateTriggerStates();
        }

        public RecorderControl() {
        }

        public RecorderControl(string ownerShellId)
            : base(ownerShellId) {
            PreInit();
            InitializeComponent();
            InitializeControls();
            PostInit();
        }

        public RecorderControl(string ownerShellId, XmlNode node)
            : base(ownerShellId) {
            PreInit();
            InitializeComponent();
            InitializeControls();
            Serializer(node, false);
            PostInit();
        }

        // On entry, node is the shell node.
        public override void Serializer(XmlNode node, bool isSerialize) {
            base.Serializer(node, isSerialize);
            XmlNode c = Project.GetChildNode(node, "recorder");
            if (c == null) return;
            // Serialize tabs in reverse order because of setup dependencies.
            OptionsTab.SerializeOptions(c, isSerialize);
            AxesTab.SerializeAxes(c, isSerialize);
            TriggersTab.SerializeTriggers(c, isSerialize);           
            JobStorageTab.SerializeJobStorage(c, isSerialize);
            MotionTab.SerializeMotion(c, isSerialize);
        }

        public override void Close() {
            LinkManager.Manager.RemoveListener(this, ComponentEventListener);
            if (RecorderScope != null) {
                RecorderScope.Close();
                RecorderScope = null;
            }
            if (Controller != null) {
                Controller.Close();
                Controller = null;
            }
            base.Close();
        }

        public void ComponentEventListener(ComponentEvent ev, Component component, object val) {
            if (ComponentEvent.ComponentAdd == ev || ComponentEvent.ComponentResume == ev) {
                AxesTab.UpdateDeviceIds();
                Controller.UpdateDevicePorts(component as LinkDevice);
            }
        }

        public void CheckAxesConfiguration() {
            // Check if Axis options have changed.
            List<AxisProperties> newAxisOptions = AxisProperties.CollectProperties(this);
            if (!AxisProperties.PartialCompareProperties(AxisOptions, newAxisOptions)) {
                JobStorageTab.ClearTitle();
                Controller.SetupAxesConfiguration(newAxisOptions);
                MotionTab.UpdateAxisInfoState();
                RecorderScope.SetupSeriesConfiguration();
                Controller.IssueSetStopsCommand();
                Controller.IssueISOPVTMonitorSwitchOff();
                Controller.CheckMonitoringActive(true);
            }
        }

        public void CheckTriggerConfiguration() {
            // Check if Trigger options have changed.
            List<TriggerProperties> newTriggerOptions = TriggerProperties.CollectProperties(this);
            if (!TriggerProperties.PartialCompareProperties(TriggerOptions, newTriggerOptions)) {
                Controller.SetupTriggersConfiguration(newTriggerOptions);
                MotionTab.UpdateTriggerInfoState();
            }
        }

        void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            TabControl tc = sender as TabControl;
            if (tc != null) {
                TabItem ti = tc.SelectedItem as TabItem;
                if (ti != null) {
                    if ((ti.Header.ToString() == "Devices")
                        || (ti.Header.ToString() == "Axes")
                        || (ti.Header.ToString() == "Triggers")) {
                        CheckAxisPending = true;
                    } else if (CheckAxisPending) {
                        CheckAxesConfiguration();
                        CheckTriggerConfiguration(); // Do this after CheckAxesConfiguration.
                        CheckAxisPending = false;
                    }
                }
            }
        }

        public static string FormatPositionValue(double x) {
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

        public uint ConvertFPSToMilliseconds(uint fps) {
            if (RecorderScope != null) return RecorderScope.ConvertFPSToMilliseconds(fps);
            return 0;
        }

        // Set the mode of the controller to the currently selected Recorder Action.
        public bool SetMode() {
            int index = RecorderAction.SelectedIndex;
            if (index < 0 || index >= (int)RecorderActionKinds.RecorderActionNum) {
                index = 0;
                RecorderAction.SelectedIndex = index;
            }
            RecorderActionKinds action = (RecorderActionKinds)index;
            if (Controller != null && (Controller.AxesReady || (action == RecorderActionKinds.RecorderActionIdle))) {
                // Convert RecorderAction {Idle,Play,Record} to RecorderModeKind.
                RecorderModeKinds mode = RecorderModeKinds.RecorderModeIdle;
                switch (action) {
                    case RecorderActionKinds.RecorderActionPlay:
                        mode = RecorderModeKinds.RecorderModePlayStandby;
                        CurrentSystemModeId = 0;
                        break;
                    case RecorderActionKinds.RecorderActionRecord:
                        mode = RecorderModeKinds.RecorderModeRecordStandby;
                        CurrentSystemModeId = 0;
                        break;
                    default:
                        mode = RecorderModeKinds.RecorderModeIdle;
                        ++CurrentSystemModeId;
                        if (CurrentSystemModeId == 0) CurrentSystemModeId = 1;
                        break;                    
                }
                // Send SetMode message, and let response set mode.
                Controller.IssueSetModeCommandToAll((byte)mode, CurrentSystemModeId);
                return true;
            }
            return false;
        }

        // Here is where mode actions are taken.
        public void UpdateMode(byte mode, byte id) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                if (Controller != null && !IsClosing && mode < (byte)RecorderModeKinds.RecorderModeNum) {
                    RecorderMode = (RecorderModeKinds)mode;
                    if (LastRecorderMode == RecorderMode) {
                        if ((RecorderMode != RecorderModeKinds.RecorderModeLocalOnly)
                           && (RecorderMode != RecorderModeKinds.RecorderModeIdle)
                            && (RecorderMode != RecorderModeKinds.RecorderModeIdleStopped)
                            ) return;
                        if (id == LastSystemModeId) return;
                    }
                    LastSystemModeId = id;
                    lock (Controller.SchedulerLock) {
                        Controller.PlayIsSignalled = false;
                        Controller.RecordIsSignalled = false;
                        switch (RecorderMode) {
                            case RecorderModeKinds.RecorderModeLocalOnly:
                            case RecorderModeKinds.RecorderModeIdle:
                                // Switch chart back to normal mode.
                                // Reset chart cache.
                                RecorderScope.ResetCache();
                                RecorderScope.ResetSeries();
                                Controller.ResetTriggers();
                                // Reset timeline?
                                break;
                            case RecorderModeKinds.RecorderModePlayStandby:
                                // Copy pvt cache to chart cache setting actual pos to zero.
                                // Show static chart of current pvtCache.
                                Controller.CurrentPlayPVTCacheIndex = 0;
                                Controller.LastChartCacheIndex = 0;
                                Controller.LastChartCacheTime = 0.0;
                                Controller.LastStoppedTime = 0.0;
                                RecorderScope.ResetCache();
                                RecorderScope.ResetSeries();
                                RecorderScope.ChartCache.DeepCopy(Controller.PVTCache);
                                RecorderScope.ChartCache.ZeroElements(1, AxesController.FIELDS_PER_AXIS); // time,actual,target.
                                RecorderScope.ChartCache.RefreshMinMax();
                                RecorderScope.RenewChart();
                                Controller.ResetTriggers();
                                break;
                            case RecorderModeKinds.RecorderModePlaying:
                                // Start playback.
                                // TODO: unless resuming from a pause.
                                Controller.PlayIsSignalled = true;
                                break;
                            case RecorderModeKinds.RecorderModeRecordStandby:
                                // Reset chart cache & pvt cache?
                                // Reset timeline?
                                Controller.CurrentRecordStartTime = 0.0;
                                RecorderScope.ResetCache();
                                RecorderScope.ResetSeries();
                                Controller.PVTCache.Reset();
                                Controller.ResetTriggers();
                                break;
                            case RecorderModeKinds.RecorderModeRecording:
                                Controller.RecordIsSignalled = true;
                                // Make sure iso sending is active.
                                Controller.CheckMonitoringActive(true);
                                break;
                            case RecorderModeKinds.RecorderModeIdleStopped:
                                break;
                            case RecorderModeKinds.RecorderModePlayStopped:
                                // Rewind CurrentPlayPVTCacheIndex to LastStoppedTime.
                                Controller.RewindCurrentPlayPVTCacheIndex();
                                break;
                            case RecorderModeKinds.RecorderModeRecordStopped:
                                Controller.CurrentRecordStartTime = Controller.LastStoppedTime;
                                break;
                        }
                    }
                    LastRecorderMode = RecorderMode;
                    RecorderStatus.Text = RecorderModeMessages[(int)mode];
                    RecorderStatus.Background = RecorderModeBrushes[(int)mode];
                }
            } else {
                Application.Current.Dispatcher.Invoke((Action)(() => { UpdateMode(mode, id); }));
            }
        }

        public void UpdateTriggerState(TriggerProperties tp) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                if (!IsClosing) {
                    Button b = this.FindName("TriggerState" + tp.TriggerId.ToString()) as Button;
                    if (b != null) {
                        b.Background = TriggerStateBrushes[tp.IsOn ? 1 : 0];
                    }
                }
            } else {
                Application.Current.Dispatcher.Invoke((Action)(() => { UpdateTriggerState(tp); }));
            }
        }

        public void UpdateTriggerStates() {
            if (Application.Current.Dispatcher.CheckAccess()) {
                if (!IsClosing && TriggerOptions != null) {
                    foreach (TriggerProperties tp in TriggerOptions) {
                        Button b = this.FindName("TriggerState" + tp.TriggerId.ToString()) as Button;
                        if (b != null) {
                            b.Background = TriggerStateBrushes[tp.IsOn ? 1 : 0];
                        }
                    }
                }
            } else {
                Application.Current.Dispatcher.Invoke((Action)(() => { UpdateTriggerStates(); }));
            }
        }

        public void UpdateAxisState(AxisPortProperties ap) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                if (!IsClosing) {
                    for (int k = 0; k < ap.AxesOnPort.Count; k++) {
                        Button b = this.FindName("AxisState" + ap.AxesOnPort[k].AxisId.ToString()) as Button;
                        if (b != null) {
                            b.Background = AxisStateBrushes[(int)ap.AxesOnPort[k].AxisState];
                        }
                    }
                }
            } else {
                Application.Current.Dispatcher.Invoke((Action)(() => { UpdateAxisState(ap); }));
            }
        }

        public void UpdateAxisStates() {
            if (Application.Current.Dispatcher.CheckAccess()) {
                if (!IsClosing && Controller != null && Controller.AxisPorts != null) {
                    lock (Controller.ControllerLock) {
                        foreach (AxisPortProperties ap in Controller.AxisPorts) {
                            for (int k = 0; k < ap.AxesOnPort.Count; k++) {
                                Button b = this.FindName("AxisState" + ap.AxesOnPort[k].AxisId.ToString()) as Button;
                                if (b != null) {
                                    b.Background = AxisStateBrushes[(int)ap.AxesOnPort[k].AxisState];
                                }
                            }
                        }
                    }
                }
            } else {
                Application.Current.Dispatcher.Invoke((Action)(() => { UpdateAxisStates(); }));
            }
        }

        public void UpdateAxisPosition(int axisNum, double position) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                TextBlock tb = this.FindName("AxisPosition" + axisNum) as TextBlock;
                if (tb != null) {
                    tb.Text = FormatPositionValue(position);
                }
            } else {
                Application.Current.Dispatcher.Invoke((Action)(() => { UpdateAxisPosition(axisNum, position); }));
            }
        }

        // On entry, node is the shell node.
        [ShellDeserializer("recorder")]
        public static ShellControl RecorderShellDeserializer(string shellId, XmlNode node) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                return new RecorderControl(shellId, node);
            }
            return (ShellControl)Application.Current.Dispatcher.Invoke(
                    new Func<ShellControl>(() => { return RecorderShellDeserializer(shellId, node); })
                );
        }

        public static string OpenRecorderShell() {
            if (Application.Current.Dispatcher.CheckAccess()) {
                Shell shell = Shell.OpenShell("Recorder", 600, 480, -1, -1, 2);           
                string shellId = shell.ShellId;
                if (shell.MainControl == null) {
                    if (!shell.SetControl(new RecorderControl(shellId))) {
                        return "Error: unable to create shell recorder control.";
                    }
                }
                return shellId;
            }
            return (string)Application.Current.Dispatcher.Invoke(
                    new Func<string>(() => { return OpenRecorderShell(); })
                );
        }

        [ScriptFunction("recorder", "Opens recorder view. Returns recorder Id.",
            typeof(Jint.Delegates.Func<String>))]
        public static string RecorderView() {
            string id = RecorderControl.OpenRecorderShell();
            Shell s = Shell.GetShell(id);
            if (s != null) {
                RecorderControl p = s.MainControl as RecorderControl;
                if (p != null) {
                    return id;
                }
                s.Close();
                return "Error: unable to open recorder control.";
            }
            return "Error: unable to open recoder shell.";
        }



        [ScriptFunction("dumpchart", "Dumps chart cache.",
            typeof(Jint.Delegates.Func<String, String, String>),
            "Recorder id", "Data file name.")]
        public static string DumpChartCache(string recorderId, string dataFileName) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                if (!String.IsNullOrWhiteSpace(dataFileName)) {
                    Shell s = Shell.GetShell(recorderId);
                    if (s != null) {
                        RecorderControl p = s.MainControl as RecorderControl;
                        if (p != null) {
                            string header = "";
                            return p.RecorderScope.ChartCache.DumpToFile(dataFileName, true, header);
                        }
                    }
                }
                return "Error: bad file name.";
            }
            return (string)Application.Current.Dispatcher.Invoke(
                    new Func<String>(() => { return DumpChartCache(recorderId, dataFileName); })
                );
        }
    }
}


