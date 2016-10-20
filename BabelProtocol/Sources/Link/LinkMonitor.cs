using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Babel.Core;
using Babel.XLink;

namespace Babel.BabelProtocol {

    public class LinkMonitor {

        public const String TAG = "LinkMonitor";

        public enum LINK_TASK {
	        LINK_TASK_RESET=0,
	        LINK_TASK_SERVICE
        };

        // Bits indicating if duplexing or crossover applies.
        public enum LINK_DUPLEX_KIND {
	        LINK_DUPLEX_KIND_NONE=0,
	        LINK_DUPLEX_KIND_HALF=1,
	        LINK_DUPLEX_KIND_CROSS=2,
        };

        // Bits indicating state of link duplex and crossover.
        public enum DUX_MODE {
	        DUX_RX=1,
	        DUX_TX=2,
	        DUX_FULL=3,
	        DUX_CROSS=4
        };

        // Baud rate failure timeout:
        public const int BAUD_RATE_TIMEOUT = 2000; //ms, Must receive a message within this time after baud change.
        public const int CONX_ACTIVITY_TIMEOUT = 4000; //ms, Connection timeout.

        // Note: for half-duplex, timeouts depend on baud rate 
        // and time to send maximum length message (64 bytes).
        // 64 * (8+2) = 640 bits per message.
        // 9600 bps ==> 640/9600 = 67ms.
        // Ping = 5 bytes = 6ms.
        public const int DUX_PING_SEND_TIMEOUT =100; //ms, Needs to be longer than time to send longest message.
        public const int DUX_DATA_SEND_TIMEOUT =100; //ms, send window.
        public const int DUX_DATA_RECEIVE_TIMEOUT =(DUX_DATA_SEND_TIMEOUT+10); //ms, receive window.
        public const int DUX_PING_RATE =50; //ms, governs how often link is polled.

        // Bits for indicating when a ping or reply message was i/o.
        public const byte DUX_PING_SENT =1;
        public const byte DUX_REPLY_SENT =2;
        public const byte DUX_PING_RECEIVED =4;
        public const byte DUX_REPLY_RECEIVED =8;

        public const byte DUX_STATE_START =0;
        public const byte DUX_MASTER_STATE_START =1;
        public const byte DUX_MASTER_STATE_TX_CHECK =2;
        public const byte DUX_MASTER_STATE_TX_PING_CHECK =3;
        public const byte DUX_MASTER_STATE_RX_CHECK =4;
        public const byte DUX_SLAVE_STATE_START =10;
        public const byte DUX_SLAVE_STATE_TX_CHECK =11;

        public static ushort Destination = 0x0000; // TODO: factor this out.

        public NetIfManager Manager;
        public LinkDriver IoNetIf;
        public int NumMessagesRead;

        public LinkMonitor(LinkDriver ltx) {
            IoNetIf = ltx;
            Manager = ltx.Manager;
            NumMessagesRead = 0;
        }

        public void ResetNumberMessagesRead() {
            NumMessagesRead = 0;
        }
        public int GetNumberMessagesRead() {
            return NumMessagesRead;
        }

        public void PingLink() {
            IoNetIf.LinkPing();
        }

        public void ResetLink(ushort destination) {
            /* TODO: task handler.
            if (IoNetIf.ioHandlers.deviceFuncs != null) {
                IoNetIf.ioHandlers.deviceFuncs->taskHandler(ltx, ProtocolConstants.LINK_TASK_RESET);
            }
             */
            IoNetIf.ResetLinkDriver();
            SendLinkHandshakePacket(ProtocolConstants.PID_PING, destination, ProtocolConstants.PID_ARG_RESET); // ZZZ Pair or all on link.
        }

