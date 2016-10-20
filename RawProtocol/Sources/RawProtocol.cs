using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Babel.Core;

namespace Babel.RawProtocol {
    public sealed class RawProtocol : Module {
        private static List<string> DependantModules = new List<string>();

        static RawProtocol() {
            DependantModules.Add("BabelXLink");
        }

        public RawProtocol() {
        }

        public override List<string> Dependencies() {
            return DependantModules;
        }

        protected override byte GetModuleId() { return 7; }
        protected override ushort GetAuthCode() { return 0xbabe; }
    }
}
