using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Babel.BabelProtocol {

    public enum VariableKind {
        VK_None,
        VK_OnOff,
        VK_Enum,
        VK_Bits,
        VK_Byte,
        VK_SByte,
        VK_Short,
        VK_UShort,
        VK_Int,
        VK_UInt,
        VK_ID,
        VK_Long,
        VK_ULong,
        VK_LID,
        VK_Float,
        VK_Double,
        VK_String
    }
}
