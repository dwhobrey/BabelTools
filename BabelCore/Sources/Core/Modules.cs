using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Controls.Primitives;

using System.Reflection;

namespace Babel.Core {

    // Modules must include a public class derived from this class.
    // The Module namespace must have the same name.
    // The class name must be the same as the module/assembly short name.
    public class Module {

        public const String TAG = "Module";

        public string Name;
        public bool Installed; // Indicates when module has been installed.

        public Module() {
            Name = null;
            Installed = false;
        }

        // Returns a list of other module names this module depends on.
        public virtual List<string> Dependencies() {
            return null;
        }

        public virtual string GetDescription() {
            Assembly a = GetType().Assembly;
            if (!Installed) return Name + ", " + Settings.GetAssemblyVersion(a) + ", not installed.";
            return Name + ", " + Settings.GetAssemblyVersion(a) + ", " + Settings.GetAssemblyCopyright(a) + " " + Settings.GetAssemblyCompany(a);
        }

        protected virtual byte GetModuleId() { return 0; }
        protected virtual ushort GetAuthCode() { return 0; }

        protected virtual bool StartApp() {
            return false;
        }
        protected virtual bool EndApp() {
            return false;
        }
        protected virtual bool OpenProject(string fileName, string projectKind, bool isNew) {
            return false;
        }
        protected virtual bool ConfigProject() {
            return false;
        }
        protected virtual bool StartProject() {
            return false;
        }
        protected virtual bool SaveProject() {
            return false;
        }
        protected virtual bool CloseProject() {
            return false;
        }
        protected virtual bool OpenShell(string shellId) {
            return false;
        }
        protected virtual bool CloseShell(string shellId) {
            return false;
        }

        internal bool IsAuthenticated() {
            try {
                return Lima.CheckModuleAuthenticated(GetModuleId(), GetAuthCode()) == 0;
            } catch (Exception) {
            }
            return false;
        }

        internal bool AuthStartApp() {
            if (IsAuthenticated()) return StartApp();
            return false;
        }
        internal bool AuthEndApp() {
            if (IsAuthenticated()) return EndApp();
            return false;
        }
        internal bool AuthOpenShell(string shellId) {
            if (IsAuthenticated()) return OpenShell(shellId);
            return false;
        }
        internal bool AuthCloseShell(string shellId) {
            if (IsAuthenticated()) return CloseShell(shellId);
            return false;
        }
        internal bool AuthOpenProject(string fileName, string projectKind, bool isNew) {
            if (IsAuthenticated()) return OpenProject(fileName, projectKind, isNew);
            return false;
        }
        internal bool AuthConfigProject() {
            if (IsAuthenticated()) return ConfigProject();
            return false;
        }
        internal bool AuthStartProject() {
            if (IsAuthenticated()) return StartProject();
            return false;
        }
        internal bool AuthSaveProject() {
            if (IsAuthenticated()) return SaveProject();
            return false;
        }
        internal bool AuthCloseProject() {
            if (IsAuthenticated()) return CloseProject();
            return false;
        }
    }

    public class Modules {

        public const String TAG = "Modules";

        static List<Module> ModuleAddIns = new List<Module>();

        internal static List<Module> GetModuleAddIns() {
            return ModuleAddIns;
        }

        public static string ModuleDescriptions() {
            string s = "";
            foreach (Module m in ModuleAddIns) {
                s += m.GetDescription() + "\n";
            }
            if (!String.IsNullOrWhiteSpace(s)) {
                s = "Modules:\n" + s;
            }
            return s;
        }

        static Type GetModuleType(Assembly a) {
            Type result = null;
            if (a != null) {
                Type[] types = a.GetExportedTypes();
                foreach (Type t in types) {
                    Type b = t.BaseType;
                    if (b != null && b.FullName.Equals("Babel.Core.Module")) return t;
                }
            }
            return result;
        }

        static Module LoadModule(string shortAssemblyName,string fileName) {
            Module m = null;
            if (!String.IsNullOrWhiteSpace(shortAssemblyName)&&!String.IsNullOrWhiteSpace(fileName))
                try {
                    Assembly assembly = Assembly.LoadFrom(fileName);
                    Type type = GetModuleType(assembly);
                    if (type == null) return null;
                    object typeInstance = Activator.CreateInstance(type);
                    m = typeInstance as Module;
                    if (m == null) return null;
                    m.Name = shortAssemblyName;
                } catch (Exception e) {
                    Settings.BugReport("Modules.LoadModule: couldn't load module '"+shortAssemblyName+"': " + e.Message);
                    m = null;
                }
            return m;
        }

