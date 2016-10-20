using System;
using System.Xml;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text;
using System.Diagnostics;
using Babel.Core;
using Babel.XLink;
using Jint;

using libusbK;

namespace Babel.Usb {

    public class UsbLinkManager {

        public const String TAG = "UsbLinkManager";

        const int MonitorConnectionsPeriod = 10000; //ms, time between connection tests.

        public static UsbLinkManager Manager;

        private Object UsbConnectionLock;

        bool IsRunning = false;
        MonitorConnectionsThread ConnectionMonitorThread;
        KHOT_PARAMS HotInitParams;
        HotK Hot;

        private UsbLinkManager() {
            bool result = false;
            try {
                result = AllKFunctions.LibK_Context_Init(IntPtr.Zero, IntPtr.Zero);
            } catch (Exception) {
                result = false;
            }
            if (!result) {
                if (Settings.DebugLevel > 0) Log.d(TAG, "Could not initialise Usb driver.");
            }
            UsbConnectionLock = new Object();
            IsRunning = true;
            ConnectionMonitorThread = new MonitorConnectionsThread();
        }

        public static void Init() {
            Manager = new UsbLinkManager();
            Manager.SetupHotPlug();
            Manager.ConnectionMonitorThread.Start();
        }

        public void Close() {
            IsRunning = false; // This stops ConnectionMonitoring.
            ConnectionMonitorThread = null;
            FreeHotPlug();
        }

        public static void ConfigProject() {
            Init();
        }
        public static void StartProject() {
        }
        public static void CloseProject() {
            if (Manager != null) {
                Manager.Close();
                Manager = null;
            }
        }

        // Scan the current usb devices to check all connected.
        private void CheckConnections() {
            lock (UsbConnectionLock) {
                KLST_DEVINFO_HANDLE deviceInfo;
                LstK lst = new LstK(KLST_FLAG.NONE);
                int deviceCount = 0;
                lst.Count(ref deviceCount);
                while (lst.MoveNext(out deviceInfo)) {
                    LinkDevice d = LinkManager.Manager.FindDevice(UsbLinkDevice.GetDevIdFromHandle(deviceInfo));
                    // Check if d is a suspended Acc with same SN but different V/P.
                    if (d == null || ((d.State!=ComponentState.Working) /*&& !UsbLinkDevice.CompareVidPid(d, deviceInfo)*/)) {
                        if (UsbPermissionValidator.CheckAllowed(deviceInfo.Common.Vid, deviceInfo.Common.Pid)) {
                            UsbLinkDevice.TryOpeningDevice(deviceInfo);
                        }
                    }
                }
                lst.Free();
            }
        }

        /// <summary>
        /// Thread for notifying if any missed new connections.
        /// </summary>
        public class MonitorConnectionsThread {

            public Thread Task;

            public MonitorConnectionsThread() {
                Task = new Thread(new ThreadStart(Run));
                Task.Name = "MonitorConnectionsThread";
                Task.Priority = ThreadPriority.Normal;
            }

            public void Start() { if (Task != null) Task.Start(); }

            public void Run() {
                try {
                    while (Manager != null && Manager.IsRunning) {
                        try {
                            Manager.CheckConnections();
                            Thread.Sleep(MonitorConnectionsPeriod);
                        } catch (Exception) {
                            // ignore.
                        }
                    }
                } catch (Exception) {
                    // Ignore: probably null reference when Manager closed.
                }
            }
        }

        private void SetupHotPlug() {
            if (Hot == null) {
                // Set up a wild filter because we're interesed in various usb devices.
                HotInitParams.PatternMatch.DeviceInterfaceGUID = "*";

                // The PLUG_ALL_ON_INIT flag will force plug events for matching devices that are already connected.
                HotInitParams.Flags = KHOT_FLAG.PLUG_ALL_ON_INIT;

                HotInitParams.OnHotPlug = OnHotPlug;

                // Set initial hot handle user context.
                // This is used to count connected devices and detect the first OnHotPlug event (Int32.MaxValue).
                AllKFunctions.LibK_SetDefaultContext(KLIB_HANDLE_TYPE.HOTK, new IntPtr(Int32.MaxValue));

                // Start hot-plug detection.
                Hot = new HotK(ref HotInitParams);
            }
        }

