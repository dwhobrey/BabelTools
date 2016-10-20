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
    /// Interaction logic for LockBox.xaml
    /// </summary>
    public partial class LockBox : Window {

        string Password;

        public LockBox(string password) {
            Password = password;
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
        }

        bool TestIfOkToClose() {
            return (String.IsNullOrEmpty(Password) || Password.Equals(LockPassword.Password));
        }

        void EnterButton_Click(object sender, EventArgs e) {
            if (TestIfOkToClose()) {
                Close();
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                if (TestIfOkToClose()) {
                    e.Handled = true;
                    Close();
                }
            }
            base.OnPreviewKeyDown(e);
        }
    }
}
