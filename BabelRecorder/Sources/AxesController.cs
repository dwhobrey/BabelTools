using System;
using System.Threading;
using System.Collections.Generic;

using Babel.Core;
using Babel.XLink;
using Babel.BabelProtocol;

namespace Babel.Recorder {

    public class AxesController : MessageExchange.IMessageHandler {
        #region // Constants.
        public const String TAG = "AxesController";

        public const byte AXIS_CMD_MODE = 40; // Set mode: (cmd,uint8 mode,id).
        public const byte AXIS_CMD_STATUS_REPORT = 41; // Returns (PVTQSize,SystemMode,SystemId,SystemStatus,InputTriggers).	
        public const byte AXIS_CMD_POS_REPORT = 42; // Returns axes positions: (AVKs,(float|int16)+).
        public const byte AXIS_CMD_RESET = 50; // Reset axes: (cmd,uint8 0xff=all,N=axis).
        public const byte AXIS_CMD_HOME = 51; // Home axes: (uint8 0xff=all,N=axis,int homePos). 
        public const byte AXIS_CMD_GOTO = 52; // Home axes: (uint8 0xff=all,N=axis,int homePos). 
        public const byte AXIS_CMD_ZERO = 53; // Zero axes: (cmd,uint8 0xff=all,N=axis).
        public const byte AXIS_CMD_SET_STOPS = 54; // Set end stops: (cmd,{float plusStop,float minusStop}+).
        public const byte AXIS_CMD_HOME_RPM = 55; // Set homing rpm: (cmd,uint8 0xff=all, N=axis, int rpm).
        public const byte AXIS_CMD_TRIGGER_ENABLE = 60; // Set/get global enable flag for triggers: (cmd,uint8 triggersOn).
        public const byte AXIS_CMD_TRIGGER_SET = 61; // Set triggers: (cmd,{uint8 triggerIndex,uint32 msDelay,uint32 msPulseLength,uint16 repeatNum,uint8 activeHigh}+).
        public const byte AXIS_CMD_TRIGGER_FIRE = 62; // Schedule triggers for immediate firing: (cmd,{uint8 triggerIndex}+).
        public const byte AXIS_CMD_TRIGGER_OUTPUT = 63; // Write directly to trigger output register: (cmd,uint8 outputValue).
        public const byte AXIS_CMD_PVT_SUBMIT = 70; // Submit PVT for multiple axes: (cmd, uint32 relativeTime, {float position}+).
        public const byte AXIS_CMD_PVT_REPORT = 71; // A relay of a PVT submission.
        public const byte AXIS_CMD_BINARY_TX = 80; // send binary
        public const byte AXIS_CMD_BINARY_RX = 81; // get binary

        public const byte AxisValueNotSet=0;
        public const byte AxisValueVelocity=1;
        public const byte AxisValuePosition=2;
        public const byte AxisValueAngle = 3;

        public const int AXIS_PVT_FLAGS_POS	= 0x1; // <<(2*axisIndex) Flag set indicates PVT data contains position parameter.
        public const int AXIS_PVT_FLAGS_VEL	= 0x2; // <<(2*axisIndex) Flag set indicates PVT data contains velocity parameter.
        public const int AXIS_PVT_FLAGS_TIME = 0x4000; // Flag set indicates PVT data contains time parameter.
        public const int AXIS_PVT_FLAGS_INT = 0x8000; // Flag set indicates PVT data type is int16 rather than float.
        public const int AXIS_PVT_FREQUENCY	= 20; // Number of PVT updates per second.
        public const uint AXIS_PVT_PERIOD_INTERVAL =	(1000/AXIS_PVT_FREQUENCY); //ms, time between PVTs.
        public const uint RPM_UPDATE_TIMEOUT_INTERVAL = 3000; //ms, Timeout for updating rpm.
        public const uint STATUS_REPORT_TIMEOUT_INTERVAL = 2000; //ms, Timeout for heart beat from controller.
        public const uint STATUS_REPORT_ISO_INTERVAL = 500; //ms, Time betweem reports.
        public const uint TRIGGER_REPORT_TIMEOUT_INTERVAL = 610; //ms, Time between updating trigger display states.
        public const uint TRIGGER_ISO_INTERVAL = 25; //ms, Time between monitoring for input triggers.
        public const uint PVT_MONITORING_TIMEOUT_INTERVAL = 500; //ms, Timeout for checking Iso PVTs from controller.
        public const uint PVT_ISO_INTERVAL = 40; //ms, Time between sending PVTs, > 20 Hz, i.e. < 50ms.
        public const uint PVT_SEND_TIMEOUT_INTERVAL = 10; //ms, must be << 50ms.
        public const int PVT_MAX_TIME_DEVIATION = 10; //ms, PVT's whose time deviates less than this are merged together.

        public const int ISO_ID_TRG = 11;
        public const int ISO_ID_STA = 12;

        public const int PARAMETER_INDEX_INPUT_TRIGGERS = 8;
        public const int PARAMETER_INDEX_PVT_MONITORING = 12;
        public const int FIELDS_PER_AXIS = 2; // (actual position, target value).
        #endregion
        #region // Member vars.

        bool IsClosing;
        bool CheckInputTriggers;
        bool CheckOutputTriggers;
        public bool PlayIsSignalled;
        public bool RecordIsSignalled;
        public int CurrentPlayPVTCacheIndex;
        public int LastChartCacheIndex;
        public double LastChartCacheTime;
        public double LastStoppedTime;
        public double CurrentRecordStartTime;

        public uint PVTStartTime; // Time when started play/record.
        public uint PVTPerformTime;
        public uint PVTRelativeTime;
        public uint PVTLastRelativeTime;

        ProtocolHub Hub;
        RecorderControl Recorder;
        Thread MessageReplyHandlerTask;
        Thread SchedulerTask;
        public DataCache PVTCache;
        public List<AxisPortProperties> AxisPorts;
        int[] PortNoToAxisPortPropertiesIndex;
        public Object ControllerLock;
        public Object SchedulerLock;
        public Object TriggerLock;
        #endregion
        public AxesController(RecorderControl recorder) {
            Recorder = recorder;
            IsClosing = false;
            CheckInputTriggers = false;
            CheckOutputTriggers = false;
            PlayIsSignalled = false;
            RecordIsSignalled = false;
            CurrentPlayPVTCacheIndex = 0;
            LastChartCacheIndex = 0;
            LastChartCacheTime = 0.0;
            CurrentRecordStartTime = 0.0;
            LastStoppedTime = 0.0;
            PVTStartTime = 0;
            PVTPerformTime = 0;
            PVTRelativeTime = 0;
            PVTLastRelativeTime = 0;
            Hub = null;
            PVTCache = new DataCache(recorder.ShellId, false, true, true, 0);
            AxisPorts = new List<AxisPortProperties>();
            PortNoToAxisPortPropertiesIndex = new int[16];
            ControllerLock = new Object();
            SchedulerLock = new Object();
            TriggerLock = new Object();

            if (String.IsNullOrWhiteSpace(ProtocolCommands.BOpen(recorder.ShellId, recorder.ShellId))) {
                ProtocolCommands.Commander.Exchanges.TryGetValue(recorder.ShellId, out Hub);
            }

            MessageReplyHandlerTask = new Thread(new ThreadStart(MessageReplyHandler));
            MessageReplyHandlerTask.Name = "AxesControllerReplyHandlerThread:" + recorder.ShellId;
            MessageReplyHandlerTask.Priority = ThreadPriority.AboveNormal;
            MessageReplyHandlerTask.Start();

            SchedulerTask = new Thread(new ThreadStart(Scheduler));
            SchedulerTask.Name = "AxesControllerSchedulerThread:" + recorder.ShellId;
            SchedulerTask.Priority = ThreadPriority.AboveNormal;
            SchedulerTask.Start();
        }

