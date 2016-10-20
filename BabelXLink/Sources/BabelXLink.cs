using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Babel.Core;

namespace Babel.XLink {
    public sealed class BabelXLink : Module {
        private static List<string> DependantModules = new List<string>();

        static BabelXLink() {
            DependantModules.Add("BabelCore");
        }
        public BabelXLink() {
        }
        public override List<string> Dependencies() {
            return DependantModules;
        }
        protected override byte GetModuleId() { return 3; }
        protected override ushort GetAuthCode() { return 0xbabe; }

        protected override bool ConfigProject() {
            LinkManager.ConfigProject();
            return false;
        }
        protected override bool StartProject() {
            LinkManager.StartProject();
            return false;
        }
        protected override bool CloseProject() {
            LinkManager.CloseProject();
            return false;
        }

    }
}
