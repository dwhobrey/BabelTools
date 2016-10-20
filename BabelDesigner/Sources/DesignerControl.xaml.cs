using System;
using System.IO;
using System.Windows;
using System.Xml;
using System.Windows.Controls;
using System.Drawing;
using Babel.Resources;
using Babel.Core;

namespace Babel.Designer {
    public partial class DesignerControl : ShellControl {

        static DesignerControl() {
            // DefaultStyleKeyProperty.OverrideMetadata(typeof(GraphControl),new FrameworkPropertyMetadata(typeof(GraphControl)));
        }

        public DesignerControl() {
            InitializeComponent();
        }

        public DesignerControl(string ownerShellId)
            : base(ownerShellId) {
            InitializeComponent();
        }

        public DesignerControl(string ownerShellId, XmlNode node)
            : base(ownerShellId) {
            InitializeComponent();
            Serializer(node, false);
        }

        public override void Init() {
            base.Init();
        }

        public override void Close() {
            base.Close();
        }

        public override string KindName { get { return "designer"; } }

        // On entry, node is the shell node.
        [ShellDeserializer("designer")]
        public static ShellControl DesignerShellDeserializer(string shellId,XmlNode node) {
            if (Application.Current.Dispatcher.CheckAccess()) {
                return new DesignerControl(shellId, node);
            }
            return (ShellControl)Application.Current.Dispatcher.Invoke(
                    new Func<ShellControl>(() => { return DesignerShellDeserializer(shellId, node); })
                );
        }

        [ScriptFunction("designer", "Opens designer view.",
            typeof(Jint.Delegates.Func<String>))]
        public static string OpenDesignerShell() {
            if (Application.Current.Dispatcher.CheckAccess()) {
                Shell shell = Shell.OpenShell("Designer", 300, 500);
                string shellId = shell.ShellId;
                if (shell.MainControl == null) {
                    if (!shell.SetControl(new DesignerControl(shellId))) {
                        return "Error: unable to create shell designer control.";
                    }
                }
                return shellId;
            }
            return (string)Application.Current.Dispatcher.Invoke(
                    new Func<string>(() => { return OpenDesignerShell(); })
                );
        }

    }
}
