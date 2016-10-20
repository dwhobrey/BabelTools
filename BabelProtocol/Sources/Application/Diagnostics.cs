using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Babel.Core;
using Babel.XLink;

namespace Babel.BabelProtocol {

    public class Diagnostics {

        public static byte LogNetIfIndex = ProtocolConstants.NETIF_USER_BASE;
        public static int NumFreeIOBuffers = 0;
        public static int NumInUseIOBuffers = 0;
        public static short DeviceCheckCount = 0;

        // Used for diagnostics.
        public static void DeviceCheck(NetIfManager manager) {
            NumInUseIOBuffers = 0;
            NumFreeIOBuffers = manager.IoBuffersFreeHeap.RemainingCapacity();
            foreach (LinkDriver p in manager.IoNetIfs.Values) {
                NumInUseIOBuffers += p.LinkWriteQueue.Size();
                NumInUseIOBuffers += p.LinkVerifyQueue.Size();
            }
            int diff = manager.IoBuffersFreeHeap.Capacity() - NumFreeIOBuffers - NumInUseIOBuffers;
            if (diff > 4) { // Allow for pipe being 2 bigger than num buffers + 1 task.
                ++DeviceCheckCount;
            }
        }

        public static byte GetDeviceStatus(NetIfManager manager, byte[] buffer) {
            DeviceCheck(manager);
            String s = String.Format("F={0:x4},U={1:x4},S={2:x4}.",
                NumFreeIOBuffers, NumInUseIOBuffers, manager.IoBuffersFreeHeap.Capacity());
            byte[] tmp = Primitives.StringToByteArray(s);
            int len = tmp.Length;
            Array.Copy(tmp, 0, buffer, ProtocolConstants.GENERAL_DATA_ARRAY_OFFSET, len);
            return (byte)len;
        }

        /*
        Check indices of page tables are consistent.
        Returns 0 if ok, else (page,index) of error.
        */
        public static short CheckParameterTable() {
	        // Not implemented.
	        return 0;
        }

        public static string DumpMessage(PacketBuffer b) {
            byte k;
            String s = "{" + String.Format("flagsPid={0:x2},pktLen={1:x2},iP={2:x2},dP={3:x2},", b.flagsPid, b.pktLen, b.iNetIf, b.dNetIf);
            for (k = 0; k < b.pktLen; k++) {
                s += String.Format("{0:x2} ", b.buffer[k]);
            }
            s += "}\n";
            return s;
        }

        public static string DumpHandshake(PacketBuffer b) {
            return "{" + String.Format("pid={0:x2},arg={1:x2}", b.negPidPid() & ProtocolConstants.PID_MASK, b.arg()) + "}\n";
        }

        // Debug aid: loops over message Q, showing messages in buffer.
        public static string DumpPacketQueue(NetIfManager manager, DequeBlockingCollection<int> q) {
            String s = "";
	        DequeBlockingCollection<int> tmp = new DequeBlockingCollection<int>(q);
            int n = 0;
            int idx,count=q.Size();
            while ((idx = tmp.Pop()) != -1) {
                PacketBuffer b = manager.IoBuffers[idx];
                s += String.Format("{0:d}/{1:d}:cmd={2:x2}", n,count,b.command());
                s += DumpMessage(b);
		        ++n;
            } 
            return s;
        }

        // Log a message to Log port (default isNETIF_USB_DEV_0).
        // Prefixes BabelMilliTicker binary value to start.
        // Truncates message if too long.
        // Returns zero on success.
        public static int Logger(MessageExchange exchange, byte logNetIfIndex, byte[] buffer) {
            byte len = Primitives.Strlen(buffer);
            byte max = (byte)(exchange.Manager.MaxPacketSize - ProtocolConstants.GENERAL_OVERHEADS_SIZE - ProtocolConstants.TICKER_SIZE);
            if (len > max) len = max;
            BabelMessage message = BabelMessage.CreateCommandMessage(exchange, false,
                Router.RouterAction.PostToNetIf, logNetIfIndex,
                ProtocolConstants.MEDIATOR_DEVICE_LOG,
                ProtocolConstants.ADRS_LOCAL, 
                ProtocolConstants.ADRS_LOCAL, 0,
                ProtocolConstants.IDENT_MEDIATOR, len, ProtocolConstants.TICKER_SIZE, buffer);
            byte[] tmp = Primitives.UIntToByteArray(Primitives.GetBabelMilliTicker());
            Array.Copy(tmp, message.DataAry, tmp.Length);
            if (!message.Exchange.SubmitMessage(message, null, false, 0)) {
                return 1;
            }
            return 0;
        }
    }
}
