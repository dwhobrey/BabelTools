using System;
using System.Collections.Generic;

using Babel.Core;
using Babel.XLink;

namespace Babel.BabelProtocol {

    public class MediatorNetIf : LinkDriver {

        class IsoTask {
            public LinkDriver ownerDriver;
            public AddressParams addressParams;
            public uint msTicksRelativeToGo;
            public uint msInterval;      
            public uint repeatCount;
            public int ioIndex;
            public int isoId,state,cmd;

            public IsoTask() {
                addressParams = new AddressParams();
                ioIndex = -1;
                isoId = 0;
                state = 0;
                cmd = 0;
            }
        }

        // Task state flags during task service processing.
        const int IsoMonitor = 0x01;
        const int IsoReschedule = 0x02;
        const int IsoRemove = 0x03;

        uint LastIsoUpdateTicks;
        int IsoTaskSize = 16;

        IsoTask[] IsoTasks;
        int[] IsoTaskQueue;

        MessageExchange Exchange;

        public MediatorNetIf(NetIfManager manager, MessageExchange exchange)
            : base(manager, new VirtualDevice("Mediator"), ProtocolConstants.NETIF_MEDIATOR_PORT, false, true, false, true) {
                Exchange = exchange;
                SchedulerInterval = 1;
                IsoTaskQueue = new int[IsoTaskSize];
                IsoTasks = new IsoTask[IsoTaskSize];
                for (int k = 0; k < IsoTaskSize; ++k) {
                    IsoTasks[k] = new IsoTask();
                }
                InitTasks();
        }

        protected override void Reset() {
	        ResetTasks();
        }

        // Submit a mediator command to link WriteQ.
        // For Mediator messages:
        // Inbound=0, HasSender=1,
        // Receiver=ADRS_LOCAL, RNetIf=NETIF_MEDIATOR_PORT,
        // sender=NodeAdrs, ident=0, SNetIf=link netIf
        public int SendLinkMediatorCommand(LinkDriver ltx, bool verified, byte cmd, byte dataLen, byte[] pData, byte bufferIndex) {
            int idx = Manager.Factory.CreateGeneralMessage(verified, cmd, 
                ProtocolConstants.ADRS_LOCAL, Manager.NodeAdrs, 0, (byte)0, 
                dataLen, pData, bufferIndex);
            if (idx != -1) {
                if (ltx.LinkWriteQueue.Push(idx) == -1) {
                    Manager.IoBuffersFreeHeap.Release(idx);
                    return -1;
                }
            }
            return idx;
        }

