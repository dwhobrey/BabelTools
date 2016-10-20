using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Babel.Core;

namespace Babel.BabelProtocol {

    public class VariableValue {

        public enum VariableValueKind {
            VV_Zero,
            VV_Min,
            VV_Max
        }

        public VariableKind Kind;
        public int Size;    
        public long ValueLong;
        public float ValueFloat;
        public double ValueDouble;
        public string ValueString;

        public static VariableKind StringToVK(string s) {
            try {
                return (VariableKind)Enum.Parse(typeof(VariableKind), s);
            } catch (Exception) {
            }
            return VariableKind.VK_None;
        }

        public static StorageFlags StringToSF(string s) {
            try {
                return (StorageFlags)Enum.Parse(typeof(StorageFlags), s);
            } catch (Exception) {
            }
            return StorageFlags.SF_None;
        }

        // Convert to minimal string representation, e.g. strip decimal point if integral.
        public static String DoubleToString(double v) {
            if (v == (long)v) return ((long)v).ToString();
            return v.ToString();
        }

        // Convert number to double. Checks for hexadecimal numbers: 0X, 0x, or Letter.
        public static double StringToDouble(String s) {
            if (s == null) return 0.0;
            s = s.Trim();
            if (s == "") return 0.0;
            if (s.StartsWith("0X") || s.StartsWith("0x")) {
                try {
                    return (double)Convert.ToInt64(s.Substring(2),16);
                } catch (Exception) {
                }
            } else if (Char.IsLetter(s[0])) {
                try {
                    return (double)Convert.ToInt64(s,16);
                } catch (Exception) {
                }
            }
            try {
                return Convert.ToDouble(s);
            } catch (Exception) {
            }
            return 0.0;
        }

        public static int DataSize(VariableKind k) {
            switch (k) {
                case VariableKind.VK_OnOff:
                case VariableKind.VK_Enum:
                case VariableKind.VK_Bits:
                case VariableKind.VK_Byte:
                case VariableKind.VK_SByte:
                case VariableKind.VK_String: return 1;
                case VariableKind.VK_Short:
                case VariableKind.VK_UShort: return 2;
                case VariableKind.VK_Int:
                case VariableKind.VK_UInt:
                case VariableKind.VK_ID:
                case VariableKind.VK_Float: return 4;
                case VariableKind.VK_Long:
                case VariableKind.VK_LID:
                case VariableKind.VK_Double:
                case VariableKind.VK_ULong: return 8;
                default:
                    return 0;
            }
        }

        public static double MaxValue(VariableKind k) {
            switch (k) {
                case VariableKind.VK_OnOff: return 1.0;
                case VariableKind.VK_Enum: return (double)0xff;
                case VariableKind.VK_Bits: return (double)0xffffffff;
                case VariableKind.VK_Byte: return (double)0xff;
                case VariableKind.VK_SByte: return (double)SByte.MaxValue;
                case VariableKind.VK_String: return 1.0;
                case VariableKind.VK_Short: return (double)short.MaxValue;
                case VariableKind.VK_UShort: return (double)ushort.MaxValue;
                case VariableKind.VK_Int: return (double)Int32.MaxValue;
                case VariableKind.VK_UInt: return (double)UInt32.MaxValue;
                case VariableKind.VK_ID: return (double)UInt32.MaxValue;
                case VariableKind.VK_Float: return (double)float.MaxValue;
                case VariableKind.VK_Long: return (double)Int64.MaxValue;
                case VariableKind.VK_LID: return (double)UInt64.MaxValue;
                case VariableKind.VK_Double: return double.MaxValue;
                case VariableKind.VK_ULong: return (double)UInt64.MaxValue;
                default:
                    return 0.0;
            }
        }

        public static double MinValue(VariableKind k) {
            switch (k) {
                case VariableKind.VK_OnOff:
                case VariableKind.VK_Enum:
                case VariableKind.VK_Bits:
                case VariableKind.VK_Byte: return 0.0;
                case VariableKind.VK_SByte: return (double)SByte.MinValue;
                case VariableKind.VK_String: return 1.0;
                case VariableKind.VK_Short: return (double)short.MinValue;
                case VariableKind.VK_UShort: return (double)ushort.MinValue;
                case VariableKind.VK_Int: return (double)Int32.MinValue;
                case VariableKind.VK_UInt: return (double)UInt32.MinValue;
                case VariableKind.VK_ID: return (double)UInt32.MinValue;
                case VariableKind.VK_Float: return (double)float.MinValue;
                case VariableKind.VK_Long: return (double)Int64.MinValue;
                case VariableKind.VK_LID: return (double)UInt64.MinValue;
                case VariableKind.VK_Double: return double.MinValue;
                case VariableKind.VK_ULong: return (double)UInt64.MinValue;
                default:
                    return 0.0;
            }
        }

