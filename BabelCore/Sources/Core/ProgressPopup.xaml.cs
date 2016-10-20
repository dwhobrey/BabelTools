using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Windows.Threading;

namespace Babel.Core {

    public partial class ProgressPopup : Window {

        public class Tracker : INotifyPropertyChanged {

            private int progress;
            public event PropertyChangedEventHandler PropertyChanged;

            public Tracker() {
            }

            public Tracker(int value) {
                progress = value;
            }

            public int Progress {
                get { return progress; }
                set {
                    progress = value;
                    OnPropertyChanged("Progress");
                }
            }

            protected void OnPropertyChanged(string name) {
                PropertyChangedEventHandler handler = PropertyChanged;
                if (handler != null) {
                    handler(this, new PropertyChangedEventArgs(name));
                }
            }
        }

        private readonly Tracker tracker;

        public ProgressPopup() {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
            tracker = new Tracker();
            DataContext = tracker;
        }

        public void UpdateProgress(int progressPercentage) {
            tracker.Progress = progressPercentage;
        }
    }
}
