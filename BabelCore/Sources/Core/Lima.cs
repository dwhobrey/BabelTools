using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Management;

namespace Babel.Core {

    static class Extension {
        public static string DecodeAscii(this byte[] buffer) {
            int count = Array.IndexOf<byte>(buffer, 0, 0);
            if (count < 0) count = buffer.Length;
            return Encoding.UTF8.GetString(buffer, 0, count);
        }
    }

    public class Lima {

        static List<string> ValidLicenses = new List<string>();

        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
        private struct IdentityStruct {
            public uint hash;
            public byte idKind;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public char[] textEncoding;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
        private struct InfoStruct {
            public ushort year;
            public ushort month;
            public byte numModules;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] moduleIds;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool GetVolumeInformation(
            string rootPathName,
            StringBuilder volumeNameBuffer,
            int volumeNameSize,
            ref uint volumeSerialNumber,
            ref uint maximumComponentLength,
            ref uint fileSystemFlags,
            StringBuilder fileSystemNameBuffer,
            int nFileSystemNameSize);

        public static string GetDrive(string driveLetter) {
            string functionReturnValue = "?";
            try {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "root\\CIMV2", "SELECT * FROM Win32_DiskDrive WHERE Model LIKE '%C%'");
                foreach (ManagementObject queryObj in searcher.Get()) {
                    string s = (string)queryObj.GetPropertyValue("SerialNumber");
                    if (!String.IsNullOrWhiteSpace(s))
                        functionReturnValue = s;
                }
            } catch (ManagementException err) {
                functionReturnValue = "Error:" + err.Message;
            }
            return functionReturnValue;
        }

        [ScriptFunction("info", "Show system info.",
             typeof(Jint.Delegates.Func<String>))]
        public static string LimaTest() {
            return Lima.GetDrive("C");
        }

        [DllImport("BabelLima.dll")]
        public static extern int LimaStatus();

        static bool ReportedStatus = false;

        public static bool CheckStatus {
            get {
                string errmsg = "";
                try {
                    if (LimaStatus() == 100) return true;
                } catch (Exception e) {
                    errmsg = e.Message;
                }

                if (!ReportedStatus) {
                    ReportedStatus = true;
                    Settings.BugReport("Unable to load BabelLima.dll. "+errmsg);
                }
                return false;
            }
        }

        [DllImport("BabelLima.dll")]
        private static extern void GetIdentity(ref IdentityStruct p);

        public static string GetComputerKey() {
            IdentityStruct p = new IdentityStruct();
            if (CheckStatus) 
                GetIdentity(ref p); 
            else 
                return "Unknown";
            return new string(p.textEncoding);
        }

        public static uint GetComputerCode() {
            IdentityStruct p = new IdentityStruct();
            if (CheckStatus)
                GetIdentity(ref p);
            else
                return 0;
            return p.hash;
        }

        [DllImport("BabelLima.dll")]
        private static extern void ClearLicenses();

        [DllImport("BabelLima.dll")]
        private static extern int AddLicense(byte[] p);

        [DllImport("BabelLima.dll")]
        internal static extern int CheckModuleAuthenticated(byte moduleId,ushort authCode);

        [DllImport("BabelLima.dll")]
        private static extern int InfoLicense(byte[] p, ref InfoStruct q);

        internal static string GetLicensesDetails() {
            string s = ""; int count = 0;
            foreach (string lic in ValidLicenses) {
                InfoStruct info = new InfoStruct();
                byte[] p = System.Text.Encoding.UTF8.GetBytes(lic);
                if (InfoLicense(p, ref info) == 0) {
                    string monthName = (new DateTime(2000, 1+info.month, 1)).ToString("MMM");
                    if (count > 0) s += "\n";
                    s += lic + ", " + monthName + " " + info.year;
                    for (int k = 0; k < info.numModules; k++) {
                        s += " " + info.moduleIds[k];
                    }
                    ++count;
                }
            }
            return s;
        }

        // Read licenses from registry and add to Lima.
        // Note Lima will reject expired licenses.
        internal static void LoadLicenses() {
            List<string> lics = Settings.GetRegistryList("Licenses");
            ValidLicenses.Clear();
            if (CheckStatus) ClearLicenses(); else return;
            foreach (string lic in lics) {
                if (!String.IsNullOrWhiteSpace(lic)) {
                    byte[] p = System.Text.Encoding.UTF8.GetBytes(lic);
                    if (AddLicense(p) == 0) {
                        ValidLicenses.Add(lic);
                    }
                }
            }
            if (ValidLicenses.Count != lics.Count) {
                Settings.SaveRegistryList("Licenses", ValidLicenses);
            }
        }

        internal static void RemoveLicenses() {
            if(CheckStatus) ClearLicenses();
            ValidLicenses.Clear();
            Settings.SaveRegistryList("Licenses", ValidLicenses);
        }

        internal static string UpdateLicenses(string lics, out int numAdded, out int numInvalid) {
            numAdded = 0;
            numInvalid = 0;
            if (!CheckStatus) return "Missing BabelLima.dll";
            string r = null;
            string[] ary = Settings.ConvertStringToList(lics);
            if (ary.Length > 0) {
                foreach (string s in ary) {
                    if (!String.IsNullOrWhiteSpace(s) && !ValidLicenses.Contains(s)) {
                        byte[] p = System.Text.Encoding.UTF8.GetBytes(s);
                        int err = AddLicense(p);
                        if (err == 0) {
                            ValidLicenses.Add(s);
                            ++numAdded;
                        } else {
                            if (err == 15) r = "Expired";
                            else r = "Invalid";
                            ++numInvalid;
                        }
                    }
                }
                if (numAdded>0) {
                    Settings.SaveRegistryList("Licenses", ValidLicenses);
                }
            }
            return r;
        }
    }
}

