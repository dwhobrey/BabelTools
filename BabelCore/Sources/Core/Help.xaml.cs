using System;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Windows.Resources;

namespace Babel.Core {

    public partial class Help : Window {
        public Help() {
            InitializeComponent();
            this.Title = String.Format("{0} Help", Settings.AppName);
            this.Owner = Application.Current.MainWindow;
            helpBrowser.Loaded += HelpBrowser_OnLoaded;
        }

        void HelpBrowser_OnLoaded(object sender, RoutedEventArgs e) {
            try {
                String helpfile = Path.Combine(Settings.ExeDir, "help");
                helpfile = Path.Combine(helpfile, Settings.AppFileName + "Help.html");
                Uri uri = new Uri(helpfile, UriKind.Absolute);
                helpBrowser.Navigate(uri);
            } catch(Exception ex) {
                string m = ex.Message;
            }
        }
    }
}
