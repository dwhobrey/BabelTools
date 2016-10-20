using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace Babel.Core {
    public class Scripts {
        static int NewNameCounter;
        static SortedDictionary<string, string> ScriptFiles = new SortedDictionary<string, string>();

        public static string GetScriptFilePath(string scriptName) {
            if (String.IsNullOrWhiteSpace(scriptName)) return null;
            if (scriptName[0] == '.') {
                // TODO: script name = '.'.
            }
            string f = null;
            ScriptFiles.TryGetValue(scriptName,out f);
            return f;
        }

        public static bool IsValidName(string name) {
            if (GetScriptFilePath(name) != null) return true;
            return ScriptBinder.IsFunctionName(name);
        }

        public static string GetScriptNames(int numberPerLine) {
            string result = ""; int count = 0;
            foreach (KeyValuePair<string, string> v in ScriptFiles) {
                result += v.Key;
                ++count;
                if ((count % numberPerLine) != 0) result += " ";
                else result += "\n";
            }
            return result;
        }

        public static string Complete(string prefix) {
            foreach (KeyValuePair<string, string> v in ScriptFiles) {
                string s = v.Key;
                if (s.StartsWith(prefix)) {
                    if (s.Length > prefix.Length) {
                        return s.Substring(prefix.Length);
                    }
                }
            }
            return null;
        }

        public static string EscapeStringLiteral(string value) {
            return value.Replace("\\", "\\\\").Replace("'", "\\'").Replace(Environment.NewLine, "\\n");
        }

        public static string StripComments(string s) {
            if (s.StartsWith("# ") || s.StartsWith("//")) {
                return null;
            }
            return s;
        }

        public static string SubVars(string s, ArrayList args) {
            int startIndex, varIndex, k;
            string old = s;
            for (k = 0; k < 5; k++) {
                startIndex = 0;
                while ((varIndex = s.IndexOf("{", startIndex)) >= 0) {
                    startIndex = varIndex + 1;
                    int endIndex = s.IndexOf('}', varIndex);
                    if ((endIndex < 0) || (endIndex - varIndex) != 2) {
                        if (startIndex < s.Length) continue;
                        break;
                    }
                    string name = s.Substring(varIndex + 1, endIndex - varIndex - 1);
                    if ((name[0] < '0') || (name[0] > '9')) continue;
                    string val = null;
                    int n = Convert.ToInt16(name);
                    if (args != null && n < args.Count) val = (string)args[n];
                    if (val == null) val = "";
                    s = s.Substring(0, varIndex) + val + s.Substring(1 + endIndex);
                }
                if (s == old) break;
                old = s;
            }
            return s;
        }

        // Also checks if file/dir exits in testDir just in case it is not in script dictionary.
        public static string GenerateNewFileName(string baseName, string newExt, string testDir) {
            string fileName;
            if (newExt == null) newExt = "";
            do {
                fileName = baseName + ++NewNameCounter;
                if (testDir != null) {
                    if (File.Exists(Path.Combine(testDir, fileName + newExt))) fileName = null;
                }
            } while (GetScriptFilePath(fileName) != null);
            return fileName;
        }

        [ScriptFunction("rehash", "Updates the script cache by rescanning path.",
            typeof(Jint.Delegates.Func<String>))]
        public static string UpdateScriptCache() {
            string result = "";
            List<string> dirs = Project.GetProjectPaths();
            ScriptFiles.Clear();
            int k = dirs.Count;
            while (k-- > 0) {
                try {
                    string dir = dirs[k];
                    IEnumerable<string> files = Directory.EnumerateFiles(dir, "*" + Project.ScriptExt);
                    foreach (string f in files) {
                        string s = Path.GetFileNameWithoutExtension(f);
                        ScriptFiles.Add(s, f);
                    }
                } catch (Exception e) {
                    result += "Error: updating script cache:" + e.Message;
                }
            }
            return result;
        }

        public static string GetCommentHelp(string s) {
            if (s.StartsWith("# ") || s.StartsWith("//")) {
                if (s.Length == 2) return "";
                return s.Substring(2);
            }
            return null;
        }

        public static string GetScriptHelp(String scriptName) {
            string result = "";
            string filePath = GetScriptFilePath(scriptName);
            if (String.IsNullOrWhiteSpace(filePath)) return "Error: script name not found: try updating script cache.";
            try {
                StreamReader sr = new StreamReader(filePath);
                string s;
                while ((s = sr.ReadLine()) != null) {
                    s = GetCommentHelp(s);
                    if (s != null) {
                        result += s + "\n";
                    } else break;
                }
                sr.Close();
                sr = null;
            } catch (Exception e) {
                result = "Error: reading script " + scriptName + " from " + filePath + "\n"
                    + "Exception:" + e.Message;
            }
            return result;
        }

        static string ShowScript(string scriptName) {
            return Settings.ReadInFile(GetScriptFilePath(scriptName));
        }

        public static string LoadScript(ScriptEngine se, String scriptName, ArrayList args) {
            string result = ""; int lineNum = 0;
            string filePath = GetScriptFilePath(scriptName);
            if (String.IsNullOrWhiteSpace(filePath)) return "Error: script name not found: try updating script cache.";
            ArrayList ary = new ArrayList();
            ary.Add(EscapeStringLiteral(filePath));
            foreach (string s in args)
                ary.Add(EscapeStringLiteral(s));
            try {
                StreamReader sr = new StreamReader(filePath);
                StringSplitter ss = new StringSplitter(Scripts.IsValidName);
                string s, r;
                while ((s = sr.ReadLine()) != null) {
                    s = StripComments(s);
                    if (s != null) try {
                            ++lineNum;
                            r = SubVars(s, ary);
                            if (se.GetScriptFilteringEnabled())
                                result += ScriptFilter.Filter(se, ss, r);
                            else
                                result += r;
                            result += "\n";
                        } catch (Exception e) {
                            result = "Error: filtering script " + scriptName + " line:" + lineNum + ":" + s + "\n"
                                + "Exception:" + e.Message;
                        }
                }
                sr.Close();
                sr = null;
            } catch (Exception e) {
                result = "Error: loading script " + scriptName + " from " + filePath + "\n"
                    + "Exception:" + e.Message;
            }
            return result;
        }
    }
}