        // Read parameter values from RAM or EEPROM.
        // Command syntax: data[]={ readId, cmdFlags, page, <numRead:>numToRead, {parameterIndexes}+ }
        //						<[{[VarKind[,StoreFlgs,ArgsFlgs,value data]}*]>.
        // Or: data[]={ readId, cmdFlags, page, name} when BX_MEDIATOR_DEVICE_RWV_FLAGS_BY_NAME cmdFlag set.
        // In reply to a BY_NAME: strips out request name and sets NAME flag to reply in std form.
        // Returns number of values read / changed depending on readFlags:
        // BX_MEDIATOR_DEVICE_MONITORVAR = Iso compare and return true if parameters have changed.
        // For each parameter, replies with:
        //  {DSK [,DSF, [ ramValue,] [ eepromValue,] [defaultValue]
        //       [dataSize, minValue, maxValue] [name + '\0'] ]}.
        public byte MediatorReadCommand(MessageTransaction mtx) { 
            DeviceParameterTable pt = Exchange.ParameterTable;
            DeviceParameter d;
            PacketBuffer g = mtx.MsgBuffer;
            byte[] pAry;
            int numReadIndex, dataIndex;
            bool addMeta,addKindOnly;
            byte n, j, k, byteCount, numToRead;
            byte readId, cmdFlags,pageNum,parameterVarKind,parameterFlags,parameterSize,metaSize;
            byte c = 0, maxData = (byte)(Exchange.Manager.MaxPacketSize - ProtocolConstants.GENERAL_OVERHEADS_SIZE); 
            byte[] paramIndexes = new byte[8];
            n = g.dataLength();
            if(n<5) return 0;
            numReadIndex=-1;
            dataIndex = ProtocolConstants.GENERAL_DATA_ARRAY_OFFSET;
            pAry = g.buffer;
            readId = pAry[dataIndex++];
            cmdFlags = pAry[dataIndex++];
            pageNum = pAry[dataIndex++];
            n -= 3;
            // TODO: table page selection.
            addKindOnly = (cmdFlags & ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_PACK_KIND) != 0; 
            addMeta = addKindOnly||(cmdFlags & ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_PACK) == 0; 
            if((cmdFlags&ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_BY_NAME)!=0) {
                pAry[dataIndex+n]=0;
                string name = Primitives.GetArrayStringValue(pAry, dataIndex, n);
                // Search for name in table.
                d = pt.Find(name); 
                if(d==null) {
                    pAry[dataIndex-1]=cmdFlags;
                    dataIndex += n; 
                    numToRead=0;
                } else {
                    // If found, fall through and add meta data.
                    cmdFlags|= ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_NAME;
                    numToRead=1;
                    paramIndexes[0] = (byte)d.ParamIndex;  
                }                               
            } else {
                --n; 
                numToRead = (byte)(pAry[dataIndex++] & 0xf);
                if (numToRead > n) numToRead = n; 
                j = numToRead; 
                if (numToRead > 8) numToRead = 8; 
                for (k = 0; k < numToRead; k++) 
                    paramIndexes[k] = pAry[dataIndex+k]; 
            }
            if(numToRead>0) {
                dataIndex = ProtocolConstants.GENERAL_DATA_ARRAY_OFFSET;
                pAry = g.buffer;
                pAry[dataIndex++] = readId;
                pAry[dataIndex++] = cmdFlags; // Restore parameters.
                pAry[dataIndex++] = pageNum;
                numReadIndex = dataIndex;
                pAry[dataIndex++] = 0; 
                for (k = 0; k < numToRead; k++)
                    pAry[dataIndex++] = paramIndexes[k]; 
            } 
            byteCount=(byte)dataIndex; 
            g.dataLength(byteCount);
            k=0;           
            while(k<numToRead) {
                byte paramIndex = paramIndexes[k]; 
                if(paramIndex>pt.GetLastEntryIndex()) break;
                d = pt.Find(paramIndex);
                if (d == null) d = DeviceParameterTable.NullParameter;
                n = 0;
                parameterVarKind = (byte)d.ParamVarKind;
                if (addMeta) { 
                    // Write out representation kind.
                    metaSize = 1;
                    if ((byteCount + metaSize) >= maxData) break;
                    pAry[dataIndex+n++]=parameterVarKind;
                } else {
                    metaSize = 0;
                }
                if(parameterVarKind!=(byte)VariableKind.VK_None) {
                    VariableValue p; byte[] b;
                    byte argsFlags,argsIndex=0;
                    parameterSize = (byte)d.ParamSize;       
                    parameterFlags =(byte)d.ParamStorageFlags;
                    if (addMeta&&!addKindOnly) { 
                        // Write out parameter flags & space for argsFlags.
                        metaSize += 2;
                        if ((byteCount + metaSize) >= maxData) break;
                        pAry[dataIndex+n++]= parameterFlags;
                        argsIndex = n++;
                    }
                    argsFlags=0;
                    // Now write out parameter details depending on cmd flags.       
                    if(((cmdFlags&ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_RAM)!=0)
                            &&((parameterFlags&(byte)StorageFlags.SF_RAM)!=0)) {
                        p = d.RamValue; 
                        if(p!=null) {                
                            if(parameterVarKind==(byte)VariableKind.VK_String) { // Get size of string.
                                byte maxSize= (byte)d.MaxValue.ValueLong;
                                int s = p.ValueString.Length; 
                                if(s>maxSize) s = maxSize;
                                parameterSize=(byte)(1+s); // Allow one for len byte.
                            }                    
                            if((byteCount+metaSize+parameterSize)<maxData) {
                                argsFlags|=ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_RAM;
                                metaSize+=parameterSize;           
                                if(parameterVarKind==(byte)VariableKind.VK_String) { // Prefix a len byte.
                                    --parameterSize;
                                    pAry[dataIndex+n++]=parameterSize;
                                    b = Primitives.StringToByteArray(p.ValueString);
                                    for (j = 0; j < parameterSize; j++) pAry[dataIndex+n++]=b[j];
                                } else {
                                    if ((mtx.TaskCmd != ProtocolConstants.MEDIATOR_DEVICE_ISOMONVAR) || (c != 0) || (paramIndex == ProtocolConstants.PARAMETER_TABLE_INDEX_TICKER)) { 
                                        Array.Copy(p.GetBytes(), 0, pAry, dataIndex+n, parameterSize);
                                    } else {
                                        int h = parameterSize;
                                        b = p.GetBytes();
                                        if(h>b.Length) h = b.Length;
                                        for (j = 0; j < parameterSize; j++) {
                                            int i=dataIndex+n+j;
                                            if ((c == 0) && pAry[i] != b[j]) c = 1; 
                                            pAry[i] = b[j]; 
                                        }
                                    }
                                    n += parameterSize;
                                }
                            }                    
                        }                                               
                    }
                    if(((cmdFlags&ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_EEPROM)!=0)
                            &&((parameterFlags&(byte)StorageFlags.SF_EEPROM)!=0)) { 
                        p = d.EEPromValue;
                        if (p != null) {
                            if (parameterVarKind == (byte)VariableKind.VK_String) { // Get size of string.
                                byte maxSize = (byte)d.MaxValue.ValueLong;
                                int s = p.ValueString.Length; 
                                if (s > maxSize) s = maxSize;
                                parameterSize = (byte)(1 + s); // Allow one for len byte.
                            }
                            if ((byteCount + metaSize + parameterSize) < maxData) {
                                argsFlags |= ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_EEPROM;
                                metaSize += parameterSize;
                                if (parameterVarKind == (byte)VariableKind.VK_String) { // Prefix a len byte.
                                    --parameterSize;
                                    pAry[dataIndex+n++]= parameterSize;
                                    b = p.GetBytes();
                                    for (j = 0; j < parameterSize; j++) pAry[dataIndex+n++]=b[j];
                                } else {
                                    Array.Copy(p.GetBytes(), 0, pAry, dataIndex+n, parameterSize);
                                    n += parameterSize;
                                }
                            }
                        }                                                                           
                    }
                    if (parameterVarKind == (byte)VariableKind.VK_String) 
                        parameterSize=1;
                    else {
                        if((cmdFlags&ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_DEFAULT)!=0) {            
                            if((byteCount+metaSize+parameterSize)<maxData) {
                                argsFlags|=ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_DEFAULT;
                                metaSize+=parameterSize;
                                Array.Copy(d.DefaultValue.GetBytes(), 0, pAry, dataIndex+n, parameterSize);
                                n += parameterSize;
                            }                                                           
                        }
                    }            
                    if((cmdFlags&ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_RANGE)!=0) {
                        byte rangeSize = (byte)(2 + parameterSize*2);
                        if((byteCount+metaSize+rangeSize)<maxData) {
                            argsFlags|=ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_RANGE;
                            metaSize += rangeSize;
                            pAry[dataIndex+n++]=parameterSize;
                            pAry[dataIndex+n++]=(byte)d.ParamCategory;  
                            Array.Copy(d.MinValue.GetBytes(), 0, pAry, dataIndex+n, parameterSize);
                            n += parameterSize;
                            Array.Copy(d.MaxValue.GetBytes(), 0, pAry, dataIndex+n, parameterSize);
                            n += parameterSize;
                        }                  
                    }                 
                    if((cmdFlags&ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_NAME)!=0) {
                        // Get size of string.
                        parameterSize=0;
                        string s = d.ParamName;
                        if (s != null) parameterSize = (byte)s.Length;
                        if (parameterSize > ProtocolConstants.MAX_PARAMETER_NAME_SIZE)
                            parameterSize = ProtocolConstants.MAX_PARAMETER_NAME_SIZE;
                        if((byteCount+metaSize+1+parameterSize)<maxData) {
                            argsFlags|=ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_NAME;
                            metaSize+=(byte)(1+parameterSize);
                            pAry[dataIndex+n++]=parameterSize;
                            b = Primitives.StringToByteArray(s);
                            for (j = 0; j < parameterSize; j++) pAry[dataIndex+n++]= b[j];                                     
                        }
                    }
                    if (addMeta && !addKindOnly) 
                        pAry[dataIndex+argsIndex]=argsFlags;
                }        
                byteCount+=metaSize;
                dataIndex += metaSize;
                g.dataLength((byte)(g.dataLength()+metaSize));
                ++k;      
            }
            if (numReadIndex >= 0) g.buffer[numReadIndex] = (byte)((k<<4)+numToRead);        
            mtx.ChangeDir=true;
            mtx.Finish = MessageTransaction.FinishAction.Normal;
            if (mtx.TaskCmd == ProtocolConstants.MEDIATOR_DEVICE_ISOMONVAR) return c; 
            return k; 
        }

