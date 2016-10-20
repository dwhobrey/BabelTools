using System;
using System.IO;
using System.Xml;
using System.Xml.Xsl;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Resources;
using System.Reflection;

namespace Babel.Core {
    public class Project {

        public readonly static string ScriptExt = ".bs";
        public readonly static string ProjectExt = ".bsx";
        public readonly static string ProjectKindPostfix = "projectstart";
        public readonly static string ProjectRootNodeName = "/data";

        static List<string> RecentProjects = null;
        static List<string> ProjectKinds = null;

        static string ProjectKind = null;
        static string ProjectSettingsFile = null;
        static string ProjectDir = null;
        static string ProjectScriptDir = null;
        static XmlDocument ProjectSettingsDoc = null;
        static XmlNode ProjectSettingsRoot = null;
        static List<string> ProjectScriptPaths = null;

        static string OldUserScriptPath = null;

        [ScriptVariable("path", "Get or set semicolon separated list of script directories.")]
        public static String UserScriptPath = null;

        public static int isnewproject = 0;

        static Project() {
            ClearProject();
        }

        public static void ClearProject() {
            ShellTraceListener.SetState(false);
            ProjectKind = null;
            ProjectSettingsFile = null;
            ProjectSettingsDoc = null;
            ProjectSettingsRoot = null;
            ProjectScriptPaths = null;
            ProjectScriptDir = null;
            OldUserScriptPath = null;
            UserScriptPath = null;
            isnewproject = 0;
        }

        [ScriptFunction("projectdir", "Get the project root directory.",
            typeof(Jint.Delegates.Func<String>))]
        public static string GetProjectDir() {
            if (String.IsNullOrWhiteSpace(ProjectDir)) return "";
            return ProjectDir;
        }

        [ScriptFunction("isnewproject", "Indicates if this is a new project.",
            typeof(Jint.Delegates.Func<Int32>))]
        public static int IsNewProject() {         
            return isnewproject;
        }

        public static string GetProjectScriptDir() {
            return ProjectScriptDir;
        }

        static void AddPathToProjectPaths(string p) {
            string[] u = Settings.ConvertStringToList(p);
            foreach (string s in u) {
                if (Directory.Exists(s)) ProjectScriptPaths.Add(s);
            }
        }

        public static List<string> GetProjectPaths() {
            if (ProjectScriptPaths == null) ProjectScriptPaths = new List<string>();
            if (UserScriptPath != OldUserScriptPath) ProjectScriptPaths.Clear();
            if (ProjectScriptPaths.Count==0) {
                string p;
                AddPathToProjectPaths(UserScriptPath);
                if (Directory.Exists(ProjectScriptDir)) ProjectScriptPaths.Add(ProjectScriptDir);
                p = System.Environment.GetEnvironmentVariable(Settings.PathEnvVarName, EnvironmentVariableTarget.User);
                AddPathToProjectPaths(p);
                p = System.Environment.GetEnvironmentVariable(Settings.PathEnvVarName);
                AddPathToProjectPaths(p);
                p = Path.Combine(Settings.ExeDir, "scripts");
                if (Directory.Exists(p)) ProjectScriptPaths.Add(p);
                if (Directory.Exists(Settings.ExeDir)) ProjectScriptPaths.Add(Settings.ExeDir);
            }
            OldUserScriptPath = UserScriptPath;
            return ProjectScriptPaths;
        }

        public static List<string> GetRecentProjects() {
            if (RecentProjects == null) {
                List<string> a = Settings.GetRegistryList("RecentProjects");
                RecentProjects = new List<string>();
                foreach (string s in a) if (File.Exists(s)) RecentProjects.Add(s);
            }
            return RecentProjects;
        }

        public static List<string> GetProjectKinds() {
            if (ProjectKinds == null) {
                string dir = Path.Combine(Settings.ExeDir, "scripts");
                ProjectKinds = new List<string>();
                IEnumerable<string> files = Directory.EnumerateFiles(dir, "*" + ProjectKindPostfix + Project.ScriptExt);
                foreach (string f in files) {
                    string name = Path.GetFileNameWithoutExtension(f);
                    if (name.Length > ProjectKindPostfix.Length) {
                        name = name.Substring(0, name.Length - ProjectKindPostfix.Length); // chop of kindPattern.
                        ProjectKinds.Add(name);
                    }
                }
            }
            return ProjectKinds;
        }

