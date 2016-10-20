using System;
using System.Windows;
using System.Windows.Controls;

namespace Babel.Core {
    /// <summary>
    /// Interaction logic for LicenseBox.xaml
    /// </summary>
    public partial class LicenseBox : Window {

        public LicenseBox() {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
            this.Title = Settings.AppName + " Licensing";
            this.ComputerKey.Text = Lima.GetComputerKey();
            this.CurrentKeys.Text = Lima.GetLicensesDetails();
        }

        void closeLicenseButton_Click(object sender, EventArgs e) {
            Close();
        }

        void removeLicenseButton_Click(object sender, EventArgs e) {
            Lima.RemoveLicenses();
            this.CurrentKeys.Text = "None";
            Settings.BugReport("Restart application for changes to take effect.");
        }
        void addLicenseButton_Click(object sender, EventArgs e) {
            int numAdded = 0;
            int numInvalid = 0;
            string s = this.AddKeys.Text;
            string r = Lima.UpdateLicenses(s,out numAdded,out numInvalid);
            if (numAdded > 0) {
                this.CurrentKeys.Text = Lima.GetLicensesDetails();
                Modules.UpdateInstalledModules();
            }
            if (numInvalid > 0) {
                Settings.BugReport("Unable to add "+numInvalid+" license(s): " + r +".");
            }
        }
    }
}
