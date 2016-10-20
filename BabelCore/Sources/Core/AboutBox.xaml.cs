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
    /// Interaction logic for AboutBox.xaml
    /// </summary>
    public partial class AboutBox : Window {

        Assembly AppAssembly;

        public AboutBox(Assembly appAssembly) {
            AppAssembly = appAssembly;
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;
            this.Title = String.Format("About {0}", Settings.AppName);
            this.firstLine.Text = String.Format("{0}, {1}", AssemblyProduct, AssemblyDescription );
            this.secondLine.Text = String.Format("Version {0}, {1} {2}", AssemblyVersion, AssemblyCopyright, AssemblyCompany);
            this.ModuleList.Text = Modules.ModuleDescriptions();
        }

        #region Assembly Attribute Accessors

        public string AssemblyTitle {
            get {
                return Settings.GetAssemblyTitle(AppAssembly);
            }
        }

        public string AssemblyVersion {
            get {
                return Settings.GetAssemblyVersion(AppAssembly);
            }
        }

        public string AssemblyDescription {
            get {
                return Settings.GetAssemblyDescription(AppAssembly);
            }
        }

        public string AssemblyProduct {
            get {
                return Settings.GetAssemblyProduct(AppAssembly);
            }
        }

        public string AssemblyCopyright {
            get {
                return Settings.GetAssemblyCopyright(AppAssembly);
            }
        }

        public string AssemblyCompany {
            get {
                return Settings.GetAssemblyCompany(AppAssembly);
            }
        }
        #endregion

        void closeButton_Click(object sender, EventArgs e) {
            Close();
        }
    }
}
