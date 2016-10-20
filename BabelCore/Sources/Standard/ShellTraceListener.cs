using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Babel.Core {

    public class ShellTraceListener : TextWriterTraceListener {

        private static bool IsTracing = false;
        private static ShellTraceListener Instance = null;

        private ShellTraceListener() {
        }

        public override void WriteLine(string message) {
            if (IsTracing && !String.IsNullOrWhiteSpace(message))
                Shell.Write(Shell.LogShellId, "Trace " + message);
        }

        public static void SetState(bool turnOn) {
            if (turnOn) {
                if (Instance == null) {
                    Instance = new ShellTraceListener();
                    Debug.Listeners.Add(Instance);
                    Debug.AutoFlush = true;
                }
            } else {
                if (Instance != null) {
                    Debug.Listeners.Remove(Instance);
                    Instance = null;
                }
            }
            IsTracing = turnOn;
        }

        public static bool GetState() {
            return IsTracing;
        }
    }
}
