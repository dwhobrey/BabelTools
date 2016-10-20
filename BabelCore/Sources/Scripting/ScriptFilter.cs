using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace Babel.Core {

    public class ScriptFilter {

        public static string Filter(ScriptEngine se, StringSplitter ss, string s) {
            ArrayList args = ss.Split(s);
            if (args == null) return s;
            int n = args.Count - 1;
            s = (string)args[0];
            // Check if it's a script name, map to: load( "scriptname" , ...);
            bool isHelp = "help".Equals(s);
            string f = Scripts.GetScriptFilePath(s);
            bool isScript = (!String.IsNullOrWhiteSpace(f));
            if (!isScript)
                s += '(';
            else
                s = "load(\"" + s + "\",";
            if (n != 0) {
                for (int k = 1; k <= n; k++) {
                    string arg = (string)args[k];
                    if (isHelp||se.ApplyQuotes(arg)) {
                        if(arg.Length==0 || (arg[0]!='"'&&arg[0]!='\''))
                            arg = "'" + arg + "'";
                    }
                    s += arg;
                    if (k < n) s += ',';
                }
            } else if(isScript) s += "[]";
            if (ss.GetBuffer().EndsWith("\n")) s += ");\n";
            else s += ");";
            return s;
        }

        public static string Filter(ScriptEngine se, string s) {
            s = Scripts.StripComments(s);
            if (s != null) {
                if (s.StartsWith("? ") || (s == "?")) {
                    s = "help " + s.Substring(1);
                }
                return Filter(se, new StringSplitter(Scripts.IsValidName), s);
            }
            return null;
        }

    }
}