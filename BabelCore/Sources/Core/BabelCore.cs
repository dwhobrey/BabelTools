using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;
using Babel.Resources;

namespace Babel.Core {

    internal sealed class BabelCore : Module {

        static BabelCore Core = new BabelCore();

        private BabelCore() {
            Name = "BabelCore";
        }

        internal static BabelCore CoreModule { get { return Core; } }

        protected override byte GetModuleId() { return 1; }
        protected override ushort GetAuthCode() { return 0xbabe; }

        protected override bool StartApp() {
            return false;
        }
        protected override bool EndApp() {
            Modules.SaveProject();
            Modules.CloseProject();
            return false;
        }

        // fileName,null,false = Open specified existing project.
        // null,null,false = Open existing project: select from file browser.
        // null,projectKind,true = Open new project of specified kind, get new name from file browser.
        protected override bool OpenProject(string fileName, string projectKind, bool isNew) {
            if (fileName == null) {
                string description = "Project";
                if (String.IsNullOrWhiteSpace(projectKind)) description += "(*)";
                else description += "(" + projectKind + ")";
                fileName = Settings.GetFileNameFromUser(!isNew, Project.ProjectExt, description);
            }
            if (fileName != null) {
                Modules.SaveProject();
                MainWindow mw = (MainWindow)Application.Current.MainWindow;
                mw.SetTitle(Path.GetFileNameWithoutExtension(fileName));
                Modules.CloseProject();
                if (!Project.OpenProject(fileName,projectKind)) return true;
                Modules.ConfigProject();
                Modules.StartProject();
                mw.RefreshRecentProjects();
            }
            return false;
        }
        protected override bool ConfigProject() {
            Shell.ConfigProject();
            return false;
        }
        protected override bool StartProject() {
            Shell.StartProject();
            return false;
        }
        protected override bool SaveProject() {
            Shell.SaveProject();
            Project.SaveProject();
            return false;
        }
        protected override bool CloseProject() {
            Shell.CloseProject();
            Project.CloseProject();
            return false;
        }
    }
}
