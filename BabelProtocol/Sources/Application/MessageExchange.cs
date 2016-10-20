using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Babel.Core;
using Babel.XLink;

namespace Babel.BabelProtocol {

    public class MessageExchange : Component {

        public const String TAG = "MessageExchange";

        public interface IMessageHandler {
            void OnBabelMessage(BabelMessage msg);
        }

        public enum MessageHandlerKind {
            Normal, Thread, Shell
        }

        public int StaleMinutes = 1; // Number of minutes before a stale waiting request is removed.
        public int OutgoingCapacity = 100; // Max size of outgoing Q before messages submissions are rejected.

        public string ShellId;
        public InterruptSource Interrupter;
        public NetIfManager Manager;
        public DeviceParameterTable ParameterTable;
        private bool IsClosing;
        private LinkedBlockingCollection<MessageBinder> OutgoingQueue;
        private LinkedBlockingCollection<BabelMessage> IncomingQueue;
        private List<MessageBinder> IncomingListeners;
        private List<MessageBinder> WaitingForReply;
        private MessageDispatcherThread DispatcherThread;
        private MessageReceiverThread ReceiverThread;

        // Construct a new Exchange.
        public MessageExchange(string shellOrTitleId, string name, string masterSN) {
            Shell s = Shell.GetShell(shellOrTitleId);
            if (s == null) 
                s = Shell.GetShell(Shell.ConsoleShellId);
            if (s != null) {
                Interrupter = s.Interrupter;
                ShellId = s.ShellId;
            }
            Id = name;
            IsClosing = false;
            ParameterTable = new DeviceParameterTable();
            ParameterTabelInit(masterSN);
            Manager = new NetIfManager(ShellId, masterSN, Interrupter);
            Manager.AddDriver(new MediatorNetIf(Manager, this));

            OutgoingQueue = new LinkedBlockingCollection<MessageBinder>();
            IncomingQueue = new LinkedBlockingCollection<BabelMessage>();
            IncomingListeners = new List<MessageBinder>();
            WaitingForReply = new List<MessageBinder>();

            DispatcherThread = new MessageDispatcherThread(this);
            DispatcherThread.Start();
            ReceiverThread = new MessageReceiverThread(this);
            ReceiverThread.Start();

            Manager.Start();
        }

        /* Setup a basic system page in parameter table.
         */
        private void ParameterTabelInit(String masterSN) {
            ParameterTable.Put((int)ProtocolConstants.PARAMETER_TABLE_INDEX_PARAMS,0,(int)VariableKind.VK_Byte,1,
                    (int)StorageFlags.SF_RAM|(int)StorageFlags.SF_ReadOnly,0,255,10,10,10,"Params");
            ParameterTable.Put((int)ProtocolConstants.PARAMETER_TABLE_INDEX_PAGES, 0, (int)VariableKind.VK_Byte, 1,
                    (int)StorageFlags.SF_RAM | (int)StorageFlags.SF_ReadOnly, 0, 255, 1, 1, 1, "Pages");
            ParameterTable.Put(
                    (int)ProtocolConstants.PARAMETER_TABLE_INDEX_TICKER, 0, 
                    (int)VariableKind.VK_UInt, 4,
                    (int)StorageFlags.SF_RAM | (int)StorageFlags.SF_Dynamic,
                    VariableValue.MinValue(VariableKind.VK_UInt),
                    VariableValue.MaxValue(VariableKind.VK_UInt),
                    VariableValue.MinValue(VariableKind.VK_UInt),
                    VariableValue.MinValue(VariableKind.VK_UInt),
                    VariableValue.MinValue(VariableKind.VK_UInt),
                    "Ticker");
            ParameterTable.Put(
                    (int)ProtocolConstants.PARAMETER_TABLE_INDEX_HARDTYP, 0, 
                    (int)VariableKind.VK_Byte, 1,
                    (int)StorageFlags.SF_EEPROM|(int)StorageFlags.SF_ReadOnly,
                    0,255,1,1,1,
                    "HardTyp");
            ParameterTable.Put(
                    (int)ProtocolConstants.PARAMETER_TABLE_INDEX_HARDVER, 0, 
                    (int)VariableKind.VK_Byte, 1,
                    (int)StorageFlags.SF_EEPROM|(int)StorageFlags.SF_ReadOnly,
                    0,255,1,1,1,
                    "HardVer");
            ParameterTable.Put(
                    (int)ProtocolConstants.PARAMETER_TABLE_INDEX_SOFTTYP, 0, 
                    (int)VariableKind.VK_Byte, 1,
                    (int)StorageFlags.SF_EEPROM|(int)StorageFlags.SF_ReadOnly,
                    0,255,1,1,1,
                    "SoftTyp");
            ParameterTable.Put(
                    (int)ProtocolConstants.PARAMETER_TABLE_INDEX_SOFTVER, 0, 
                    (int)VariableKind.VK_Byte, 1,
                    (int)StorageFlags.SF_EEPROM|(int)StorageFlags.SF_ReadOnly,
                    0,255,1,1,1,
                    "SoftVer");
            ParameterTable.Put(
                    (int)ProtocolConstants.PARAMETER_TABLE_INDEX_SYSKEY, 0, 
                    (int)VariableKind.VK_UInt, 4,
                    (int)StorageFlags.SF_RAM | (int)StorageFlags.SF_Dynamic,
                    VariableValue.MinValue(VariableKind.VK_UInt),
                    VariableValue.MaxValue(VariableKind.VK_UInt),
                    VariableValue.MinValue(VariableKind.VK_UInt),
                    VariableValue.MinValue(VariableKind.VK_UInt),
                    VariableValue.MinValue(VariableKind.VK_UInt),
                    "SysKey");
            ParameterTable.Put(
                    (int)ProtocolConstants.PARAMETER_TABLE_INDEX_SERIALNO, 0, 
                    (int)VariableKind.VK_String,30,
                    (int)StorageFlags.SF_EEPROM|(int)StorageFlags.SF_ReadOnly,
                    0, 30, 0, 0, masterSN,
                    "SerialNo");
            ParameterTable.Put(
                    (int)ProtocolConstants.PARAMETER_TABLE_INDEX_PRONAME, 0, 
                    (int)VariableKind.VK_String,30,
                    (int)StorageFlags.SF_EEPROM|(int)StorageFlags.SF_ReadOnly,
                    0,30,0,0,"None",
                    "ProName");
        }

