using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Xsl;
using Jint;
using Jint.Native;
using Jint.Expressions;

namespace Babel.Core {
    public class ScriptEngine : JintEngine {

        public enum ProcessAction { None, Stop, Continue, Pause, Break };

        public static string[] SystemVars = { 
                "null", "Infinity", "NaN",  "SF_", "VK_" 
        };

        public class Task {
            static int TaskIdCount = 0;
            public enum TaskAction { Every = 1, After = 2 };
            public bool IsEnabled;
            public TaskAction When;
            public int DelayDifference;
            public int SecondDelay;
            public string TaskId;
            public string Command;
            public Task(TaskAction when, int delay, string command) {
                IsEnabled = true;
                When = when;
                SecondDelay = delay;
                Command = command;
                TaskId = "" + ++TaskIdCount;
            }
            public override string ToString() {
                return TaskId
                    + "," + (IsEnabled ? "enabled" : "disabled")
                    + "," + When.ToString()
                    + "," + SecondDelay
                    + "," + DelayDifference
                    + ", (" + Command + ")"
                    ;
            }
            public static string Header() {
                return "//TaskId,Enabled,When,RTG,Delay,Command";
            }
        }

        bool AutoQuotingEnabled;
        bool ScriptFilteringEnabled;
        bool SchedulingEnabled;
        bool IsRunning;
        bool IsClosing;
        bool IsPaused;
        AutoResetEvent BreakEvent;
        Shell ParentShell;
        [ThreadStatic]
        static ScriptEngine ThreadScriptEngine;
        Thread EngineThread;
        Thread SchedulerThread;
        Thread BreakThread;
        BlockingCollection<string> ScriptQueue = new BlockingCollection<string>();
        List<Task> TaskQueue = new List<Task>();

        public static Shell GetCurrentShell() {
            if (ThreadScriptEngine == null) {
                return null;// Shell.GetShell(Shell.ConsoleShellId);
            }
            return ThreadScriptEngine.ParentShell;
        }

        private ScriptEngine(Shell shell, Options options)
            : base(options) {
            DisableSecurity();
            AllowClr(true);
            AutoQuotingEnabled = true;
            ScriptFilteringEnabled = true;
            SchedulingEnabled = true;
            IsRunning = false;
            IsClosing = false;
            IsPaused = true;
            BreakEvent = new AutoResetEvent(false);
            ParentShell = shell;
            EngineThread = new Thread(new ThreadStart(ScriptProcessor));
  //          EngineThread.SetApartmentState(ApartmentState.STA); //? STA prevents SignalAndWait working for bget.
            EngineThread.Name = "Engine:" + shell.ShellId;
            SchedulerThread = new Thread(new ThreadStart(ScheduleProcessor));
            SchedulerThread.Name = "Scheduler:" + shell.ShellId;
            BreakThread = new Thread(new ThreadStart(BreakPoller));
            BreakThread.SetApartmentState(ApartmentState.STA);
            BreakThread.Name = "Breaker:" + shell.ShellId;
        }

        public static ScriptEngine Create(Shell shell) {
            ScriptEngine se = new ScriptEngine(shell, Options.Ecmascript5 /* | Options.Strict */);
            ScriptBinder.RegisterScriptElements(se);
            se.IsPaused = false;
            se.EngineThread.Start();
            se.SchedulerThread.Start();
            se.BreakThread.Start();
            return se;
        }

        public void Close() {
            int waits = 50, curCount = ScriptQueue.Count;
            SchedulingEnabled = false;
            while ((ScriptQueue.Count > 0) && (waits-- > 0)) {
                Thread.Sleep(100);
                if (curCount != ScriptQueue.Count) {
                    waits = 50;
                    curCount = ScriptQueue.Count;
                }
            }
            IsClosing = true;
            IsPaused = false;
            BreakEngine();
            ScriptQueue.Add(";");
        }

        private ExecutionVisitor GetVisitor() {
            return Visitor;
        }

        public bool GetScriptFilteringEnabled() {
            return ScriptFilteringEnabled;
        }

        public bool IsVarInScope(string varName) {
            try {
                JsInstance i = Visitor.CurrentScope[varName];
                if (i == null || i.Class.Equals("Undefined")) return false;
                return true;
            } catch (Exception) {
            }
            return false;
        }

