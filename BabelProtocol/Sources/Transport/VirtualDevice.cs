using System;

using Babel.Core;
using Babel.XLink;

namespace Babel.BabelProtocol {

    public class VirtualDevice : Component, ILinkNetIf {

        public VirtualDevice(string name) {
            Id = name;
            State = ComponentState.Working;
        }
        public bool HasHeartBeat() {
            return false;
        }
        public bool Compare(LinkDevice d) {
            return false;
        }
        public int GetWriteQueueSize() {
            return 0;
        }
        public int GetIOCount(bool reads) {
            return 0;
        }
        public void ResetIOCounters() {
        }
        public void Close() {
        }
        public void Suspend() {
        }
        public byte[] Read() {
            return null;
        }
        public byte[] BlockingRead() {
            return null;
        }
        public bool WriteBufferEmpty(byte deviceContextIndex) {
            return false;
        }
        public bool Write(byte[] buffer, int offset, int length) {
            return false;
        }
        public void SetDuplexKind(byte deviceContextIndex, byte duplexKind) {
        }
        public void PerformBaudAction(byte deviceContextIndex, byte rateIndex, byte baudAction) {
        }
    }
}
