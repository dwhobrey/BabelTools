using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Babel.Core {

    public class Log {

        public static void d(string shellId,String tag, String message) {
            if (shellId != null && !String.IsNullOrWhiteSpace(message))
                Shell.Write(shellId, "Debug " + tag + "." + message);
        }
        public static void w(string shellId, String tag, String message) {
            if (shellId != null && !String.IsNullOrWhiteSpace(message))
                Shell.Write(shellId, "Warning " + tag + "." + message);
        }

        public static void d(String tag, String message) {
            if (!String.IsNullOrWhiteSpace(message))
                Shell.Write(Shell.LogShellId, "Debug "+ tag + "." + message);
        }
        public static void w(String tag, String message) {
            if (!String.IsNullOrWhiteSpace(message))
                Shell.Write(Shell.LogShellId, "Warning " + tag + "." + message);
        }
        public static void i(String tag, String message) {
            // Simply ignores log.
        }
    }

}