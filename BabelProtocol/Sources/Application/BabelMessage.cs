using System;

using Babel.Core;
using Babel.XLink;

namespace Babel.BabelProtocol {

    /// <summary>
    /// Wraps an underlying message buffer in a friendly object.
    /// </summary>
    public class BabelMessage : IConvertible {

        public enum MessageError {
            None,
            BadNetIf
        }

        public static String TAG = "BabelMessage";

        public bool IsHandshake;
        public bool IsGeneral;
        public bool Verified;
        public MessageError LastError;
        public byte PostNetIfIndex;
        public byte Pid;
        public byte Arg;
        public byte LinkId;
        public ushort Receiver;
        public ushort Sender;
        public byte SenderId;
        public byte FlagsRS;
        public byte Cmd;
        public byte DataLen;
        public byte[] DataAry;
        public byte CurrentBlock;
        public byte OutgoingNetIfIndex;
        public byte IncomingNetIfIndex;
        public Router.RouterAction Dispatch;

        public MessageExchange Exchange;

        private BabelMessage(MessageExchange exchange) {
            Exchange = exchange;
            LastError = MessageError.None;
            CurrentBlock = 0;
        }

        public String HeaderToString() {
            return String.Format("(pid={0:x2},rsf={1:x2},r={2:x2},s={3:x2},sid={4:x2})",
                Pid, FlagsRS, Receiver, Sender, SenderId);
        }

        override public String ToString() {
            int k;
            String s = "{" + HeaderToString() + ",";
            for (k = 0; k < DataLen; k++) {
                s += String.Format("{0:x2} ", DataAry[k]);
            }
            s += "}";
            return s;
        }

        public String ReadVarMessageToString() {
            int k, cmdFlags, numRead, numToRead;
            String s = "{" + HeaderToString() + ",";
            cmdFlags = (DataAry[0] & 0xff);
            numToRead = (DataAry[1] & 0x0f);
            numRead = (DataAry[2] & 0xf0) >> 4;
            s += String.Format("(cf={0:x2},n2r={1:x2},nr={2:x2}),<", cmdFlags, numToRead, numRead);
            for (k = 3; k < DataLen; k++) {
                if (k == numToRead) s += ">";
                s += String.Format("{0:x2} ", DataAry[k]);
            }
            s += "}";
            return s;
        }

        /// <summary>
        /// Convert raw message to BabelMessage.
        /// Called by a port with an incoming raw message.
        /// Must return as soon as possible, so convert and put on incoming Q for later relay.
        /// Caller frees the underlying raw message.
        /// TODO: Process long message if necessary: start a long list.
        /// </summary>
        /// <param name="exchange"></param>
        /// <param name="mtx"></param>
        /// <returns></returns>
        public static byte ProcessIncomingRawMesssage(MessageExchange exchange, MessageTransaction mtx) {
            PacketBuffer g = mtx.MsgBuffer;
            BabelMessage m = new BabelMessage(exchange);
            m.Pid = (byte)(g.flagsPid&ProtocolConstants.PID_MASK);
            m.Verified = (m.Pid == ProtocolConstants.PID_GENERAL_V);
            m.Receiver = ProtocolConstants.ADRS_LOCAL;
            m.FlagsRS = g.flagsRS();
            m.IncomingNetIfIndex = mtx.ONetIfIndex;
            m.OutgoingNetIfIndex = ProtocolConstants.NETIF_UNSET;
            m.Cmd = 0;
            switch (m.Pid) {
                case ProtocolConstants.PID_GENERAL:
                case ProtocolConstants.PID_GENERAL_V:
                    m.IsGeneral = true;
                    m.IsHandshake = false;
                    m.Receiver = g.receiver();
                    m.Cmd = g.command();
                    m.FlagsRS = g.flagsRS();
                    break;
                default: 
                    return 1;
            }
            m.Sender = g.sender();
            m.SenderId = g.senderId();
            m.DataLen = g.dataLength();
            m.DataAry = mtx.CopyMessageData();
            // Now submit message to be relayed later.
            exchange.SubmitIncomingMessage(m);
            return 0;
        }

        public static BabelMessage CreateHandshakeMessage(MessageExchange exchange,
                Router.RouterAction dispatch, byte postNetIfIndex, byte pid, byte arg) {
            BabelMessage m = new BabelMessage(exchange);
            m.IsHandshake = true;
            m.IsGeneral = false;
            m.Pid = pid;
            m.Arg = arg;
            m.PostNetIfIndex = postNetIfIndex;
            m.Dispatch = dispatch;
            m.IncomingNetIfIndex = ProtocolConstants.NETIF_UNSET;
            m.OutgoingNetIfIndex = ProtocolConstants.NETIF_UNSET;
            return m;
        }

