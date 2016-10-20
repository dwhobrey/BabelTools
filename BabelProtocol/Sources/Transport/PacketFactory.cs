using System;
using System.Collections;

using Babel.Core;
using Babel.XLink;

namespace Babel.BabelProtocol {

    public class PacketFactory {

        public NetIfManager Manager;

        public PacketFactory(NetIfManager manager) {
            Manager = manager;
        }

        // Create a command general message.
        public int CreateGeneralMessage(bool verified, byte cmd, 
                ushort receiver, ushort sender, byte flagsRS, byte senderId, 
                byte dataLen, byte[] pData, byte bufferIndex) {
            if (dataLen > (Manager.MaxPacketSize - ProtocolConstants.GENERAL_OVERHEADS_SIZE))
                return -1;
            int idx = Manager.IoBuffersFreeHeap.Allocate();
            if (idx != -1) {
                PacketBuffer b = Manager.IoBuffers[idx];
                byte pid = verified ? ProtocolConstants.PID_GENERAL_V : ProtocolConstants.PID_GENERAL;
                b.flagsPid = pid;
                b.dNetIf = ProtocolConstants.NETIF_UNSET;
                b.iNetIf = ProtocolConstants.NETIF_UNSET;
                b.pktLen = (byte)(ProtocolConstants.GENERAL_OVERHEADS_SIZE + dataLen);
                b.sync(ProtocolConstants.BYTE_SYNC);
                b.negPidPid(Primitives.NibbleToPid(pid));
                b.destination(LinkMonitor.Destination); // TODO: set destination.
                b.arg(0);
                b.receiver(receiver);
                b.sender(sender);
                b.senderId(senderId);
                b.flagsRS(flagsRS);
                b.command(cmd);
                b.dataLength(dataLen);
                byte dataIndex = ProtocolConstants.GENERAL_DATA_ARRAY_OFFSET;
                byte[] p = b.buffer;
                for (byte k = 0; k < dataLen; k++)
                    p[dataIndex++] = pData[bufferIndex + k];
                b.UpdateCheckSum(b.pktLen);
            }
            return idx;
        }   
    }
}