        // Write parameter values to RAM or EEPROM.
        // Writes default value if no value given.
        // Command syntax: data[]={writeId, cmdFlags, page, <numWritten:>numToWrite, {parameterIndex}+[data].
        // Returns number written along with parameter Index's. 
        byte MediatorWriteCommand(MessageTransaction mtx) {
            DeviceParameterTable pt = Exchange.ParameterTable;
            DeviceParameter d;
            PacketBuffer g = mtx.MsgBuffer;
            byte[] pAry;
            int dataIndex;
            VariableValue val = null;
            byte n,h,k,j,writeId,cmdFlags,pageNum,retArgsLen,maxSize=0;
            VariableKind parameterVarKind;
            StorageFlags parameterFlags;
            byte parameterSize, numToWrite;
            int paramIndexes, numWrittenIndex;
            n = g.dataLength();
            if(n<5) return 0;
            dataIndex = ProtocolConstants.GENERAL_DATA_ARRAY_OFFSET;
            pAry = g.buffer;
            writeId = pAry[dataIndex++];
            cmdFlags = pAry[dataIndex++];
            pageNum = pAry[dataIndex++];
            numWrittenIndex = dataIndex;
            numToWrite = (byte)(pAry[dataIndex++] & 0xf);
            n-=4;
            if(numToWrite>n) numToWrite=n;
            j = numToWrite;
            if (numToWrite > 8) numToWrite = 8;
            paramIndexes = dataIndex;
            retArgsLen = (byte)(dataIndex + numToWrite);
            g.dataLength((byte)(g.dataLength() - (4 + j))); //3=(wId, cmdFlags, page, nW:nTW), j= num original idx's.
            dataIndex += j;
            h=0; k=0;      
            while(k<numToWrite) {
                int paramIndex = pAry[paramIndexes+k];
                if(paramIndex>pt.GetLastEntryIndex()) break;
                d=pt.Find(paramIndex);
                if(d==null) d = DeviceParameterTable.NullParameter;
                parameterSize = (byte)d.ParamSize;
                if(parameterSize!=0) {
                    int valPtrIndex=0;
                    parameterVarKind = (VariableKind)d.ParamVarKind;
                    parameterFlags = (StorageFlags)d.ParamStorageFlags;
                    if(parameterVarKind==VariableKind.VK_String) { // Note: only one string allowed per message.
                        parameterSize=0; 
                        if(n>0) { // Strings must be at least 1 byte long: len byte.
                            byte lenAdj;
                            maxSize = (byte)d.MaxValue.ValueLong; 
                            parameterSize = pAry[dataIndex++];
                            lenAdj=parameterSize;
                            n--;
                            if(parameterSize>maxSize) parameterSize = maxSize;                            
                            if(n<parameterSize) {
                                parameterSize = n;
                                lenAdj=parameterSize;
                            }                        
                            valPtrIndex = dataIndex;    
                            n-=lenAdj;
                            dataIndex+=lenAdj; 
                        }                           
                    } else {                
                        if(n>=parameterSize) {
                            val = new VariableValue(parameterVarKind, pAry, dataIndex, (int)parameterSize); 
                            valPtrIndex = dataIndex;               
                            n-=parameterSize;
                            dataIndex+=parameterSize;
                        } else {
                            val = d.DefaultValue;
                            valPtrIndex = -1;
                        }
                        val.CheckInRange(d.MinValue, d.MaxValue);
                    } 
                    if(parameterSize>0) { 
                        if((parameterFlags&StorageFlags.SF_ReadOnly)==0) {                                     
                            if(((cmdFlags&ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_EEPROM)!=0)
                                &&((parameterFlags&StorageFlags.SF_EEPROM)!=0)) {
                                    if (parameterVarKind == VariableKind.VK_String) {
                                        if(valPtrIndex>=0) {
                                            d.EEPromValue = new VariableValue(parameterVarKind, pAry, valPtrIndex, (int)parameterSize);
                                        } else {
                                            d.EEPromValue = new VariableValue(parameterVarKind, pAry, 0, (int)0);
                                        }
                                    } else {
                                        d.EEPromValue = val;
                                    }                                                    
                            } 
                            if((cmdFlags&ProtocolConstants.MEDIATOR_DEVICE_RWV_FLAGS_RAM)!=0)  {
                                if (parameterVarKind == VariableKind.VK_String) {
                                    if (valPtrIndex >= 0) {
                                        d.RamValue = new VariableValue(parameterVarKind, pAry, valPtrIndex, (int)parameterSize);
                                    } else {
                                        d.RamValue = new VariableValue(parameterVarKind, pAry, 0, (int)0);
                                    }
                                } else {
                                    d.RamValue = val;
                                }                     
                            } 
                        }                
                    }     
                    ++h;           
                }                   
                ++k;
                ++paramIndex;
            }
            if (numWrittenIndex >= 0)
                pAry[numWrittenIndex] = h; // Reply to caller with number of parameters written. 
            g.dataLength(retArgsLen);        
            mtx.ChangeDir=true;
            mtx.Finish = ((g.flagsRS() & ProtocolConstants.MESSAGE_FLAGS_ACK) != 0) 
                ? MessageTransaction.FinishAction.Normal : MessageTransaction.FinishAction.Free;
            return h; 
        }

