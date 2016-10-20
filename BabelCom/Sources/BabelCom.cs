using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Babel.Core;

namespace Babel.Com {
    public sealed class BabelCom : Module {
        private static List<string> DependantModules = new List<string>();

        static BabelCom() {
            DependantModules.Add("BabelXLink");
        }

        public BabelCom() {
        }

        public override List<string> Dependencies() {
            return DependantModules;
        }

        protected override byte GetModuleId() { return 4; }
        protected override ushort GetAuthCode() { return 0xbabe; }
    }
}
