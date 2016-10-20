using System;
using System.Xml;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Babel.Core;
using Babel.XLink;
using Jint;

namespace Babel.Usb {

    // Class for managing usb access permissions,
    // such as lists of valid VIDs & PIDs.
    public class UsbPermissionValidator {

        private class VPRange {

            public int LoVid, HiVid;
            public int LoPid, HiPid;

            public VPRange(int loVid, int hiVid, int loPid, int hiPid) {
                LoVid = loVid;
                HiVid = hiVid;
                LoPid = loPid;
                HiPid = hiPid;
            }
        }

        public static bool AllowAllUsbDevices = true;
        public const int GoogleVID = 0x18D1;
	    public const int GoogleAccessoryPIDLo = 0x2D00;
	    public const int GoogleAccessoryPIDHi = 0x2D01;

        private static Object ClassLock = new Object();
        private static List<VPRange> Includes = new List<VPRange>();
        private static List<VPRange> Excludes = new List<VPRange>();

        static UsbPermissionValidator() {
            Init();
        }

        public static void AddInclude(int loVid, int hiVid, int loPid, int hiPid) {
            Includes.Add(new VPRange(loVid, hiVid, loPid, hiPid));
        }
        public static void AddExclude(int loVid, int hiVid, int loPid, int hiPid) {
            Excludes.Add(new VPRange(loVid, hiVid, loPid, hiPid));
        }
        public static void Init() {
            AddInclude(0x0, 0xffff, 0x0, 0xffff);
            AddInclude(GoogleVID, GoogleVID, GoogleAccessoryPIDLo, GoogleAccessoryPIDHi);
        }
        public static void Clear() {
            lock (ClassLock) {
                Includes.Clear();
                Excludes.Clear();
            }
        }

        public static bool CheckAllowed(int vid, int pid) {
            if (AllowAllUsbDevices) return true;
            lock (ClassLock) {
                // First check exclude list.
                foreach (VPRange r in Excludes) {
                    if (vid >= r.LoVid && vid <= r.HiVid && pid >= r.LoPid && pid <= r.HiPid) return false;
                }
                foreach (VPRange r in Includes) {
                    if (vid >= r.LoVid && vid <= r.HiVid && pid >= r.LoPid && pid <= r.HiPid) return true;
                }
            }
            return false;
        }
    }

}
