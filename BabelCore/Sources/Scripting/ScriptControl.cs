using System;
using System.Windows;
using System.Xml;

using Babel.Resources;

namespace Babel.Core {

    public class ScriptControl : ShellControl, ICompleter {

        protected bool IsScriptControlClosing;
        protected bool IsInteractive;
        protected bool UseTimePrompt;
        protected string ShellStartScriptName;
        protected string ShellEndScriptName;
        protected ScriptEngine ShellEngine;
        protected XmlNode ParentXmlNode;

        public ScriptControl()
            : base() {
        }

        protected ScriptControl(string ownerShellId, string startScriptName = "", string endScriptName = "", 
            bool isInteractive = false, bool useTimePrompt = false)
            : base(ownerShellId) {
            ShellStartScriptName = startScriptName;
            ShellEndScriptName = endScriptName;
            IsInteractive = isInteractive;
            UseTimePrompt = useTimePrompt;
            ShellEngine = null;
            ParentXmlNode = null;
        }

        public override void Init() {
            base.Init();
            ShellEngine = ScriptEngine.Create(Shell.GetShell(OwnerShellId));
            Clear();
            if(!String.IsNullOrWhiteSpace(ShellStartScriptName))
                RunScript(OwnerShellId, ShellStartScriptName);
            if (ParentXmlNode != null) {
                ShellEngine.Serializer(ParentXmlNode, false);
                ParentXmlNode = null;
            }
        }

        public override void Close() {
            base.Close();
            if (IsScriptControlClosing) return;
            IsScriptControlClosing = true;
            if(!String.IsNullOrWhiteSpace(ShellEndScriptName))
                RunScript(OwnerShellId, ShellEndScriptName);
            if (ShellEngine != null) {
                ShellEngine.Close();
                ShellEngine = null;
            }
        }

        // On entry, node is the shell node.
        public override void Serializer(XmlNode node, bool isSerialize) {
            base.Serializer(node, isSerialize);
            XmlNode c = Project.GetChildNode(node, "script");
            if (c == null) return;
            if (isSerialize) {
                Project.SetNodeAttributeValue(c, "shellstart", ShellStartScriptName);
                Project.SetNodeAttributeValue(c, "shellend", ShellEndScriptName);
                Project.SetNodeAttributeValue(c, "isinteractive", IsInteractive.ToString());
                Project.SetNodeAttributeValue(c, "usetimeprompt", UseTimePrompt.ToString());
                if (ShellEngine != null) {
                    ShellEngine.Serializer(c, true);
                }
            } else {
                ParentXmlNode = c;
                ShellStartScriptName = Project.GetNodeAttributeValue(c, "shellstart", "");
                ShellEndScriptName = Project.GetNodeAttributeValue(c, "shellend", "");
                IsInteractive = Convert.ToBoolean(Project.GetNodeAttributeValue(c, "isinteractive", "false"));
                UseTimePrompt = Convert.ToBoolean(Project.GetNodeAttributeValue(c, "usetimeprompt", "false"));
            }
        }

        public string Complete(string prefix) {
            string postfix = null;
            if(ShellEngine!=null) postfix = ShellEngine.Complete(prefix);
            if (postfix == null) postfix = ScriptBinder.Complete(prefix);
            return postfix;
        }

        [ScriptFunction("run", "Run command on shell.\n Adds to shells command queue.",
            typeof(Jint.Delegates.Func<String, String, bool, String>),
            "Id of shell to run script on.", "Script or commands to run.", "Flag to echo command.")]
        public static string RunScript(string id, string script, bool showCmd = false) {
            if (!String.IsNullOrWhiteSpace(id) && !String.IsNullOrWhiteSpace(script)) {
                Shell s = Shell.GetShell(id);
                if (s != null) {
                    if (s.MainControl.Dispatcher.CheckAccess()) {
                        if (s.MainControl is ScriptControl) {
                            ScriptControl c = s.MainControl as ScriptControl;
                            if (c.ShellEngine != null) c.ShellEngine.PerformCommand(script, showCmd);
                        }
                    } else {
                        s.MainControl.Dispatcher.Invoke((Action)(() => { RunScript(s.ShellId, script, showCmd); }));
                    }
                }
            }
            return "";
        }

        public static string Break(string id) {
            if (!String.IsNullOrWhiteSpace(id)) {
                Shell s = Shell.GetShell(id);
                if (s != null) {
                    if (s.MainControl.Dispatcher.CheckAccess()) {
                        if (s.MainControl is ScriptControl) {
                            ScriptControl c = s.MainControl as ScriptControl;
                            if (c.ShellEngine != null) c.ShellEngine.BreakEngine();
                        }
                    } else {
                        s.MainControl.Dispatcher.Invoke((Action)(() => { Break(s.ShellId); }));
                    }
                }
            }
            return "";
        }

