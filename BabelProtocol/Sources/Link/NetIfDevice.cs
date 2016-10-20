using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Babel.Core;
using Babel.XLink;

namespace Babel.BabelProtocol {
    /// <summary>
    /// Wraps a LinkDevice via a LinkListener.
    /// NetIfDevices are used by the transport layer to support multiple readers.
    /// </summary>
    public class NetIfDevice : ILinkNetIf {

        protected Object Owner;
        protected LinkDevice IoLinkDevice;
        protected BlockingCollection<byte[]> ReadQueue;

        public NetIfDevice(Object owner, LinkDevice dev) {
            Owner = owner;
            IoLinkDevice = dev;
            ReadQueue = new BlockingCollection<byte[]>();
            dev.AddListener(owner, this.ComponentLinkListener);
        }

        /// <summary>
        /// Delegate callback for listening to device events.
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        /// <param name="v"></param>
        public void ComponentLinkListener(ComponentEvent ev, Component component, object val) {
            if (component == IoLinkDevice) {
                if (ComponentEvent.ReadComplete==ev) {
                    ReadQueue.TryAdd(val as byte[]);
                }
            }
        }

        public void Close() {
            IoLinkDevice.Close();
        }

        public void Suspend() {
            IoLinkDevice.Suspend();
        }

        public void SetDuplexKind(byte deviceContextIndex, byte duplexKind) {

        }
        public void PerformBaudAction(byte deviceContextIndex, byte rateIndex, byte baudAction) {

        }

        // This should only return true once the write buffer is empty: i.e. actually transferred.
        public bool WriteBufferEmpty(byte deviceContextIndex) {
            return IoLinkDevice.WriteBufferEmpty(deviceContextIndex);
        }

        // Returns true if successfully written.
        public bool Write(byte[] p, int offset, int length) {           
            return IoLinkDevice.Write(p,offset,length);
        }

        // Non blocking read.
        // Returns byte array if successfully read.
        public byte[] Read() {
            byte[] p = null;
            ReadQueue.TryTake(out p);
            return p;
        }

        // Blocking read.
        // Returns byte array if successfully read.
        public byte[] BlockingRead() {
            byte[] p = null;
            ReadQueue.TryTake(out p, System.Threading.Timeout.Infinite);
            return p;
        }

        public ComponentState GetComponentState() {
            return IoLinkDevice.State;
        }

        public void NotifyStateChange(ComponentState s) {
            IoLinkDevice.NotifyStateChange(s);
        }

        public string GetComponentId() {
            return IoLinkDevice.Id;
        }

        public int GetSessionId() {
            return IoLinkDevice.SessionId;
        }

        public bool HasHeartBeat() {
            return IoLinkDevice.HasHeartBeat();
        }
        public bool Compare(LinkDevice d) {
            return IoLinkDevice == d;
        }
        public int GetWriteQueueSize() {
            return IoLinkDevice.GetWriteQueueSize();
        }

        public int GetIOCount(bool reads) {
            return IoLinkDevice.GetIOCount(reads);
        }

        public void ResetIOCounters() {
            IoLinkDevice.ResetIOCounters();
        }
    }
}