        public static void SaveRecentProjects() {
            if (RecentProjects != null) {
                Settings.SaveRegistryList("RecentProjects", RecentProjects);
            }
        }

        public static void AddRecentProject(string projectPath) {
            List<string> a = GetRecentProjects();
            if (a.Contains(projectPath)) {
                a.Remove(projectPath);
            }
            if (a.Count > 0) {
                a.Insert(0, projectPath);
            } else
                a.Add(projectPath);
            while (a.Count > 5) {
                a.RemoveAt(a.Count - 1);
            }
        }

        public static XmlNode CreateNode(string name) {
            if (ProjectSettingsDoc != null) {
                return ProjectSettingsDoc.CreateNode(XmlNodeType.Element, name, null);
            }
            return null;
        }

        public static XmlAttribute CreateAttribute(string name) {
            if (ProjectSettingsDoc != null) {
                return (XmlAttribute)ProjectSettingsDoc.CreateNode(XmlNodeType.Attribute, name, null);
            }
            return null;
        }

        public static XmlNode GetNode(string s) {
            if (ProjectSettingsRoot != null) {
                return ProjectSettingsRoot.SelectSingleNode(s);
            }
            return null;
        }

        public static XmlNodeList GetNodes(XmlNode n, string s) {
            if (n != null) {
                return n.SelectNodes(s);
            }
            return null;
        }

        public static XmlNodeList GetNodes(string s) {
            return GetNodes(ProjectSettingsRoot,s);
        }

        public static XmlNode GetChildNode(XmlNode n, string name, string id=null) {
            XmlNode c = null;
            if (n != null) {
                c = n.SelectSingleNode(name+((id==null)?"":("[@name='"+id+"']")));
                if (c == null) {
                    c = CreateNode(name);
                    if (c != null) {
                        if(id!=null) SetNodeAttributeValue(c, "name", id);
                        n.AppendChild(c);
                    }
                }
            }
            return c;
        }

        public static string GetNodeAttributeValue(XmlNode n, string attribute, string defaultValue) {
            if (n != null) {
                XmlAttribute a = n.Attributes[attribute];
                if (a != null) {
                    return a.Value;
                }
            }
            return defaultValue;
        }

        public static int GetNodeAttributeValue(XmlNode n, string attribute, int defaultValue) {
            if (n != null) {
                XmlAttribute a = n.Attributes[attribute];
                if (a != null) {
                    string v = a.Value.ToUpper();
                    if (v.StartsWith("0X"))
                        return Convert.ToInt32(v, 16);
                    return Convert.ToInt32(v);
                }
            }
            return defaultValue;
        }

        public static void SetNodeAttributeValue(XmlNode n, string attribute, string value) {
            if (n != null) {
                XmlAttribute a = n.Attributes[attribute];
                if (a == null) {
                    a = CreateAttribute(attribute);
                    n.Attributes.SetNamedItem(a);
                }
                a.Value = value;
            }
        }

        public static void SetNodeAttributeValue(XmlNode n, string attribute, int value) {
            if (n != null) {
                XmlAttribute a = n.Attributes[attribute];
                if (a == null) {
                    a = CreateAttribute(attribute);
                    n.Attributes.SetNamedItem(a);
                }
                a.Value = value.ToString();
            }
        }

        public static string GetNodeValue(XmlNode n) {
            if (n != null) {
                return n.InnerText;
            }
            return "";
        }

        public static void SetNodeValue(XmlNode n, string value) {
            if (n != null) {
                n.InnerText = value;
            }
        }