        public void Serializer(XmlNode node, bool isSerialize) {
            XmlNode se = Project.GetChildNode(node, "scriptengine");
            if (se == null) return;
            XmlNode p = Project.GetChildNode(se, "properties");
            if (p == null) return;
            if (isSerialize) {
                p.RemoveAll();
                foreach (string s in Visitor.CurrentScope.GetKeys()) {
                    if (s != null) {
                        try {
                            if (String.IsNullOrWhiteSpace(s)) continue;
                            Boolean ignore = false;
                            foreach (string sv in SystemVars) {
                                if (s.StartsWith(sv)) {
                                    ignore = true;
                                    break;
                                }
                            }
                            if (ignore) continue;
                            JsInstance i = Visitor.CurrentScope[s];
                            if (i != null && i.Value!=null) {
                                string v = i.Value.ToString(); // This might through an exception.
                                XmlNode c = Project.GetChildNode(p, "property", s);
                                if (c != null) {
                                    Project.SetNodeAttributeValue(c, "type", i.Class);
                                    Project.SetNodeAttributeValue(c, "value", v);
                                }
                            }
                        } catch (Exception) {

                        }
                    }
                }
            } else {
                XmlNodeList nodeList = Project.GetNodes(se, "properties/*");
                foreach (XmlNode c in nodeList) {
                    if (c != null) {
                        string id = Project.GetNodeAttributeValue(c, "name", "");
                        if(!String.IsNullOrWhiteSpace(id)) {
                            string typeName =  Project.GetNodeAttributeValue(c, "type", "String");
                            string value = Project.GetNodeAttributeValue(c, "value", null);
                            if(value!=null && typeName!=null) {
                                if(typeName.Equals("Number")) {
                                    double d = 0.0f;
                                    if(Double.TryParse(value,out d)) {
                                        SetParameter(id, d);
                                    }
                                } else if(typeName.Equals("Int32")) {
                                    try {
                                        int n = Convert.ToInt32(value,10);
                                        SetParameter(id, n);
                                    } catch(Exception) {
                                    }
                                } else if(typeName.Equals("Boolean")) {
                                    try {
                                        bool n = Convert.ToBoolean(value);
                                        SetParameter(id, n);
                                    } catch(Exception) {
                                    }
                                } else {
                                    SetParameter(id, value); 
                                }
                            }
                        }
                    }
                }
            }
        }

        // TODO: detect case when function call used as arg: "...,fred(1),...".
        public bool ApplyQuotes(string arg) {
            return AutoQuotingEnabled && arg.Length > 0
                && StringSplitter.IsAlpha(arg[0])
                && !IsVarInScope(arg);
        }

        [ScriptFunction("autoquoting", "Get or set the auto quoting flag.",
            typeof(Jint.Delegates.Func<String, String>), "Optional new state: 'True' or 'False'.")]
        public static string AutoQuoting(string state = "") {
            if (!String.IsNullOrWhiteSpace(state)) {
                ThreadScriptEngine.AutoQuotingEnabled = (state.ToUpper()[0] == 'T');
            }
            return ThreadScriptEngine.AutoQuotingEnabled.ToString();
        }

        [ScriptFunction("scriptfiltering", "Get or set the script filtering flag.",
            typeof(Jint.Delegates.Func<String, String>), "Optional new state: 'True' or 'False'.")]
        public static string ScriptFiltering(string state = "") {
            if (!String.IsNullOrWhiteSpace(state)) {
                ThreadScriptEngine.ScriptFilteringEnabled = (state.ToUpper()[0] == 'T');
            }
            return ThreadScriptEngine.ScriptFilteringEnabled.ToString();
        }

        public void PerformCommand(string cmd, bool showCmd) {
            if (!String.IsNullOrWhiteSpace(cmd)) {
                if(ScriptFilteringEnabled)
                    cmd = ScriptFilter.Filter(this, cmd);
                if (!String.IsNullOrWhiteSpace(cmd)) {
                    if (!cmd.EndsWith("\n")) cmd += "\n";
                    if (showCmd) {
                        Shell.Write(ParentShell.ShellId, cmd, true, false);
                    }
                    if (cmd.Equals("break\n") || cmd.StartsWith("break;")) {
                        BreakEngine();                   
                    } else  if (cmd.Equals("continue\n") || cmd.StartsWith("continue;")) {
                        IsPaused = false;                 
                    } else {
                        ScriptQueue.Add(cmd);
                    }
                    string state = IsPaused ? "Paused: type 'continue;' to run." : "Running.";
                    MainWindow.UpdateStatus("Shell#" + ParentShell.ShellId,state);
                }
            }
        }

        // This will keep setting Jint's exit flag to true causing it to exit.
        // Leaves it in a Paused state.
        public void BreakPoller() {
          //  ReturnStatement statement = new ReturnStatement((Expression)null);
            int count = 0;
            do {
                BreakEvent.WaitOne();
                while (IsRunning) {
                    if (count > 5) {
                        count = 0;
                        Shell.Write("Attempting to interrupt shell...");
                        EngineThread.Interrupt();
                        ConditionVariable.Interrupt(EngineThread);
                    }
                    Shell.Write("Attempting ("+ count++ +") to pause shell...");
                    Visitor.ForceBreak();
                    Thread.Sleep(1000);
                }           
            } while (!IsClosing);
        }

