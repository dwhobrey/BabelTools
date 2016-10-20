using System;
using System.Collections.Generic;

using Babel.Core;

namespace Babel.Recorder {
    public sealed class BabelRecorder : Module {
        private static List<string> DependantModules = new List<string>();

        static BabelRecorder() {
            DependantModules.Add("BabelUsb");
            DependantModules.Add("BabelProtocol");        
        }

        public BabelRecorder() {
        }

        public override List<string> Dependencies() {
            return DependantModules;
        }

        protected override byte GetModuleId() { return 6; }
        protected override ushort GetAuthCode() { return 0xbabe; }

        protected override bool ConfigProject() {
            return false;
        }
        protected override bool StartProject() {         
            return false;
        }
        protected override bool CloseProject() {          
            return false;
        }
    }
}
