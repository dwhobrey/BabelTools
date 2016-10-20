using System;
using System.Threading;

using Babel.Core;
using Babel.XLink;

namespace Babel.BabelProtocol {
    /// <summary>
    /// When messages are received from the Babel device,
    /// this handler posts them to the interested parties
    /// via the specified thread context.
    /// </summary>
    public class MessageBinder {

        public const String TAG = "MessageBinder";

        public MessageExchange Exchange;
        public BabelMessage Message;
        public BabelMessage Reply;
        public MessageExchange.IMessageHandler Handler;
        public MessageExchange.MessageHandlerKind Kind;
        public Object Context;
        public byte Ident;
        public int NumReplies;
        public long ElapsedTime;

        public MessageBinder(MessageExchange exchange, BabelMessage message, 
            MessageExchange.IMessageHandler handler,
            MessageExchange.MessageHandlerKind kind, Object context, byte ident, int numReplies) {
            Exchange = exchange;
            Message = message;
            Reply = null;
            Handler = handler;
            Kind = kind;
            Context = context;
            Ident = ident;
            NumReplies = numReplies;
            ElapsedTime = DateTime.UtcNow.Ticks;
        }

        public class MessageHandlerThread {
            MessageBinder MsgBinder;
            Thread Task;
            public MessageHandlerThread(MessageBinder msgBinder) {
                MsgBinder = msgBinder;
                Task = new Thread(new ThreadStart(Run));
                Task.Name = "MessageBinder";
                Task.Priority = ThreadPriority.AboveNormal;
            }
            public void Run() {
                try {
                    MsgBinder.Handler.OnBabelMessage(MsgBinder.Reply);
                } catch (Exception) {
                }
            }
            public void Start() { if (Task != null) Task.Start(); }
        }

        public void CallHandler() {
            if (Handler != null && Reply != null) {
                try {
                    switch (Kind) {
                        case MessageExchange.MessageHandlerKind.Shell:
                            if (Context != null && Context is Shell) {
                                Shell s = Context as Shell;
                                // TODO: Run on shell thread.
                                try {
                                    Handler.OnBabelMessage(Reply);
                                } catch (Exception) {
                                }
                            }
                            break;
                        case MessageExchange.MessageHandlerKind.Thread:
                            MessageHandlerThread t = new MessageHandlerThread(this);
                            t.Start();
                            break;
                        default: // NORMAL: run on same thread. Handler should use a concurrent queue.
                            try {
                                Handler.OnBabelMessage(Reply);
                            } catch (Exception) {
                            }
                            break;
                    }
                } catch (Exception e) {
                    // 	if(e instanceof InterruptedException) throw (InterruptedException)e;
                    if (Settings.DebugLevel>0) 
                        Log.d(TAG, "MessageBinder.callHandler exception:" + e.Message);
                }
            }
        }
    }
}