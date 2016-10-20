using System;
using System.Collections.Generic;

using Babel.Core;

namespace Babel.Usb {
    public sealed class BabelUsb : Module {
        private static List<string> DependantModules = new List<string>();

        static BabelUsb() {
            DependantModules.Add("BabelXLink");
        }

        public BabelUsb() {
        }

        public override List<string> Dependencies() {
            return DependantModules;
        }

        protected override byte GetModuleId() { return 5; }
        protected override ushort GetAuthCode() { return 0xbabe; }

        protected override bool ConfigProject() {
            UsbLinkManager.ConfigProject();
            return false;
        }
        protected override bool StartProject() {
            UsbLinkManager.StartProject();
            return false;
        }
        protected override bool CloseProject() {
            UsbLinkManager.CloseProject();
            return false;
        }
    }
}