        public void BreakEngine() {
            if (IsRunning) {
                IsPaused = true;
            }
            BreakEvent.Set(); // Cause poller thread to exit.
        }

        private string GetLine(string text) {
            int i = text.IndexOf("\n");
            if (i > 0) return text.Substring(0, i);
            return text;
        }

        public string ApplyProcessAction(ProcessAction action) {
            switch (action) {
                case ProcessAction.Break:
                    BreakEngine();
                    break;
                case ProcessAction.Continue:
                    IsPaused = false;
                    break;
                case ProcessAction.Pause:
                    IsPaused = true;
                    break;
                case ProcessAction.Stop:
                    BreakEngine();
                    string s;
                    while (ScriptQueue.TryTake(out s)) ;
                    break;
                default:
                    break;
            }
            if (IsRunning) return "Running";
            if (IsPaused) return "Paused";
            return "Stopped";
        }

        private void ScriptProcessor() {
            ThreadScriptEngine = this;
            object v;
            string s, r;
            while (!IsClosing) {
                try {
                    if (IsPaused) {
                        Thread.Sleep(200);
                    } else {
                        s = ScriptQueue.Take();//ParentShell.Interrupter.GetToken); // Ignore interrupt here, Close will stop Q.
                        if ((s != null) && (s.Length > 0)) {
                            r = null;
                            IsRunning = true;
                            try {
                                this.Global.ErrorStack.Clear();
                                //this.SetDebugMode(true);
                                v = this.Run(s);
                                if (v != null) r = v.ToString();
                            } catch (Exception e) {
                                r = "Error in script:\n" + GetLine(s) + "\n";
                                s = e.Message;
                                r += s + "\n";
                                foreach (string m in this.Global.ErrorStack) {
                                    if(!s.StartsWith(m))
                                        r += m + "\n";
                                }
                            }
                            IsRunning = false;
                            if ((r != null) && (r.Length > 0)) {
                                Shell.Write(ParentShell.ShellId, r, false, true); // TODO: set params properly.
                            }
                        }
                    }
             //   } catch(OperationCanceledException) { // This is generated when Take is interrupted prior to closing.
             //       //IsClosing = true;
             //       break;
                } catch (Exception e) {
                    Shell.WriteLine(e.Message); // TODO: writes to console.
                }
            }
        }

        // Internal method for evaluating a script from within a script!
        private string EvalScript(string scriptName, string script) {
            Program p;
            try {
                p = JintEngine.Compile(script, Visitor.DebugMode);
            } catch (Exception e) {
                string m = "In script " + scriptName + ": ";
                throw new JsException(Visitor.Global,Visitor.Global.SyntaxErrorClass.New(m+e.Message));
            }
            try {
                p.Accept((IStatementVisitor)Visitor);
            } catch (Exception e) {
                throw new JsException(Visitor.Global,Visitor.Global.EvalErrorClass.New(e.Message));
            }
            if (Visitor.Result == null) return "";
            return Visitor.Result.ToString();
        }