        void InitTasks() {
            for (int k = 0; k < IsoTaskSize; k++) {
                IsoTasks[k].ioIndex = -1;
            }
            NumTasks = 0;
            LastIsoUpdateTicks = Primitives.GetBabelMilliTicker();
        }

        void ResetTasks() {
            IsoTask t;
            for (int k = 0; k < IsoTaskSize; k++) {
                t = IsoTasks[k];
                if (t.ioIndex != -1) {	// Free message buffer.
                    Manager.IoBuffersFreeHeap.Release(t.ioIndex);
                    t.ioIndex = -1;
                }
            }
            NumTasks = 0;
            LastIsoUpdateTicks = Primitives.GetBabelMilliTicker();
        }

        protected void ResetLinkTasks() {
            IsoTask t;
            int j;
            byte r = 0;
            for (int k = 0; k < NumTasks; k++) {
                j = IsoTaskQueue[k];
                t = IsoTasks[j];
                if (t.ownerDriver == this) {
                    if (t.ioIndex != -1) {
                        Manager.IoBuffersFreeHeap.Release(t.ioIndex);
                        t.ioIndex = -1;
                    }
                    continue;
                }
                IsoTaskQueue[r++] = j;
            }
            NumTasks = r;
        }

        // Make a copy of message for task handler.
        // If pktLen!=0, sets idx and copy pktLen to that.
        // Returns new index if successful.
        int CopyMessage(int idx, byte pktLen) {
	        int ioIndex=Manager.IoBuffersFreeHeap.Allocate();
            if(ioIndex!=-1) {
                PacketBuffer a = Manager.IoBuffers[idx];
                PacketBuffer b = Manager.IoBuffers[ioIndex];
                if (pktLen == 0) {
                    pktLen = (byte)(ProtocolConstants.GENERAL_OVERHEADS_SIZE + a.dataLength());
                } else {
                    a.pktLen = pktLen;
                }
		        b.meta(a);
                Array.Copy(a.buffer, 0, b.buffer, 0, pktLen);
	        }
	        return ioIndex;
        }

