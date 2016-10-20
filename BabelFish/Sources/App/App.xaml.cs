using System;
using System.Configuration;
using System.Windows;

using Babel.Core;

namespace Babel.Fish {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        protected override void OnStartup(StartupEventArgs e) {
            // hook on error before app really starts
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            base.OnStartup(e);
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
            MessageBox.Show(e.ExceptionObject.ToString(), "Babel Fish Exception");
        }

        public App() {
            try {
                InitializeComponent();
            } catch (Exception e) {
                MessageBox.Show(e.ToString(),"Babel Fish Exception");
            }
        }
    }
}
