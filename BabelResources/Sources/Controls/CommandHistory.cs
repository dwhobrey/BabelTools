using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Babel.Resources {

    public interface ICompleter {
        // Returns postfix completion if prefix found in implementators word list.
        // Otherwise returns null.
        string Complete(string prefix);
    }

    class CommandHistory {
        int currentPosn = 0;
        string lastCommand = "";
        List<Tuple<string,string>> history = new List<Tuple<string,string>>(); // promptId, command.

        public CommandHistory() {
        }

        public string ShowHistory(int numToShow=10) {
            string s = "";
            lock (history) {
                int len = history.Count;
                if (numToShow <= 0 || numToShow >= len) numToShow = len;
                for (int k = len - numToShow; k < len; k++) {
                    s += history[k].Item1 + ":" + history[k].Item2 + "\n";
                }
            }
            return s;
        }

        public void AddToHistory(string promptId, string command) {
            if (!String.IsNullOrWhiteSpace(promptId) && !String.IsNullOrWhiteSpace(command) && (command != lastCommand)) {
                lock (history) {
                    history.Add(new Tuple<string, string>(promptId, command));
                    lastCommand = command;
                    currentPosn = history.Count;
                }
            }
        }

        public string FindCommandForPromptId(string promptId) {
            if (!String.IsNullOrWhiteSpace(promptId)) {
                lock (history) {
                    foreach (Tuple<string, string> t in history) {
                        if (t.Item1.Equals(promptId))
                            return t.Item2;
                    }
                }
            }
            return null;
        }

        public bool DoesPreviousCommandExist() {
            return currentPosn > 0;
        }

        public bool DoesNextCommandExist() {
            lock (history) {
                return currentPosn < history.Count - 1;
            }
        }

        public string GetPreviousCommand() {
            lock (history) {
                if (currentPosn > 0) --currentPosn;

                if (currentPosn < history.Count) {
                    lastCommand = history[currentPosn].Item2;
                }
            }
            return lastCommand;
        }

        public string GetNextCommand() {
            lock (history) {
                if ((1 + currentPosn) < history.Count) {
                    lastCommand = history[++currentPosn].Item2;
                }
            }
            return lastCommand;
        }

        public string LastCommand {
            get { return lastCommand; }
        }

        // Returns postfix completion if prefix found in history.
        public string Complete(string prefix) {
            lock (history) {
                for (int k = history.Count - 1; k >= 0; --k) {
                    string s = history[k].Item2;
                    if (s != null && (prefix.Length == 0 || s.StartsWith(prefix))) {
                        if (s.Length > prefix.Length) {
                            return s.Substring(prefix.Length);
                        }
                    }
                }
            }
            return null;
        }
    }
}