        // Checks if the current/next Vno is in order.
        // Updates the verification missing Q if necessary.
        // Returns false if ok, i.e. if Vno is in order.
        // Returns true if Vno was out of order.
        public bool CheckOrderMissingQ(byte vno, bool isCurrent) {
            byte lastmno = IoNetIf.vnoLastInput;
            int n = (int)(lastmno + ProtocolConstants.VNO_DELTA);
            if (((vno > lastmno) && (vno < n)) || ((n >= ProtocolConstants.VNO_SIZE) && (vno <= (n % ProtocolConstants.VNO_SIZE)))) {
                // Add missing vno's to missingQ.
                n = lastmno;
                while ((n = (int)((n + 1) % ProtocolConstants.VNO_SIZE)) != vno) {
                    IoNetIf.LinkMissingQueue.Push((byte)n);
                }
                if (!isCurrent) {
                    if (vno > 0)
                        --vno;
                    else
                        vno = (byte)(ProtocolConstants.VNO_SIZE - 1);
                }
                IoNetIf.vnoLastInput = vno;
                return false;
            }
            return true;
        }

        private bool VnoVerifyComparison(int idx, object w) {
            return (Manager.IoBuffers[idx].arg() == (byte)w);
        }

        // Finds message with Vno in VerifyQ.
        // Returns message idx, or -1.
        public int FindVnoInVerifyQueue(DequeBlockingCollection<int> q, byte vno) {
            return q.FindFirstOccurrence(VnoVerifyComparison, vno);
        }

        // Submit a handshake command to link WriteQ.
        // For example, Resend Vno command, ping, reply, etc.
        public int SendLinkHandshakePacket(byte pid, ushort destination, byte arg) {
            int idx = Manager.IoBuffersFreeHeap.Allocate();
            if (idx != -1) {
                PacketBuffer b = Manager.IoBuffers[idx];
                b.flagsPid = pid;
                b.dNetIf = ProtocolConstants.NETIF_UNSET;
                b.iNetIf = ProtocolConstants.NETIF_UNSET;
                b.pktLen = ProtocolConstants.HANDSHAKE_PACKET_SIZE;
                b.sync(ProtocolConstants.BYTE_SYNC);
                b.negPidPid(Primitives.NibbleToPid(pid));
                b.destination(destination);
                b.arg(arg);
                b.UpdateCheckSum(b.pktLen);
                if (IoNetIf.LinkWriteQueue.PushFirst(idx) == -1) {
                    Manager.IoBuffersFreeHeap.Release(idx);
                    return -1;
                }
            }
            return idx;
        }

        public void LinkResend() {
            byte vno;
            IoNetIf.vnoResendCounter = 0;
            if ((vno = IoNetIf.LinkMissingQueue.Pop()) != ProtocolConstants.VNO_NULL) {
                IoNetIf.LinkMissingQueue.Push(vno);
                SendLinkHandshakePacket(ProtocolConstants.PID_RESEND, LinkMonitor.Destination, vno); // TODO: LinkId for resend.
            }
        }