        public override void ServiceTasks(uint curTicks, LinkTaskKind taskAction) {
	        IsoTask t,s;
	        int h,j,k,ioIndex;
            byte r;
            bool checkSchedule=false,checkRemove=false;
	        uint elapsedTicks;
            if (NumTasks == 0) return;
            if (taskAction == LinkTaskKind.Reset) {
                ResetLinkTasks();
                return;
            }
            elapsedTicks = curTicks - LastIsoUpdateTicks;
	        LastIsoUpdateTicks = curTicks;
	        for(k=0;k<NumTasks;k++) {
		        t = IsoTasks[IsoTaskQueue[k]];
		        if(elapsedTicks>=t.msTicksRelativeToGo) { // Perform this task.			
			        do {
                        bool isIsoMsg = (t.cmd==ProtocolConstants.MEDIATOR_DEVICE_ISOMSG);
                        if (isIsoMsg) {
                            ioIndex = CopyMessage(t.ioIndex, 0);
                            if (ioIndex == -1) break;
                        } else {
                            ioIndex = t.ioIndex;
                        }
                        MessageTransaction bmc = new MessageTransaction(Manager);
                        j = bmc.StartMessageTransaction(t.ioIndex);
			            if(j!=0) break;
                        bmc.TaskCmd = (byte)t.cmd;
                        j = t.ownerDriver.CommandHandler(bmc);
			            if(j!=0) break;
				        // Make a copy before its finalised.
                        if (!isIsoMsg) {
                            byte pktLen = (byte)(ProtocolConstants.GENERAL_OVERHEADS_SIZE + bmc.MsgBuffer.dataLength());
                            ioIndex = CopyMessage(t.ioIndex, pktLen);
                            if (ioIndex == -1) break;
                            t.ioIndex = ioIndex;
                            bmc.ReturnCmd = bmc.TaskCmd;
                        }
			            bmc.FinishMessageTransaction();
			        } while(false);
			        // Check if task to be removed.
			        if(t.repeatCount>0) {
				        if(--t.repeatCount==0){
					        // Free message buffer.
					        Manager.IoBuffersFreeHeap.Release(t.ioIndex);
					        t.ioIndex = -1;
					        // Mark task for later removal.
					        t.state = IsoRemove;
					        checkRemove=true;
				        }
			        }
                    if ((t.state & IsoRemove) == 0) {
				        // Mark task as requiring rescheduling.
                        t.state |= IsoReschedule;
				        checkSchedule=true;
			        }
			        // Update elapsedTicks:
			        elapsedTicks -= t.msTicksRelativeToGo;	
		        } else {
			        t.msTicksRelativeToGo-=elapsedTicks;
			        break;
		        }
	        }
	        if(checkRemove) {
		        r=0; 
		        for(k=0;k<NumTasks;k++) {
			        j = IsoTaskQueue[k];
			        if(IsoTasks[j].ioIndex!=-1) {
				        IsoTaskQueue[r++] = j;
			        }
		        }
		        NumTasks = r;
	        }	
	        if(checkSchedule) { // Reschedule tasks.
		        for(j=0;j<NumTasks;j++) { // Find first task that doesn't require rescheduling.
                    if ((IsoTasks[IsoTaskQueue[j]].state & IsoReschedule) == 0) break;
		        }
		        for(k=j-1;k>=0;k--) {			
			        t = IsoTasks[IsoTaskQueue[k]];
                    t.state=0;
                    t.msTicksRelativeToGo = t.msInterval - 1; // Note, minus one because 1 tick used during scheduling.
			        r = (byte)k;
			        do { // Ripple current task down Q until position found.
                        if ((r + 1) == NumTasks) { // Check next not past end.
					        break;
				        }
					    s = IsoTasks[IsoTaskQueue[r+1]];
                        if (t.msTicksRelativeToGo < s.msTicksRelativeToGo) { // '<' for fairness.
                            s.msTicksRelativeToGo -= t.msTicksRelativeToGo;
						    break;
					    } 
                        // Find location deeper in schedule.
						t.msTicksRelativeToGo-=s.msTicksRelativeToGo;
						// Swap over tasks in Q.
						h = IsoTaskQueue[r];
						IsoTaskQueue[r] = IsoTaskQueue[r+1];
						IsoTaskQueue[r+1] = h;									
						++r;
			        } while(r<NumTasks);
                    if (k == 0) break;
		        }	
	        }
        }

