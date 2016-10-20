using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Babel.Core;

namespace Babel.Designer {
    public sealed class BabelDesigner : Module {
        private static List<string> DependantModules = new List<string>();

        static BabelDesigner() {
            DependantModules.Add("BabelCore");
        }
        public BabelDesigner() {
        }
        public override List<string> Dependencies() {
            return DependantModules;
        }
        protected override byte GetModuleId() { return 2; }
        protected override ushort GetAuthCode() { return 0xbabe; }
    }
}