using System;
using System.Collections;
using System.Collections.Generic;

using Babel.Core;
using Babel.XLink;

namespace Babel.BabelProtocol {

        public sealed class BabelProtocol : Module {
        private static List<string> DependantModules = new List<string>();

        static BabelProtocol() {
            DependantModules.Add("BabelXLink");
        }

        public BabelProtocol() {
        }

        public override List<string> Dependencies() {
            return DependantModules;
        }

        protected override byte GetModuleId() { return 5; }
        protected override ushort GetAuthCode() { return 0xbabe; }

        protected override bool ConfigProject() {
            ProtocolCommands.ConfigProject();
            return false;
        }
        protected override bool StartProject() {
            ProtocolCommands.StartProject();
            return false;
        }
        protected override bool SaveProject() {
            ProtocolCommands.SaveProject();
            return false;
        }
        protected override bool CloseProject() {
            ProtocolCommands.CloseProject();
            return false;
        }
    }

}
