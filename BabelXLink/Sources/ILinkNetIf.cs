using System;

using Babel.Core;

namespace Babel.XLink {

    public interface ILinkNetIf {
        ComponentState GetComponentState();
        void NotifyStateChange(ComponentState s);
        string GetComponentId();
        bool HasHeartBeat();
        bool Compare(LinkDevice d);
        bool WriteBufferEmpty(byte deviceContextIndex);
        int GetSessionId();
        int GetWriteQueueSize();
        int GetIOCount(bool reads);
        void ResetIOCounters();
        void Close();
        void Suspend();
        byte[] Read();
        byte[] BlockingRead();
        bool Write(byte[] buffer, int offset, int length);
        void SetDuplexKind(byte deviceContextIndex,byte duplexKind);
        void PerformBaudAction(byte deviceContextIndex, byte rateIndex, byte baudAction);
    }
}
