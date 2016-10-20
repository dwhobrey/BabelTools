using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace Babel.Core {
    public class Error {

        [DllImport("kernel32.dll")]
        public static extern int GetLastError();

        public static string Message() {
            int err = GetLastError();
            return "Error " + err.ToString("X") + ":" + new Win32Exception(err).Message;
        }
    }
}
