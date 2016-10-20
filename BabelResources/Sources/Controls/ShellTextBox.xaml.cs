using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading;

namespace Babel.Resources {

    public partial class ShellTextBox : TextBox {
        private static int InstanceCounter = 0;
        public int CmdCount;
        public int MaxBufferSize;
        private int InstanceNumber;
        private string PromptPrefix;
        private string CurrentPrompt;
        private string TextCache;
        private CommandHistory History;
        private FlushThread Flusher;
        private bool IsRunning;

        static ShellTextBox() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ShellTextBox),new FrameworkPropertyMetadata(typeof(ShellTextBox)));
        }

        public ShellTextBox() : base() {
            InstanceNumber = ++InstanceCounter;
            IsRunning = true;
            CmdCount = 0;
            MaxBufferSize = 50000;
            PromptPrefix = "";
            CurrentPrompt = "";
            TextCache = null;
            Completer = null;
            PromptEnabled = true;
            TimePromptEnabled = false;
            Text = "";
            History = new CommandHistory();
            Flusher = new FlushThread(this);
            Flusher.Start();
        }

        public bool TimePromptEnabled { get; set; }
        public bool PromptEnabled { get; set; }
        public ICompleter Completer { get; set; }

        public void Close() {
            IsRunning = false;
        }

        private bool IsCaretAtCurrentLine() {
            return (Text.Length - SelectionStart) <= GetCurrentLine().Length;
        }

        private int GetCurrentCaretColumnPosition() {
            string currentLine = GetCurrentLine();
            int currentCaretPosition = SelectionStart;
            return (currentCaretPosition - Text.Length + currentLine.Length);
        }

        private bool IsCaretJustBeforePrompt() {
            string s = GetCurrentLine();
            int caretPosition = (Text.Length - SelectionStart);
            return (s.Length - caretPosition) == (1+s.IndexOf('>'));
        }

        private bool IsCaretAtWritablePosition() {
            string s = GetCurrentLine();
            int lineLen = s.Length;
            return ((Text.Length - SelectionStart) <= lineLen)
                && ((SelectionStart - Text.Length + lineLen) >= s.IndexOf('>'));
        }

        private bool IsTerminatorKey(Key key) {
            return key == Key.Enter;
        }

        private bool IsTerminatorKey(char keyChar) {
            return ((int)keyChar) == 13;
        }

        private void MoveCaretToEndOfText() {
            SelectionStart = CaretIndex = Text.Length;
            ScrollToEnd();
        }

        public string GetCurrentLine() {
            if (Text.Length > 0)
                return GetLineText(GetLineIndexFromCharacterIndex(CaretIndex));
            else
                return "";
        }

        public string GetTextAtPrompt() {
            MoveCaretToEndOfText();
            string s=GetCurrentLine();
            return s.Substring(1+s.IndexOf('>'));
        }

        public string GetTextToEnter(out bool isAtEndOfLine) {
            isAtEndOfLine = CaretIndex == Text.Length;
            string s = GetTextAtPrompt();
            if (String.IsNullOrWhiteSpace(s))
                s = null;
            else {
                //AddHistory(CurrentPrompt, s);
                AddHistory(GetCurrentLine(),s);
                WriteText("\n", true, false);
            }
            return s;
        }

        public string GetHistory(int num) {
            return History.ShowHistory(num);
        }

        public string GetPromptLine() {
            MoveCaretToEndOfText();
            return GetCurrentLine();
        }

        private string GetNewPrompt() {
            string prefix;
            if (TimePromptEnabled)
                prefix = DateTime.Now.ToString("HH:mm:ss.ffffff");
            else {
                ++CmdCount;
                prefix = CmdCount.ToString();
            }
            CurrentPrompt = prefix + (PromptPrefix.Length > 0 ? ("/" + PromptPrefix) : "") + ">";
            return CurrentPrompt;
        }

        public string GetPromptId(string commandLine) {
            string id=null;
            if (!String.IsNullOrWhiteSpace(commandLine)) {
                char[] a = commandLine.ToCharArray();
                int j=0, k = 0, n = commandLine.Length;
                if (a[k] == '!') { ++j;  ++k; --n; }
                while (k < a.Length && (Char.IsDigit(a[k])||(a[k]==':')||(a[k]=='.'))) ++k;
                if(k>j)
                    id = new String(a, j, k - j);
            }
            return id;
        }

        private void AddText(string text) {
            string s=Text+text;
            if (s.Length > MaxBufferSize) {
                s = s.Substring(s.Length - MaxBufferSize);
            }
            Text = s;
            MoveCaretToEndOfText();
        }

        public void DisplayPrompt() {
            if (PromptEnabled||TimePromptEnabled) {
                if (Text.Length == 0 || !Text.EndsWith(">")) {
                    if (Text.Length > 0 && !Text.EndsWith("\n")) Text += "\n";
                    GetNewPrompt();
                    AddText(CurrentPrompt);
                }
            }
            ScrollToEnd();
        }

        public void HidePrompt() {
            if (Text.EndsWith(CurrentPrompt)) {
                Text = Text.Substring(0, Text.Length - CurrentPrompt.Length);
                if (CmdCount > 1) --CmdCount;
            }
        }

        public string GetPromptPrefix() {
            return PromptPrefix;
        }

        public void SetPromptPrefix(string s) {
            FlushCache();
            HidePrompt();
            PromptPrefix = s;
            if (!TimePromptEnabled && CmdCount > 1) --CmdCount;
            GetNewPrompt();
            DisplayPrompt();
        }

        public void ClearBox() {
            Text = "";
            MoveCaretToEndOfText();
            DisplayPrompt();
        }

        private void FlushCache() {
            if (Application.Current.Dispatcher.CheckAccess()) {
                lock (this) {
                    if (!String.IsNullOrEmpty(TextCache)) {
                        AddText(TextCache);
                        TextCache = null;
                        ScrollToEnd();
                    }
                }
            } else
                Application.Current.Dispatcher.Invoke((Action)(() => { FlushCache(); }));
        }

        private void CacheText(string text) {
            lock (this) {
                if (TextCache == null)
                    TextCache = text;
                else
                    TextCache += text;
                if (PromptEnabled || TimePromptEnabled) {
                    if (!TextCache.EndsWith("\n")) TextCache += "\n";
                    GetNewPrompt();
                    TextCache += CurrentPrompt;
                }
            }
        }

        public void WriteText(string text, bool onPromptLine, bool onNewLine) {
            if (!String.IsNullOrEmpty(text)) {
                if (onNewLine) {
                    FlushCache();
                    HidePrompt();
                    if (Text.Length > 0 && !Text.EndsWith("\n")) Text += "\n";
                    while (text.StartsWith("\n")) text = text.Substring(1);
                } else if (!onPromptLine) {
                    FlushCache();
                    HidePrompt();
                } else {
                    CacheText(text);
                    return;
                }
                AddText(text);
                DisplayPrompt();
            }
        }

        private void ReplaceTextAtPrompt(string text) {
            MoveCaretToEndOfText();
            string currentLine = GetCurrentLine();
            int promptLen = 1 + currentLine.IndexOf('>');
            int charactersAfterPrompt = currentLine.Length - promptLen;
            if (charactersAfterPrompt == 0)
                AddText(text);
            else {
                Select(Text.Length - charactersAfterPrompt, charactersAfterPrompt);
                SelectedText = text;
                MoveCaretToEndOfText();
            }
        }

        public void AddHistory(string prompt, string command) {
            string promptId = GetPromptId(prompt);
            History.AddToHistory(promptId,command);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e) {
            if (e.Key == Key.Back && IsCaretJustBeforePrompt()) {
                e.Handled = true;
                return;
            }
            // If the caret is anywhere else, set it back when a key is pressed.
            if (!IsCaretAtWritablePosition() && !((Keyboard.Modifiers & ModifierKeys.Control) != 0 || IsTerminatorKey(e.Key))) {
                MoveCaretToEndOfText();
            }

            // Prevent caret from moving before the prompt
            if ((e.Key==Key.Back||e.Key == Key.Left) && IsCaretJustBeforePrompt()) {
                e.Handled = true;
            } else if (e.Key == Key.Down) {
                if (History.DoesNextCommandExist()) {
                    ReplaceTextAtPrompt(History.GetNextCommand());
                }
                e.Handled = true;
            } else if (e.Key == Key.Up) {
                if (History.DoesPreviousCommandExist()) {
                    ReplaceTextAtPrompt(History.GetPreviousCommand());
                }
                e.Handled = true;
            } else if ((e.Key == Key.Right)||(e.Key == Key.Tab)) {
                // Performs command completion
                string currentTextAtPrompt = GetTextAtPrompt();
                string prefix = currentTextAtPrompt;
                int n = prefix.Length-1;
                while(n>0) {
                    if (!Char.IsLetterOrDigit(prefix[n])) break;
                    --n;
                }
                if (n > 0 && (n+1)<prefix.Length) prefix = prefix.Substring(n + 1);
                string postfix = History.Complete(prefix);
                if(postfix==null && Completer!=null) {
                    postfix = Completer.Complete(prefix);
                }
                if (postfix != null) {
                    AddText(postfix);
                    e.Handled = true;
                }
            } else if (e.Key == Key.Enter) {
                string currentTextAtPrompt = GetTextAtPrompt();
                if (currentTextAtPrompt.Length > 0 && currentTextAtPrompt.StartsWith("!")) {
                    string promptId = GetPromptId(currentTextAtPrompt);
                    string s = History.FindCommandForPromptId(promptId);
                    if (s != null) {
                        ReplaceTextAtPrompt(s);
                        e.Handled = true;
                    }
                }
            }
        }

        public class FlushThread {
            public Thread Task;
            public ShellTextBox ConsoleTB;
            public FlushThread(ShellTextBox c) {
                ConsoleTB = c;
                Task = new Thread(new ThreadStart(Run));
                Task.Name = "FlushThread:" + c.InstanceNumber;
            }
            public void Start() {
                if (Task != null) Task.Start();
            }
            public void Run() {
                while (ConsoleTB.IsRunning) {
                    try {
                        ConsoleTB.FlushCache();
                        Thread.Sleep(400);
                    } catch (Exception) {
                        // ignore.
                    }
                }
            }
        }
    }
}