        [ScriptParamsFunction("load", "Load a script file into current shell.",
           typeof(Func<String, String, String, String, String,
           String, String, String, String, String, String>),
           "Name of script.", "Optional arguments, e.g.\n"
           + " load('scriptname', a1, a2, ..., a9);")]
        public static string PerformScript(String scriptName,
             String arg1, String arg2, String arg3, String arg4, String arg5,
             String arg6, String arg7, String arg8, String arg9) {
            ArrayList ary = new ArrayList();
            if (String.IsNullOrWhiteSpace(scriptName)) {
                return "Error: script name to load not given.";
            }
            if (arg1 != null) { // Holy Mackerel! All this because Delegates don't support var args.
                ary.Add(arg1);
                if (arg2 != null) {
                    ary.Add(arg2);
                    if (arg3 != null) {
                        ary.Add(arg3);
                        if (arg4 != null) {
                            ary.Add(arg4);
                            if (arg5 != null) {
                                ary.Add(arg5);
                                if (arg6 != null) {
                                    ary.Add(arg6);
                                    if (arg7 != null) {
                                        ary.Add(arg7);
                                        if (arg8 != null) {
                                            ary.Add(arg8);
                                            if (arg9 != null) {
                                                ary.Add(arg9);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            string script = Scripts.LoadScript(ThreadScriptEngine, scriptName, ary);
            if (script.StartsWith("Error:")) return script;
            return ThreadScriptEngine.EvalScript(scriptName, script);
        }

        public string ListVars(string pattern) {
            List<string> vars = new List<string>();
            string result = "";
            foreach (string s in Visitor.CurrentScope.GetKeys()) {
                if (Regex.IsMatch(s, pattern)) {
                    vars.Add(s);
                }
            }
            vars.Sort();
            foreach (string s in vars) {
                result += s + "\n";
            }
            return result;
        }

        public string Complete(string prefix) {
            foreach (string s in Visitor.CurrentScope.GetKeys()) {
                if (s.StartsWith(prefix)) {
                    if (s.Length > prefix.Length) {
                        return s.Substring(prefix.Length);
                    }
                }
            }
            return null;
        }

        private void AddTask(Task t) {
            lock (TaskQueue) {
                bool wasAdded = false;
                int k, n = TaskQueue.Count;
                int totalDelay = 0, priorTotal;
                for (k = 0; k < n; k++) {
                    priorTotal = totalDelay;
                    totalDelay += TaskQueue[k].DelayDifference;
                    if (t.SecondDelay < totalDelay) {
                        TaskQueue[k].DelayDifference = totalDelay - t.SecondDelay;
                        t.DelayDifference = t.SecondDelay - priorTotal;
                        TaskQueue.Insert(k, t);
                        wasAdded = true;
                        break;
                    }
                }
                if (!wasAdded) {
                    t.DelayDifference = t.SecondDelay - totalDelay;
                    TaskQueue.Add(t);
                }
            }
        }

        private int FindTask(string taskId) {
            int j = -1;
            if (!String.IsNullOrWhiteSpace(taskId)) {
                lock (TaskQueue) {
                    int k, n = TaskQueue.Count;
                    for (k = 0; k < n; k++) {
                        Task t = TaskQueue[k];
                        if (taskId.Equals(t.TaskId)) {
                            j = k;
                            break;
                        }
                    }
                }
            }
            return j;
        }

        public void RemoveTask(string taskId) {
            if (String.IsNullOrWhiteSpace(taskId)) return;
            lock (TaskQueue) {
                int k = FindTask(taskId);
                if (k >= 0) {
                    if ((k + 1) < TaskQueue.Count) {
                        TaskQueue[k + 1].DelayDifference += TaskQueue[k].DelayDifference;
                    }
                    TaskQueue.RemoveAt(k);
                }
            }
        }

        private string ApplyTaskState(int tid, string state) {
            Task t = TaskQueue[tid];
            if (!String.IsNullOrWhiteSpace(state)&&(state.Length>=2)) {
                switch (state.ToUpper().Substring(0, 2)) {
                    case "EN": t.IsEnabled = true; return "enabled";
                    case "DI": t.IsEnabled = false; return "disabled";
                    case "RE": RemoveTask(t.TaskId); return "removed";
                    case "EV": t.When = Task.TaskAction.Every; return "Every";
                    case "AF": t.When = Task.TaskAction.After; return "After";
                    default: break;
                }
            }
            return t.ToString();
        }

        public string TaskState(string taskId, string state) {
            if (String.IsNullOrWhiteSpace(taskId)) return "";
            string result = "";
            lock (TaskQueue) {
                if (taskId.Equals("*")) {
                    int k;
                    if (String.IsNullOrWhiteSpace(state)) {
                        result = Task.Header() + "\n";
                        if (TaskQueue.Count == 0) result += "None\n";
                    }
                    for (k = 0; k < TaskQueue.Count; k++) {
                        Task t = TaskQueue[k];
                        string s = ApplyTaskState(k, state);
                        if (s[0] == 'r') --k;
                        result += s + "\n";
                    }
                } else {
                    int k = FindTask(taskId);
                    if (k >= 0) {
                        result = ApplyTaskState(k, state);
                    } else
                        result = "Task not found.";
                }
            }
            return result;
        }

        public string ScheduleTask(string script, string when, int delay) {
            if (String.IsNullOrWhiteSpace(when) || String.IsNullOrWhiteSpace(script)
                || (delay < 0)) return "0";
            Task.TaskAction a;
            if (when.ToUpper().StartsWith("E")) a = Task.TaskAction.Every;
            else a = Task.TaskAction.After;
            Task t = new Task(a, delay, script);
            AddTask(t);
            return t.TaskId;
        }

        private void ScheduleProcessor() {
            while (!IsClosing) {
                try {
                    if (!IsPaused && SchedulingEnabled) {
                        lock (TaskQueue) {
                            int n = TaskQueue.Count;
                            while (n-- > 0) {
                                Task t = TaskQueue[0];
                                if (--t.DelayDifference <= 0) {
                                    if (TaskQueue.Count > 1) {
                                        TaskQueue[1].DelayDifference += t.DelayDifference;
                                    }
                                    TaskQueue.RemoveAt(0);
                                    if (t.When == Task.TaskAction.Every) {
                                        AddTask(t);
                                    }
                                    if (t.IsEnabled) {
                                        ScriptQueue.Add(t.Command);
                                    }
                                } else break;
                            }
                        }
                    }
                    Thread.Sleep(1000);
                } catch (Exception e) {
                    Shell.WriteLine(e.Message); // TODO: writes to console.
                }
            }
        }
    }
}
