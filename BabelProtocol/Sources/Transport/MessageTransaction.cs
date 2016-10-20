using System;
using System.Collections;

using Babel.Core;
using Babel.XLink;

namespace Babel.BabelProtocol {

    public class MessageTransaction {

        public enum FinishAction {
            Normal, // Carry out normal transaction completion on finishing.
            Free, // Just free buffer on finishing.
            Keep // Just keep buffer on finishing.
        }

        public NetIfManager Manager;
        public PacketBuffer MsgBuffer;
        public byte ONetIfIndex; 
        public int IoIndex;
        public FinishAction Finish;
        public Router.RouterAction Dispatch;
        public byte TaskCmd, ReturnCmd;
        public bool ChangeDir;

        public MessageTransaction(NetIfManager manager) {
            Manager = manager;
        }

        public byte[] CopyMessageData() {
            byte len = MsgBuffer.dataLength();
            byte[] ary = new byte[len];
            byte[] q = MsgBuffer.buffer;
            byte k, qIndex = PacketBuffer.dataArrayIndex;
            for (k = 0; k < len; k++) {
                ary[k] = q[qIndex++];
            }
            return ary;
        }

        // Helper routine for accessing messages.
        // ioIndex must point to a valid message, i.e. meta is set.
        // Returns 0 on success.
        // Returns error code on parse failure.
        public byte StartMessageTransaction(int ioIndex) {
            MsgBuffer = Manager.IoBuffers[ioIndex];
            IoIndex = ioIndex;
            ChangeDir = false;
            Finish = FinishAction.Free; // Free messages by default: prevents unhandled recursions.
            Dispatch = Router.RouterAction.PostToNetIf;
            TaskCmd = 0;
            ReturnCmd = 0;
            ONetIfIndex = MsgBuffer.iNetIf;
            switch (MsgBuffer.flagsPid&ProtocolConstants.META_FLAGS_PID) {
                case ProtocolConstants.PID_GENERAL_V:
                     MsgBuffer.flagsPid&=ProtocolConstants.META_FLAGS_PID; // Clear out any resend value.
                     break;
                case ProtocolConstants.PID_GENERAL:
                    break;
                default:
                    return 100;
            }
            return 0;
        }

        // Save value in message.
        // Returns 0 on success.
        public byte StoreMessageValue(byte[] pValue, byte len) {
            if (len <= (Manager.MaxPacketSize - ProtocolConstants.GENERAL_OVERHEADS_SIZE)) {
                byte pIndex = 0, dataIndex=PacketBuffer.dataArrayIndex;
                MsgBuffer.dataLength(len);
                while (len-- > 0)
                    MsgBuffer.buffer[dataIndex++] = pValue[pIndex++];
                ChangeDir = true;
                Finish = FinishAction.Normal;
                return 0;
            }
            return 1;
        }

        // Append value to message data area.
        // Returns 0 on success.
        public byte AppendMessageValue(byte[] pValue, byte len) {
            byte dataLen = MsgBuffer.dataLength();
            if ((dataLen + len) <= (Manager.MaxPacketSize - ProtocolConstants.GENERAL_OVERHEADS_SIZE)) {
                byte pIndex = 0, dataIndex = (byte)(PacketBuffer.dataArrayIndex + dataLen);
                MsgBuffer.dataLength((byte)(dataLen+len));
                while (len-- > 0)
                    MsgBuffer.buffer[dataIndex++] = pValue[pIndex++];
                return 0;
            }
            return 1;
        }

        // Helper routine for finalizing message state ready for sending.
        // If justFree, simply frees message buffer.
        // If dispatchAction=BabelConstants.SEND_TO_ROUTER, sends message to router.
        // If dispatchAction=BabelConstants.POST_TO_WRITE, pushes message onto oPort's Write Q.
        public void FinishMessageTransaction() {
            if (Finish == FinishAction.Free) {
                Manager.IoBuffersFreeHeap.Release(IoIndex);
            } else if (Finish == FinishAction.Keep) {
            } else {
                if (ReturnCmd != 0) {
                    MsgBuffer.command(ReturnCmd);
                }
                if (ChangeDir) {
                    // Change general message direction: 
                    // r<>s, RNetIf<>SNetIf, rPort<>sPort,
                    // Invert Is Reply flag, clear ACK flag
                    byte ports,flags = MsgBuffer.flagsRS();
                    ushort sender = MsgBuffer.sender();
                    MsgBuffer.sender(MsgBuffer.receiver());
                    MsgBuffer.receiver(sender);
                    flags ^= ProtocolConstants.MESSAGE_FLAGS_IS_REPLY;
                    flags &= Primitives.ByteNeg(ProtocolConstants.MESSAGE_FLAGS_ACK);
                    // Now swap ports.
                    ports = (byte)(flags & ProtocolConstants.MESSAGE_PORTS_MASK);
                    ports = (byte)((ports << 2) | (ports >> 2));
                    MsgBuffer.flagsRS((byte)((ports & ProtocolConstants.MESSAGE_PORTS_MASK) | (flags & ProtocolConstants.MESSAGE_FLAGS_MASK)));
                } else { 
                    // Caller might be relaying message to another adrs:netIf.
                    // So leave flagsRS alone.
                }
                MsgBuffer.pktLen = (byte)(ProtocolConstants.GENERAL_OVERHEADS_SIZE + MsgBuffer.dataLength());
                MsgBuffer.UpdateCheckSum(MsgBuffer.pktLen);
                if (Dispatch == Router.RouterAction.SendToRouter) {
                    Router.RouteMessage(Manager, IoIndex);
                } else if (Dispatch == Router.RouterAction.PostToNetIf) {
                    Router.PostMessage(Manager, IoIndex, ONetIfIndex);
                }
            }
        }
    }
}