        public static void Serializer(XmlNode node, bool isSerialize) {
            MainWindow.Serializer(node, isSerialize);
            XmlNode c = Project.GetChildNode(node, "project");
            if (c == null) return;
            if (isSerialize) {
                SetNodeAttributeValue(c, "kind", ProjectKind); 
                SetNodeAttributeValue(c, "path", UserScriptPath);         
            } else {
                ProjectKind = GetNodeAttributeValue(c, "kind", "general");  
                UserScriptPath = GetNodeAttributeValue(c, "path", "");       
            }
        }

        public static void CloseProject() {
            ClearProject();
        }

        public static bool OpenProject(string fileName,string projectKind) {
            bool result = false;
            if (!String.IsNullOrWhiteSpace(fileName)) {
                try {
                    ProjectSettingsDoc = new XmlDocument();
                    ProjectSettingsDoc.PreserveWhitespace = true;
                    ProjectSettingsFile = fileName;
                    if (File.Exists(ProjectSettingsFile)) {
                        using (XmlReader reader = new XmlTextReader(ProjectSettingsFile)) {
                            if (reader != null) {
                                try {
                                    ProjectSettingsDoc.Load(reader);
                                    result = true;
                                } catch (Exception) {
                                }
                            }
                        }
                    }
                    if (!result) { // must be a new project.
                        isnewproject = 1;
                        Uri uri = new Uri("pack://application:,,,/BabelCore;component/Resources/ProjectSettings.xml", UriKind.Absolute);
                        StreamResourceInfo info = Application.GetResourceStream(uri);
                        ProjectSettingsDoc.Load(info.Stream);
                        ProjectSettingsRoot = ProjectSettingsDoc.DocumentElement;
                        if (String.IsNullOrWhiteSpace(projectKind)) projectKind = "general";
                        // Set Doc kind to projectKind, set console shellstart and shellend.
                        XmlNode n = Project.GetNode(ProjectRootNodeName+"/project");
                        if(n!=null)
                            SetNodeAttributeValue(n, "kind", projectKind);
                        n = Project.GetNode(ProjectRootNodeName + "/shells/shell[@name='1']/script");
                        if (n != null) {
                            SetNodeAttributeValue(n, "shellstart", projectKind + "projectstart");
                            SetNodeAttributeValue(n, "shellend", projectKind + "projectend");
                        }
                    } else {
                        isnewproject = 0;
                        ProjectSettingsRoot = ProjectSettingsDoc.DocumentElement;
                    }
                    try {
                        ProjectDir = Path.GetDirectoryName(ProjectSettingsFile);
                    } catch (Exception) {
                        ProjectDir = Settings.ExeDir;
                    }
                    ProjectScriptDir = Settings.SetupDirectory(ProjectDir, "scripts", Settings.ExeDir);
                    result = ProjectScriptDir != null;
                    if (result) {
                        Serializer(Project.GetNode(ProjectRootNodeName), false);
                        AddRecentProject(fileName);
                    } else {
                        Settings.BugReport("Couldn't create project script directory: " + ProjectDir + "/scripts.");
                    }
                } catch (Exception e) {
                    Settings.BugReport("Couldn't load project settings: " + e.Message);
                    result = false;
                }
            }
            return result;
        }

        public static void SaveProject() {
            if (!String.IsNullOrWhiteSpace(ProjectSettingsFile)) {
                Serializer(Project.GetNode(ProjectRootNodeName),true);
                bool wasSaved = false;
                try {
                    using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                            "Babel.Core.Resources.Indenter.xslt")) {
                        if (s != null) {
                            using (XmlReader reader = new XmlTextReader(s)) {
                                XslCompiledTransform transform = new XslCompiledTransform();
                                transform.Load(reader, XsltSettings.TrustedXslt, null);
                                XmlWriterSettings xmlWriterSettings = transform.OutputSettings;
                                using (XmlWriter writer = XmlWriter.Create(ProjectSettingsFile, xmlWriterSettings)) {
                                    transform.Transform(ProjectSettingsDoc, writer);
                                    wasSaved = true;
                                }
                            }
                        }
                    }
                } catch (Exception e) {
                    Settings.BugReport("Couldn't save project settings: " + e.Message);
                }
                if (!wasSaved) {
                    ProjectSettingsDoc.Save(ProjectSettingsFile);
                }
            }
        }
    }
}
