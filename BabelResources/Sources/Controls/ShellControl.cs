using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading;
using System.Windows.Threading;
using System.Xml;
using System.Windows.Markup;

namespace Babel.Resources {

    [ContentProperty("Content")]
    public class ShellControl : UserControl {

        protected string OwnerShellId;
        protected bool IsClosing;
        protected Dispatcher ControlDispatcher;

        public ShellControl()
            : base() {
        }

        protected ShellControl(string ownerShellId)
            : base() {
            IsClosing = false;
            OwnerShellId = ownerShellId;
            Width = double.NaN;
            Height = double.NaN;
        }

        public string ShellId { get { return OwnerShellId; } }

        public virtual void Close() {
            if (IsClosing) return;
            IsClosing = true;
            if (ControlDispatcher != null) {
                ControlDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                ControlDispatcher = null;
            }
        }

        public virtual void Clear() {
            // Override and clear contents.
        }

        public virtual void Init() {
            Thread t = new Thread(new ThreadStart(ShellControlThread));
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Name = "ShellControl:" + OwnerShellId;
            t.Start();
        }

        public virtual string KindName { get { return null; } }

        // On entry, node is the shell node.
        public virtual void Serializer(XmlNode node, bool isSerialize) {

        }

        void ShellControlThread() {
            ControlDispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(ControlDispatcher));
            System.Windows.Threading.Dispatcher.Run();
        }

        public virtual void ShellSizeChanged() {
            // Override to handle size change.
        }

        protected void Shell_SizeChanged(object sender, RoutedEventArgs e) {
            ShellSizeChanged();
        }

        public virtual void Write(string text, bool onPromptLine = false, bool onNewLine = false) {
            // Override to write text to control.
        }

        public virtual string GetEnterCommand(out bool isAtEndOfLine) {
            isAtEndOfLine = false;
            return null;
        }
    }
}

