using System;
//using System.IO;
using System.Collections;
//using System.Collections.Generic;
//using System.Reflection;
//using System.Xml;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Windows;
//using System.Windows.Media.Imaging;
//using System.Windows.Controls;
using System.Text.RegularExpressions;
using Jint.Native;
//using Babel.Resources;

namespace Babel.Core {

    /// <summary>
    /// Standard shell utility commands.
    /// </summary>
    public class StandardCommands {

        [ScriptFunction("which", "Returns the full path associated with a script.",
            typeof(Jint.Delegates.Func<String, String>), "Script name.")]
        public static string Which(string scriptName) {
            if (!String.IsNullOrWhiteSpace(scriptName)) {
                string s = Scripts.GetScriptFilePath(scriptName);
                if (s != null) return s;
            }
            return "Error: script not found.";
        }

        [ScriptFunction("cat", "Returns the contents of a file or script.",
            typeof(Jint.Delegates.Func<String, String>), "File or script name.")]
        public static string Cat(string fileName) {
            if (!String.IsNullOrWhiteSpace(fileName)) {
                string fp = Scripts.GetScriptFilePath(fileName);
                if (fp == null) fp = Shell.FindFile(Shell.ConsoleShellId, fileName);
                if (fp != null) {
                    return Settings.ReadInFile(fp);
                }
            }
            return "Error: file not found.";
        }

        [ScriptFunction("row", "Select n'th row from text.",
            typeof(Jint.Delegates.Func<Int32, String, String>),
            "Row number to select, starting from 0.","Text to select row from.")]
        public static string Row(int row, string text) {
            string[] lines = Regex.Split(text, "\r\n|\r|\n");
            if (row>=0 && lines.Length > row) return lines[row];
            return "";
        }

        [ScriptFunction("col", "Select n'th column from text.",
            typeof(Jint.Delegates.Func<Int32, String, String>),
            "Column number to select, starting from 0.", "Text to select column from.")]
        public static string Col(int col, string text) {
            StringSplitter ss = new StringSplitter();
            ArrayList cols = ss.SplitArgs(text);
            if (cols!=null && col >= 0 && cols.Count > col) return (string)(cols[col]);
            return "";
        }

        [ScriptFunction("trace", "Get or set system debug tracing state.",
        typeof(Jint.Delegates.Func<String, String>),
            "New state: 'on' or 'off'.")]
        public static string TraceVar(string state="") {
            if (!String.IsNullOrWhiteSpace(state)) {
                ShellTraceListener.SetState(state.ToLower().Equals("on"));
            } 
            return (ShellTraceListener.GetState() ? "on" : "off");
        }

        [ScriptFunction("log", "Get or set system log state.",
        typeof(Jint.Delegates.Func<String, String>),
            "New state: 'on' or 'off'.")]
        public static string LogVar(string state = "") {
            if (!String.IsNullOrWhiteSpace(state)) {
                Settings.LogOn=state.ToLower().Equals("on");
            }
            return (Settings.LogOn ? "on" : "off");
        }

        [ScriptFunction("logbinary", "Get or set system log binary state.",
        typeof(Jint.Delegates.Func<String, String>),
            "New state: 'on' or 'off'.")]
        public static string LogBinaryVar(string state = "") {
            if (!String.IsNullOrWhiteSpace(state)) {
                Settings.LogBinary=state.ToLower().Equals("on");
            }
            return (Settings.LogBinary ? "on" : "off");
        }

        [ScriptFunction("debug", "Get or set system debug reporting level.",
            typeof(Jint.Delegates.Func<Int32, Int32>),
            "Level: 0 (off) to 9 (verbose).")]
        public static Int32 DebugLevelVar(int level = -1) {
            if (level>=0 && level<10) {
                Settings.DebugLevel = level;
            } 
            return Settings.DebugLevel;
        }
    }
}