        static void ResolveDependencyOrder(List<Module> unorderedModules) {
            List<string> orderedNames = new List<string>();
            int k=0,r = unorderedModules.Count;
            while (r-- > 0) {
                k = unorderedModules.Count;
                if (k == 0) break;
                while (k-- > 0) {
                    Module m = unorderedModules[k];
                    List<string> d = m.Dependencies();
                    bool resolved = (d == null || d.Count == 0);
                    if (!resolved) {
                        resolved = true;
                        foreach (string name in d) {
                            if (!orderedNames.Contains(name)) resolved = false;
                        }
                    }
                    if (resolved) {
                        orderedNames.Add(m.Name);
                        ModuleAddIns.Add(m);
                        unorderedModules.RemoveAt(k);
                    }
                }
            }
            if (k > 0) {
                foreach (Module m in unorderedModules) ModuleAddIns.Add(m);
                Settings.BugReport("Modules.ResolveDependencyOrder: warning circular dependencies.");
            }
        }

        internal static void UpdateInstalledModules() {
            foreach (Module m in ModuleAddIns) {
                if (!m.Installed && m.IsAuthenticated()) {
                    m.Installed = true;
                    Assembly a = m.GetType().Assembly;
                    if (a == null) continue;
                    Type[] types = a.GetExportedTypes();
                    foreach (Type t in types) {
                        ScriptBinder.ScanTypeForScriptElements(t);
                        Shell.ScanTypeForShellDeserializers(t);
                    }
                }
            }
            Scripts.UpdateScriptCache();
        }

        internal static Assembly FindAssembly(string assemblyName) {
            foreach (Module m in ModuleAddIns) {
                if (m.Installed && m.IsAuthenticated()) {
                    Assembly a = m.GetType().Assembly;
                    if (a != null && a.FullName.Equals(assemblyName)) return a;
                }
            }
            return null;
        }

        static void LoadModules() {
            try {
                List<Module> unorderedModules = new List<Module>();
                string dir = Path.Combine(Settings.ExeDir, "modules");
                IEnumerable<string> files = Directory.EnumerateFiles(dir, "*.dll");
                foreach (string f in files) {
                    string name = Path.GetFileNameWithoutExtension(f);
                    Module m = LoadModule(name,f);
                    if (m != null) unorderedModules.Add(m);
                }
                unorderedModules.Add(BabelCore.CoreModule); // Add Core last so it gets resolved first!
                ResolveDependencyOrder(unorderedModules);
            } catch (Exception e) {
                Settings.BugReport("Modules.LoadModules: error loading modules: " + e.Message);
            }
        }

        public static void StartApp() {
            Lima.LoadLicenses();
            LoadModules();
            if (ModuleAddIns.Count < 2) {
                Settings.BugReport("Modules.StartApp: warning: only Core module loaded.");
            }
            UpdateInstalledModules();
            foreach (Module m in ModuleAddIns) {
                try {
                    if (m.AuthStartApp()) break;
                } catch (Exception) {
                }
            }
        }
        public static void EndApp() {
            int k = ModuleAddIns.Count;
            while(k-->0) {
                try {
                    if (ModuleAddIns[k].AuthEndApp()) break;
                } catch (Exception) {
                }
            }
        }
        // Call in 1st order.
        public static void OpenProject(string fileName, string projectKind, bool isNew) {
            foreach (Module m in ModuleAddIns) {
                try {
                    if (m.AuthOpenProject(fileName, projectKind, isNew)) break;
                } catch (Exception e) {
                    Log.i(TAG, "OpenProject exception:" + e.Message);
                }
            }
        }
        // Call in 1st order.
        public static void ConfigProject() {
            foreach (Module m in ModuleAddIns) {
                try {
                    if (m.AuthConfigProject()) break;
                } catch (Exception e) {
                    Log.i(TAG, "ConfigProject exception:" + e.Message);
                }
            }
        }
        // Call in 1st order.
        public static void StartProject() {
            foreach (Module m in ModuleAddIns) {
                try {
                    if (m.AuthStartProject()) break;
                } catch (Exception e) {
                    Log.i(TAG, "StartProject exception:" + e.Message);
                }
            }
        }

        // Call Core last.
        public static void SaveProject() {
            int k = ModuleAddIns.Count;
            while (k-- > 0) {
                try {
                    if (ModuleAddIns[k].AuthSaveProject()) break;
                } catch (Exception) {
                }
            }
        }

        // Call Core last.
        public static void CloseProject() {
            int k = ModuleAddIns.Count;
            while (k-- > 0) {
                try {
                    if (ModuleAddIns[k].AuthCloseProject()) break;
                } catch (Exception e) {
                    Log.i(TAG, "CloseProject exception:" + e.Message);
                }
            }
        }

        public static void OpenShell(string shellId) {
            foreach (Module m in ModuleAddIns) {
                try {
                    if (m.AuthOpenShell(shellId)) break;
                } catch (Exception) {
                }
            }
        }
        public static void CloseShell(string shellId) {
            int k = ModuleAddIns.Count;
            while (k-- > 0) {
                try {
                    if (ModuleAddIns[k].AuthCloseShell(shellId)) break;
                } catch (Exception) {
                }
            }
        }
    }
}
