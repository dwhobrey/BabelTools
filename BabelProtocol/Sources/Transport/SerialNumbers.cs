using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Babel.Core;
using Babel.XLink;

namespace Babel.BabelProtocol {
   
    public class SerialNumber {
        public byte Len;
        public byte[] Text;

        public SerialNumber() {
            Len = 0;
            Text = new byte[ProtocolConstants.SERIAL_NUM_ASCII_SIZE + 2];
        }
    }

    public class SerialNumbers {

        public uint MasterProduceCode;
        public byte[] MasterSN;
        public string MasterSNString;
        public TokenAllocator SerialNumbersFreeHeap;

        public int SerialNumbersSize = 8;
        public SerialNumber[] IoSerialNumbers;

        public NetIfManager Manager;

        public SerialNumbers(NetIfManager manager, string masterSN) {
            int k;
            Manager = manager;
            if (!String.IsNullOrWhiteSpace(masterSN)) {
                MasterSNString = masterSN;
                if(!MasterSNString.EndsWith("\0")) MasterSNString+="\0";
            } else {
                int n = Convert.ToInt32(Manager.ShellId);
                MasterSNString = Settings.SerialNumberFormatter(Lima.GetComputerCode(), (char)(Primitives.NibbleToHexChar((byte)n)),'0');
            }
            MasterSN = Primitives.StringToByteArray(MasterSNString);
            UpdateProductCode(MasterSN);
            SerialNumbersFreeHeap = new TokenAllocator(SerialNumbersSize);
            IoSerialNumbers = new SerialNumber[SerialNumbersSize];
            for (k = 0; k < SerialNumbersSize; k++) IoSerialNumbers[k] = new SerialNumber(); 
        }

        public void CopyToSerialNumber(int idx, byte len, byte[] p, byte bufferIndex) {
            if (idx < SerialNumbersSize) {
                Array.Copy(p, bufferIndex, IoSerialNumbers[idx].Text, 0, len + 1);
                IoSerialNumbers[idx].Len = len;
            }
        }

        // IOPort set up serial number for device at other end of link.
        // For the master port, set up temp SN and append netIfIndex.
        // For all other ports, set SN idx to null. It will be set when device connects.
        public void NetIfSerialNumberSetup(byte netIfIndex) {
            LinkDriver p = Manager.GetLinkDriver(netIfIndex);
            if (p != null) {
                if (netIfIndex == ProtocolConstants.NETIF_USER_BASE) {
                    byte len;
                    int idx = SerialNumbersFreeHeap.Allocate();
                    if (idx != -1) {
                        len = Primitives.Strlen(MasterSN);
                        CopyToSerialNumber(idx, len, MasterSN, 0);
                        IoSerialNumbers[idx].Text[len - 1] = Primitives.NibbleToHexChar(netIfIndex);
                    }
                    p.SerialIndex = idx;
                } else {
                    p.SerialIndex = -1;
                }
            }
        }

        // Checks the port table to see if it includes serialNumber.
        // Returns the port or null if not found.
        public LinkDriver FindNetIfSerialNumber(byte[] s) {
            if (s == null) return null;
            foreach (LinkDriver p in Manager.IoNetIfs.Values) {
                int idx = p.SerialIndex;
                if ((idx != -1) && (Primitives.Strcmp(IoSerialNumbers[idx].Text, s) == 0)) return p;
            }
            return null;
        }

        // Copy master SN to buffer.
        // Modifies end of SN to be netIfIndex.
        // Returns len.
        public byte CopyMasterSerialNumber(byte[] buffer, byte bufferIndex, byte netIfIndex) {
            int idx; byte len = 0; byte[] s;
            LinkDriver p = Manager.GetLinkDriver(ProtocolConstants.NETIF_USER_BASE);
            if (p != null) {
                idx = p.SerialIndex; // Get the master's SN.
                if ((idx == -1) || idx >= SerialNumbersSize) {
                    s = MasterSN;
                } else {
                    s = IoSerialNumbers[idx].Text;
                }
                len = Primitives.Strlen(s);
                if (len == 0) { // Should never happen.
                    s = Primitives.StringToByteArray("XXX-0000-0\0");
                    len = Primitives.Strlen(s);
                }
                Array.Copy(s, 0, buffer, bufferIndex, len);
                // Modify end of SN to be netIfIndex.
                buffer[len - 1] = Primitives.NibbleToHexChar(netIfIndex);
            }
            return len;
        }

        void UpdateProductCode(byte[] pSN) {
	        int len = Primitives.Strlen(pSN);
	        if(len>5) {
		        int k = 4;
		        uint n = 0, m;
		        while(k<len) {
			        byte d = pSN[k++];
                    if (d >= '0' && d <= '9') m = (uint)(d - '0');
                    else if (d >= 'A' && d <= 'F') m = (uint)(d - 'A' + 10);
                    else break;
			        n=(uint)(16*n)+m;
		        }
		        if(n>0) MasterProduceCode=n;
	        }
        }

        // Update the serial number for device at other end of netIf.
        public void UpdateSerialNumbers(byte[] buffer, byte bufferIndex, byte len, byte netIfIndex, ushort senderAdrs) {
            int idx;
            if ((netIfIndex == ProtocolConstants.NETIF_USER_BASE) && (senderAdrs == ProtocolConstants.ADRS_LOCAL)) {
                UpdateProductCode(buffer);
                // This is the master's SN, so save it.
                Manager.AppBabelSaveSerialNumber(buffer, bufferIndex);
            }
            LinkDriver p = Manager.GetLinkDriver(netIfIndex);
            if (p != null) {
                idx = p.SerialIndex;
                if (idx == -1) {
                    idx = SerialNumbersFreeHeap.Allocate();
                }
                CopyToSerialNumber(idx, len, buffer, bufferIndex);
            }
        }
    }
}