        public static bool Equal(VariableValue a, VariableValue b) {
            if (a == null || b == null) return false;
            if (a.Kind != b.Kind) return false;
            switch (a.Kind) {
                case VariableKind.VK_OnOff:
                case VariableKind.VK_Enum:
                case VariableKind.VK_Byte:
                case VariableKind.VK_UShort:
                case VariableKind.VK_Bits:
                case VariableKind.VK_UInt:
                case VariableKind.VK_ID:
                case VariableKind.VK_SByte:
                case VariableKind.VK_Short:
                case VariableKind.VK_Int:
                case VariableKind.VK_Long:
                case VariableKind.VK_ULong:
                case VariableKind.VK_LID:
                    return a.ValueLong == b.ValueLong;
                case VariableKind.VK_Float:
                    return a.ValueFloat == b.ValueFloat;
                case VariableKind.VK_Double:
                    return a.ValueDouble == b.ValueDouble;
                case VariableKind.VK_String:
                    return a.ValueString.CompareTo(b.ValueString) == 0;
                default:
                    break;
            }
            return false;
        }

        public static String Format(VariableKind kind, double val, int size) {
            switch (kind) {
                case VariableKind.VK_OnOff:
                case VariableKind.VK_Enum:
                    return String.Format("0x{0:x2}", (Byte)val);
                case VariableKind.VK_Bits:
                    if (size < 0) size = DataSize(kind);
                    switch (size) {
                        case 4:
                            return String.Format("0x{0:x8}", (UInt32)val);
                        case 2:
                            return String.Format("0x{0:x4}", (UInt16)val);
                        default:
                            return String.Format("0x{0:x2}", (Byte)val);
                    }
                case VariableKind.VK_Byte:
                    return String.Format("0x{0:x2}", (Byte)val);
                case VariableKind.VK_UShort:
                    return String.Format("0x{0:x4}", (ushort)val);
                case VariableKind.VK_UInt:
                    return String.Format("0x{0:x8}", (UInt32)val);
                case VariableKind.VK_ULong:
                    return String.Format("0x{0:x16}", (UInt64)val);
                case VariableKind.VK_ID:
                    return String.Format("0x{0:x8}", (UInt32)val);
                case VariableKind.VK_LID:
                    return String.Format("0x{0:x16}", (UInt64)val);
                case VariableKind.VK_String:
                    return "\"" + DoubleToString(val) + "\"";
                case VariableKind.VK_Float:
                    return ((float)val).ToString();
                case VariableKind.VK_Double:
                    return val.ToString();
                case VariableKind.VK_SByte:
                case VariableKind.VK_Short:
                case VariableKind.VK_Int:
                case VariableKind.VK_Long:
                    return val.ToString();
                default:
                    break;
            }
            return "0";
        }

        public VariableValue(VariableKind kind, VariableValueKind val) {
            double d = 0.0;
            Kind = kind;
            Size = DataSize(Kind);
            switch (val) {
                case VariableValueKind.VV_Min:
                    d = MinValue(kind); break;
                case VariableValueKind.VV_Max:
                    d = MaxValue(kind); break;
                default: break;// VV_Zero
            }
            SetValue(d);
        }

        public VariableValue(VariableKind kind, string s, int size=-1) {
            Kind = kind;
            if (size < 0) Size = DataSize(Kind);
            else Size = size;
            SetValue(s);
        }

        public VariableValue(VariableKind kind, byte[] ary, int startIndex, int len) {
            Kind = kind;
            SetValue(ary, startIndex, len);
        }

        public double CompareTo(VariableValue b) {
            if (b == null) return 1.0;
            if (Kind == VariableKind.VK_String) {
                if (b.Kind != VariableKind.VK_String) return 1.0;
                return (double)ValueString.CompareTo(b.ValueString);
            }
            return GetDouble() - b.GetDouble();
        }

        public void Assign(VariableValue v) {
            Kind = v.Kind;
            Size = v.Size;
            ValueLong = v.ValueLong;
            ValueFloat = v.ValueFloat;
            ValueDouble = v.ValueDouble;
            ValueString = v.ValueString;
        }

