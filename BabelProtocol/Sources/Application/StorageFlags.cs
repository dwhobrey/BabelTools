using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Babel.BabelProtocol {

    [Flags]
    public enum StorageFlags {
        SF_None = 0,
        SF_RAM = 1,
        SF_EEPROM = 2,
        SF_ReadOnly = 4,
        SF_Dynamic = 8
    }
}