        public static BabelMessage CreateCommandMessage(MessageExchange exchange, bool verified,
                Router.RouterAction dispatch, byte postNetIfIndex, byte cmd, ushort receiver, 
                ushort sender, byte flagsRS, byte ident, byte dataLen, byte dataOffset, byte[] dataAry) {
            BabelMessage m;
            if (dataLen >= ProtocolConstants.GENERAL_TAIL_SIZE) 
                return null; // Longer messages not implemented yet.
            m = new BabelMessage(exchange);
            m.IsHandshake = false;
            m.IsGeneral = true;
            m.Verified = verified;
            m.PostNetIfIndex = postNetIfIndex;
            m.Dispatch = dispatch;
            m.IncomingNetIfIndex = ProtocolConstants.NETIF_UNSET;
            m.OutgoingNetIfIndex = ProtocolConstants.NETIF_UNSET;
            m.Cmd = cmd;
            m.Receiver = receiver;
            m.Sender = sender;
            m.FlagsRS = flagsRS;
            m.SenderId = ident;
            m.DataLen = dataLen;
            if(dataOffset<=0)
                m.DataAry = dataAry;
            else {
                byte[] ary = new byte[dataAry.Length + dataOffset];
                Array.Copy(dataAry, 0, ary, dataOffset, dataAry.Length);
                m.DataAry = ary;
            }
            return m;
        }

        // Generates segments for long messages as necessary.
        // TODO: segmenting.
        private bool HasRemainingTransfers() {
            int idx;
            if (CurrentBlock == 0) {
            }
            idx = Exchange.Manager.Factory.CreateGeneralMessage(Verified, Cmd, Receiver, Sender, FlagsRS, SenderId, DataLen, DataAry, 0);
            if (idx != -1) {
                if (Dispatch == Router.RouterAction.PostToNetIf)
                    Router.PostMessage(Exchange.Manager, idx, OutgoingNetIfIndex);
                else
                    Router.RouteMessage(Exchange.Manager, idx);
            }
            return false;
        }

        // Returns true if message sent successfully.
        // Returns false if low level write Q count above water mark.
        // If connection dies during a long xfer, abort & return false.
        public bool DispatchMessage(MessageBinder binder) {
            if (PostNetIfIndex == ProtocolConstants.NETIF_UNSET) {
                OutgoingNetIfIndex = ProtocolConstants.NETIF_USER_BASE;
            } else {
                OutgoingNetIfIndex = PostNetIfIndex;
            }
            LinkDriver p = Exchange.Manager.GetLinkDriver(OutgoingNetIfIndex);
            if (p == null) {
                LastError = MessageError.BadNetIf;
                return false;
            }
            if (p.GetWriteQueueSize() > p.LinkWriteQWaterMark) 
                return false;
            if (p.GetComponentState() != ComponentState.Working) 
                return false;

            if (binder.Message.SenderId != 0) {
                binder.Exchange.SubmitWaiter(binder);
            }
            while (HasRemainingTransfers()) ;
            return true;
        }

        public TypeCode GetTypeCode() {
            return TypeCode.Object;
        }

        bool IConvertible.ToBoolean(IFormatProvider provider) {
                return true;
        }

        byte IConvertible.ToByte(IFormatProvider provider) {
            return SenderId;
        }

        char IConvertible.ToChar(IFormatProvider provider) {
            return Convert.ToChar(SenderId);
        }

        DateTime IConvertible.ToDateTime(IFormatProvider provider) {
            return Convert.ToDateTime(0.0);
        }

        decimal IConvertible.ToDecimal(IFormatProvider provider) {
            return Convert.ToDecimal(SenderId);
        }

        double IConvertible.ToDouble(IFormatProvider provider) {
            return SenderId;
        }

        short IConvertible.ToInt16(IFormatProvider provider) {
            return SenderId;
        }

        int IConvertible.ToInt32(IFormatProvider provider) {
            return SenderId;
        }

        long IConvertible.ToInt64(IFormatProvider provider) {
            return SenderId;
        }

        sbyte IConvertible.ToSByte(IFormatProvider provider) {
            return Convert.ToSByte(SenderId);
        }

        float IConvertible.ToSingle(IFormatProvider provider) {
            return Convert.ToSingle(SenderId);
        }

        string IConvertible.ToString(IFormatProvider provider) {
            return ToString();
        }

        object IConvertible.ToType(Type conversionType, IFormatProvider provider) {
            return Convert.ChangeType(SenderId, conversionType);
        }

        ushort IConvertible.ToUInt16(IFormatProvider provider) {
            return Convert.ToUInt16(SenderId);
        }

        uint IConvertible.ToUInt32(IFormatProvider provider) {
            return Convert.ToUInt32(SenderId);
        }

        ulong IConvertible.ToUInt64(IFormatProvider provider) {
            return Convert.ToUInt64(SenderId);
        }

    }
}