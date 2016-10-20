using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Babel.Core {
    public class StatusInformationBar {

        private bool IsRunning;
        private bool HasChanged;
        private TextBox StatusBox;
        private StatusThread Task;
        private Dictionary<string, string> Indicators;

        public StatusInformationBar(TextBox tb) {
            StatusBox = tb;
            IsRunning = true;
            HasChanged = true;
            Indicators = new Dictionary<string, string>();
            Task = new StatusThread(this);
            Task.Start();
        }

        public void Close() {
            IsRunning = false;
        }

        public void UpdateStatus(string key, string text) {
            if (!String.IsNullOrWhiteSpace(key)) {
                lock (Indicators) {
                    if (String.IsNullOrWhiteSpace(text)) {
                        Indicators.Remove(key);
                    } else {
                        Indicators[key] = text;
                    }
                    HasChanged = true;
                }
            }

        }

        private void Update() {
            if (Application.Current.Dispatcher.CheckAccess()) {
                string s = ""; 
                lock (Indicators) {
                    bool isFirst = true;
                    foreach (KeyValuePair<string, string> kvp in Indicators) {
                        if (isFirst) isFirst = false;
                        else s += " | ";
                        s += kvp.Key + ":" + (String.IsNullOrWhiteSpace(kvp.Value) ? "" : kvp.Value);
                    }
                    HasChanged = false;
                }
                StatusBox.Text = s;
            } else
                Application.Current.Dispatcher.Invoke((Action)(() => { Update(); }));
        }

        public class StatusThread {
            public Thread Task;
            StatusInformationBar Bar;
            public StatusThread(StatusInformationBar b) {
                Bar = b;
                Task = new Thread(new ThreadStart(Run));
                Task.Name = "StatusThread";

            }
            public void Start() {
                if (Task != null) Task.Start();
            }
            public void Run() {
                while (Bar.IsRunning) {
                    try {
                        if (Bar.HasChanged) {
                            Bar.Update();
                        }
                        Thread.Sleep(1000);
                    } catch (Exception) {
                        // ignore.
                    }
                }
            }
        }
    }
}
