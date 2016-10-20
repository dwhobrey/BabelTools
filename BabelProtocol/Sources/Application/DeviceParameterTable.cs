using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Babel.BabelProtocol {

    public class DeviceParameterTable {

        public readonly static DeviceParameter NullParameter = new DeviceParameter();

        int LastEntry;
        public Dictionary<int, DeviceParameter> IndexTable;
        public Dictionary<string, DeviceParameter> NameTable;

        public DeviceParameterTable() {
            LastEntry = -1;
            IndexTable = new Dictionary<int, DeviceParameter>();
            NameTable = new Dictionary<string, DeviceParameter>();
        }

        public HashSet<DeviceParameter> GetEntries() {
            HashSet<DeviceParameter> h = new HashSet<DeviceParameter>();
            foreach (DeviceParameter d in IndexTable.Values) {
                h.Add(d);
            }
            foreach (DeviceParameter d in NameTable.Values) {
                h.Add(d);
            }
            return h;
        }

        public static int ConvertToIndex(string nameOrIndex) {
            int val;
            if (int.TryParse(nameOrIndex, out val)) return val;
            return -1;
        }

        // Find table entry given name or index.
        // Returns entry or null.
        public DeviceParameter Find(string nameOrIndex) {
            DeviceParameter d = null;
            if (!String.IsNullOrWhiteSpace(nameOrIndex)) {
                int val;
                if (int.TryParse(nameOrIndex, out val))
                    IndexTable.TryGetValue(val, out d);
                else {
                    nameOrIndex = nameOrIndex.Trim().ToLower();
                    NameTable.TryGetValue(nameOrIndex, out d);
                }
            }
            return d;
        }

        // Find table entry given index.
        // Returns entry or null.
        public DeviceParameter Find(int index) {
            DeviceParameter d = null;
            IndexTable.TryGetValue(index, out d);
            return d;
        }

        public int GetLastEntryIndex() {
            return LastEntry;
        }

        // Add or update table entry.
        public void Add(DeviceParameter d) {
            if (d != null) {
                lock (IndexTable) {
                    if (!String.IsNullOrWhiteSpace(d.ParamName))
                        NameTable[d.ParamName] = d;
                    if (d.ParamIndex >= 0) {
                        IndexTable[d.ParamIndex] = d;
                        if (d.ParamIndex > LastEntry)
                            LastEntry = d.ParamIndex;
                    }
                }
            }
        }

        // Add an entry to table.
        // deviceParameterFields must correspond to DeviceParameter fields in order.
        // Returns null if entry was added, otherwise error message.
        public string Put(Object[] deviceParameterFields) {
            if (deviceParameterFields != null && deviceParameterFields.Length == DeviceParameter.FieldNames.Length) {           
                DeviceParameter.FieldId fieldId=0;
                try {
                    DeviceParameter d = new DeviceParameter();
                    fieldId=DeviceParameter.FieldId.IndexField;
                    d.ParamIndex = Convert.ToInt32(deviceParameterFields[(int)fieldId].ToString());
                    fieldId=DeviceParameter.FieldId.CategoryField;
                    d.ParamCategory = Convert.ToInt32(deviceParameterFields[(int)fieldId].ToString());
                    fieldId=DeviceParameter.FieldId.VariableKindField;
                    d.ParamVarKind = VariableValue.StringToVK(deviceParameterFields[(int)fieldId].ToString());
                    fieldId=DeviceParameter.FieldId.SizeField;
                    d.ParamSize = Convert.ToInt32(deviceParameterFields[(int)fieldId].ToString());
                    if (d.ParamSize < 0) d.ParamSize = VariableValue.DataSize(d.ParamVarKind);
                    fieldId=DeviceParameter.FieldId.StorageFlagsField;
                    d.ParamStorageFlags = VariableValue.StringToSF(deviceParameterFields[(int)fieldId].ToString());
                    fieldId=DeviceParameter.FieldId.MinValueField;
                    d.MinValue = new VariableValue(d.ParamVarKind,deviceParameterFields[(int)fieldId].ToString());
                    fieldId=DeviceParameter.FieldId.MaxValueField;
                    d.MaxValue = new VariableValue(d.ParamVarKind,deviceParameterFields[(int)fieldId].ToString());
                    fieldId=DeviceParameter.FieldId.DefaultValueField;
                    d.DefaultValue = new VariableValue(d.ParamVarKind,deviceParameterFields[(int)fieldId].ToString());
                    fieldId=DeviceParameter.FieldId.RamValueField;
                    d.RamValue = new VariableValue(d.ParamVarKind,deviceParameterFields[(int)fieldId].ToString());
                    fieldId=DeviceParameter.FieldId.EepromValueField;
                    d.EEPromValue = new VariableValue(d.ParamVarKind, deviceParameterFields[(int)fieldId].ToString());
                    fieldId=DeviceParameter.FieldId.NameField;
                    d.ParamName = deviceParameterFields[(int)fieldId].ToString().Trim().ToLower();
                    Add(d);
                } catch (Exception e) {
                    return "Error: parameter field "+DeviceParameter.FieldNames[(int)fieldId] +" in wrong format:"+e.Message;
                }
                return null;
            }
            return "Error: not enough parameter fields in array.";
        }

        // Add an entry to table.
        // Returns null on success, otherwise error message.
        public string Put(int parameterIndex, int parameterCategory, int parameterVarKind,
                int parameterSize,int parameterStorageFlags,
                Object minValue, Object maxValue, Object defaultValue,
                Object ramValue, Object eepromValue, string parameterName) {
            DeviceParameter d = new DeviceParameter();
            VariableKind s;
            try {
                d.ParamIndex = parameterIndex;
                d.ParamCategory = parameterCategory;
                d.ParamVarKind = VariableValue.StringToVK(parameterVarKind.ToString());
                if (d.ParamVarKind == VariableKind.VK_String)
                    s = VariableKind.VK_Byte;
                else
                    s = d.ParamVarKind;
                if (parameterSize < 0) d.ParamSize = VariableValue.DataSize(d.ParamVarKind);
                else d.ParamSize = parameterSize;
                d.ParamStorageFlags = VariableValue.StringToSF(parameterStorageFlags.ToString());
                d.MinValue = new VariableValue(s,minValue.ToString());
                d.MaxValue = new VariableValue(s,maxValue.ToString());
                d.DefaultValue = new VariableValue(s,defaultValue.ToString());
                d.RamValue = new VariableValue(d.ParamVarKind,ramValue.ToString());
                d.EEPromValue = new VariableValue(d.ParamVarKind, eepromValue.ToString());
                d.ParamName = (parameterName==null?null:parameterName.Trim().ToLower());
                Add(d);
            } catch (Exception e) {
                return "Error: parameter field in wrong format:" + e.Message;
            }
            return null;
        }

        // Update a field in a table entry. Create new entry if necessary.
        // Returns null on success, otherwise error message.
        public string PutField(string parameterNameOrIndex, string fieldNameOrId, object value) {
            string result = null;
            if (!String.IsNullOrWhiteSpace(parameterNameOrIndex) && value!=null) {
                DeviceParameter d = Find(parameterNameOrIndex);
                bool isNew = (d == null);
                if (isNew) {
                    d = new DeviceParameter(parameterNameOrIndex);
                }
                switch (DeviceParameter.GetFieldId(fieldNameOrId)) {
                    case DeviceParameter.FieldId.IndexField:
                        try {
                            int v = (int)value;
                            if(v>=0) {
                                if (!isNew && d.ParamIndex >= 0) {
                                    IndexTable.Remove(d.ParamIndex);
                                }
                                d.ParamIndex = v;
                                isNew = true;
                            } else
                                result = "Error: negative index.";
                        } catch (Exception) {
                            result = "Error: invalid index type.";
                        }
                        break;
                    case DeviceParameter.FieldId.CategoryField:
                        try {
                            int v = (int)value;
                            if(v>=0) {
                                d.ParamCategory = v;
                            } else
                                result = "Error: negative category.";
                        } catch (Exception) {
                            result = "Error: invalid category type.";
                        }
                        break;
                    case DeviceParameter.FieldId.VariableKindField:
                        try {
                            VariableKind v = VariableValue.StringToVK(value.ToString());
                            d.ParamVarKind = v;
                            if(v==VariableKind.VK_None)
                                result = "Error: bad representation kind.";
                        } catch (Exception) {
                            result = "Error: invalid representation kind type.";
                        }
                        break;
                    case DeviceParameter.FieldId.SizeField:
                        try {
                            int v = (int)value;
                            if(v>=0) {
                                d.ParamSize = v;
                            } else
                                d.ParamSize = VariableValue.DataSize(d.ParamVarKind);
                        } catch (Exception) {
                            result = "Error: invalid size value.";
                        }
                        break;
                    case DeviceParameter.FieldId.StorageFlagsField:
                        try {
                            StorageFlags v = VariableValue.StringToSF(value.ToString());
                            d.ParamStorageFlags = v;
                            if (v == StorageFlags.SF_None) 
                                result = "Error: bad storage flags.";
                        } catch (Exception) {
                            result = "Error: invalid storage flags type.";
                        }
                        break;
                    case DeviceParameter.FieldId.MinValueField:
                        try {
                            d.MinValue = new VariableValue(d.ParamVarKind,value.ToString());
                        } catch (Exception) {
                            result = "Error: invalid min value type.";
                        }
                        break;
                    case DeviceParameter.FieldId.MaxValueField:
                        try {
                            d.MaxValue = new VariableValue(d.ParamVarKind, value.ToString());
                        } catch (Exception) {
                            result = "Error: invalid max value type.";
                        }
                        break;
                    case DeviceParameter.FieldId.DefaultValueField:
                        try {
                            d.DefaultValue = new VariableValue(d.ParamVarKind,value.ToString());
                        } catch (Exception) {
                            result = "Error: invalid default value type.";
                        }
                        break;
                    case DeviceParameter.FieldId.RamValueField:
                        try {
                            d.RamValue = new VariableValue(d.ParamVarKind,value.ToString());
                        } catch (Exception) {
                            result = "Error: invalid ram value type.";
                        }                       
                        break;
                    case DeviceParameter.FieldId.EepromValueField:
                        try {
                            d.EEPromValue = new VariableValue(d.ParamVarKind,value.ToString());
                        } catch (Exception) {
                            result = "Error: invalid Eeprom value type.";
                        }                       
                        break;
                    case DeviceParameter.FieldId.NameField: 
                        try {
                            string s = value.ToString().Trim().ToLower();
                            if (!isNew && !String.IsNullOrWhiteSpace(d.ParamName)) {
                                NameTable.Remove(d.ParamName);
                            }
                            d.ParamName = s;
                            isNew = true;
                        } catch (Exception) {
                            result = "Error: invalid index type.";
                        }                       
                        break;
                    default:
                        result = "Error: invalid field name or Id.";
                        break;
                }
                if (isNew && result==null) Add(d);
            }
            return result;
        }

        public string GetParameters(string parameterNameOrIndex) {
            if (!String.IsNullOrWhiteSpace(parameterNameOrIndex)) {
                DeviceParameter d = Find(parameterNameOrIndex);
                if (d != null) {
                    return d.ToString();
                }
            }
            return null;
        }

        // Get field value from table entry.
        // Returns null if not found.
        public object GetField(string parameterNameOrIndex, string fieldNameOrId) {
            if (!String.IsNullOrWhiteSpace(parameterNameOrIndex)) {
                DeviceParameter d = Find(parameterNameOrIndex);
                if (d != null) {
                    switch (DeviceParameter.GetFieldId(fieldNameOrId)) {
                        case DeviceParameter.FieldId.IndexField:
                            return d.ParamIndex;
                        case DeviceParameter.FieldId.CategoryField:
                            return d.ParamCategory;
                        case DeviceParameter.FieldId.VariableKindField:
                            return d.ParamVarKind;
                        case DeviceParameter.FieldId.SizeField:
                            return d.ParamSize;
                        case DeviceParameter.FieldId.StorageFlagsField:
                            return d.ParamStorageFlags;
                        case DeviceParameter.FieldId.MinValueField:
                            return d.MinValue;
                        case DeviceParameter.FieldId.MaxValueField:
                            return d.MaxValue;
                        case DeviceParameter.FieldId.DefaultValueField:
                            return d.DefaultValue;
                        case DeviceParameter.FieldId.RamValueField:
                            return d.RamValue;
                        case DeviceParameter.FieldId.EepromValueField:
                            return d.EEPromValue;
                        case DeviceParameter.FieldId.NameField:
                            return d.ParamName;
                        default:
                            break;
                    }
                }
            }
            return null;
        }
    }
}
