using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Babel.Core;
using Babel.XLink;

namespace Babel.BabelProtocol {

    public class LinkDriver {

        public const int LinkWriteQSize = 64;
        public const int LinkVerifyQSize = 64;
        public const byte LinkMissingQSize = 64;
        public const int LinkMappingBufferSize = 256;
        public const int SchedulerIntervalDefault = 300; //ms, time between scheduler actions.
        public const int ResponseIntervalDefault = 2000; //ms, Max period after which link is considered unresponsive.
        public int LinkWriteQWaterMark = LinkWriteQSize - 5;

        public uint baudTimeout;
        public uint duplexTimeout;
        public uint duplexReplyTimeout;
        public bool HasRead;
        public bool NotStale;
        public bool DoesIO;
        public bool HasTasks;
        public bool MappingEnabled;
        public bool InputReset;
        public byte NetIfIndex;
        public byte NumTasks;
        public byte NumIOAttempts;
        public byte NumOverflowed;
        public byte vnoResendCounter;
        public byte vnoLastInput;
        public byte vnoLastOutput;
        public byte DeviceContextIndex;
        public byte duplexKind;
        public byte duplexApply;
        public byte duplexMode;
        public byte duplexDefaultMode;
        public byte duplexNumWaiting;
        public byte duplexPingReply;
        public byte duplexState;
        public int SerialIndex;
        public int SchedulerInterval;
        public int ResponseInterval;
        public ILinkNetIf IoNetIfDevice;
        public NetIfManager Manager;
        public LinkIOThreads IOThreads;
        public LinkMonitor Monitor;
        public PacketParser Parser;
        public DequeBlockingCollection<int> LinkWriteQueue; // IoIndex's.
        public DequeBlockingCollection<int> LinkVerifyQueue; // IoIndex's.
        public DequeBlockingCollection<byte> LinkMissingQueue; // Vno's. Size must be at least Vno max, i.e. a byte.
        public byte[] OutputMapBuffer;

        void ClearLinkDriver() {
            InputReset = false;
            NumIOAttempts = 0;
            NumOverflowed = 0;
            vnoLastInput = (ProtocolConstants.VNO_SIZE - 1);
            vnoLastOutput = 0;
            vnoResendCounter = 0;
            baudTimeout = 0;
        }

        public LinkDriver(NetIfManager manager, ILinkNetIf netIfDevice, byte netIfIndex, 
                bool mappingEnabled, bool threaded, bool doesIO, bool hasTasks) {
            Manager = manager;
            IoNetIfDevice = netIfDevice;
            NetIfIndex = netIfIndex;
            MappingEnabled = mappingEnabled;
            DoesIO = doesIO;
            HasTasks = hasTasks;
            NumTasks = 0;
            DeviceContextIndex = 0;
            duplexKind = (byte)LinkMonitor.LINK_DUPLEX_KIND.LINK_DUPLEX_KIND_NONE;
            duplexApply = 0;
            Monitor = (DoesIO ? new LinkMonitor(this) : null);
            Parser = (DoesIO ? new PacketParser(this) : null);
            IOThreads = (threaded ? new LinkIOThreads(this) : null);

            NotStale = false;
            HasRead = false;
            SerialIndex = -1;
            SchedulerInterval = SchedulerIntervalDefault;
            ResponseInterval = ResponseIntervalDefault;

            LinkWriteQueue = new DequeBlockingCollection<int>(LinkWriteQSize, -1);
            LinkVerifyQueue = new DequeBlockingCollection<int>(LinkVerifyQSize, -1);
            LinkMissingQueue = new DequeBlockingCollection<byte>(LinkMissingQSize, ProtocolConstants.VNO_NULL);

            OutputMapBuffer = (mappingEnabled ? new byte[LinkMappingBufferSize] : null);

            ClearLinkDriver();

            manager.SerialNumberManager.NetIfSerialNumberSetup(NetIfIndex);
        }

        public void ResetLinkDriver() {
            int ioIndex;
            Reset();
            while ((ioIndex = LinkWriteQueue.Pop()) != -1)
                Manager.IoBuffersFreeHeap.Release(ioIndex);
            while ((ioIndex = LinkVerifyQueue.Pop()) != -1)
                Manager.IoBuffersFreeHeap.Release(ioIndex);
            LinkWriteQueue.Clear();
            LinkVerifyQueue.Clear();
            LinkMissingQueue.Clear();
            ClearLinkDriver();
        }

        public virtual void Close() {
            StopLinkDriver();
            ResetLinkDriver();
            if (IoNetIfDevice != null) {
                IoNetIfDevice.Close();
                IoNetIfDevice = null;
            }
        }

        protected virtual void Reset() {
        }

        public virtual void ServiceTasks(uint curTicks, LinkTaskKind taskAction) {
        }

        public void Suspend() {
            if (IoNetIfDevice != null)
                IoNetIfDevice.Suspend();
        }

        public void StopLinkDriver() {
            if (IOThreads != null) 
                IOThreads.StopLink();
        }
        public void StartLinkDriver() {
            if (IOThreads != null) 
                IOThreads.StartLink();
        }

        public ComponentState GetComponentState() {
            return IoNetIfDevice.GetComponentState();
        }

