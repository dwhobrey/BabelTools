using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Babel.Core {
    /// <summary>
    /// Interaction logic for MessageDialog.xaml
    /// </summary>
    public partial class MessageDialog : Window {

        public MessageDialog(string message) {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
            this.message.Text = message;
        }

        void okButton_Click(object sender, EventArgs e) {
            Close();
        }

        public static void Show(string message) {
            MessageDialog m = new MessageDialog(message);
            m.Show();
        }
    }
}
