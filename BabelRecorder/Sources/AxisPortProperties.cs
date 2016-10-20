using System;
using System.Threading;
using System.Collections.Generic;

using Babel.Core;
using Babel.XLink;
using Babel.BabelProtocol;

namespace Babel.Recorder {

    // Holds details on each port, such as axes controlled through it.
    public class AxisPortProperties {

        public ComponentState CurrentComponentState;
        public byte NetIfIndex;
        public int AxisPortPropertiesIndex;
        public int DataOffset;
        public int DataCount;
        public int CurrentPVTQueueSize;
        public int CurrentInputTriggers;
        public int CurrentOutputTriggers;
        public int PriorInputTriggers;
        public int PriorOutputTriggers;
        public uint StatusReportTimeout;
        public uint TriggerReportTimeout;
        public uint PVTMonitoringTimeout;
        public uint IsoActualTimeout;
        public int DeviceNumber;
        public string DeviceId;
        public LinkDriver Port;
        public List<AxisProperties> AxesOnPort;
        public List<long> TargetOffsetTable;
        public List<long> ActualOffsetTable;

        public void Reset() {
            CurrentComponentState = ComponentState.NotConfigured;
            NetIfIndex = 0;
            AxisPortPropertiesIndex = 0;
            DataOffset = 0;
            DataCount = 0;
            CurrentPVTQueueSize = 0;
            CurrentInputTriggers = 0;
            CurrentOutputTriggers = 0;
            PriorInputTriggers = 0;
            PriorOutputTriggers = 0;
            StatusReportTimeout = 0;
            TriggerReportTimeout = 0;
            PVTMonitoringTimeout = 0;
            IsoActualTimeout = 0;
            DeviceNumber = 0;
            DeviceId = null;
            Port = null;
            AxesOnPort.Clear();
            TargetOffsetTable.Clear();
            ActualOffsetTable.Clear();
        }

        public AxisPortProperties() {
            AxesOnPort = new List<AxisProperties>();
            TargetOffsetTable = new List<long>();
            ActualOffsetTable = new List<long>();
            Reset();
        }

        // Copies all members apart from lists, which are cleared.
        public void PartialCopy(AxisPortProperties p) {
            CurrentComponentState = p.CurrentComponentState;
            NetIfIndex = p.NetIfIndex;
            AxisPortPropertiesIndex = 0;
            DataOffset = 0;
            DataCount = 0;
            CurrentPVTQueueSize = p.CurrentPVTQueueSize;
            CurrentInputTriggers = p.CurrentInputTriggers;
            CurrentOutputTriggers = p.CurrentOutputTriggers;
            PriorInputTriggers = p.PriorInputTriggers;
            PriorOutputTriggers = p.PriorOutputTriggers;
            StatusReportTimeout = p.StatusReportTimeout;
            TriggerReportTimeout = p.TriggerReportTimeout;
            PVTMonitoringTimeout = p.PVTMonitoringTimeout;
            IsoActualTimeout = p.IsoActualTimeout;
            DeviceNumber = p.DeviceNumber;
            DeviceId = p.DeviceId;
            Port = p.Port;
            TargetOffsetTable.Clear();
            ActualOffsetTable.Clear();
        }
    }
}