using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using Babel.Core;
using Babel.XLink;

namespace Babel.BabelProtocol {

    public class ParameterManager {

        public const String TAG = "ParameterManager";

        public DeviceParameterTable CurrentDeviceTable;
        public Dictionary<int, String> ParameterIndexToNameTable;

        public ParameterManager() {
		    CurrentDeviceTable = new DeviceParameterTable();
            ParameterIndexToNameTable = new Dictionary<int, String>();
	    }

        private void NotifyChangeListeners(DeviceParameter d) {
        }

        private void CheckVerifiedWrites(DeviceParameter d) {
	    }

        public static List<double> ProcessReadVarMessageViaOffsets(BabelMessage msg, List<long> offsetTable) {
            int n = offsetTable.Count;
            List<double> p;
            if (n == 0) return null;
            p = new List<double>(n);
            for (int k = 0; k < n;k++) {
                long v = offsetTable[k];
                // (paramIndex<<24) | (((int)paramVarKind) << 16) | (dataSize << 8) | idx)
                int idx = (int)(v & 0xff);
                v >>= 8;
                int len = (int)(v & 0xff);
                v >>= 8;
                VariableKind paramVarKind = (VariableKind)(v & 0xff);
                try {
                    p.Add(VariableValue.GetDouble(paramVarKind, msg.DataAry, idx, len));
                } catch(Exception) {
                    // Index out of range.
                    return null;
                }
            }
            return p;
        }