        public override string ToString() {
            switch (Kind) {
                case VariableKind.VK_OnOff:
                case VariableKind.VK_Enum:
                    return String.Format("0x{0:x2}", (Byte)ValueLong);
                case VariableKind.VK_Bits:
                    switch (Size) {
                        case 4:
                            return String.Format("0x{0:x8}", (UInt32)ValueLong);
                        case 2:
                            return String.Format("0x{0:x4}", (UInt16)ValueLong);
                        default:
                            return String.Format("0x{0:x2}", (Byte)ValueLong);
                    }
                case VariableKind.VK_Byte:
                    return String.Format("0x{0:x2}", (Byte)ValueLong);
                case VariableKind.VK_UShort:
                    return String.Format("0x{0:x4}", (ushort)ValueLong);
                case VariableKind.VK_UInt:
                    return String.Format("0x{0:x8}", (UInt32)ValueLong);
                case VariableKind.VK_ULong:
                    return String.Format("0x{0:x16}", (UInt64)ValueLong);
                case VariableKind.VK_ID:
                    return String.Format("0x{0:x8}", (UInt32)ValueLong);
                case VariableKind.VK_LID:
                    return String.Format("0x{0:x16}", (UInt64)ValueLong);
                case VariableKind.VK_String:
                    return "\"" + ValueString + "\"";
                case VariableKind.VK_Float:
                    return ValueFloat.ToString();
                case VariableKind.VK_Double:
                    return ValueDouble.ToString();
                case VariableKind.VK_SByte:
                case VariableKind.VK_Short:
                case VariableKind.VK_Int:
                case VariableKind.VK_Long:
                    return ValueLong.ToString();
                default:
                    break;
            }
            return "0";
        }

        public byte[] GetBytes() {
            byte[] b = null;
            switch (Kind) {
                case VariableKind.VK_None:
                    b = new byte[0];
                    break;
                case VariableKind.VK_OnOff:
                case VariableKind.VK_Enum:
                case VariableKind.VK_Byte:
                case VariableKind.VK_UShort:
                case VariableKind.VK_Bits:
                case VariableKind.VK_UInt:
                case VariableKind.VK_ID:
                case VariableKind.VK_SByte:
                case VariableKind.VK_Short:
                case VariableKind.VK_Int:
                case VariableKind.VK_Long:
                case VariableKind.VK_ULong:
                case VariableKind.VK_LID:
                    b = new byte[Size];
                    Primitives.SetArrayValue(ValueLong, b, 0, Size);
                    break;
                case VariableKind.VK_String:
                    return Encoding.UTF8.GetBytes(ValueString);
                case VariableKind.VK_Float:
                    b = BitConverter.GetBytes(ValueFloat);
                    break;
                case VariableKind.VK_Double:
                    b = BitConverter.GetBytes(ValueDouble);
                    break;
                default:
                    break;
            }
            return b;
        }

        public double GetDouble() {
            switch (Kind) {
                case VariableKind.VK_OnOff:
                case VariableKind.VK_Enum:
                case VariableKind.VK_Byte:
                case VariableKind.VK_UShort:
                case VariableKind.VK_Bits:
                case VariableKind.VK_UInt:
                case VariableKind.VK_ID:
                case VariableKind.VK_SByte:
                case VariableKind.VK_Short:
                case VariableKind.VK_Int:
                case VariableKind.VK_Long:
                case VariableKind.VK_ULong:
                case VariableKind.VK_LID:
                    return (double)ValueLong;
                case VariableKind.VK_Float:
                    return (double)ValueFloat;
                case VariableKind.VK_Double:
                    return ValueDouble;
                default:
                    break;
            }
            return 0.0;
        }

        public void SetValue(double d) {
            switch (Kind) {
                case VariableKind.VK_OnOff:
                case VariableKind.VK_Enum:
                case VariableKind.VK_Byte:
                case VariableKind.VK_UShort:
                case VariableKind.VK_Bits:
                case VariableKind.VK_UInt:
                case VariableKind.VK_ID:
                case VariableKind.VK_SByte:
                case VariableKind.VK_Short:
                case VariableKind.VK_Int:
                case VariableKind.VK_Long:
                case VariableKind.VK_ULong:
                case VariableKind.VK_LID:
                    ValueLong = (long)d;
                    ValueString = DoubleToString(ValueLong);
                    break;
                case VariableKind.VK_Float:
                    ValueFloat = (float)d;
                    ValueString = DoubleToString(ValueFloat);
                    break;
                case VariableKind.VK_Double:
                case VariableKind.VK_String:
                    ValueDouble = d;
                    ValueString = DoubleToString(ValueDouble);
                    break;
                default:
                    ValueLong = 0;
                    ValueString = "";
                    break;
            }
        }

