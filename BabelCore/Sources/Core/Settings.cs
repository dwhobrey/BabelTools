using System;
using System.Reflection;
using System.Text;
using System.IO;
using System.Windows;
using System.Windows.Resources;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Babel.Core {

    public class Settings {

        public static int DebugLevel = 5; // Debug reporting level.
        public static bool LogOn = true;
        public static bool LogBinary = false; // Dump logs as hex strings.

        // Default Long SN format (29 chars):
        // CCC-NNNNNNNN-MMM-TTT-JJKL-X-P, where product code is replaced by Lima Computer Code.
        // 12345678901234567890123456789
        // Short SN format (15 chars):
        // CCC-NNNNN-MTJ-P
        // 123456789012345

        public static string SerialNumberFormat = "DJW-{0:X8}-BBF-AND-0100-{1}-{2}\0"; // Must have a trailing null included as part of string.

        // Ident strings must be < 30 & null terminated - as part of string length.
        public static String AppManufacturer = "Darren Whobrey\0";
        public static String AppModelName = "Babel Fish\0";
        public static String AppDescription = "MCU Monitoring Application\0";
        public static String AppVersion = "1.0.0.0\0";
        public static String AppURI = "http://www.genericminds.com\0";

        public static string AppName = "BabelApp";

        public static string AppFileName = null;
        public static string PathEnvVarName = null; // File search path, set to AppFileName+"Path". 
        public static string ExeDir = null;

        public readonly static char[] PathSeparators = new Char[] { ',', ';' };
        public readonly static char[] PathDirSeparators 
            = new Char[] { Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar};

        static int NewFolderCounter;

        static Settings() {
            try {
                AppName = GetAssemblyTitle(Assembly.GetEntryAssembly());
                string exePath = Assembly.GetEntryAssembly().Location;
                AppFileName = Path.GetFileNameWithoutExtension(exePath);
                PathEnvVarName = AppFileName + "Path";
                try {
                    ExeDir = Path.GetDirectoryName(exePath);
                } catch (Exception) {
                    ExeDir = "c:\\";
                }
            } catch (Exception e) {
                BugReport("Couldn't init settings: " + e.Message);
            }
        }

        public static String SerialNumberFormatter(uint productCode, Char protocolPostfix, Char netIfIndex) {
            return String.Format(SerialNumberFormat, productCode, protocolPostfix, netIfIndex);
        }

        public static string ConvertListToString(System.Collections.IEnumerable a) {
            string r = "";
            if (a != null)
                foreach (object o in a) r += o.ToString() + ";";
            return r;
        }

        public static string[] ConvertStringToList(string s) {
            if (String.IsNullOrWhiteSpace(s)) return new string[0];
            return s.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries);
        }

        public static void BugReport(string s) {
            MessageDialog.Show(s);
        }

        public static Popup MakeAppToast(string s) {
            Popup p = new Popup();
            Grid g = new Grid();
          //  g.Background = new SolidColorBrush(Color.FromArgb(0xFF,0x00,0xFF,0xCC));
            g.Background = Brushes.Green;
            g.Width = 200.0;
            g.Height = 100.0;
            TextBlock t = new TextBlock();
            t.Text = s;
            t.Foreground = Brushes.Yellow;
            t.TextAlignment = TextAlignment.Center;
            t.HorizontalAlignment=HorizontalAlignment.Stretch;
            t.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(t);
            p.Child = g;
            p.PlacementTarget = Application.Current.MainWindow;
            p.Placement = PlacementMode.Center;
            return p;
        }

        private static void ShowAppToast(Popup p) {
            p.IsOpen = true;
        }

        private static void CancelAppToast(Popup p) {
            p.IsOpen = false;
        }

        public static Popup MakeToast(string s) {
            return Application.Current.Dispatcher.Invoke(new Func<Popup>(() => { return MakeAppToast(s); }));
        }
        public static void ShowToast(Popup p) {
            Application.Current.Dispatcher.Invoke((Action)(() => { ShowAppToast(p); }));
        }
        public static void CancelToast(Popup p) {
            Application.Current.Dispatcher.Invoke((Action)(() => { CancelAppToast(p); }));
        }

        public static string GetRelativePath(string basePath, string absPath) {
            try {
                if (String.IsNullOrWhiteSpace(basePath) || String.IsNullOrWhiteSpace(absPath)) return absPath;
                Uri baseUri = new Uri(basePath);
                Uri absUri = new Uri(absPath);
                Uri rel = baseUri.MakeRelativeUri(absUri);
                return rel.ToString();
            } catch (Exception) {
            }
            return "";
        }

        public static string GetAbsolutePath(string basePath, string relPath) {
            try {
                if(!String.IsNullOrWhiteSpace(relPath))
                    relPath = Uri.UnescapeDataString(relPath);
                if (String.IsNullOrWhiteSpace(basePath)) return relPath;
                if (String.IsNullOrWhiteSpace(relPath)) return basePath;
                Uri baseUri = new Uri(basePath);
                Uri absUri = new Uri(baseUri, relPath);
                return Uri.UnescapeDataString(absUri.AbsolutePath);
            } catch (Exception) {
            }
            return "";
        }

        public static string GenerateNewFolderName(string baseName, string testDir) {
            string folderName;
            do {
                folderName = baseName + ++NewFolderCounter;
            } while (testDir != null && Directory.Exists(Path.Combine(testDir, folderName)));
            return folderName;
        }

        public static string GetRegistryValue(string keyName) {
            string v = null;
            try {
                RegistryKey appKey = System.Windows.Forms.Application.UserAppDataRegistry;
                if (appKey != null) {
                    RegistryKey r = appKey.CreateSubKey("Settings");
                    if (r != null) {
                        v = (string)r.GetValue(keyName, "");
                        r.Close();
                    }
                    appKey.Close();
                }
            } catch (Exception) {
            }
            if (v == null) v = "";
            return v;
        }

        public static void SaveRegistryValue(string keyName, string value) {
            try {
                RegistryKey appKey = System.Windows.Forms.Application.UserAppDataRegistry;
                if (appKey != null) {
                    RegistryKey r = appKey.CreateSubKey("Settings");
                    if (r != null) {
                        r.SetValue(keyName, value == null ? "" : value);
                        r.Close();
                    }
                    appKey.Close();
                }
            } catch (Exception) {
            }
        }

        public static List<string> GetRegistryList(string keyName) {
            List<string> values = new List<string>();
            string p = GetRegistryValue(keyName);
            if (!String.IsNullOrWhiteSpace(p)) {
                string[] a = ConvertStringToList(p);
                foreach (string s in a) {
                    values.Add(s);
                }
            }
            return values;
        }

        public static void SaveRegistryList(string keyName, System.Collections.IEnumerable values) {
            SaveRegistryValue(keyName, ConvertListToString(values));
        }

        public static string FindFileOnPath(List<string> pathList, string fileName) {
            if (File.Exists(fileName)) return fileName;
            foreach(string p in pathList) {
                try {
                    string s = Path.Combine(p, fileName);
                    if (File.Exists(s)) return s;
                } catch (Exception) {
                }
            }
            return null;
        }

        public static string ReadInFile(string filePath) {
            string result = "";
            if (String.IsNullOrWhiteSpace(filePath)) return result;
            try {
                using (StreamReader sr = new StreamReader(filePath)) {
                    string s = null;
                    while ((s = sr.ReadLine()) != null) {
                        result += s + "\n";
                    }
                }
            } catch (Exception e) {
                result = "Error: unable to read file" + filePath + ", " + e.Message;
            }
            return result;
        }

        public static string GetFileNameFromUser(bool exists, string ext, string description) {
            string fileName = null;
            try {
                System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog();
                openFileDialog.InitialDirectory = Settings.GetRegistryValue("LastProjectDir");
                openFileDialog.DefaultExt = ext;
                openFileDialog.Filter = description + " files (*" + ext + ")|*" + ext + "|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;
                openFileDialog.CheckFileExists = exists;
                
                if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                    fileName = openFileDialog.FileName;
                    if (!String.IsNullOrWhiteSpace(fileName)) {
                        if (!Path.HasExtension(fileName)) {
                            fileName += ext;
                        }
                        string p;
                        try {
                            p = Path.GetDirectoryName(fileName);
                        } catch (Exception) {
                            p = null;
                        }
                        if (String.IsNullOrWhiteSpace(p))
                            p = Path.GetPathRoot(fileName);
                        Settings.SaveRegistryValue("LastProjectDir", p);
                    } else fileName = null;
                }
            } catch (Exception) {
                fileName = null;
            }
            return fileName;
        }

        public static string SaveFileNameFromUser(string fileName, string ext, string description) {
            string saveName = null;
            try {
                if (fileName == null) fileName = "";
                System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog();
                saveFileDialog.FileName = fileName;
                string p = null;
                try {
                    p = Path.GetDirectoryName(fileName);
                } catch (Exception) {
                    p = null;
                }
                if (p == null) p = Settings.GetRegistryValue("LastProjectDir");
                if (p == null) p = Project.GetProjectScriptDir();
                saveFileDialog.InitialDirectory = p;
                saveFileDialog.DefaultExt = ext;
                saveFileDialog.Filter = description + " files (*" + ext + ")|*" + ext + "|All files (*.*)|*.*";
                saveFileDialog.FilterIndex = 1;
                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.CheckFileExists = false;
                if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                    saveName = saveFileDialog.FileName;
                    if (!String.IsNullOrWhiteSpace(saveName)) {
                        if (!Path.HasExtension(saveName)) {
                            saveName += ext;
                        }
                        try {
                            p = Path.GetDirectoryName(saveName);
                        } catch (Exception) {
                            p = null;
                        }
                        if (String.IsNullOrWhiteSpace(p))
                            p = Path.GetPathRoot(saveName);
                        Settings.SaveRegistryValue("LastProjectDir", p);
                    } else
                        saveName = null;
                }
            } catch (Exception) {
                saveName = null;
            }
            return saveName;
        }

        public static string SetupDirectory(string baseDir, string dirName, string defaultDir) {
            string resultDir = null;
            try {
                if (String.IsNullOrWhiteSpace(baseDir)) baseDir = defaultDir;
                resultDir = Path.Combine(baseDir, dirName);
                if (!Directory.Exists(resultDir)) {
                    Directory.CreateDirectory(resultDir);
                }
            } catch (Exception) {
                resultDir = null;
            }
            return resultDir;
        }

        #region Assembly info helpers.
        public static string GetAssemblyTitle(Assembly a) {
            object[] attributes = a.GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
            if (attributes.Length > 0) {
                AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
                if (titleAttribute.Title != "") {
                    return titleAttribute.Title;
                }
            }
            return System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
        }

        public static string GetAssemblyVersion(Assembly a) {
            return a.GetName().Version.ToString();
        }

        public static string GetAssemblyDescription(Assembly a) {
            object[] attributes = a.GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
            if (attributes.Length == 0) {
                return "";
            }
            return ((AssemblyDescriptionAttribute)attributes[0]).Description;
        }

        public static string GetAssemblyProduct(Assembly a) {
            object[] attributes = a.GetCustomAttributes(typeof(AssemblyProductAttribute), false);
            if (attributes.Length == 0) {
                return "";
            }
            return ((AssemblyProductAttribute)attributes[0]).Product;
        }

        public static string GetAssemblyCopyright(Assembly a) {
            object[] attributes = a.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
            if (attributes.Length == 0) {
                return "";
            }
            return ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
        }

        public static string GetAssemblyCompany(Assembly a) {
            object[] attributes = a.GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
            if (attributes.Length == 0) {
                return "";
            }
            return ((AssemblyCompanyAttribute)attributes[0]).Company;
        }
        #endregion
    }
}