        public void Close() {
            IsClosing = true;
            if (DispatcherThread != null) {
                Primitives.Interrupt(DispatcherThread.Task);
                DispatcherThread = null;
            }
            if (ReceiverThread != null) {
                Primitives.Interrupt(ReceiverThread.Task);
                ReceiverThread = null;
            }
            if (Manager != null) {
                Manager.Close();
                Manager = null;
            }
        }

        public override string ToString() {
            return Id
                + ",SId=" + ShellId
                + ",OQ=" + OutgoingQueue.Size()
                + ",IQ=" + IncomingQueue.Size()
                + ",IL=" + IncomingListeners.Count
                + ",WR=" + WaitingForReply.Count
                + ",PM=" + Manager.ToString();
        }

        public bool IsClosingState() {
            return IsClosing;
        }

        public int OutgoingQueueSize() {
            return OutgoingQueue.Size();
        }

        public bool AddListenerNetIf(LinkDevice dev, byte netIfIndex) {
            // Important: check device not already in use.
            if ((dev == null) || (Manager.GetLinkDriver(dev) != null)) return false;
            NetIfDevice pd = new NetIfDevice(this, dev); // TODO: who should owner be?
            LinkDriver lp = new LinkDriver(Manager, pd, netIfIndex, true, true, true, false);
            Manager.AddDriver(lp);
            lp.StartLinkDriver();
            return true;
        }

        public bool AddDirectNetIf(LinkDevice dev, byte netIfIndex) {
            // Important: check device not already in use.
            if ((dev == null) || (Manager.GetLinkDriver(dev) != null)) return false;
            LinkDriver lp = new LinkDriver(Manager, dev, netIfIndex, true, true, true, false);
            Manager.AddDriver(lp);
            lp.StartLinkDriver();
            return true;
        }

        /**
         * Removes the listener.
         * 
         * @param handler
         *            The interface passed to the original addMessageListener call.
         */
        public void RemoveMessageListener(IMessageHandler handler) {
            if (handler != null) {
                lock (IncomingListeners) {
                    foreach (MessageBinder h in IncomingListeners) {
                        if (h.Handler == handler) {
                            IncomingListeners.Remove(h);
                            break;
                        }
                    }
                }
            }
        }

        /**
         * Adds a listener for incoming messages.
         * 
         * @param handler
         *            Interface to listener.
         * @param ident
         *            The ident of messages to listen for. If ident<0, all messages will be heard.
         */
        public void AddMessageListener(IMessageHandler handler, byte ident, int numReplies) {
            if (handler != null) {
                RemoveMessageListener(handler);
                MessageBinder h = new MessageBinder(this, null, handler, MessageHandlerKind.Thread, null, ident, numReplies);
                lock (IncomingListeners) {
                    IncomingListeners.Add(h);
                }
            }
        }

        /**
         * Asynchronously send a message using server context.
         * 
         * @param message
         *            The message to send.
         * @param handler
         *            Optional handler for reply. 
         * @param createThread. 
         *            Either use a new thread or service receiver thread to invoke handler.
         * Returns true if message was submitted successfully.
         */
        public bool SubmitMessage(BabelMessage message, IMessageHandler handler, bool createThread, int numReplies) {
            if (message != null && (OutgoingQueue.Size() < OutgoingCapacity)) {
                OutgoingQueue.Add(new MessageBinder(this, message, handler, createThread ? MessageHandlerKind.Thread : MessageHandlerKind.Normal, null, message.SenderId,numReplies));
                return true;
            }
            return false;
        }