        // Setup an isochronous task.
        // Format: 
        // for ISO[MON]VAR: as per standard ReadCommand,
        // but CmdFlags preceded by: ISODetails={IsoId,IsoIntLo,IsoIntHi,RepeatLo,RepeatHi},
        // for ISOMSG: <cmd=ISOMSG> ISODetails {msgCmd,...},
        // where: IsoInt is the millisecond interval between reads, 0=remove,
        // Repeat is the number of times to send data, 0=forever.
        // Id for deleting task is SenderParams.
        int MediatorIsoCommand(MessageTransaction mtx) {
            PacketBuffer g = mtx.MsgBuffer;
            IsoTask t;
            byte[] pAry;
            int dataIndex;
	        int pIndex;
            uint msInterval,repeatCount;
            byte n,k,isoId;
            bool isIsoMsg;
            n = g.dataLength();
            if (n < 3) return 0;
            pAry = g.buffer;
            pIndex = dataIndex = ProtocolConstants.GENERAL_DATA_ARRAY_OFFSET;
            isoId = pAry[dataIndex++];
            msInterval = pAry[dataIndex++];
	        msInterval += (uint)(pAry[dataIndex++]<<8);
	        // First check if this is a cancel or update request.
	        // Find isoTask, if it exists, using isoId + senderParams as id.
            for (k = 0; k < NumTasks; k++) {
                do {
                    byte j;
                    t = IsoTasks[IsoTaskQueue[k]];
                    if (t.isoId == isoId
                        && t.addressParams.Sender == g.sender()
                        && t.addressParams.SenderId == g.senderId()
                        && t.addressParams.flagsRS== (g.flagsRS() & ProtocolConstants.MESSAGE_PORTS_MASK)) {
                        // Free message buffer.
                        Manager.IoBuffersFreeHeap.Release(t.ioIndex);
                        t.ioIndex = -1;
                        // Remove from task queue.
                        for (j = k; j < (NumTasks - 1); j++) {
                            IsoTaskQueue[j] = IsoTaskQueue[j + 1];
                        }
                        --NumTasks;
                        if (k < NumTasks) continue;
                    }
                    break;
                } while (true);
            }
            if (msInterval == 0) return 0;
            isIsoMsg = (g.command() == ProtocolConstants.MEDIATOR_DEVICE_ISOMSG);
            if (isIsoMsg) {
                if (n < 6) return 0; // Must be at least 5 iso parameters + 1 cmd byte.
            } else if(n<8) return 0; // Must be at least 5 iso parameter bytes + 3 read bytes.
	        // Set up a new isoTask.
	        // First check if a task slot is free.
	        if(NumTasks==IsoTaskSize) return 0;
	        t = null;
	        for(k=0;k<IsoTaskSize;k++) {
		        if(IsoTasks[k].ioIndex==-1) {
			        t = IsoTasks[k];
			        break;
		        }
	        }
	        if(t==null) return 0;	
	        // Second, get iso params.
            repeatCount = pAry[dataIndex++];
            repeatCount += (uint)(pAry[dataIndex++] << 8);
            t.isoId = isoId;
	        t.repeatCount = repeatCount;
	        t.msTicksRelativeToGo = 0; // Schedule for immediate execution because inserted at head of Q.
	        t.msInterval = msInterval;
	        t.ioIndex = mtx.IoIndex;
            t.state = 0;
            t.cmd = g.command(); // Original msg iso cmd.
            t.ownerDriver = (isIsoMsg ? Manager.GetLinkDriver(ProtocolConstants.NETIF_MEDIATOR_PORT) : this); // For IsoMsg, needs to be MediatorNetIf.
            t.addressParams.SenderId = g.senderId();
            t.addressParams.Sender = g.sender();
            t.addressParams.flagsRS = (byte)(g.flagsRS() & ProtocolConstants.MESSAGE_PORTS_MASK);
	        // Now convert iso to a regular read message.
            // For ISOMSG cmd, g->command needs to be pulled from next dataAry byte.
            if (isIsoMsg) {
                g.command(pAry[dataIndex++]);
                --n;
            } else {
                g.command(ProtocolConstants.MEDIATOR_DEVICE_READVAR);
            }
            n -= 5;
            g.dataLength(n);
            while (n-- > 0) pAry[pIndex++] = pAry[dataIndex++];
	        mtx.Finish=MessageTransaction.FinishAction.Keep; // Keep iobuffer.
	        // Finally insert task at head of queue.
	        for(n=(byte)NumTasks;n>0;n--) {
		        IsoTaskQueue[n] = IsoTaskQueue[n-1];
	        }
	        IsoTaskQueue[0]=k;
	        ++NumTasks;
	        return 1;
        }

