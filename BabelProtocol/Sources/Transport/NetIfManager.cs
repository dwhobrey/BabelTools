using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Babel.Core;
using Babel.XLink;

namespace Babel.BabelProtocol {

    public class NetIfManager {

        public const String TAG = "NetIfManager";

        public byte PollResendPriority = 200; // DEBUG: this is a very sensitive parameter. Need to control resend rate via timer.
        public ushort NodeAdrs = ProtocolConstants.ADRS_LOCAL;
        public byte MaxPacketSize = ProtocolConstants.MAX_PACKET_SIZE;
        public int IoBuffersHeapSize = 200;

        public PacketBuffer[] IoBuffers;
        public Dictionary<int, LinkDriver> IoNetIfs;
        public TokenAllocator IoBuffersFreeHeap;
        public SerialNumbers SerialNumberManager;
        public PacketFactory Factory;
        public string ShellId;
        public InterruptSource Interrupt;

        public NetIfManager(string shellId, string masterSN, InterruptSource interrupt) {
            ShellId = shellId;
            Interrupt = interrupt;
            IoBuffersFreeHeap = new TokenAllocator(IoBuffersHeapSize);
            IoBuffers = new PacketBuffer[IoBuffersHeapSize];
            for (int k = 0; k < IoBuffersHeapSize; ++k) {
                IoBuffers[k] = new PacketBuffer(MaxPacketSize);
            }
            IoNetIfs = new Dictionary<int, LinkDriver>();
            Factory = new PacketFactory(this);
            SerialNumberManager = new SerialNumbers(this,masterSN);
        }

        public override string ToString() {
            bool isFirst = true;
            string s = "{";
            foreach (LinkDriver p in IoNetIfs.Values) {
                if(!isFirst) s+=";";
                else isFirst = false;
                s += p.NetIfIndex + "," + p.GetComponentId();
            }
            return s + "}";
        }

        public void ResetDriver(byte netIfIndex) {
            LinkDriver p = GetLinkDriver(netIfIndex);
            if (p != null) {
                p.Monitor.ResetLink(LinkMonitor.Destination); //TODO: set destination.
            }
        }

        public void AddDriver(LinkDriver p) {
            if (p != null) {
                LinkDriver q = null;
                if (IoNetIfs.TryGetValue(p.NetIfIndex, out q)) {
                    if (q != null) {
                        q.Suspend();
                    }
                }
                IoNetIfs[p.NetIfIndex] = p;
            }
        }

        public void RemoveDriver(LinkDriver p) {
            if (p != null) {
                LinkDriver q = null;
                if (IoNetIfs.TryGetValue(p.NetIfIndex, out q)) {
                    if (q != null) {
                        q.Suspend();
                        q.StopLinkDriver();
                        q.Close();
                        IoNetIfs.Remove(q.NetIfIndex);
                    }
                }
            }
        }

        public void PollLinks() {
            foreach (LinkDriver p in IoNetIfs.Values) {
                if (p.IOThreads==null)
                    p.PollLinkDriver();
            }
        }

        public void Stop() {
            foreach (LinkDriver p in IoNetIfs.Values) {
                p.StopLinkDriver();
            }
        }
        public void Start() {
            foreach (LinkDriver p in IoNetIfs.Values) {
                p.StartLinkDriver();
            }
        }
        public void Close() {
            Stop();
            foreach (LinkDriver p in IoNetIfs.Values) {
                p.Close();
            }
        }

        public void AppBabelDeviceReset() {
        }

        public void AppBabelSaveSerialNumber(byte[] serialNumber, byte bufferIndex) {
            // TODO: save sn?
        }

        public LinkDriver GetLinkDriver(byte netIfIndex) {
            LinkDriver p = null;
            IoNetIfs.TryGetValue(netIfIndex, out p);
            return p;
        }

        public LinkDriver GetLinkDriver(string devId) {
            foreach (LinkDriver p in IoNetIfs.Values) {
                if (p.GetComponentId().Equals(devId)) return p;
            }
            return null;
        }

        public LinkDriver GetLinkDriver(LinkDevice d) {
            foreach (LinkDriver p in IoNetIfs.Values) {
                if (p.Compare(d)) return p;
            }
            return null;
        }

        public ComponentState GetComponentState(byte netIfIndex) {
            LinkDriver p = GetLinkDriver(netIfIndex);
            if (p != null) {
                return p.GetComponentState();
            }
            return ComponentState.Problem;
        }

        public string GetComponentId(byte netIfIndex) {
            LinkDriver p = GetLinkDriver(netIfIndex);
            if (p != null) {
                return p.GetComponentId();
            }
            return null; 
        }

        public int GetSessionId(byte netIfIndex) {
            LinkDriver p = GetLinkDriver(netIfIndex);
            if (p != null) {
                return p.GetSessionId();
            }
            return 0;
        }

        public int GetWriteQueueSize(byte netIfIndex) {
            LinkDriver p = GetLinkDriver(netIfIndex);
            if (p != null) {
                return p.GetWriteQueueSize();
            }
            return -1;
        }

        public int GetWriteQueueCapacity(byte netIfIndex) {
            LinkDriver p = GetLinkDriver(netIfIndex);
            if (p != null) {
                int c = p.LinkWriteQueue.Capacity();
                if (c > 0) return c - 1;
            }
            return 0;
        }

        public void PingNetIf(byte netIfIndex) {
            LinkDriver p = GetLinkDriver(netIfIndex);
            if (p != null) {
                p.Monitor.PingLink();
            }
        }
    }
}