        [ScriptFunction("process", "Get or set script processor state.",
            typeof(Jint.Delegates.Func<String, String, String>), "Id of shell.",
            "Optional action: 'break', 'continue', 'pause', 'stop'.")]
        public static string Process(string id, string action = "*") {
            if (!String.IsNullOrWhiteSpace(id)) {
                Shell s = Shell.GetShell(id);
                if (s != null) {
                    if (String.IsNullOrWhiteSpace(action)) action = "*";
                    if (s.MainControl.Dispatcher.CheckAccess()) {
                        ScriptEngine.ProcessAction p = ScriptEngine.ProcessAction.None;
                        switch (action.ToUpper()[0]) {
                            case 'B': p = ScriptEngine.ProcessAction.Break; break;
                            case 'C': p = ScriptEngine.ProcessAction.Continue; break;
                            case 'P': p = ScriptEngine.ProcessAction.Pause; break;
                            case 'S': p = ScriptEngine.ProcessAction.Stop; break;
                            default: break;
                        }
                        if (s.MainControl is ScriptControl) {
                            ScriptControl c = s.MainControl as ScriptControl;
                            if (c.ShellEngine != null) return c.ShellEngine.ApplyProcessAction(p);
                        }
                        return "Error: no script engine.";
                    } else {
                        return s.MainControl.Dispatcher.Invoke(new Func<string>(()
                            => { return Process(s.ShellId, action); }));
                    }
                }
            }
            return "";
        }

        [ScriptFunction("schedule", "Schedule script to run on shell.\n Adds to shells task queue.",
            typeof(Jint.Delegates.Func<String, String, String, Int32, String>),
            "Id of shell to run task on.", "Script or commands to run.", "Either 'Every' or 'After'.",
            "Number of seconds between scheduling.")]
        public static string Schedule(string id, string script, string when, Int32 delay) {
            if (!String.IsNullOrWhiteSpace(id) && !String.IsNullOrWhiteSpace(script)) {
                Shell s = Shell.GetShell(id);
                if (s != null) {
                    if (s.MainControl.Dispatcher.CheckAccess()) {
                        if (s.MainControl is ScriptControl) {
                            ScriptControl c = s.MainControl as ScriptControl;
                            if (c.ShellEngine != null) c.ShellEngine.ScheduleTask(script, when, delay);
                            else return "Error: no script engine.";
                        }
                    } else {
                        s.MainControl.Dispatcher.Invoke((Action)(()
                            => { Schedule(s.ShellId, script, when, delay); }));
                    }
                }
            }
            return "";
        }

        [ScriptFunction("task", "Get or set task(s) state.",
            typeof(Jint.Delegates.Func<String, String, String, String>),
            "Id of shell for task(s).", "Task id or '*'.", "Optional task command.\n"
            + " Commands: 'enable','disable','remove','every','after'.")]
        public static string TaskState(string id, string taskId = "*", string state = "") {
            if (!String.IsNullOrWhiteSpace(id)) {
                if (String.IsNullOrWhiteSpace(taskId)) taskId = "*";
                Shell s = Shell.GetShell(id);
                if (s != null) {
                    if (s.MainControl.Dispatcher.CheckAccess()) {
                        if (s.MainControl is ScriptControl) {
                            ScriptControl c = s.MainControl as ScriptControl;
                            if (c.ShellEngine != null) return c.ShellEngine.TaskState(taskId, state);
                            return "Error: no script engine.";
                        }
                    } else {
                        return (string)s.MainControl.Dispatcher.Invoke(new Func<string>(()
                            => { return TaskState(s.ShellId, taskId, state); }));
                    }
                }
            }
            return "";
        }

        [ScriptFunction("vars", "Show variables.",
            typeof(Jint.Delegates.Func<String,String,String>),"Id of shell.","Filter pattern.")]
        public static string ShowVars(string id="",string pattern="^.*") {
            Shell s = Shell.GetShell(id);
            if (s != null) {
                if (s.MainControl.Dispatcher.CheckAccess()) {
                    if (s.MainControl is ScriptControl) {
                        ScriptControl c = s.MainControl as ScriptControl;
                        if (c.ShellEngine != null)
                            return c.ShellEngine.ListVars(pattern);
                        return "Error: no script engine.";
                    }
                } else {
                    return (string)s.MainControl.Dispatcher.Invoke(new Func<string>(()
                        => { return ShowVars(s.ShellId,pattern); }));
                }
            }
            return "";
        }
    }
}