        // Process read variable reply.
        // <cmd,> {readId, cmdFlags, page, numRead:numToRead, {parameterIndex}+ } [data]*.
        // Store results in tables.
        public static int ProcessReadVarMessage(BabelMessage msg,
                ParameterManager manager,
                Dictionary<int, DeviceParameter> indexTable,
                Dictionary<string, DeviceParameter> nameTable,
                Dictionary<int, String> indexToNameTable,
                List<long> offsetTable=null) {
            int numProcessed = 0;
            if (msg.DataLen > 5) {
                DeviceParameter d;
                String name;
                bool hasRamValue, hasEEPromValue, hasDefaultValue;
                bool hasMinMaxValue, hasDataFlags;
                bool isNewParam, notInNameTable, notInIndexTable;
                bool hasMeta,hasKindOnly;
                VariableKind paramVarKind;
                VariableValue ramValue, eepromValue, defaultValue;
                VariableValue minValue, maxValue;
                int readId,cmdFlags, pageNum, pIdx, numRead, numToRead, dataSize, dataFlags, argsFlags;
                int idx = 0, n = msg.DataLen, offsetIndex = 0;
                readId = (msg.DataAry[idx++] & 0xff);
                cmdFlags = (msg.DataAry[idx++] & 0xff);
                pageNum = (msg.DataAry[idx++] & 0xff);
                numToRead = (msg.DataAry[idx] & 0x0f);
                numRead = (msg.DataAry[idx++] & 0xf0) >> 4;
                pIdx = idx;
                idx += numToRead;
                if (numRead == 0)
                    return numProcessed;
                hasKindOnly = ((cmdFlags & ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_PACK_KIND) != 0);
                hasMeta = hasKindOnly||((cmdFlags & ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_PACK) == 0);
                // Now read in values.
                while (idx < n) {
                    int paramIndex = msg.DataAry[pIdx++];
                    d = null;
                    name = null;
                    hasRamValue = false;
                    hasEEPromValue = false;
                    hasDefaultValue = false;
                    hasMinMaxValue = false;
                    hasDataFlags = false;
                    isNewParam = false;
                    notInNameTable = true;
                    notInIndexTable = true;
                    dataFlags = 0;
                    defaultValue = null;
                    eepromValue = null;
                    ramValue = null;
                    minValue = null;
                    maxValue = null;
                    // First get VariableKind.
                    if (hasMeta) {
                        paramVarKind = (VariableKind)(msg.DataAry[idx++] & 0xff);
                    } else {
                        if (indexTable == null) return numProcessed;
                        indexTable.TryGetValue(paramIndex, out d);
                        notInIndexTable = (d == null);
                        if (d == null) return numProcessed;
                        paramVarKind = d.ParamVarKind;
                    }
                    dataSize = VariableValue.DataSize(paramVarKind);             
                    if (paramVarKind != VariableKind.VK_None) {
                        if (dataSize == 0) return numProcessed;
                        if (hasMeta&&!hasKindOnly) {
                            // Next read StorageFlags.
                            if ((idx + 1) <= n) {
                                hasDataFlags = true;
                                dataFlags = (msg.DataAry[idx++] & 0xff);
                            } else
                                return numProcessed;
                            // Next read ArgsFlags.
                            if ((idx + 1) <= n) {
                                argsFlags = (msg.DataAry[idx++] & 0xff);
                            } else
                                return numProcessed;
                            offsetIndex = idx;
                        } else {
                            if(hasKindOnly)
                                offsetIndex = idx;
                            hasDataFlags = true;
                            dataFlags = (int)StorageFlags.SF_Dynamic;
                            argsFlags = ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_RAM;
                        }
                        // Then work through cmdFlags:
                        if ((argsFlags & ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_RAM) != 0) {
                            if ((idx < n) && (paramVarKind == VariableKind.VK_String)) {
                                dataSize = (msg.DataAry[idx++] & 0xff);
                            }
                            if ((idx + dataSize) <= n) {
                                hasRamValue = true;
                                ramValue = new VariableValue(paramVarKind, msg.DataAry, idx, dataSize);
                                idx += dataSize;
                            } else
                                return numProcessed;
                        }
                        if ((argsFlags & ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_EEPROM) != 0) {
                            if ((idx < n) && (paramVarKind == VariableKind.VK_String)) {
                                dataSize = (msg.DataAry[idx++] & 0xff);
                            }
                            if ((idx + dataSize) <= n) {
                                hasEEPromValue = true;
                                eepromValue = new VariableValue(paramVarKind, msg.DataAry, idx, dataSize);
                                idx += dataSize;
                            } else
                                return numProcessed;
                        }
                        if ((argsFlags & ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_DEFAULT) != 0) {
                            if ((idx + dataSize) <= n) {
                                hasDefaultValue = true;
                                defaultValue = new VariableValue(paramVarKind, msg.DataAry, idx, dataSize);
                                idx += dataSize;
                            } else
                                return numProcessed;
                        }
                        if ((argsFlags & ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_RANGE) != 0) {
                            // Read in dataSize, category, min, max.
                            if ((idx + 2 + 2 * dataSize) <= n) {
                                idx++; // Skip dataSize.
                                idx++; // Skip category.
                                hasMinMaxValue = true;
                                minValue = new VariableValue(paramVarKind, msg.DataAry, idx, dataSize);
                                idx += dataSize;
                                maxValue = new VariableValue(paramVarKind, msg.DataAry, idx, dataSize);
                                idx += dataSize;
                            } else
                                return numProcessed;
                        }
                        // Finally check for name.
                        if ((argsFlags & ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_NAME) != 0) {
                            byte[] buffer = new byte[msg.DataAry.Length];
                            byte b;
                            int k = 0, len = 0;
                            if (idx < n) { // Get len byte.
                                len = (msg.DataAry[idx++] & 0xff);
                            }
                            while (idx < n) {
                                if (len-- == 0)
                                    break;
                                b = (byte)(msg.DataAry[idx++] & 0xff);
                                if (b == 0)
                                    break;
                                buffer[k++] = b;
                            }
                            try {
                                name = Encoding.UTF8.GetString(buffer, 0, k);
                            } catch (Exception) {
                                name = null;
                            }
                            if (name != null) {
                                name = name.Trim().ToUpper();
                                if (name.Length < 2 || name.Length > 8)
                                    name = null;
                            }
                        }
                    }
                    if (hasMeta && (offsetTable != null) && (offsetIndex<idx)) {
                        offsetTable.Add((paramIndex << 24) | (((int)paramVarKind) << 16) | (dataSize << 8) | offsetIndex);
                    }
                    // Now update device profile.
                    if (name == null && indexToNameTable!=null) {
                        indexToNameTable.TryGetValue(paramIndex,out name);
                        if (name == null) {
                            Log.w(TAG, "ProcessReadVarMessage: no name for device parameter:" + paramIndex + ".");
                        }
                    }
                    // First look for parameter by name:
                    if (name != null && nameTable != null) {
                        nameTable.TryGetValue(name,out d);
                        notInNameTable = (d == null);
                    }
                    if (d == null && indexTable != null) {
                        indexTable.TryGetValue(paramIndex, out d);
                        notInIndexTable = (d == null);
                    }
                    if (d != null && name != null)
                        d.ParamName = name;
                    if (d == null) {
                        d = new DeviceParameter(name, paramIndex, paramVarKind, (StorageFlags)dataFlags, dataSize, hasMinMaxValue, minValue, maxValue, defaultValue);
                        isNewParam = true;
                    }
                    if (hasDataFlags) {
                        d.ParamStorageFlags = (StorageFlags)dataFlags;
                        d.IsDynamic = ((dataFlags & (int)StorageFlags.SF_Dynamic) != 0);
                        d.IsReadOnly = ((dataFlags & (int)StorageFlags.SF_ReadOnly) != 0);
                        d.IsEEProm = ((dataFlags & (int)StorageFlags.SF_EEPROM) != 0);
                        if (!d.IsEEProm)
                            hasEEPromValue = false;
                    }
                    if (hasRamValue) {
                        if (isNewParam || !VariableValue.Equal(ramValue, d.RamValue))
                            d.HasChanged = true;
                        d.RamValue = ramValue;
                        d.HasRamValue = true;
                    }
                    if (hasEEPromValue) {
                        if (isNewParam || !VariableValue.Equal(eepromValue, d.EEPromValue))
                            d.HasChanged = true;
                        d.EEPromValue = eepromValue;
                        d.HasEEPromValue = true;
                    }
                    if (hasDefaultValue) {
                        if (isNewParam || !VariableValue.Equal(defaultValue, d.DefaultValue))
                            d.HasChanged = true;
                        d.DefaultValue = defaultValue;
                        d.HasDefaultValue = true;
                    }
                    if (hasMinMaxValue) {
                        if (isNewParam || (d.MinValue != minValue) || (d.MaxValue != maxValue))
                            d.HasChanged = true;
                        if (!isNewParam) d.UpdateMinAndMax(true, minValue, maxValue);
                    }
                    // Finally, make sure it's in the tables:
                    if (notInNameTable && nameTable != null && (name != null))
                        nameTable[name] = d;
                    if (notInIndexTable && indexTable !=null)
                        indexTable[paramIndex] = d;
                    d.RequiresRefresh = false;
                    if (manager != null) {
                        manager.NotifyChangeListeners(d);
                        manager.CheckVerifiedWrites(d);
                    }
                    ++numProcessed;
                }
            }
            return numProcessed;
        }
    }
}