        public void Reset() {
        }

        public void ResetCache() {
            if (PVTCache != null) {
                PVTCache.Reset();
            }
        }

        public void Close() {
            IsClosing = true;
            if (MessageReplyHandlerTask != null) {
                Primitives.Interrupt(MessageReplyHandlerTask);
                MessageReplyHandlerTask = null;
            }
            if (SchedulerTask != null) {
                Primitives.Interrupt(SchedulerTask);
                SchedulerTask = null;
            }
            int numAxisPorts = AxisPorts.Count;
            for (int k = 0; k < numAxisPorts; k++) {
                try {
                    AxisPortProperties ap = AxisPorts[k];
                    MainWindow.UpdateStatus("Recorder#" + Recorder.ShellId + ":" + ap.AxisPortPropertiesIndex, null);
                } catch (Exception) {
                }
            }
            Reset();
        }

        public ProtocolHub GetHub {
            get {
                if (Hub == null) {
                    Log.w(TAG, "Unable to open exchange.");
                }
                if (Hub != null) {
                    if (Hub.Exchange == null) {
                        Hub = null;
                        return null;
                    }
                    if (Hub.Exchange.IsClosingState()) {
                        Hub = null;
                        AxisPorts.Clear();
                    }
                }
                return Hub;
            }
        }

        public LinkDriver GetPort(int axisPortPropertiesIndex) {
            if (AxisPorts[axisPortPropertiesIndex].Port == null) {
                ProtocolHub h = GetHub;
                if (h != null) {
                    MessageExchange m = h.Exchange;
                    if (m != null) {
                        NetIfManager pm = m.Manager;
                        if (pm != null) {
                            byte netIfIndex = AxisPorts[axisPortPropertiesIndex].NetIfIndex;
                            if (netIfIndex != 0)
                                AxisPorts[axisPortPropertiesIndex].Port = pm.GetLinkDriver(netIfIndex);
                        }
                    }
                }
            }
            return AxisPorts[axisPortPropertiesIndex].Port;
        }

        public ComponentState GetPortState(int axisPortPropertiesIndex) {
            LinkDriver lp = GetPort(axisPortPropertiesIndex);
            ComponentState cs = (lp == null) ? ComponentState.NotConfigured : lp.GetComponentState();
            if (cs != AxisPorts[axisPortPropertiesIndex].CurrentComponentState) {
                AxisPorts[axisPortPropertiesIndex].CurrentComponentState = cs;
            }
            return cs;
        }

        public bool PortOkToSend(int axisPortPropertiesIndex) {
            if (GetPortState(axisPortPropertiesIndex) == ComponentState.Working) {
                LinkDriver lp = GetPort(axisPortPropertiesIndex);
                return (lp != null && lp.GetWriteQueueSize() < 20);
            }
            return false;
        }

        public bool AxesReady {
            get {
                for (int k = 0; k < AxisPorts.Count; k++) {
                    if (!PortOkToSend(k)) return false;
                }
                return true;
            }
        }

        // Sets up a port for new devices that match the axis deviceIds.
        // Called when a device is connected or resumed.
        public void UpdateDevicePorts(LinkDevice d) {
            if (d != null) {
                // See if device id is in AxisPorts.
                lock (ControllerLock) {
                    foreach (AxisPortProperties ap in AxisPorts) {
                        if (ap.DeviceId != null && ap.DeviceId.Equals(d.Id)) {
                            if (Hub != null && Hub.Exchange.Manager.GetLinkDriver(d.Id) == null) {
                                if (Hub.Exchange.AddListenerNetIf(d, ap.NetIfIndex)) {
                                    // Let GetPort cache port.
                                }
                            }
                            break;
                        }
                    }
                }
            }
        }

        public void RefreshDevicePorts() {
            List<LinkDevice> devs = LinkManager.Manager.GetDevices(null);
            foreach (LinkDevice d in devs) {
                UpdateDevicePorts(d);
            }
        }