        // Called just before message is written to device.
        // Determine if packet needs modification.
        // Updates DUX & BAUD flags.
        // Returns verification action if needed.
        // TODO: set destination.
        public byte PreLinkWriteFilter(PacketBuffer b) {
            int sumDiff = 0;
            byte verifyAction = ProtocolConstants.VERIFY_ACTION_NONE;
            byte c = (byte)(b.flagsPid & ProtocolConstants.META_FLAGS_PID);
            if (c == ProtocolConstants.PID_GENERAL_V) {
                // Sequence numbers are used to detect bad xfers.
                // Resends must be sent with the same Vno rather than a new Vno.
                if ((b.flagsPid & ProtocolConstants.META_FLAGS_RESEND)!=0) {
                    verifyAction = ProtocolConstants.VERIFY_ACTION_RESEND; // This stops vnoLastOutput being incremented below.
                } else {
                    verifyAction = ProtocolConstants.VERIFY_ACTION_NEW;
                    sumDiff = (int)b.arg() - (int)IoNetIf.vnoLastOutput;
                    b.arg(IoNetIf.vnoLastOutput);
                }
            } else if (c == ProtocolConstants.PID_PING) {
                byte arg = b.arg();
                if (arg == 0) { // It's a basic ping, so set arg to vno.
                    sumDiff = -(int)IoNetIf.vnoLastOutput;
                    b.arg(IoNetIf.vnoLastOutput);
                } else if (arg >= ProtocolConstants.PID_ARG_BAUD_MIN) {
                    switch (arg) {
                        case ProtocolConstants.PID_ARG_RESET:
                            break;
                        case ProtocolConstants.PID_ARG_SLAVE:
                            break;
                        case ProtocolConstants.PID_ARG_MULTI:
                            break;
                        case ProtocolConstants.PID_ARG_MASTER:
                            break;
                        default: // BAUD: Flag baud ping sent, set UART_*_SetBaudRate = netIfIndex.
                            // This is a broadcast to all on link.
                            IoNetIf.IoNetIfDevice.PerformBaudAction(IoNetIf.DeviceContextIndex, 
                                (byte)(arg - ProtocolConstants.PID_ARG_BAUD_MIN), 
                                ProtocolConstants.BAUD_ACTION_SIGNAL); 
                            IoNetIf.baudTimeout = Primitives.GetBabelMilliTicker() + LinkMonitor.BAUD_RATE_TIMEOUT; // Mark Baud broadcast for all.
                            break;
                    }
                }
                // DUX: need to block further writes.
                if ((IoNetIf.duplexKind & (byte)LINK_DUPLEX_KIND.LINK_DUPLEX_KIND_HALF) != 0) {
                    IoNetIf.duplexPingReply |= LinkMonitor.DUX_PING_SENT;
                    IoNetIf.duplexNumWaiting = (byte)0xffu;
                    IoNetIf.duplexMode = (byte)DUX_MODE.DUX_RX;
                }
            } else if (c == ProtocolConstants.PID_REPLY) {
                IoNetIf.duplexPingReply |= LinkMonitor.DUX_REPLY_SENT;// DUX: flag reply sent.
            }
            // Update checksum: assume valid on entry & work out bit change.
            if (sumDiff != 0) {
                // newChkSum = oldChkSum + (oldSumBits - newSumBits).
                byte oldChkSum = b.buffer[b.pktLen - 1];
                b.buffer[b.pktLen - 1] = (byte)(((int)oldChkSum) + sumDiff);
            }
            return verifyAction;
        }

        // Called just after write to device.
        // Check if message should be cached for verification purposes.
        // Free buffer as necessary.
        public void PostLinkWriteFilter(int idx, byte verifyAction) {
            if (verifyAction != ProtocolConstants.VERIFY_ACTION_NONE) { // Cache msgs that need to be verified in case resend needed.
                if (verifyAction == ProtocolConstants.VERIFY_ACTION_NEW) {
                    idx = IoNetIf.LinkVerifyQueue.PushForce(idx); // If cache full, treat oldest as stale.
                    if (idx != -1) {
                        Manager.IoBuffersFreeHeap.Release(idx);
                    }
                    ++IoNetIf.vnoLastOutput;
                    IoNetIf.vnoLastOutput %= ProtocolConstants.VNO_SIZE;
                }
            } else {
                Manager.IoBuffersFreeHeap.Release(idx);
            }
        }