        /**
         * Asynchronously send a message using activity context.
         * 
         * @param message
         *            The message to send.
         * @param handler
         *            Optional handler for reply. A new Activity Ui thread is used to invoke handler.
         * @param activity
         *            The activity to post replies to.
         * Returns true if message was submitted successfully.
         */
        public bool SubmitMessage(BabelMessage message, IMessageHandler handler, Shell shell,int numReplies) {
            if (message != null && (OutgoingQueue.Size() < OutgoingCapacity)) {
                OutgoingQueue.Add(new MessageBinder(this, message, handler, MessageHandlerKind.Shell, shell, message.SenderId, numReplies));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Internal. Called by message dispatcher to register a callback
        /// for a message reply.
        /// </summary>
        /// <param name="binder">The callback wanting a reply.</param>
        public void SubmitWaiter(MessageBinder binder) {
            lock (WaitingForReply) {
                WaitingForReply.Add(binder);
            }
        }

        /// <summary>
        /// Incoming messages get routed to here by the MediatorNetIf.
        /// </summary>
        /// <param name="message"></param>
        public void SubmitIncomingMessage(BabelMessage message) {
            if (message != null) {
                IncomingQueue.Add(message);
            }
        }

        /// <summary>
        /// Thread for servicing the dispatch Q.
        /// Dispatches messages to the Babel netIfs.
        /// </summary>
        private class MessageDispatcherThread {

            MessageExchange Exchange;
            public Thread Task;

            public MessageDispatcherThread(MessageExchange exchange) {
                Exchange = exchange;
                Task = new Thread(new ThreadStart(Run));
                Task.Name = "MessageDispatcherThread";
                Task.Priority = ThreadPriority.AboveNormal;
            }

            public void Start() { if (Task != null) Task.Start(); }

            public void Run() {
                MessageBinder binder;
                while (!Exchange.IsClosing) {
                    try {
                        while ((!Exchange.IsClosing) && ((binder = Exchange.OutgoingQueue.Take()) != null)) {
                            if (!binder.Message.DispatchMessage(binder)) {
                                if (binder.Message.LastError == BabelMessage.MessageError.None) {
                                    Exchange.OutgoingQueue.AddFirst(binder);
                                    // Either Q full or disconnected, so sleep for a bit.
                                    Thread.Sleep(200);
                                } else { // Unsendable, notify sender.
                                    binder.CallHandler();
                                }
                            }
                        }
                    } catch (ThreadInterruptedException) {
                        break;
                    } catch (Exception e) {
                        if (Exchange.IsClosing) break;
                        if (Settings.DebugLevel > 0)
                            Log.d(TAG, "MessageDispatcherThread exception:" + e.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Thread for servicing the incoming Q.
        /// Relays messages to listeners and waiters.
        /// </summary>
        private class MessageReceiverThread {
            MessageExchange Exchange;
            public Thread Task;

            public MessageReceiverThread(MessageExchange exchange) {
                Exchange = exchange;
                Task = new Thread(new ThreadStart(Run));
                Task.Name = "MessageReceiverThread";
                Task.Priority = ThreadPriority.AboveNormal;
            }

            public void Start() { if (Task != null) Task.Start(); }

            public void Run() {
                long staleTime;
                BabelMessage message;
                List<MessageBinder> removals = new List<MessageBinder>();
                while (!Exchange.IsClosing) {
                    try {
                        while ((!Exchange.IsClosing) && ((message = Exchange.IncomingQueue.Take()) != null)) {
                            // Relay a message to interested parties.
                            // Note: You must use different identifiers to control which caller processes response.
                            // long milliseconds = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                           // staleTime = (Environment.TickCount & Int32.MaxValue) - 60000 * Exchange.StaleMinutes;
                            staleTime = DateTime.UtcNow.Ticks - 60 * 1000 * 10000 * Exchange.StaleMinutes;
                            // For message, check for first Ident match in WaitingForReply list.
                            lock (Exchange.WaitingForReply) {
                                removals.Clear();
                                foreach (MessageBinder q in Exchange.WaitingForReply) {
                                    if (q.Handler != null && q.Message.SenderId == message.SenderId) {
                                        q.Reply = message;
                                        q.CallHandler();
                                        if (q.NumReplies > 0) {
                                            if (--q.NumReplies == 0)
                                                removals.Add(q);
                                        }
                                        break;
                                    }
                                    if ((q.NumReplies > 0) && (q.ElapsedTime < staleTime)) {
                                        removals.Add(q);
                                    }
                                }
                            }
                            foreach (MessageBinder q in removals) {
                                Exchange.WaitingForReply.Remove(q);
                            }
                            removals.Clear();
                            lock (Exchange.IncomingListeners) {
                                foreach (MessageBinder q in Exchange.IncomingListeners) {
                                    if (q.Handler != null && (q.Message.SenderId == message.SenderId || q.Message.SenderId == (byte)0xff)) {
                                        q.Reply = message;
                                        q.CallHandler();
                                    }
                                }
                            }
                        }
                    } catch (ThreadInterruptedException) {
                        break;
                    } catch (Exception e) {
                        if (Exchange.IsClosing) break;
                        if (Settings.DebugLevel > 0)
                            Log.d(TAG, "MessageReceiverThread exception:" + e.Message);
                    }
                }
            }
        }
    }
}