        // Sets up the necessary ports.
        // Clears old ports.
        public void SetupAxesConfiguration(List<AxisProperties> newAxisOptions) {
            ResetCache();
            if (Hub != null) {
                lock (ControllerLock) {
                    int k;
                    // First, build new AxisPort list.
                    List<AxisPortProperties> newAxisPorts = new List<AxisPortProperties>();
                    foreach (AxisProperties a in newAxisOptions) {
                        bool wasFound = false;
                        foreach (AxisPortProperties p in newAxisPorts) {
                            if (p.DeviceId.Equals(a.DeviceId)) {
                                wasFound = true;
                                p.AxesOnPort.Add(a);
                                break;
                            }
                        }
                        if (!wasFound) {
                            AxisPortProperties ap = new AxisPortProperties();
                            newAxisPorts.Add(ap);
                            ap.DeviceNumber = a.DeviceNumber;
                            ap.DeviceId = a.DeviceId;
                            ap.AxesOnPort.Add(a);
                        }
                    }
                    // Second, see if any ports haven't changed.
                    foreach (AxisPortProperties np in newAxisPorts) {
                        foreach (AxisPortProperties op in AxisPorts) {
                            if (np.DeviceId.Equals(op.DeviceId)) {
                                np.PartialCopy(op);
                                op.DeviceId = null; // Indicates that this port was copied.
                                break;
                            }
                        }
                    }
                    // Now remove unused ports.
                    foreach (AxisPortProperties op in AxisPorts) {
                        if (op.DeviceId != null) {
                            Hub.Exchange.Manager.RemoveDriver(op.Port);
                            op.Reset();
                        }
                    }
                    // Reset the mapping table.
                    for (k = 0; k < 16; k++) PortNoToAxisPortPropertiesIndex[k] = 0;
                    // For ports that changed,
                    // check if device is present,
                    // or assign netIfIndex's to unassigned axis ports.
                    // Also set data quick lookup parameters.
                    int dataOffset = 1;
                    k = 0;
                    foreach (AxisPortProperties np in newAxisPorts) {
                        if (np.NetIfIndex == 0) {
                            LinkDriver p = Hub.Exchange.Manager.GetLinkDriver(np.DeviceId);
                            if (p != null) {
                                np.NetIfIndex = p.NetIfIndex;
                                np.Port = p;
                            } else {
                                // Search for an unused port number.
                                for (byte j = ProtocolConstants.NETIF_USER_BASE; j < 0xf; j++) {
                                    p = Hub.Exchange.Manager.GetLinkDriver(j);
                                    if (p == null) {
                                        bool isInUse = false;
                                        foreach (AxisPortProperties aa in newAxisPorts) {
                                            if (aa.NetIfIndex == j) {
                                                isInUse = true;
                                                break;
                                            }
                                        }
                                        if (!isInUse) {
                                            np.NetIfIndex = j;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        np.DataOffset = dataOffset;
                        np.DataCount = np.AxesOnPort.Count; // (Actual pos,Target pos) per axis.
                        dataOffset += FIELDS_PER_AXIS * np.DataCount;
                        np.AxisPortPropertiesIndex = k++;
                        PortNoToAxisPortPropertiesIndex[np.NetIfIndex] = np.AxisPortPropertiesIndex;
                    }
                    foreach (AxisPortProperties np in newAxisPorts) {
                        k = 0;
                        foreach (AxisProperties ap in np.AxesOnPort) {
                            ap.DataCacheOffset = np.DataOffset + FIELDS_PER_AXIS * k;
                            ap.DataPVTOffset = 1 + FIELDS_PER_AXIS * k;
                            ap.AxisPortPropertiesIndex = np.AxisPortPropertiesIndex;
                            ++k;
                        }
                    }
                    AxisPorts = newAxisPorts;
                    Recorder.AxisOptions = newAxisOptions;
                    RefreshDevicePorts();
                }
            }
        }

        public void OnBabelMessage(BabelMessage msg) {
            ProtocolHub h = GetHub;
            if (h != null) {
                h.AddToQueue(msg.SenderId, msg);
            }
        }

        public void IssueCommand(int axisPortPropertiesIndex, byte cmd, int numArgs, byte arg1, int arg2) {
            ProtocolHub h = GetHub;
            if (h != null && PortOkToSend(axisPortPropertiesIndex)) {
                AxisPortProperties ap = AxisPorts[axisPortPropertiesIndex];
                byte[] dataArray = null;
                if (numArgs == 2) {
                    dataArray = new byte[5];
                    dataArray[0] = arg1;
                    dataArray[1] = (byte)(arg2 & 0xff);
                    dataArray[2] = (byte)((arg2 >> 8) & 0xff);
                    dataArray[3] = (byte)((arg2 >> 16) & 0xff);
                    dataArray[4] = (byte)((arg2 >> 24) & 0xff);
                } else if (numArgs == 1) {
                    dataArray = new byte[1];
                    dataArray[0] = arg1;
                } else {
                    dataArray = new byte[0];
                }
                BabelMessage message = BabelMessage.CreateCommandMessage(
                    h.Exchange, false, Router.RouterAction.PostToNetIf,
                    ap.NetIfIndex, cmd,
                    ProtocolConstants.ADRS_LOCAL, ProtocolConstants.ADRS_LOCAL, 0,
                    ProtocolConstants.IDENT_USER, (byte)dataArray.Length, (byte)0, dataArray
                );
                if (!message.Exchange.SubmitMessage(message, this, false, 0)) {
                    return;
                }
            }
        }

        public void IssueBinaryCommand(byte[] dataArray) {
            ProtocolHub h = GetHub;
            if (h != null && PortOkToSend(0) && AxisPorts.Count>0) {
                AxisPortProperties ap = AxisPorts[0];
                BabelMessage message = BabelMessage.CreateCommandMessage(
                    h.Exchange, false, Router.RouterAction.PostToNetIf,
                    ap.NetIfIndex, AXIS_CMD_BINARY_TX,
                    ProtocolConstants.ADRS_LOCAL, ProtocolConstants.ADRS_LOCAL, 0,
                    ProtocolConstants.IDENT_USER, (byte)dataArray.Length, (byte)0, dataArray
                );
                if (!message.Exchange.SubmitMessage(message, this, false, 0)) {
                    return;
                }
            }
        }

        public void IssueCommandToAll(byte cmd, int numArgs, byte arg1, int arg2) {
            int n = AxisPorts.Count;
            for (int k = 0; k < n; k++) {
                IssueCommand(k, cmd, numArgs, arg1, arg2);
            }
        }

        public void IssueSetModeCommandToAll(byte mode, byte id) {
            IssueCommandToAll(AxesController.AXIS_CMD_MODE, 2, mode, id);
            if (mode == (int)RecorderModeKinds.RecorderModePlayStandby) {
                if (PVTCache.PointList.Count > 0) {
                    List<double> v = PVTCache.PointList[0];
                    Thread.Sleep(2000); // Allow time for mode to settle.
                    IssuePVTCommand(-2, v);
                }
            }
        }

        public void IssueStopAction() {
            LastStoppedTime = LastChartCacheTime;
            if (Recorder.RecorderMode == RecorderModeKinds.RecorderModeRecording) {
                IssueSetModeCommandToAll((byte)RecorderModeKinds.RecorderModeRecordStopped, 0);
                IssueISOPVTMonitorSwitchOff();
            } else if (Recorder.RecorderMode == RecorderModeKinds.RecorderModePlaying) {
                IssueSetModeCommandToAll((byte)RecorderModeKinds.RecorderModePlayStopped, 0);
            } else {
                IssueSetModeCommandToAll((byte)RecorderModeKinds.RecorderModeIdleStopped, 0);
            }
        }

        // Set triggers:{uint8 triggerIndex,uint32 msDelay,uint32 msPulseLength,uint16 repeatNum,uint8 activeHigh}+).
        // Sends all args up to first parameter < 0.
        public void IssueSetTriggers(int axisPortPropertiesIndex, int triggerIndex, int msDelay, int msPulseLength, int repeatNum, int activeHigh) {
            ProtocolHub h = GetHub;
            if (h != null && PortOkToSend(axisPortPropertiesIndex)) {
                AxisPortProperties ap = AxisPorts[axisPortPropertiesIndex];
                int n = 0, numParams = 0, arraySize = 0;
                do {
                    if (triggerIndex < 0) break;
                    arraySize += 1;
                    ++numParams;
                    if (msDelay < 0) break;
                    arraySize += 4;
                    ++numParams;
                    if (msPulseLength < 0) break;
                    arraySize += 4;
                    ++numParams;
                    if (repeatNum < 0) break;
                    arraySize += 2;
                    ++numParams;
                    if (activeHigh < 0) break;
                    arraySize += 1;
                    ++numParams;
                } while (false);

                byte[] dataArray = new byte[arraySize];
                do {
                    if (numParams < 1) break;
                    dataArray[n++] = (byte)(triggerIndex & 0xff);
                    if (numParams < 2) break;
                    dataArray[n++] = (byte)(msDelay & 0xff);
                    dataArray[n++] = (byte)((msDelay >> 8) & 0xff);
                    dataArray[n++] = (byte)((msDelay >> 16) & 0xff);
                    dataArray[n++] = (byte)((msDelay >> 24) & 0xff);
                    if (numParams < 3) break;
                    dataArray[n++] = (byte)(msPulseLength & 0xff);
                    dataArray[n++] = (byte)((msPulseLength >> 8) & 0xff);
                    dataArray[n++] = (byte)((msPulseLength >> 16) & 0xff);
                    dataArray[n++] = (byte)((msPulseLength >> 24) & 0xff);
                    if (numParams < 4) break;
                    dataArray[n++] = (byte)(repeatNum & 0xff);
                    dataArray[n++] = (byte)((repeatNum >> 8) & 0xff);
                    if (numParams < 5) break;
                    dataArray[n++] = (byte)(activeHigh & 0xff);
                } while (false);
                BabelMessage message = BabelMessage.CreateCommandMessage(
                    h.Exchange, false, Router.RouterAction.PostToNetIf,
                    ap.NetIfIndex, AXIS_CMD_TRIGGER_SET,
                    (ushort)ProtocolConstants.ADRS_LOCAL,
                    ProtocolConstants.ADRS_LOCAL, 0,
                    ProtocolConstants.IDENT_USER,
                    (byte)dataArray.Length, (byte)0, dataArray
                );
                if (!message.Exchange.SubmitMessage(message, this, false, 0)) {
                    return;
                }
            }
        }

        // Packs the parameters into a byte array for message:
        // Message Params: uint16 motionFlags, [uint32 RelativeTime,] {float/int16 position}+.
        // If relativeTime>=0, uses that as first parameter rather than dataParameters[0].
        // If relativeTime=-1, uses dataParameters[0].
        // If relativeTime=-2, sets t=0xffffffff as a flag to indicate special standby goto command.
        // Remaining parameters are collected from dataOffset+.
        public static unsafe byte[] PackParameters(int relativeTime, List<double> dataParameters, 
                int dataOffset, int dataCount, List<AxisProperties> ap) {
            int k, j = 0, n = 0, numParams = dataParameters.Count;
            uint v;
            byte[] dataArray = new byte[2 + 4 + 4 * dataCount]; // Send target value.
            // DEBUG: fudge hardcore motionFlags:
            // hi=(AXIS_PVT_FLAGS_INT=0, 0,0,0, pIris=01, pFocus=01) = 0x05,
            // lo=(vZoom=10,vRoll=10,vTilt=10,vPan=10) = 0xAA.
            int motionFlags = AXIS_PVT_FLAGS_TIME|0x05AA; 
            if (relativeTime >= 0) 
                v = (uint)relativeTime;
            else if (relativeTime == -2) 
                v = 0xffffffff;
            else 
                v = (uint)dataParameters[0];
            dataArray[n++] = (byte)(motionFlags&0xff);
            dataArray[n++] = (byte)((motionFlags>>8) & 0xff);
            dataArray[n++] = (byte)(v & 0xff);
            dataArray[n++] = (byte)((v >> 8) & 0xff);
            dataArray[n++] = (byte)((v >> 16) & 0xff);
            dataArray[n++] = (byte)((v >> 24) & 0xff);
            k = dataOffset + FIELDS_PER_AXIS * dataCount;
            if (k < numParams) numParams = k;
            for (k = (1 + dataOffset); k < numParams; k += FIELDS_PER_AXIS) { // Increment since stepping over fields per axis.
                float f;
                if (ap[j++].Active) {
                    f = (float)dataParameters[k];
                } else {
                    f = float.NaN; // Send NaN if axis not active, controller will ignore axis value.
                }
                v = *((uint*)&f);
                dataArray[n++] = (byte)(v & 0xff);
                dataArray[n++] = (byte)((v >> 8) & 0xff);
                dataArray[n++] = (byte)((v >> 16) & 0xff);
                dataArray[n++] = (byte)((v >> 24) & 0xff);
            }
            return dataArray;
        }

        // Send a PVT command.
        // dataParameters[0..N] = {uint32 relTime,{float position}+}.
        // If relativeTime>=0 uses that as time and ignores first parameter of array.
        // TODO: what about NaN's and ordering if an axis is disabled on playback etc?
        public void IssuePVTCommand(int relativeTime, List<double> dataParameters) {
            ProtocolHub h = GetHub;
            if (h != null && AxesReady) {
                for (int k = 0; k < AxisPorts.Count; k++) {
                    AxisPortProperties ap = AxisPorts[k];
                    byte[] dataArray = PackParameters(relativeTime, dataParameters, ap.DataOffset, ap.DataCount, ap.AxesOnPort);
                    BabelMessage message = BabelMessage.CreateCommandMessage(
                        h.Exchange, false, Router.RouterAction.PostToNetIf,
                        ap.NetIfIndex, AXIS_CMD_PVT_SUBMIT,
                        (ushort)ProtocolConstants.ADRS_LOCAL, 
                        ProtocolConstants.ADRS_LOCAL, (byte)0,
                        ProtocolConstants.IDENT_USER,
                        (byte)dataArray.Length, (byte)0, dataArray
                    );
                    if (!message.Exchange.SubmitMessage(message, this, false, 0)) {
                        return;
                    }
                }
            }
        }

        public static unsafe byte[] PackStopParameters(AxisPortProperties app) {
            int n = 0, numAxis = app.AxesOnPort.Count;
            byte[] dataArray = new byte[8 * numAxis];
            float f;
            uint v;
            for (int j = 0; j < numAxis; j++) {
                AxisProperties ap = app.AxesOnPort[j];
                f = ap.PlusStop;
                v = *((uint*)&f);
                dataArray[n++] = (byte)(v & 0xff);
                dataArray[n++] = (byte)((v >> 8) & 0xff);
                dataArray[n++] = (byte)((v >> 16) & 0xff);
                dataArray[n++] = (byte)((v >> 24) & 0xff);
                f = ap.MinusStop;
                v = *((uint*)&f);
                dataArray[n++] = (byte)(v & 0xff);
                dataArray[n++] = (byte)((v >> 8) & 0xff);
                dataArray[n++] = (byte)((v >> 16) & 0xff);
                dataArray[n++] = (byte)((v >> 24) & 0xff);
            }
            return dataArray;
        }

        public void IssueSetStopsCommand() {
            ProtocolHub h = GetHub;
            if (h != null && AxesReady) {
                for (int k = 0; k < AxisPorts.Count; k++) {
                    AxisPortProperties ap = AxisPorts[k];
                    byte[] dataArray = PackStopParameters(ap);
                    BabelMessage message = BabelMessage.CreateCommandMessage(
                        h.Exchange, false, Router.RouterAction.PostToNetIf,
                        ap.NetIfIndex, AXIS_CMD_SET_STOPS,
                        (ushort)ProtocolConstants.ADRS_LOCAL, 
                        ProtocolConstants.ADRS_LOCAL, (byte)0,
                        ProtocolConstants.IDENT_USER,
                        (byte)dataArray.Length, (byte)0, dataArray
                    );
                    if (!message.Exchange.SubmitMessage(message, this, false, 0)) {
                        return;
                    }
                }
            }
        }

        // Monitoring for changes in pvt vars.
        public void IssuePVTMonitoring(int axisPortPropertiesIndex, bool switchOff) {
            ProtocolHub h = GetHub;
            if (h != null && PortOkToSend(axisPortPropertiesIndex)) {
                AxisPortProperties ap = AxisPorts[axisPortPropertiesIndex];
                byte[] dataArray = new byte[5];
                dataArray[0] = 1;
                dataArray[1] = ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_RAM;
                dataArray[2] = 1;
                dataArray[3] = PARAMETER_INDEX_PVT_MONITORING;
                dataArray[4] = (byte)(switchOff?0:1);
                BabelMessage message = BabelMessage.CreateCommandMessage(
                    h.Exchange, false, Router.RouterAction.PostToNetIf,
                    ap.NetIfIndex, ProtocolConstants.MEDIATOR_DEVICE_WRITEVAR,
                    ProtocolConstants.ADRS_LOCAL, 
                    ProtocolConstants.ADRS_LOCAL, (byte)0,
                    ProtocolConstants.IDENT_USER, (byte)dataArray.Length, (byte)0, dataArray
                );
                if (!message.Exchange.SubmitMessage(message, this, false, 0)) {
                    return;
                }
            }
        }

        public void IssueISOPVTMonitorSwitchOff() {
            int n = AxisPorts.Count;
            for (int k = 0; k < n; k++) {
                IssuePVTMonitoring(k, true);
            }
        }

        public void IssueISOInputTriggerMonitor(int axisPortPropertiesIndex, bool switchOff) {
            ProtocolHub h = GetHub;
            if (h != null && PortOkToSend(axisPortPropertiesIndex)) {
                AxisPortProperties ap = AxisPorts[axisPortPropertiesIndex];
                int isoId = ISO_ID_TRG;
                uint milliInterval = switchOff ? 0 : TRIGGER_ISO_INTERVAL;
                byte[] dataArray = new byte[9];
                dataArray[0] = (byte)isoId;
                dataArray[1] = (byte)(milliInterval & 0xff);
                dataArray[2] = (byte)((milliInterval >> 8) & 0xff);
                dataArray[3] = 0;
                dataArray[4] = 0;
                dataArray[5] = ISO_ID_TRG;
                dataArray[6] = (byte)(ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_RAM 
                                        | ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_PACK);
                dataArray[7] = 1;
                dataArray[8] = (byte)PARAMETER_INDEX_INPUT_TRIGGERS;
                BabelMessage message = BabelMessage.CreateCommandMessage(
                    h.Exchange, false, Router.RouterAction.PostToNetIf,
                    ap.NetIfIndex, ProtocolConstants.MEDIATOR_DEVICE_ISOMONVAR,
                    ProtocolConstants.ADRS_LOCAL, 
                    ProtocolConstants.ADRS_LOCAL, 0,
                    ProtocolConstants.IDENT_USER, (byte)dataArray.Length, (byte)0, dataArray
                );
                if (!message.Exchange.SubmitMessage(message, this, false, 0)) {
                    return;
                }
            }
        }

        public void IssueISOStatusReport(int axisPortPropertiesIndex) {
            ProtocolHub h = GetHub;
            if (h != null && PortOkToSend(axisPortPropertiesIndex)) {
                int isoId = ISO_ID_STA;
                uint milliInterval = STATUS_REPORT_ISO_INTERVAL;
                int repeatCount = 0;
                AxisPortProperties ap = AxisPorts[axisPortPropertiesIndex];
                byte[] dataArray = new byte[6];
                dataArray[0] = (byte)isoId;
                dataArray[1] = (byte)(milliInterval & 0xff);
                dataArray[2] = (byte)((milliInterval >> 8) & 0xff);
                dataArray[3] = (byte)(repeatCount & 0xff);
                dataArray[4] = (byte)((repeatCount >> 8) & 0xff);
                dataArray[5] = (byte)AXIS_CMD_STATUS_REPORT;
                BabelMessage message = BabelMessage.CreateCommandMessage(
                    h.Exchange, false, Router.RouterAction.PostToNetIf,
                    ap.NetIfIndex, ProtocolConstants.MEDIATOR_DEVICE_ISOMSG,
                    ProtocolConstants.ADRS_LOCAL,
                    ProtocolConstants.ADRS_LOCAL, (byte)0,
                    ProtocolConstants.IDENT_USER, (byte)dataArray.Length, (byte)0, dataArray
                );
                if (!message.Exchange.SubmitMessage(message, this, false, repeatCount)) {
                    return;
                }
            }
        }

        public List<double> ProcessPositionReport(BabelMessage b) {
            List<double> p = new List<double>();
            int valuesIndex = 2;
            int len = b.DataLen;
            ushort motionFlags;
            uint currentTime;
            long v; float f;
            if (len < 4) return p;
            len -= 2;
            motionFlags = (ushort)Primitives.GetArrayValueU(b.DataAry, 0, 2);
            currentTime = Primitives.GetBabelMilliTicker();

            p.Add((double)motionFlags);
            p.Add((double)currentTime);

            while (len > 0) {
                switch (motionFlags & 0x3) {
                    case AxisValuePosition:
                    case AxisValueVelocity:
                        if (len < 2)
                            goto SkipInt;
                        len -= 2;
                        v = Primitives.GetArrayValueS(b.DataAry, valuesIndex, 2);
                        valuesIndex += 2;
                        if (v == short.MinValue)
                            p.Add(double.NaN);
                        else {
                            p.Add((double)v);
                        }
                        break;
                    case AxisValueAngle:
                        if (len < 4)
                            goto SkipInt;
                        len -= 4;
                        f = BitConverter.ToSingle(b.DataAry, valuesIndex);
                        valuesIndex += 4;
                        p.Add((double)f);
                        break;
                    case AxisValueNotSet:
                    default:
                    SkipInt:
                        p.Add(double.NaN);
                        break;
                }
                motionFlags >>= 2;
            }
            return p;
        }

        public List<double> ProcessPVTReport(BabelMessage b) {
            List<double> p = new List<double>();
            int valuesIndex=2;
            int len = b.DataLen;
            ushort motionFlags;
            uint currentTime;
            uint relativeTime;
            bool pvtIsSequence;
            long v; float f;
            if(len<4) return p;
            len-=2;
            motionFlags = (ushort)Primitives.GetArrayValueU(b.DataAry,0,2);
            pvtIsSequence = (motionFlags&AXIS_PVT_FLAGS_TIME)!=0;
            if(pvtIsSequence) {
	            if(len<6) return p;
	            relativeTime= (uint)Primitives.GetArrayValueU(b.DataAry,valuesIndex,4);
	            len-=4; valuesIndex+=4;
            } else {
	            relativeTime=0;
            }
            currentTime = Primitives.GetBabelMilliTicker();
            if(relativeTime==0xffffffffu) {
                relativeTime=currentTime;
	            //pvtPerformGotoMove(pValues,len);
	            //return;
            }

            if(PVTStartTime==0||relativeTime==0) {
	            PVTLastRelativeTime=0;
	            PVTStartTime=currentTime-relativeTime;
	            //if(pvtIsSequence) return;
            }
			
	        if(currentTime>=(PVTStartTime+relativeTime)) { // Try to recover from stale times.
		        relativeTime=AXIS_PVT_PERIOD_INTERVAL+(currentTime-PVTStartTime);
		        if(relativeTime<(PVTLastRelativeTime+AXIS_PVT_PERIOD_INTERVAL)) {
			        relativeTime=PVTLastRelativeTime+AXIS_PVT_PERIOD_INTERVAL;
		        }
	        }
	        PVTLastRelativeTime = relativeTime;	
            p.Add((double)motionFlags);
	        p.Add((double)(PVTStartTime+relativeTime));

	        if((motionFlags&AXIS_PVT_FLAGS_INT)!=0) {		
		        while(len>0) {
                    switch(motionFlags&0x3) {
                        case AxisValuePosition:
                        case AxisValueVelocity:
                            if(len<2) 
                                goto SkipInt;
    				        len-=2;
                            v = Primitives.GetArrayValueS(b.DataAry, valuesIndex, 2);
                            valuesIndex += 2;
                            if (v == short.MinValue) 
    					        p.Add(double.NaN);
    				        else {
    					        p.Add((double)v);
    				        }
                            break;
                        case AxisValueAngle:
                            if(len<4) 
                                goto SkipInt;
                            len -= 4;
                            f = BitConverter.ToSingle(b.DataAry, valuesIndex);
                            valuesIndex += 4;
        				    p.Add((double)f);
                            break;
                        case AxisValueNotSet:
                        default:
                            SkipInt:
                            p.Add(double.NaN);
                            break;
                    }
			        motionFlags>>=2;		
		        }
	        } else {
		        while(len>0) {
                    switch(motionFlags&0x3) {
                        case AxisValuePosition:
                        case AxisValueAngle:
                            if(len<4) 
                                goto SkipReal;
                            len -= 4;
                            f = BitConverter.ToSingle(b.DataAry, valuesIndex);
                            valuesIndex += 4;
        				    p.Add((double)f);
                            break;
                        case AxisValueVelocity:
                            if(len<4) 
                                goto SkipReal;
    				        len-=4;
                            v = Primitives.GetArrayValueS(b.DataAry, valuesIndex, 2);
                            valuesIndex += 4;
                            p.Add((double)v);
                            break;
                        case AxisValueNotSet:
                        default:
                            SkipReal:
                            p.Add(double.NaN);
                            break;
                    }
			        motionFlags>>=2;
		        }
	        }
            return p;
	    }

        // Process replies from input device.
        // Handles Iso & standard cmds.
        public void MessageReplyHandler() {
            ProtocolHub h = null;
            while (!IsClosing) {
                try {
                    if (h == null) h = GetHub;
                    if (h != null) {
                        BabelMessage b = h.GetMessageFromQueue(ProtocolConstants.IDENT_USER);
                        if (b != null && b.IsGeneral) {
                            AxisPortProperties ap = null;
                            int axisPortPropertiesIndex = 0;
                            int netIfIndex = b.IncomingNetIfIndex;
                            if (netIfIndex < ProtocolConstants.NETIF_USER_BASE) {
                                Log.w(TAG, "MessageReplyHandler:wrong netIf:" + b.ToString());
                                continue;
                            }
                            lock (ControllerLock) {
                                axisPortPropertiesIndex = PortNoToAxisPortPropertiesIndex[netIfIndex];
                                if (axisPortPropertiesIndex < AxisPorts.Count)
                                    ap = AxisPorts[axisPortPropertiesIndex];
                            }
                            if (ap != null) {
                                switch (b.Cmd) { // Most commands don't reply.
                                    case ProtocolConstants.MEDIATOR_DEVICE_ISOMONVAR:
                                        switch (b.DataAry[0]) { 
                                            case ISO_ID_TRG: // Trigger report: {readId, cmdFlags, numRead, index} triggerInputByte.
                                                lock (TriggerLock) {
                                                    ap.CurrentInputTriggers = (int)b.DataAry[4];
                                                }
                                                ap.TriggerReportTimeout = Primitives.GetBabelMilliTicker() + TRIGGER_REPORT_TIMEOUT_INTERVAL;
                                                break;
                                            default:
                                                break;
                                        }
                                        break;
                                    case AXIS_CMD_PVT_REPORT:
                                        ap.PVTMonitoringTimeout = Primitives.GetBabelMilliTicker() + PVT_MONITORING_TIMEOUT_INTERVAL;
                                        IncomingDataPoint(ap, ProcessPVTReport(b),true);
                                        break;
                                    case AXIS_CMD_POS_REPORT:
                                        ap.PVTMonitoringTimeout = Primitives.GetBabelMilliTicker() + PVT_MONITORING_TIMEOUT_INTERVAL;
                                        IncomingDataPoint(ap, ProcessPositionReport(b),false);
                                        break;
                                    case AXIS_CMD_STATUS_REPORT: 
                                    //(uint8 0=SystemMode,1=SystemId,uint16 2=axesStatus,uint8 4=PVTQSize,uint8 5=InputTriggers).
                                        if (b.DataLen > 5) {
                                            ap.CurrentPVTQueueSize = (int)b.DataAry[4];
                                            lock (TriggerLock) {
                                                ap.CurrentInputTriggers = (int)b.DataAry[5];
                                            }
                                            Recorder.UpdateMode(b.DataAry[0], b.DataAry[1]);
                                            uint s = (uint)(b.DataAry[2] + (b.DataAry[3] << 8));
                                            for (int k = 0; k < ap.AxesOnPort.Count; k++) {
                                                ap.AxesOnPort[k].AxisState = (AxisStateKinds)((s >> (ap.AxesOnPort[k].DeviceRank * 2)) & 0x3);
                                            }
                                            Recorder.UpdateAxisState(ap);
                                            ap.StatusReportTimeout = Primitives.GetBabelMilliTicker() + STATUS_REPORT_TIMEOUT_INTERVAL;
                                        }
                                        break;
                                    case AXIS_CMD_BINARY_RX:
                                        break;
                                    case AXIS_CMD_MODE:
                                    case AXIS_CMD_RESET:
                                    case AXIS_CMD_HOME:
                                    case AXIS_CMD_ZERO:
                                    case AXIS_CMD_SET_STOPS: // TODO: check stops are correct.
                                    case AXIS_CMD_PVT_SUBMIT:
                                    case AXIS_CMD_TRIGGER_SET:
                                    case AXIS_CMD_TRIGGER_ENABLE:
                                    case AXIS_CMD_TRIGGER_FIRE:
                                    case AXIS_CMD_TRIGGER_OUTPUT:
                                        break;
                                    default:
                                        Log.w(TAG, "MessageReplyHandler:unhandled message:" + b.ToString());
                                        break;
                                }
                            }
                        }
                    } else {
                        Thread.Sleep(10);
                    }
                } catch (ThreadInterruptedException) {
                    break;
                } catch (Exception e) {
                    // ignore.
                    Log.d(TAG, "MessageReplyHandlerThread exception:" + e.Message);
                }
            }
            Log.d(TAG, "MessageReplyHandlerThread exiting.");
        }

        public void CheckMonitoringActive(bool forceActive) {
            lock (ControllerLock) {
                for (int k = 0; k < AxisPorts.Count; k++) {
                    AxisPortProperties ap = AxisPorts[k];
                    if (forceActive || (Primitives.GetBabelMilliTicker() > ap.PVTMonitoringTimeout))
                        IssuePVTMonitoring(k, false);
                }
            }
        }

        // Using time value of points,
        // either merge point with a close cache point or insert new point.
        private void MergeDataPoint(AxisPortProperties ap, List<double> d, bool isTarget, bool isChartCache) {
            bool seriesResetRequired = false;
            List<double> p = null;
            DataCache dc = isChartCache ? Recorder.RecorderScope.ChartCache : Recorder.Controller.PVTCache;
            int absIndex;
            // First check if recording: adjust time base.
            if (Recorder.RecorderMode == RecorderModeKinds.RecorderModeRecording) {
                if (d.Count > 0)
                    d[0] += CurrentRecordStartTime;
            }
            absIndex = dc.FindAbsolutePoint(d);
            // Next check if a reset occurred this would leave old points in cache.
            if (isChartCache && (
                    Recorder.RecorderMode == RecorderModeKinds.RecorderModeLocalOnly
                    || Recorder.RecorderMode == RecorderModeKinds.RecorderModeIdle
                    || Recorder.RecorderMode == RecorderModeKinds.RecorderModeIdleStopped)
                ) {
                if (absIndex >= 0) { // Merging points should be close to the end of the cache.
                    if ((dc.AbsoluteCount - absIndex) > 40000000) { // 40 = 2 seconds slack.
                        Recorder.RecorderScope.ResetCache();
                        seriesResetRequired = true; // This will invoke RecorderScope.ResetSeries();
                        absIndex = -1;
                    }
                }
            }
            if (absIndex < 0) { // Not found, so find close point or insert new.
                absIndex = ~absIndex; // Index of next point (p[0]>d[0]), or Count.
                if (absIndex < dc.PointList.Count) { // Check if next point is close enough.
                    p = dc.PointList[absIndex];
                    if ((p[0] - d[0]) > PVT_MAX_TIME_DEVIATION) { // Too distant, so try prior point.
                        if (absIndex > 0) {
                            --absIndex;
                            p = dc.PointList[absIndex];
                            if ((d[0] - p[0]) > PVT_MAX_TIME_DEVIATION) { // Too distant, insert after this point.
                                ++absIndex;
                                p = null;
                            } else { // Use this point.
                            }
                        } else { // Insert new point at beginning.
                            // absIndex=0;
                            p = null;
                        }
                    } else { // Use this point.
                    }
                } else { // Must be past end, check if last point is close enough.
                    if (absIndex > 0) {
                        --absIndex;
                        p = dc.PointList[absIndex];
                        if ((d[0] - p[0]) > PVT_MAX_TIME_DEVIATION) { // Too distant, insert after this point.
                            ++absIndex;
                            p = null;
                        } else { // Use this point.
                        }
                    } else { // Cache is empty, so just add point.                       
                    }
                }
                if (p == null) { // Insert new point at absIndex.
                    int numElements = 1 + FIELDS_PER_AXIS * Recorder.AxisOptions.Count;
                    p = new List<double>(numElements);
                    p.Add(d[0]);
                    for (int k = 1; k < numElements; k++) p.Add(double.NaN);
                    dc.AbsoluteInsertAt(absIndex, p);
                }
            } else {
                p = dc.PointList[absIndex];
            }
            int numData = ap.DataCount; // Fetch actual or target positions.
            int dataOffset = ap.DataOffset + (isTarget ? 1 : 0);
            try {
                for (int k = 0; k < numData; k++) {
                    p[dataOffset + 2 * k] = d[1 + k];
                }
            } catch (Exception) {
                // Ignore if d doesn't have enough members.
            }
            if (isChartCache) {
                if (d[0] != 0.0) {
                    LastChartCacheTime = d[0];
                    LastChartCacheIndex = absIndex;
                }
                RecorderAxisPoint rap = new RecorderAxisPoint(ap, p, isTarget, true, seriesResetRequired);
                Recorder.RecorderScope.AxisPointTaskQueue.AddLast(rap);
            }
        }

        private void ScheduleInfoUpdate(AxisPortProperties ap, List<double> d, bool isTarget) {
            RecorderAxisPoint rap = new RecorderAxisPoint(ap, d, isTarget, false, false);
            Recorder.RecorderScope.AxisPointTaskQueue.AddLast(rap);
        }

        // On new data point arrival from controller, deal with each recorder mode.
        // d=(time,{targetPos,actualPos}+).
        public void IncomingDataPoint(AxisPortProperties ap, List<double> d, bool isTarget) {
            if (d != null) switch (Recorder.RecorderMode) {
                    case RecorderModeKinds.RecorderModeLocalOnly:
                    case RecorderModeKinds.RecorderModeIdle:
                        // Add actual pos & target pos to chart cache.
                        // Add to chart series.
                        MergeDataPoint(ap, d, isTarget, true);
                        break;
                    case RecorderModeKinds.RecorderModePlayStandby:
                        // Leave chart as is: showing target positions on static chart.
                        ScheduleInfoUpdate(ap, d, isTarget);
                        break;
                    case RecorderModeKinds.RecorderModePlaying:
                        // Update actual pos in chart cache.
                        // Update timeline position & chart position.
                        MergeDataPoint(ap, d, isTarget, true);
                        break;
                    case RecorderModeKinds.RecorderModeRecordStandby:
                        // Leave chart as is: blank, ready for recording.
                        // Ignore incoming point until in recording mode.
                        ScheduleInfoUpdate(ap, d, isTarget);
                        break;
                    case RecorderModeKinds.RecorderModeRecording:
                        // Add actual pos & target pos to chart cache and pvtCache.
                        // Update timeline position & chart position.
                        MergeDataPoint(ap, d, isTarget, false);
                        MergeDataPoint(ap, d, isTarget, true);
                        break;
                    case RecorderModeKinds.RecorderModeIdleStopped:
                    case RecorderModeKinds.RecorderModePlayStopped:
                    case RecorderModeKinds.RecorderModeRecordStopped:
                        // Leave chart as is: showing target positions on static chart.
                        ScheduleInfoUpdate(ap, d, isTarget);
                        break;
                    default: break;
                }
        }

        public void UpdateTriggerOutputs() {
            foreach (AxisPortProperties app in AxisPorts) {
                if (app.PriorOutputTriggers != app.CurrentOutputTriggers) {
                    app.PriorOutputTriggers = app.CurrentOutputTriggers;
                    IssueCommand(app.AxisPortPropertiesIndex, AxesController.AXIS_CMD_TRIGGER_OUTPUT, 1, (byte)app.CurrentOutputTriggers, 0);
                }
            }
        }

        public void ToggleTriggerOutput(TriggerProperties tp) {
            foreach (AxisPortProperties app in AxisPorts) {
                if ((tp.OutputDeviceMask & (1 << (app.DeviceNumber - 1))) != 0) {
                    if (tp.ActiveHighOutput == tp.IsOn) {
                        app.CurrentOutputTriggers |= tp.OutputBitMask; // Set low.
                        app.CurrentOutputTriggers ^= tp.OutputBitMask;
                    } else {
                        app.CurrentOutputTriggers |= tp.OutputBitMask; // Set hi.
                    }
                }
            }
            tp.IsOn = !tp.IsOn;
            UpdateTriggerOutputs();
        }

        public void ResetTriggers() {
            if (AxisPorts == null || Recorder.TriggerOptions == null) return;
            CheckInputTriggers = false;
            CheckOutputTriggers = false;
            PlayIsSignalled = false;
            RecordIsSignalled = false;
            foreach (AxisPortProperties app in AxisPorts) {
                app.CurrentInputTriggers = 0;
                app.PriorInputTriggers = 0;
                app.CurrentOutputTriggers = 0;
                app.PriorOutputTriggers = 0;
            }
            foreach (TriggerProperties tp in Recorder.TriggerOptions) {
                tp.IsOn = false;
                tp.IsPrimedOnly = false;
                tp.NumLeft = 0;
                tp.ActionTime = 0;
                foreach (AxisPortProperties app in AxisPorts) {
                    if ((tp.OutputDeviceMask & (1 << (app.DeviceNumber - 1))) != 0) {
                        if (tp.ActiveHighOutput) {
                            app.CurrentOutputTriggers |= tp.OutputBitMask;
                            app.CurrentOutputTriggers ^= tp.OutputBitMask;
                        } else {
                            app.CurrentOutputTriggers |= tp.OutputBitMask;
                        }
                    }
                    lock (TriggerLock) {
                        if (tp.ActiveHighInput) {
                            app.CurrentInputTriggers |= tp.InputBitMask;
                            app.CurrentInputTriggers ^= tp.InputBitMask;
                        } else {
                            app.CurrentInputTriggers |= tp.InputBitMask;
                        }
                    }
                }
                CheckInputTriggers |= tp.InputEnabled;
                CheckOutputTriggers |= tp.OutputEnabled;
            }
            foreach (AxisPortProperties app in AxisPorts) {
                app.PriorOutputTriggers = ~app.CurrentOutputTriggers;
            }
            UpdateTriggerOutputs();
        }

        // Reconfigure triggers.
        public void SetupTriggersConfiguration(List<TriggerProperties> newTriggerOptions) {
            Recorder.TriggerOptions = newTriggerOptions;
            ResetTriggers();
        }

        // Poll triggers when SystemMode:
        //  {RecorderModeRecordStandby|RecorderModePlayStandby|ModeRecording|ModePlaying}.
        // Note triggers are relative to PVTStartTime.
        // Note PVTStartTime = 0 when in a Standby mode.
        // The Poller is called no faster than every 10ms.
        void TriggersPoll(uint bmt) {
            if (CheckInputTriggers) {
                bool startPlay = false, startRecord = false;
                // Determine if an input trigger has occurred, if so trigger any outputs.
                // Look for a change in input triggers: Play, Record or Input Signal states.
                foreach (TriggerProperties tp in Recorder.TriggerOptions) {
                    if (tp.InputEnabled) {
                        bool wasSignalled = (tp.PlayInput && PlayIsSignalled) || (tp.RecordInput && RecordIsSignalled);
                        if (!wasSignalled) {
                            if (!tp.SignalInput) continue;
                            bool inputBit = ((tp.AxisPort.CurrentInputTriggers & tp.InputBitMask) != 0);
                            if (inputBit != tp.ActiveHighInput) continue;
                        }
                        // Getting here means a signal occurred: Ply, Rec or Sig.
                        // So now invoke any enabled outputs triggers.
                        startPlay |= (tp.PlayOutput && Recorder.RecorderMode == RecorderModeKinds.RecorderModePlayStandby);
                        startRecord |= (tp.RecordOutput && Recorder.RecorderMode == RecorderModeKinds.RecorderModeRecordStandby);
                        if (tp.SignalOutput && tp.NumLeft == 0) { // Set up an output signal trigger.
                            tp.NumLeft = tp.PulseRepeat;
                            tp.ActionTime = tp.DelaySeconds * 1000 + Recorder.ConvertFPSToMilliseconds(tp.DelayFrames);
                            tp.IsOn = false;
                            tp.IsPrimedOnly = true; // We haven't started yet, so indicated time needs adjusting.
                        }
                    }
                }
                PlayIsSignalled = false;
                RecordIsSignalled = false;
                if (startPlay) {
                    IssueSetModeCommandToAll((byte)RecorderModeKinds.RecorderModePlaying, 0);
                } else if (startRecord) {
                    IssueSetModeCommandToAll((byte)RecorderModeKinds.RecorderModeRecording, 0);
                }
            }
            if (CheckOutputTriggers) {
                // Service output triggers:
                foreach (TriggerProperties tp in Recorder.TriggerOptions) {
                    if (tp.OutputEnabled) {
                        if (tp.NumLeft > 0) {
                            if (tp.IsPrimedOnly) {
                                tp.IsPrimedOnly = false;
                                tp.ActionTime += bmt;
                            }
                            if (tp.ActionTime <= bmt) {
                                tp.NumLeft--;
                                // Toggle trigger level.
                                foreach (AxisPortProperties app in AxisPorts) {
                                    if ((tp.OutputDeviceMask & (1 << (app.DeviceNumber - 1))) != 0) {
                                        if (tp.ActiveHighOutput == tp.IsOn) {
                                            app.CurrentOutputTriggers |= tp.OutputBitMask; // Set low.
                                            app.CurrentOutputTriggers ^= tp.OutputBitMask;
                                        } else {
                                            app.CurrentOutputTriggers |= tp.OutputBitMask; // Set hi.
                                        }
                                    }
                                }
                                if (tp.IsOn) {
                                    if (tp.NumLeft > 0) {
                                        tp.ActionTime = bmt + (uint)(tp.PulseCycle * 1000);
                                    }
                                } else {
                                    tp.ActionTime = bmt + (uint)(tp.PulseLength * 1000);
                                }
                                tp.IsOn = !tp.IsOn;
                            }
                        }
                    }
                }
                UpdateTriggerOutputs();
            }
        }

        public void RewindCurrentPlayPVTCacheIndex() {
            int n = PVTCache.PointList.Count;
            if (n > 0) {
                while (--n > 0) {
                    try {
                        if (PVTCache.PointList[n][0] == LastStoppedTime) break;
                    } catch (Exception) {
                    }
                }
            }
            CurrentPlayPVTCacheIndex = n;
        }

        // Schedule heartbeat, cache and chart tasks:
        // a) Check heartbeats are arriving, resetup if necessary.
        // b) During playback, sends outgoing PVTs at 20 Hz. 
        public void Scheduler() {
            uint pvtTimeout = 0, trigTimeout = 0, rpmTimeout = 0;
            while (!IsClosing) {
                try {
                    while (!IsClosing) {
                        uint bmt = Primitives.GetBabelMilliTicker();
                        int numAxisPorts = AxisPorts.Count;
                        for (int k = 0; k < numAxisPorts; k++) {
                            try {
                                AxisPortProperties ap = AxisPorts[k];
                                if (bmt > ap.StatusReportTimeout) {
                                    IssueISOStatusReport(k);
                                    ap.StatusReportTimeout = bmt + STATUS_REPORT_TIMEOUT_INTERVAL;
                                }
                                if (bmt > ap.PVTMonitoringTimeout) {
                                    IssuePVTMonitoring(k, false);
                                    ap.PVTMonitoringTimeout = bmt + PVT_MONITORING_TIMEOUT_INTERVAL;
                                }
                                if (bmt > ap.TriggerReportTimeout) {
                                    IssueISOInputTriggerMonitor(k, false);
                                    ap.TriggerReportTimeout = bmt + TRIGGER_REPORT_TIMEOUT_INTERVAL;
                                }
                                MainWindow.UpdateStatus("Recorder#" + Recorder.ShellId + ":" + ap.AxisPortPropertiesIndex, ap.CurrentComponentState.Name);
                            } catch (Exception) { }
                        }
                        if (bmt > trigTimeout) {
                            trigTimeout = bmt + TRIGGER_REPORT_TIMEOUT_INTERVAL;
                            Recorder.UpdateTriggerStates();
                        }
                        if (bmt > rpmTimeout) {
                            rpmTimeout = bmt + RPM_UPDATE_TIMEOUT_INTERVAL;
                            Recorder.MotionTab.UpdateHomeRPM();
                        }
                        lock (SchedulerLock) {
                            switch (Recorder.RecorderMode) {
                                case RecorderModeKinds.RecorderModeLocalOnly:
                                case RecorderModeKinds.RecorderModeIdle:
                                    pvtTimeout = 0;
                                    PVTStartTime = 0;
                                    PVTPerformTime = 0;
                                    PVTRelativeTime = 0;
                                    PVTLastRelativeTime = 0;
                                    break;
                                case RecorderModeKinds.RecorderModePlayStandby:
                                    pvtTimeout = 0;
                                    PVTStartTime = 0;
                                    PVTPerformTime = 0;
                                    PVTRelativeTime = 0;
                                    PVTLastRelativeTime = 0;
                                    TriggersPoll(bmt);
                                    break;
                                case RecorderModeKinds.RecorderModeRecordStandby:
                                    pvtTimeout = 0;
                                    PVTStartTime = 0;
                                    PVTPerformTime = 0;
                                    PVTRelativeTime = 0;
                                    PVTLastRelativeTime = 0;
                                    TriggersPoll(bmt);
                                    break;
                                case RecorderModeKinds.RecorderModeRecording:
                                    if (PVTStartTime == 0) {
                                        PVTStartTime = bmt;
                                    }
                                    TriggersPoll(bmt);
                                    break;
                                case RecorderModeKinds.RecorderModePlaying:
                                    // Check PVT Q size, send and inc cache index.
                                    if (bmt > pvtTimeout) {
                                        if (PVTStartTime == 0) {
                                            PVTStartTime = bmt;
                                        }
                                        pvtTimeout = bmt + PVT_SEND_TIMEOUT_INTERVAL;
                                        if (CurrentPlayPVTCacheIndex < PVTCache.PointList.Count) {
                                            bool bufferIsLow = false;
                                            lock (ControllerLock) {
                                                for (int k = 0; k < AxisPorts.Count; k++) {
                                                    if (AxisPorts[k].CurrentPVTQueueSize < 50) {
                                                        bufferIsLow = true;
                                                        break;
                                                    }
                                                }
                                            }
                                            if (bufferIsLow) {
                                                List<double> v = PVTCache.PointList[CurrentPlayPVTCacheIndex++];
                                                IssuePVTCommand(-1, v);
                                            }
                                        } else { // Note, do this still inside PVT scheduling until controller responds.
                                            // Tell controller to stop.
                                            IssueStopAction();
                                        }
                                    }
                                    TriggersPoll(bmt);
                                    break;
                                case RecorderModeKinds.RecorderModeIdleStopped:
                                case RecorderModeKinds.RecorderModePlayStopped:
                                case RecorderModeKinds.RecorderModeRecordStopped:
                                    pvtTimeout = 0; // TODO: what else needs to be set?
                                    PVTStartTime = 0;
                                    PVTPerformTime = 0;
                                    PVTRelativeTime = 0;
                                    PVTLastRelativeTime = 0;
                                    break;
                                default:
                                    break;
                            }
                        }
                        Thread.Sleep(10); // Must be << 40ms, i.e. 20Hz.
                    }
                } catch (ThreadInterruptedException) {
                    break;
                } catch (Exception e) {
                    // ignore.
                    Log.d(TAG, "SchedulerThread exception:" + e.Message);
                }
            }
        }
    }
}