        // Process Port commands.
        // Returns zero on success.
        public override byte CommandHandler(MessageTransaction mtx) {
            PacketBuffer g = mtx.MsgBuffer;
            Boolean isReply = ((g.flagsRS()&ProtocolConstants.MESSAGE_FLAGS_IS_REPLY)!=0);
            if (((!isReply) && (g.command() < ProtocolConstants.MEDIATOR_CONTROL_CMD_BASE))
                || (isReply && (g.senderId() == ProtocolConstants.IDENT_MEDIATOR))) {
                byte dataIndex = ProtocolConstants.GENERAL_DATA_ARRAY_OFFSET;
                byte[] tmp;
                switch (g.command()) {
                    // Standard device commands.
                    case ProtocolConstants.MEDIATOR_DEVICE_RESET: // Reset the module.
                        Manager.AppBabelDeviceReset();
                        break;
                    case ProtocolConstants.MEDIATOR_DEVICE_STATUS: // Get device status.
                        if (isReply) return 1;
                        g.dataLength(Diagnostics.GetDeviceStatus(Manager, g.buffer));
                        mtx.ChangeDir = true;
                        mtx.Finish = MessageTransaction.FinishAction.Normal;
                        break;
                    case ProtocolConstants.MEDIATOR_DEVICE_TICKER: // Get device ticker count.
                        if (isReply) return 1; // We've been sent a ticker count.
                        // Sender wants to know ticker count.
                        tmp = Primitives.UIntToByteArray(Primitives.GetBabelMilliTicker());
                        return mtx.StoreMessageValue(tmp, (byte)(tmp.Length));
                    case ProtocolConstants.MEDIATOR_DEVICE_ERASE: // TODO: erase eeprom and restart.
                        return 2;
                    case ProtocolConstants.MEDIATOR_DEVICE_READVAR:
                        return (MediatorReadCommand(mtx) == 0) ? (byte)1 : (byte)0;
                    case ProtocolConstants.MEDIATOR_DEVICE_WRITEVAR:
                        return (MediatorWriteCommand(mtx) == 0) ? (byte)1 : (byte)0;
                    case ProtocolConstants.MEDIATOR_DEVICE_ISOVAR:
                        return (MediatorIsoCommand(mtx) == 0) ? (byte)1 : (byte)0;
                    case ProtocolConstants.MEDIATOR_DEVICE_ISOMONVAR:
                        return (MediatorIsoCommand(mtx) == 0) ? (byte)1 : (byte)0;
                    case ProtocolConstants.MEDIATOR_DEVICE_ISOMSG:
                        return (MediatorIsoCommand(mtx) == 0) ? (byte)1 : (byte)0;
                    case ProtocolConstants.MEDIATOR_DEVICE_LOG:
                        if (!isReply) {
                            if (Settings.LogOn) {
                                uint ticks = (uint)Primitives.GetArrayValueU(g.buffer, dataIndex, 4);
                                string s;
                                byte logType = g.buffer[dataIndex + 4];
                                if (Settings.LogBinary || logType == 'B') {
                                    int start = dataIndex + 5;
                                    int last = dataIndex + g.dataLength();
                                    s = "(";
                                    for (int k = start; k < last; k++) {
                                        s = s + g.buffer[k].ToString("X2") + " ";
                                    }
                                    s += ")";
                                } else {
                                    s = Primitives.GetArrayStringValue(g.buffer, dataIndex + 5, g.dataLength() - 5);
                                }
                                Log.d("MediatorNetIf:" + Exchange.Id, ":" + ticks + ":" + s);
                            }
                        }
                        return 1;
                    // Connection specific commands.
                    case ProtocolConstants.MEDIATOR_DEVICE_GETSN: // Get SN of receiver device at end of link.
                        if (isReply) { // We've been sent an SN for SPort.
                            g.buffer[dataIndex + g.dataLength()] = 0;
                            Manager.SerialNumberManager.UpdateSerialNumbers(g.buffer, dataIndex, g.dataLength(), g.iNetIf, g.sender());
                            if (mtx.MsgBuffer.iNetIf != ProtocolConstants.NETIF_USER_BASE) { // Notify master of SN via connection attach command.
                                SendLinkMediatorCommand(this, true, ProtocolConstants.MEDIATOR_CONNECT_ATTACH, g.dataLength(), g.buffer, dataIndex);
                            }
                            break;
                        }
                        // Sender wants to know SN of master.
                        g.dataLength(Manager.SerialNumberManager.CopyMasterSerialNumber(g.buffer, dataIndex, g.iNetIf));
                        mtx.ChangeDir = true;
                        mtx.Finish = MessageTransaction.FinishAction.Normal;
                        break;
                    case ProtocolConstants.MEDIATOR_CONNECT_ATTACH: // TODO: Report attach and SN of device.
                    case ProtocolConstants.MEDIATOR_CONNECT_DETACH: // TODO: Report detach of device.
                    case ProtocolConstants.MEDIATOR_CONNECT_GATEWAY: // TODO: Register as a gateway.
                        return 10;
                    default:
                        // TODO: try handling via NETIF_MEDIATOR_PORT.            
                        return 11;
                }
                return 0;
            } 
            return BabelMessage.ProcessIncomingRawMesssage(Exchange, mtx);
        }

        /// <summary>
        /// Handle a message sent to virtual port.
        /// This is a helper wrapper for CommandHandler.
        /// </summary>
        /// <param name="ioIndex"></param>
        /// <returns>Returns 0 if dealt with to completion and freed.
        /// Otherwise, returns error code, caller must free buffer.
        /// </returns>
        public override byte MessageCommandHandler(int ioIndex) {
            MessageTransaction bmc = new MessageTransaction(Manager);
            byte err = bmc.StartMessageTransaction(ioIndex);
            if (err != 0) return err;
            err = CommandHandler(bmc);
            if (err != 0) return err;
            bmc.FinishMessageTransaction();
            return 0;
        }
    }
}