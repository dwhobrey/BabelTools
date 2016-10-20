using System;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Babel.Core;
using Jint;

namespace Babel.XLink {
    /// <summary>
    /// Handles javascript commands for using an event reporter.
    /// </summary>
    public class EventReporter {

        static ConcurrentDictionary<string, EventReporter> EventReporters; // The current event reporters.

        bool FullInfo; // Report component id, event, event info.
        bool ShowInHex; // Report data in hex.
        InterruptSource Interrupt;
        string ComponentIdPattern;
        BlockingCollection<string> EventReportQueue; // Device event reports are buffered for asynchronous listening.

        static EventReporter() {
            EventReporters = new ConcurrentDictionary<string, EventReporter>();
        }

        public static void Clear() {
            EventReporters.Clear();
        }

        public static void ConfigProject() {
            Clear();
        }
        public static void StartProject() {
        }
        public static void CloseProject() {
            Clear();
        }

        public EventReporter(string componentIdPattern, InterruptSource interrupt) {
            FullInfo = true;
            ShowInHex = true;
            Interrupt = interrupt;
            ComponentIdPattern = componentIdPattern;
            EventReportQueue = new BlockingCollection<string>();
        }

        /// <summary>
        /// Delegate callback for listening to device events.
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="component"></param>
        /// <param name="val"></param>
        public void ComponentEventListener(ComponentEvent ev, Component component, object val) {
            if (Regex.IsMatch(component.Id, ComponentIdPattern, RegexOptions.IgnoreCase)) {
                string result = "";
                if (FullInfo) {
                    result = component.Id + ",Event=" + ev.Name;
                }
                if(ComponentEvent.ReadComplete==ev) {
                    byte[] p = (byte[])val;
                    int len = p.Length;
                    if (FullInfo) result += ",{";
                    if (ShowInHex) {
                        result += BitConverter.ToString(p);
                    } else {
                        result += Encoding.UTF8.GetString(p, 0, len);
                    }
                    if (FullInfo) result += "}";
                }
                if (result.Length > 0)
                    EventReportQueue.Add(result);
            }
        }

        [ScriptFunction("ropen", "Open a buffered component event reporter.\n"
            + " Returns ropen Id if successful, otherwise empty string.",
            typeof(Jint.Delegates.Func<String, String, String, String>),
            "Id of shell.", "Id for this ropen.", "Id pattern for component(s).")]
        public static string ComponentOpenReporter(string id, string ropenId, string componentIdPattern = "^.*") {
            if (!String.IsNullOrWhiteSpace(id)) {
                Shell s = Shell.GetShell(id);
                if (s != null) {
                    if (s.MainControl.Dispatcher.CheckAccess()) {
                        if (!EventReporters.ContainsKey(ropenId)) {
                            EventReporter d = new EventReporter(componentIdPattern,s.Interrupter);
                            LinkManager.Manager.AddListener(s, d.ComponentEventListener);
                            EventReporters.TryAdd(ropenId, d);
                        }
                        return ropenId;
                    } else {
                        return s.MainControl.Dispatcher.Invoke(new Func<String>(() => { return ComponentOpenReporter(s.ShellId, ropenId, componentIdPattern); }));
                    }
                }
            }
            return "";
        }

        [ScriptFunction("report", "Listen for component events and wait for report.\n"
            + " Returns component event report."
            + " Note: report buffering starts as soon as component listener is opened.",
            typeof(Jint.Delegates.Func<String, String>),
            "Id of ropen reporter.")]
        public static string ComponentEventReport(string ropenId) {
            string s = null;
            EventReporter d = null;
            if (ropenId!=null)
                EventReporters.TryGetValue(ropenId, out d);
            if (d != null && d.EventReportQueue.TryTake(out s, -1,d.Interrupt.GetToken)) return s;
            return "Error: Unknown ropen Id.";
        }

        // TODO: Command to close report.
    }
}