        // Perform a handshake request received from link.
        // Consults original message for r,s details, ports=0, flags=0.
        // TODO: check what happens when a master is pinged etc. Ok?
        public void PerformLinkHandshake(PacketBuffer hp) {
            int idx;
            byte arg = hp.arg();

            // TODO: Check packet is for us first.
	
            switch (hp.negPidPid()&ProtocolConstants.PID_MASK) {
                case ProtocolConstants.PID_PING: // We received a ping. Now send a reply.
                    if (arg >= ProtocolConstants.PID_ARG_BAUD_MIN) {
                        switch (arg) {
                            case ProtocolConstants.PID_ARG_RESET:
                                IoNetIf.ResetLinkDriver();
                                break;
                            case ProtocolConstants.PID_ARG_SLAVE:
                                break;
                            case ProtocolConstants.PID_ARG_MULTI:
                                break;
                            case ProtocolConstants.PID_ARG_MASTER:
                                break;
                            default: // BAUD: Request to change baud rate.
                                IoNetIf.IoNetIfDevice.PerformBaudAction(IoNetIf.DeviceContextIndex,
                                    (byte)(arg - ProtocolConstants.PID_ARG_BAUD_MIN),
                                    ProtocolConstants.BAUD_ACTION_SAVE); 
                                break;
                        }                      
                    } else {
                        // Set linkLastInputVno=(VNO_SIZE-1) & linkLastOutputVno=0 ? 
                        CheckOrderMissingQ(arg, false);
                        IoNetIf.duplexPingReply |= LinkMonitor.DUX_PING_RECEIVED;
                    }
                    // Send a reply with Arg = num messages waiting.
                    // TODO: unless Q contains Msg destined to sender: use "more" msg code.
                    int num = IoNetIf.LinkWriteQueue.Size(); // Arg = num messages waiting.
                    SendLinkHandshakePacket(ProtocolConstants.PID_REPLY,
                        LinkMonitor.Destination, // hp.destination(), // TODO: This should be the linkAdrs.
                        (byte)((num > 255) ? 255 : num));
                    break;
                case ProtocolConstants.PID_REPLY: //TODO: Now stop pinging.
                    // arg=number of waiting messages.
                    // If arg>0 read some messages.
                    IoNetIf.duplexNumWaiting = arg;
                    IoNetIf.duplexPingReply |= LinkMonitor.DUX_REPLY_RECEIVED;
                    break;
                case ProtocolConstants.PID_RESEND: // Resend a message.
                    idx = FindVnoInVerifyQueue(IoNetIf.LinkVerifyQueue,arg);
                    if (idx == -1) {
                        SendLinkHandshakePacket(ProtocolConstants.PID_CANCEL, hp.destination(), arg);
                    } else if (IoNetIf.LinkWriteQueue.FindFirstOccurrenceIndex(idx) == -1) {
                        PacketBuffer b = Manager.IoBuffers[idx];
                        b.flagsPid|=ProtocolConstants.META_FLAGS_RESEND;
                        if (IoNetIf.LinkWriteQueue.PushFirst(idx) == -1) {
                            Manager.IoBuffersFreeHeap.Release(idx);
                        }
                    }
                    break;
                case ProtocolConstants.PID_CANCEL: // Remove message from missingQ.
                    IoNetIf.LinkMissingQueue.RemoveFirstOccurrence(arg);
                    break;
                default: break;
            }
            // TODO: IoNetIf.activityTimeout=Primitives.GetBabelMilliTicker() + LinkMonitor.CONX_ACTIVITY_TIMEOUT;
            IoNetIf.baudTimeout = Primitives.GetBabelMilliTicker() + LinkMonitor.BAUD_RATE_TIMEOUT; // Reset timer after input from specific sender.	
        }

