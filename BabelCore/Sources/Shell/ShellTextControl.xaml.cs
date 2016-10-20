using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading;
using System.Windows.Threading;
using System.Xml;
using Babel.Resources;

namespace Babel.Core {

    public partial class ShellTextControl : ScriptControl {

        static ShellTextControl() {
 //           DefaultStyleKeyProperty.OverrideMetadata(typeof(ShellTextControl),new FrameworkPropertyMetadata(typeof(ShellTextControl)));
        }

        public ShellTextControl()
            : base(null, null, null, false) {
            InitializeComponent();
            GetContents.Completer = this;
            GetContents.PromptEnabled = false;
        }

        ShellTextControl(string ownerShellId, string startScriptName, string endScriptName, 
            bool isInteractive, bool useTimePrompt=false)
            : base(ownerShellId, startScriptName, endScriptName, isInteractive, useTimePrompt) {
            InitializeComponent();
            GetContents.Completer = this;
            GetContents.PromptEnabled = isInteractive;
            GetContents.TimePromptEnabled = useTimePrompt;
        }

        ShellTextControl(string ownerShellId,XmlNode node)
            : base(ownerShellId) {
            InitializeComponent();
            Serializer(node, false);
            GetContents.Completer = this;
            GetContents.PromptEnabled = IsInteractive;
            GetContents.TimePromptEnabled = UseTimePrompt;
        }

        protected ShellTextBox GetContents {
            get {
                return FindName("Contents") as ShellTextBox;
            }
        }

        public override string KindName { get { return "text"; } }

        // On entry, node is the shell node.
        public override void Serializer(XmlNode node, bool isSerialize) {
            base.Serializer(node, isSerialize);
            XmlNode t = Project.GetChildNode(node, "textcontrol");
            if (t == null) return;
            ShellTextBox b = GetContents;
            if (b != null) {
                if (isSerialize) {
                    Project.SetNodeAttributeValue(t, "prompt", b.GetPromptPrefix());
                } else {
                    b.SetPromptPrefix(Project.GetNodeAttributeValue(t, "prompt", ""));
                }
            }
        }

        protected void Shell_OnKeyUp(Object sender, KeyEventArgs e) {
            if (e.Key == Key.Return) {
                bool isAtEndOfLine = false;
                string command = GetEnterCommand(out isAtEndOfLine);
                ScriptControl.RunScript(OwnerShellId, command, !isAtEndOfLine);
            } else if (e.Key == Key.C) {
                if (e.KeyboardDevice.IsKeyDown(Key.LeftCtrl)
                   || e.KeyboardDevice.IsKeyDown(Key.RightCtrl)
                    ) {
                        ScriptControl.Break(OwnerShellId);
                }
            }
        }

        public override void Close() {
            base.Close();
            GetContents.Close();
        }

        public override void Clear() {
            base.Clear();
            GetContents.ClearBox();
        }

        public override void Write(string text, bool onPromptLine = false, bool onNewLine = false) {
            GetContents.WriteText(text, onPromptLine, onNewLine);
        }

        public override string GetEnterCommand(out bool isAtEndOfLine) {
            return GetContents.GetTextToEnter(out isAtEndOfLine);
        }

        // On entry, node is the shell node.
        [ShellDeserializer("text")]
        public static ShellControl TextShellDeserializer(string shellId, XmlNode node) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                return new ShellTextControl(shellId, node);
            }
            return (ShellControl)Application.Current.Dispatcher.Invoke(
                    new Func<ShellControl>(() => { return TextShellDeserializer(shellId,node); })
                );
        }

        [ScriptFunction("text", "Opens a new text shell.\n Returns shell Id.",
           typeof(Func<String, int, int, int, int, int, String, String, String, String, bool, bool, String>),
           "Title for shell.",
           "Width.", "Height.", "X position.", "Y position.", "Window state {0,1,2}.",
           "Shell Id.", "Working dir.",
           "Start up script: 'shellstart'.", "End script: 'shellend'.",
           "Is interactive.", "Use time prompt."
           )]
        public static string OpenTextShell(string title,
            int width = 300, int height = 200, int x = -1, int y = -1, int windowState=0,
            string shellId = "", string workingDir="",
            string scriptStart = "shellstart", string scriptEnd = "shellend",
            bool isInteractive = false, bool useTimePrompt = false) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                Shell shell = Shell.OpenShell(title,width,height,x,y,windowState,shellId,workingDir);
                shellId = shell.ShellId;
                if (shell.MainControl == null) {
                    if (!shell.SetControl(new ShellTextControl(shellId, scriptStart, scriptEnd, isInteractive, useTimePrompt))) {
                        return "Error: unable to create shell script control.";
                    }
                }
                return shellId;
            }
            return (string)Application.Current.Dispatcher.Invoke(
                    new Func<string>(() => { return OpenTextShell(title, width, height, x, y, windowState,shellId, workingDir, scriptStart, scriptEnd, isInteractive, useTimePrompt); })
                );
        }

        [ScriptFunction("prompt", "Sets the command line prompt.",
            typeof(Jint.Delegates.Func<String, String, String>), "Shell id.", "Prompt string or null to return current.")]
        public static string SetCommandPrompt(string id = "", string prompt=null) {
            Shell s = Shell.GetShell(id);
            if (s != null) {
                if (s.MainControl.Dispatcher.CheckAccess()) {
                    ShellTextControl c = s.MainControl as ShellTextControl;
                    if (c != null) {
                        ShellTextBox b = c.GetContents;
                        if (b != null) {
                            if (prompt==null) {
                                return b.GetPromptPrefix();
                            }
                            prompt = prompt.Trim();
                            b.SetPromptPrefix(prompt);
                            return "";
                        }
                    }
                } else {
                    return (string)s.MainControl.Dispatcher.Invoke(new Func<string>(() => { return SetCommandPrompt(s.ShellId, prompt); }));
                }
            }
            return "Error: could not access prompt.";
        }

        [ScriptFunction("history", "Returns the command history for shell.",
            typeof(Jint.Delegates.Func<String, Int32, String>), "Shell id.", "Number of entries to show.")]
        public static string ShowHistory(string id="", int num=10) {
            Shell s = Shell.GetShell(id);
            if (s != null) {
                if (s.MainControl.Dispatcher.CheckAccess()) {
                    ShellTextControl c = s.MainControl as ShellTextControl;
                    if (c != null) {
                        ShellTextBox b = c.GetContents;
                        if (b != null)
                            return b.GetHistory(num);
                    }
                } else {
                    return (string)s.MainControl.Dispatcher.Invoke(new Func<string>(() => { return ShowHistory(s.ShellId, num); }) );
                }
            }
            return "Error: no history available.";
        }
    }
}