        private void FreeHotPlug() {
            if (Hot != null) {
                Hot.Free();
                Hot = null;
            }
        }

        private static void OnHotPlug(KHOT_HANDLE hotHandle,
                                      KLST_DEVINFO_HANDLE deviceInfo,
                                      KLST_SYNC_FLAG plugType) {
            LinkDevice d;
            int totalPluggedDeviceCount = (int)hotHandle.GetContext().ToInt64();
            if (totalPluggedDeviceCount == int.MaxValue) {
                // OnHotPlug is being called for the first time on handle: hotHandle.Pointer.
                totalPluggedDeviceCount = 0;
            }
            switch (plugType) {
                case KLST_SYNC_FLAG.ADDED: // Arrival.
                    totalPluggedDeviceCount++;
                    lock (Manager.UsbConnectionLock) {
                        if (Settings.DebugLevel > 4) {
                            Log.d(TAG, "OnHotPlug.Added: " + UsbLinkDevice.DeviceInfoToString(deviceInfo));
                        }
                        if (UsbPermissionValidator.CheckAllowed(deviceInfo.Common.Vid, deviceInfo.Common.Pid)) {
                            UsbLinkDevice.TryOpeningDevice(deviceInfo);
                        } else {
                            if (Settings.DebugLevel > 4) {
                                Log.d(TAG, "OnHotPlug.Added: Not allowed: " + UsbLinkDevice.DeviceInfoToString(deviceInfo));
                            }
                        }
                    }
                    break;
                case KLST_SYNC_FLAG.REMOVED: // Removal.
                    totalPluggedDeviceCount--;
                    lock (Manager.UsbConnectionLock) {
                        if (Settings.DebugLevel > 4) {
                            Log.d(TAG, "OnHotPlug.Removed: " + UsbLinkDevice.DeviceInfoToString(deviceInfo));
                        }
                        d = LinkManager.Manager.FindDevice(UsbLinkDevice.GetDevIdFromHandle(deviceInfo));
                        UsbLinkDevice u = d as UsbLinkDevice;
                        if (u != null) u.Disconnect(deviceInfo); // Disconnect device but keep in Active list.
                    }
                    break;
                default:
                    return;
            }
            hotHandle.SetContext(new IntPtr(totalPluggedDeviceCount));
        }

        [ScriptFunction("usb", "Returns a list of usb devices.",
            typeof(Jint.Delegates.Func<String>))]
        public static string GetUsbDevices() {
            string report = "usb:\n";
            KLST_DEVINFO_HANDLE deviceInfo;
            LstK lst = new LstK(KLST_FLAG.NONE);
            while (lst.MoveNext(out deviceInfo)) {
                report += deviceInfo.SerialNumber
                    + ",Connected=" + deviceInfo.Connected
                    + ",Service=" + deviceInfo.Service
                    + ",Pid=" + deviceInfo.Common.Pid.ToString("X4")
                    + ",Vid=" + deviceInfo.Common.Vid.ToString("X4")
                    + ",Mfg=" + deviceInfo.Mfg
                    + ",DeviceDesc=" + deviceInfo.DeviceDesc
                    + "\n";
            }
            lst.Free();
            return report;
        }

        [ScriptFunction("usbaccsn", "Get or set accessory serial number.",
            typeof(Jint.Delegates.Func<String,String>),
            "New serial number, recommended format: CCC-MMM-TTT-JJKL-NNNNNNNN-XX-P")]
        public static string UsbAccSNVar(string sn=null) {
            if (sn != null) {
                if (sn.Length > 30) sn = sn.Substring(0, 30);
                sn += "\0";
                UsbLinkAccessory.ACCSerialNumber = sn;
            }
            return UsbLinkAccessory.ACCSerialNumber;
        }
    }
}
