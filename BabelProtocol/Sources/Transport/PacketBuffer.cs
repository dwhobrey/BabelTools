using System;

using Babel.XLink;

namespace Babel.BabelProtocol {

    public class PacketBuffer {

        public const byte syncIndex = 0;
        public const byte negPidPidIndex = 1;
        public const byte destinationIndex = 2; // Allow 2 bytes.
        public const byte argIndex = 4;
        public const byte receiverIndex = 5; // Allow 2 bytes.
        public const byte senderIndex = 7; // Allow 2 bytes.
        public const byte senderIdIndex = 9;
        public const byte rsFlagsIndex = 10;
        public const byte commandIndex = 11;
        public const byte dataLengthIndex = 12;
        public const byte dataArrayIndex = 13;
        public const byte orderLoIndex = 14;
        public const byte orderHiIndex = 15;

        public byte flagsPid;
        public byte pktLen;
        public byte iNetIf; // Packet was received on this input port.
        public byte dNetIf; // Packet was routed to this destination port.
        public byte[] buffer;

        public void meta(PacketBuffer b) {
            flagsPid = b.flagsPid;
            pktLen = b.pktLen;
            iNetIf = b.iNetIf;
            dNetIf = b.dNetIf;
        }

        // Header
        public byte sync() { return buffer[syncIndex]; }
        public void sync(byte v) { buffer[syncIndex] = v; }
        public byte negPidPid() { return buffer[negPidPidIndex]; }
        public void negPidPid(byte v) { buffer[negPidPidIndex] = v; }
        public ushort destination() { return (ushort)(buffer[destinationIndex] + (buffer[destinationIndex + 1] << 8)); }
        public void destination(ushort v) { buffer[destinationIndex] = (byte)(v & 0xff); buffer[destinationIndex + 1] = (byte)((v >> 8) & 0xff); }
        public byte arg() { return buffer[argIndex]; }
        public void arg(byte v) { buffer[argIndex] = v; }
        // Receiver
        public ushort receiver() { return (ushort)(buffer[receiverIndex]+ (buffer[receiverIndex + 1] << 8)); }
        public void receiver(ushort v) { buffer[receiverIndex]=(byte)(v&0xff);buffer[receiverIndex+1]=(byte)((v>>8)&0xff);}
        public byte flagsRS() { return buffer[rsFlagsIndex]; }
        public void flagsRS(byte v) { buffer[rsFlagsIndex] = v; }
        // Sender
        public ushort sender() { return (ushort)(buffer[senderIndex] + (buffer[senderIndex + 1] << 8)); }
        public void sender(ushort v) { buffer[senderIndex]=(byte)(v&0xff);buffer[senderIndex+1]=(byte)((v>>8)&0xff); }
        public byte senderId() { return buffer[senderIdIndex]; }
        public void senderId(byte v) { buffer[senderIdIndex] = v; }
        // Cmd
        public byte command() { return buffer[commandIndex]; }
        public void command(byte v) { buffer[commandIndex] = v; }
        // Data
        public byte dataLength() { return buffer[dataLengthIndex]; }
        public void dataLength(byte v) { buffer[dataLengthIndex] = v; }
        public byte dataAry(byte idx) { return buffer[dataArrayIndex + idx]; }
        public void dataAry(byte idx, byte v) { buffer[dataArrayIndex + idx] = v; }
        // Order
        public byte orderLo() { return buffer[orderLoIndex]; }
        public void orderLo(byte v) { buffer[orderLoIndex] = v; }
        public byte orderHi() { return buffer[orderHiIndex]; }
        public void orderHi(byte v) { buffer[orderHiIndex] = v; }

        public PacketBuffer(byte size) {
            buffer = new byte[size];
        }

        public override string ToString() {
            return Diagnostics.DumpMessage(this);
        }

        // On entry, packetLength = total length including space for chksum.
        public void UpdateCheckSum(byte packetLength) {
            int chksum = 0;
            byte k = ProtocolConstants.CHECK_START_OFFSET;
            packetLength -= ProtocolConstants.CHECK_START_OFFSET;
            while (packetLength-- > 1) {
                chksum += buffer[k++];
            }
            buffer[k] = (byte)(256 - (chksum % 256));
        }
    }
}