        public string GetComponentId() {
            return IoNetIfDevice.GetComponentId();
        }

        public int GetSessionId() {
            return IoNetIfDevice.GetSessionId();
        }

        public bool Compare(LinkDevice d) {
            return IoNetIfDevice.Compare(d);
        }

        public int GetWriteQueueSize() {
            return LinkWriteQueue.Size() + IoNetIfDevice.GetWriteQueueSize();
        }
        public int GetIOCount(bool reads) {
            return IoNetIfDevice.GetIOCount(reads);
        }

        public void ResetIOCounters() {
            IoNetIfDevice.ResetIOCounters();
        }

        public void LinkPing() {
            if (DoesIO) {
                Monitor.SendLinkHandshakePacket(ProtocolConstants.PID_PING, LinkMonitor.Destination, 0); // TODO: LinkId for resend.
            }
        }

        // Process input stream from port and route results.
        public void LinkRead(bool blockOnRead) {
            if (DoesIO) {
                byte[] buffer;
                if (blockOnRead)
                    buffer = IoNetIfDevice.BlockingRead();
                else
                    buffer = IoNetIfDevice.Read();
                if (buffer == null) {
                    // TODO: Count failures. 
                    ++NumIOAttempts;
                    if (NumIOAttempts >= 3) {
                        Thread.Sleep(200);
                    }
                    Parser.ResetParser();
                    return;
                } else HasRead = true;
                if (InputReset) {
                    InputReset = false;
                    Parser.ResetParser();
                }
                NumIOAttempts = 0;
                Parser.ProtocolParser(buffer);
            }
        }

        // Stream message from WriteQ and send to output device or message handler.
        // Returns 0 on success, 1 if caller should retry write.
        public byte LinkWrite() {
            if (DoesIO) {
                if (IoNetIfDevice.WriteBufferEmpty(DeviceContextIndex)) {
                    int idx, n; byte[] q;
                    if (NumOverflowed > 0) {
                        q = OutputMapBuffer;
                        n = NumOverflowed;
                        NumOverflowed = 0;
                        IoNetIfDevice.Write(q, Manager.MaxPacketSize, n);
                    } else if ((idx = LinkWriteQueue.Peek()) != -1) {
                        PacketBuffer b = Manager.IoBuffers[idx];
                        byte[] r;
                        byte verifyAction = Monitor.PreLinkWriteFilter(b);
                        n = b.pktLen;
                        q = b.buffer;
                        if (MappingEnabled && ((r = OutputMapBuffer) != null)) {
                            byte c;
                            int j, k = 0;
                            if (n-- > 0) { // Step over Sync byte.
                                r[k] = q[k]; 
                                ++k; 
                            } 
                            j = k;
                            while (n-- > 0) {
                                c = q[k++];
                                if ((c == ProtocolConstants.BYTE_SYNC) || (c == ProtocolConstants.BYTE_ESC)) {
                                    r[j++] = ProtocolConstants.BYTE_ESC;
                                    c = (c == ProtocolConstants.BYTE_ESC ? ProtocolConstants.BYTE_ESC_ESC : ProtocolConstants.BYTE_ESC_SYNC);
                                }
                                r[j++] = c;
                            }
                            n = j;
                            q = OutputMapBuffer;
                        }
                        if (n > Manager.MaxPacketSize) { // If message bigger than packet, chop in two.
                            NumOverflowed = (byte)n;
                            n = Manager.MaxPacketSize;
                        }
                        if (!IoNetIfDevice.Write(q, 0, n)) {
                            NumOverflowed = 0;
                            // TODO: Count failures. Take action: mark item as unsent, flush, place on end of queue?
                            ++NumIOAttempts;
                            if (NumIOAttempts >= 3) {
                                Thread.Sleep(200);
                            }
                            return 1;
                        }
                        if (LinkWriteQueue.PopIfEqual(idx) == idx) { // Check if detach/flush occurred.
                            Monitor.PostLinkWriteFilter(idx,verifyAction);
                            NumIOAttempts = 0;
                        }
                    }
                } else {
                    return 1;
                }
            } else {
                int n;
                byte r;
                // Call messageHandler on WriteQ.
                while ((n = LinkWriteQueue.Pop()) != -1) {
                    r = MessageCommandHandler(n);
                    if (r != 0) {
                        Manager.IoBuffersFreeHeap.Release(n);
                    }
                }
            }
            return 0;
        }

        public void PollLinkDriver() {
            // 1)  Service link protocols.
            if(Monitor!=null)
                Monitor.LinkProtocol();
            // 2) Process input stream from port and route results.
            LinkRead(false);
            // 3) Stream message from WriteQ and send to output device.
            LinkWrite();
        }

        public virtual byte CommandHandler(MessageTransaction mtx) {
            return 0;
        }

        /// <summary>
        /// Handle a command message.
        /// When overriding, free message buffer on success if no longer used.
        /// </summary>
        /// <param name="ioIndex">Index of message.</param>
        /// <returns>Return non-zero on error, caller will free message buffer.</returns>
        public virtual byte MessageCommandHandler(int ioIndex) {
            return 1; // By default, free message if unhandled.
        }
    }
}