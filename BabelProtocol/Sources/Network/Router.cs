using System;
using System.Collections.Generic;

using Babel.Core;
using Babel.XLink;

namespace Babel.BabelProtocol {

    public class Router {

        public const String TAG = "Router";

        public const bool isMaster = true;

        public enum RouterAction {
            PostToNone,
            SendToRouter,
            PostToNetIf
        }

        // Push message on to RNetIfIndex's Write Q.
        // Messages that cannot be posted are freed.
        public static void PostMessage(NetIfManager manager, int ioIndex, byte rNetIfIndex) {
            String dbgmsg = null;
            if (ioIndex == -1)
                return;
            LinkDriver p = manager.GetLinkDriver(rNetIfIndex);
            if (p == null) {
                rNetIfIndex = ProtocolConstants.NETIF_UNSET;
            }

            if ((p != null) || !isMaster) { // Check destination netIf was not detached.
                if (p == null) {
                    p = manager.GetLinkDriver(ProtocolConstants.NETIF_BRIDGE_LINK);
                }
                if (p != null) {
                    PacketBuffer b = manager.IoBuffers[ioIndex];
                    // Check for circular routing: examine prior destination netIf.
                    if ((b.dNetIf != ProtocolConstants.NETIF_UNSET) && (b.dNetIf == rNetIfIndex)) {
                        // Ignore circular messages.
                        dbgmsg = String.Format("Circular message on netIf [{0}]: ditching message.", rNetIfIndex);
                    } else {
                        b.dNetIf = rNetIfIndex;
                        if (p.NetIfIndex == ProtocolConstants.NETIF_BRIDGE_LINK) {
                            byte netIfs = (byte)((rNetIfIndex << 4) | b.iNetIf); // Use destination to pass netIfs across bridge.
                            b.destination((ushort)(((256 - netIfs) << 8) | netIfs));
                        }
                        if (p.LinkWriteQueue.Push(ioIndex) != -1) { // Check post queue not full.
                            if (Settings.DebugLevel > 6)
                                Log.d(TAG, "Posting:" + ioIndex + " to " + p.NetIfIndex);
                            return;
                        }
                        dbgmsg = String.Format("Write Q full on netIf [{0}]: ditching message.", rNetIfIndex);
                    }
                }
            }
            // Messages that were not posted are freed.
            if (Settings.DebugLevel > 1)
                Log.w(TAG, dbgmsg);
            manager.IoBuffersFreeHeap.Release(ioIndex);
        }

        // Route a message received from one of the NetIfs.
        // Requires posting to write Q of receiver NetIf.
        // Routing rules:
        // 1) Test receiver address (RA) and receiver port (RP).
        // 2) If RA=BabelConstants.ADRS_LOCAL or RA=NodeAdrs:
        //	    a) route to RP NetIf WriteQ.
        // 3) Otherwise:
        //	    a) check for gateway.
        //	    b) otherwise discard.
        // TODO: add support for gateway NetIfs.
        // Discards unroutable messages.
        // Frees message buffer as necessary.
        public static void RouteMessage(NetIfManager manager, int ioIndex) {
            PacketBuffer b;
            ushort receiver;
            byte rNetIf;
            if (ioIndex == -1) return;
            b = manager.IoBuffers[ioIndex];
            switch (b.flagsPid & ProtocolConstants.PID_MASK) {
                case ProtocolConstants.PID_GENERAL:
                case ProtocolConstants.PID_GENERAL_V:
                    receiver = b.receiver();
                    if ((b.flagsRS() & (ProtocolConstants.PORT_MASK << 2)) == (ProtocolConstants.PORT_BRIDGE << 2)) {
                        rNetIf = isMaster ? ProtocolConstants.NETIF_BRIDGE_LINK : ProtocolConstants.NETIF_BRIDGE_PORT;
                    } else {
                        rNetIf = isMaster ? ProtocolConstants.NETIF_MEDIATOR_PORT : ProtocolConstants.NETIF_UNSET;
                    }
                    break;
                default:
                    receiver = ProtocolConstants.ADRS_LOCAL;
                    rNetIf = isMaster ? ProtocolConstants.NETIF_MEDIATOR_PORT : ProtocolConstants.NETIF_UNSET;
                    break;
            }
            if ((receiver != ProtocolConstants.ADRS_LOCAL) && (receiver != manager.NodeAdrs)) {
                // Message needs to be routed to a remote address.
                // TODO: Check for gateway.
                // For now, return to sender or send to bridge.
                rNetIf = isMaster ? b.iNetIf : ProtocolConstants.NETIF_UNSET;
            }
            PostMessage(manager, ioIndex, rNetIf);
        }
    }
}