        public void SetValue(string s) {
            if (String.IsNullOrWhiteSpace(s)) {
                s = "";
            } else {
                s = s.Trim();
            }
            ValueString = s;
            switch (Kind) {
                case VariableKind.VK_OnOff:
                case VariableKind.VK_Enum:
                case VariableKind.VK_Byte:
                case VariableKind.VK_UShort:
                case VariableKind.VK_Bits:
                case VariableKind.VK_UInt:
                case VariableKind.VK_ID:
                case VariableKind.VK_SByte:
                case VariableKind.VK_Short:
                case VariableKind.VK_Int:
                case VariableKind.VK_Long:
                case VariableKind.VK_ULong:
                case VariableKind.VK_LID:
                    ValueLong = (long)StringToDouble(s);
                    break;
                case VariableKind.VK_Float:
                    ValueFloat = (float)StringToDouble(s);
                    break;
                case VariableKind.VK_String:
                case VariableKind.VK_Double:
                    if (Kind == VariableKind.VK_String) Size = ValueString.Length;
                    ValueDouble = StringToDouble(s);
                    break;
                default:
                    ValueLong = 0;
                    break;
            }
        }

        public static double GetDouble(VariableKind kind, byte[] ary, int startIndex, int len) {
            switch (kind) {
                case VariableKind.VK_OnOff:
                case VariableKind.VK_Enum:
                case VariableKind.VK_Byte:
                case VariableKind.VK_UShort:
                case VariableKind.VK_Bits:
                case VariableKind.VK_UInt:
                case VariableKind.VK_ID:
                case VariableKind.VK_ULong:
                case VariableKind.VK_LID:
                    return Primitives.GetArrayValueU(ary, startIndex, len);
                case VariableKind.VK_SByte:
                case VariableKind.VK_Short:
                case VariableKind.VK_Int:
                case VariableKind.VK_Long:
                    return Primitives.GetArrayValueS(ary, startIndex, len);
                case VariableKind.VK_Float:
                    return BitConverter.ToSingle(ary, startIndex);
                case VariableKind.VK_Double:
                    return BitConverter.ToDouble(ary, startIndex);
                default:
                    break;
            }
            return 0.0;
        }

        public void SetValue(byte[] ary, int startIndex, int len) {
            Size = len;
            switch (Kind) {
                case VariableKind.VK_OnOff:
                case VariableKind.VK_Enum:
                case VariableKind.VK_Byte:
                case VariableKind.VK_UShort:
                case VariableKind.VK_Bits:
                case VariableKind.VK_UInt:
                case VariableKind.VK_ID:
                case VariableKind.VK_ULong:
                case VariableKind.VK_LID:
                    ValueDouble = Primitives.GetArrayValueU(ary, startIndex, len);
                    break;
                case VariableKind.VK_SByte:
                case VariableKind.VK_Short:
                case VariableKind.VK_Int:
                case VariableKind.VK_Long:
                    ValueDouble = Primitives.GetArrayValueS(ary, startIndex, len);
                    break;
                case VariableKind.VK_String:
                    SetValue(Primitives.GetArrayStringValue(ary, startIndex, len));
                    return;
                case VariableKind.VK_Float:
                    ValueDouble = BitConverter.ToSingle(ary, startIndex); break;
                case VariableKind.VK_Double:
                    ValueDouble = BitConverter.ToDouble(ary, startIndex); break;
                default:
                    ValueDouble = 0;
                    break;
            }
            SetValue(ValueDouble);
        }

        public void CheckInRange(VariableValue minValue, VariableValue maxValue) {
            switch (Kind) {
                case VariableKind.VK_OnOff:
                case VariableKind.VK_Enum:
                case VariableKind.VK_Byte:
                case VariableKind.VK_UShort:
                case VariableKind.VK_Bits:
                case VariableKind.VK_UInt:
                case VariableKind.VK_ID:
                case VariableKind.VK_SByte:
                case VariableKind.VK_Short:
                case VariableKind.VK_Int:
                case VariableKind.VK_Long:
                case VariableKind.VK_ULong:
                case VariableKind.VK_LID:
                    if(ValueLong<minValue.ValueLong) ValueLong = minValue.ValueLong;
                    else if (ValueLong > maxValue.ValueLong) ValueLong = maxValue.ValueLong;
                    break;
                case VariableKind.VK_Float:
                    if (ValueFloat < minValue.ValueFloat) ValueFloat = minValue.ValueFloat;
                    else if (ValueFloat > maxValue.ValueFloat) ValueFloat = maxValue.ValueFloat;
                    break;
                case VariableKind.VK_Double:
                    if (ValueDouble < minValue.ValueDouble) ValueDouble = minValue.ValueDouble;
                    else if (ValueDouble > maxValue.ValueDouble) ValueDouble = maxValue.ValueDouble;
                    break;
                default: 
                    break;
            }
        }
    }
}
