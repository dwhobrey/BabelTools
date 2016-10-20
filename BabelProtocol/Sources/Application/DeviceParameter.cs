using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Babel.BabelProtocol {

    public class DeviceParameter {
        public enum FieldId {
            IndexField, CategoryField, VariableKindField,
            SizeField, StorageFlagsField,
            MinValueField, MaxValueField, DefaultValueField,
            RamValueField, EepromValueField, NameField, NullField
        }
        public readonly static string[] FieldNames = {
            "index","category","kind",
            "size","flags",
            "min","max","default",
            "ram","rom","name"
        };
        public VariableKind ParamVarKind;
        public StorageFlags ParamStorageFlags;
        public int ParamIndex;
        public int ParamSize;
        public int ParamCategory;
        public bool RequiresRefresh;
        public bool HasChanged;
        public bool HasRamValue;
        public bool HasEEPromValue;
        public bool HasDefaultValue;
        public bool HasMinMaxValue;
        public bool IsDynamic;
        public bool IsReadOnly;
        public bool IsEEProm;
        public double RangeDiff;
        public VariableValue MinValue;
        public VariableValue MaxValue;
        public VariableValue DefaultValue;
        public VariableValue RamValue;
        public VariableValue EEPromValue;
        public string ParamName;

        public override string ToString() {
            return "{" + ParamIndex
                + "," + ParamCategory
                + "," + ParamVarKind
                + "," + ParamSize
                + "," + "("+ParamStorageFlags.ToString("F")+")"
                + "," + MinValue
                + "," + MaxValue
                + "," + DefaultValue
                + "," + RamValue
                + "," + EEPromValue
                + "," + "\"" + (ParamName == null ? "" : ParamName) + "\"}";
            ;

        }

        public static FieldId GetFieldId(string fieldNameOrId) {
            if (!String.IsNullOrWhiteSpace(fieldNameOrId)) {
                int val;
                if (int.TryParse(fieldNameOrId, out val)) {
                    if ((val >= 0) && (val < (int)(FieldId.NullField)))
                        return (FieldId)val;
                } else {
                    FieldId id = FieldId.IndexField;
                    fieldNameOrId = fieldNameOrId.ToLower();
                    foreach (string s in FieldNames) {
                        if (s.StartsWith(fieldNameOrId)) return id;
                        ++id;
                    }

                }
            }
            return FieldId.NullField;
        }

        public static int GetReadWriteVarFlag(string fieldNameOrId) {
            if ("range".StartsWith(fieldNameOrId)) return ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_RANGE;
            switch (GetFieldId(fieldNameOrId)) {
                case FieldId.EepromValueField: return ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_EEPROM;
                case FieldId.DefaultValueField: return ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_DEFAULT;
                default: break;
            }
            return ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_RAM;
        }

        public DeviceParameter(string parameterNameOrIndex = null) {
            ParamVarKind = VariableKind.VK_None;
            ParamSize = 0;
            ParamIndex = -1;
            ParamName = null;
            if (!String.IsNullOrWhiteSpace(parameterNameOrIndex)) {
                String s = parameterNameOrIndex.Trim();
                int val;
                if (int.TryParse(s, out val)) {
                    ParamIndex = (val >= 0 ? val : -1);
                } else {
                    ParamName = s.ToLower();
                }
            }
        }

        public DeviceParameter(String name, int parameterIndex, VariableKind representationKind, 
            StorageFlags paramStorageFlags,
            int parameterSize,
            bool hasMinMax, VariableValue minValue, VariableValue maxValue, VariableValue defaultValue) {
            ParamName = name;
            ParamIndex = parameterIndex;
            ParamVarKind = representationKind;
            ParamStorageFlags = paramStorageFlags;
            ParamSize = parameterSize;
            IsDynamic = false;
            IsReadOnly = false;
            IsEEProm = false;

            DefaultValue = defaultValue;
            RamValue = null;
            EEPromValue = null;

            RequiresRefresh = true;
            HasChanged = true;
            HasRamValue = false;
            HasEEPromValue = false;
            HasDefaultValue = false;

            UpdateMinAndMax(hasMinMax, minValue, maxValue);
        }

        public void UpdateMinAndMax(bool hasMinMax, VariableValue minValue, VariableValue maxValue) {
            HasMinMaxValue = hasMinMax;
            if (hasMinMax) {
                if (minValue.CompareTo(maxValue) > 0.0) {
                    VariableValue v = minValue;
                    minValue = maxValue;
                    maxValue = v;
                }
                MinValue = minValue;
                MaxValue = maxValue;
            } else {
                MinValue = new VariableValue(ParamVarKind, VariableValue.VariableValueKind.VV_Min);
                MaxValue = new VariableValue(ParamVarKind, VariableValue.VariableValueKind.VV_Max);
            }
            RangeDiff = MaxValue.CompareTo(MinValue);
            if (RangeDiff < 1.0)
                RangeDiff = 1.0;
        }
    }
}