        // Checks Vno if necessary then routes message.
        public void DispatchLinkPacket(PacketParser p) {

            // TODO: Check packet is for us first.

            // TODO: IoNetIf.activityTimeout=Primitives.GetBabelMilliTicker() + LinkMonitor.CONX_ACTIVITY_TIMEOUT;
            IoNetIf.baudTimeout = Primitives.GetBabelMilliTicker() + LinkMonitor.BAUD_RATE_TIMEOUT; // Reset timer after input from specific sender.

            if ((IoNetIf.duplexNumWaiting > 0) && (IoNetIf.duplexNumWaiting != 0xffu))
                IoNetIf.duplexNumWaiting--; //DUX.
            // Check vno sequence.
            if (p.Pid == ProtocolConstants.PID_GENERAL_V) {
                byte arg = p.CurrentPacket.arg();
                if (CheckOrderMissingQ(arg, true)) {
                    // It's a resent packet, check missingQ & remove.                        
                    if (!IoNetIf.LinkMissingQueue.RemoveFirstOccurrence(arg)) {
                        // Not on Q, so ignore: it was sent ok previously.
                        // Keep buffer for next msg.
                        //IoBuffersFreeHeap.Push(IoIndex);
                        //IoIndex=-1;
                        return;
                    }
                }
            }
            // Dispatch message.
            if (Settings.DebugLevel > 6)
                Log.d(TAG, "Dispatching:"+ p.IoIndex + ".");
            ++NumMessagesRead;
            Router.RouteMessage(Manager, p.IoIndex);
            p.IoIndex = -1;
        }

        // TODO: Note: logic may change if multi-connections per link.
        public void LinkDuplexInit() {
            // TODO: LinkDuplexInit: implement.
        }

        /*
	        FSM for handling half-duplex i/o.
	        States for master:	
	        1) Full/TX Start: init timeout
	        2) Full/TX Check timeout & if Ping sent flag raised.
	        3) if timeout only, send ping, set timeout, move to ping sent check.
	        4) Check if ping sent or timeout to 1.
	        5) Ping sent flag raised. Switch to RX mode. Set timeout. Block TX.
	        6) RX mode: check if duplexNumWaiting==0 or timeout. Move to 1) if so.
	        States for slave:
	        1) RX start: block tx. 
	           check if ping received: yes: send reply+Wno, set timeout, move to reply sent check.
	        2) Reply sent flag raised. or timeout to 1. set send timeout.
	        3) enable tx. send until timeout. goto 1.
	        In bxPerformLinkHandshake, for ping reply: if in DUX RX mode, get Wno.
	        Read Wno msgs, or timeout.
	        Slave: start timer & stop sending if timeout.
	        Block sending while in DUX RX mode.
        */
        public void LinkDuplex() {
            // TODO: LinkDuplex: implement.
        }

        public void LinkProtocol() {
            if (IoNetIf.DoesIO) {
                // 1) Do resends.
                if (IoNetIf.vnoResendCounter++ > Manager.PollResendPriority) {
                    LinkResend();
                }
                /*
                // 2) Check if need to reset baud rate.
                uint8 netIfIndex = ltx->portNumber;
                if ((BabelMilliTicker > ltx->baudTimeout) ||	// General check if any activity on link for baud verification.
                    (bxPortBaudRateSaved[netIfIndex] != bxPortBaudRateCurrent[netIfIndex])) {
                    if (bxPortBaudRateFixed[netIfIndex] == 0) {// Not fixed, so change to default if master, cycle if slave.
                        // TODO: BAUD: if slave, cycle through indexes.
                        ltx->ioHandlers.deviceFuncs->PerformBaudAction(ltx->DeviceContextIndex, 0, BX_BAUD_ACTION_SET);
                    } else { // Fixed, so change to requested baud rate.
                        ltx->ioHandlers.deviceFuncs->PerformBaudAction(ltx->DeviceContextIndex, bxPortBaudRateSaved[netIfIndex], BX_BAUD_ACTION_SAVE);
                    }
                    // TODO: BAUD: if master, for both cases should send a baud ping broadcast.
                    ltx->baudTimeout = BabelMilliTicker + BX_BAUD_RATE_TIMEOUT; // Reset baud timer for all.
                }
                // 3) Check if need to change duplex mode. // TODO: add phyDuplex to init parameters - or auto detect?
                if (ltx->duplexKind & BK_LINK_DUPLEX_KIND_HALF) { // TODO: if multi-link, HD too when NumConnections>1.
                    bxLinkDuplex(ltx);
                }
                */
            }
        }
    }
}
