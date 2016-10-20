using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Babel.Core;
using Babel.XLink;

namespace Babel.BabelProtocol {

    /// <summary>
    /// Manages a dictionary of Q's for incoming messages indexed on message ident.
    /// </summary>
    public class ProtocolHub {
        public MessageExchange Exchange;
        public ConcurrentDictionary<int, LinkedBlockingCollection<BabelMessage>> IncomingQueues;

        public ProtocolHub(MessageExchange exchange) {
            Exchange = exchange;
            IncomingQueues = new ConcurrentDictionary<int, LinkedBlockingCollection<BabelMessage>>();
        }

        public void Close() {
            Exchange.Close();
            IncomingQueues.Clear();
        }

        public LinkedBlockingCollection<BabelMessage> GetQueue(int id) {
            LinkedBlockingCollection<BabelMessage> q = null;
            IncomingQueues.TryGetValue(id, out q);
            return q;
        }

        public int GetQueueLength(int id) {
            LinkedBlockingCollection<BabelMessage> q = GetQueue(id);
            if (q != null) return q.Size();
            return 0;
        }

        public void AddToQueue(int id, BabelMessage m) {
            LinkedBlockingCollection<BabelMessage> q = GetQueue(id);
            if (q == null) {
                q = new LinkedBlockingCollection<BabelMessage>();
                IncomingQueues.TryAdd(id, q);
            }
            q.Add(m);
        }

        public BabelMessage GetMessageFromQueue(int id) {
            LinkedBlockingCollection<BabelMessage> q = GetQueue(id);
            if (q != null) return q.Take();
            return null;
        }
    }
}
