using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using Babel.Core;
using Babel.XLink;

namespace Babel.BabelProtocol {

    public class LinkIOThreads {

        public static String TAG = "LinkIOThreads";

        private bool Started;
        public LinkDriver LnkPrt;
        public LinkSchedulerThread SchedulerThread;
        public LinkReadThread ReadThread;
        public LinkWriteThread WriteThread;

        public LinkIOThreads(LinkDriver lp) {
            LnkPrt = lp;
            Started = false;
            SchedulerThread = null;
            ReadThread = null;
            WriteThread = null;
        }

        public void StartLink() {
            if (!Started) {
                Started = true;
                // Create threads as necessary.
                WriteThread = new LinkWriteThread(this, LnkPrt);
                if (LnkPrt.DoesIO || LnkPrt.HasTasks) {
                    SchedulerThread = new LinkSchedulerThread(this, LnkPrt);
                    if (LnkPrt.DoesIO)
                        ReadThread = new LinkReadThread(this, LnkPrt);
                }
                // Now start threads.         
                if (ReadThread != null)
                    ReadThread.Start();
                if (WriteThread != null)
                    WriteThread.Start();
                if (SchedulerThread != null)
                    SchedulerThread.Start();
            }
        }

        public void StopLink() {
            Started = false;
            if (SchedulerThread != null) {
                Primitives.Interrupt(SchedulerThread.Task);
                SchedulerThread = null;
            }
            if (ReadThread != null) {
                Primitives.Interrupt(ReadThread.Task);
                ReadThread = null;
            }
            if (WriteThread != null) {
                Primitives.Interrupt(WriteThread.Task);
                WriteThread = null;
            }
        }

        // Schedules regular actions on the port:
        // 1) Heart beat if required.
        // 2) Determine if link is working or unresponsive.
        // 3) Pop & push resend requests, issue resend.
        public class LinkSchedulerThread {

            LinkIOThreads Master;
            LinkDriver LnkPrt;
            public Thread Task;

            public LinkSchedulerThread(LinkIOThreads master, LinkDriver lp) {
                Master = master;
                LnkPrt = lp;
                Task = new Thread(new ThreadStart(Run));
                Task.Name = "LinkSchedulerThread:" + LnkPrt.NetIfIndex;
                Task.Priority = ThreadPriority.Highest;
            }

            public void Start() { if (Task != null) Task.Start(); }

            public void Run() {
                uint lastResponseCheckTime = Primitives.GetBabelMilliTicker();
                for (; ; ) {
                    if (!Master.Started) break;
                    try {
                        uint curTime = Primitives.GetBabelMilliTicker();
                        if (LnkPrt.HasTasks && (LnkPrt.NumTasks > 0))
                            LnkPrt.ServiceTasks(curTime, LinkTaskKind.Service);
                        if (LnkPrt.DoesIO) {
                            if ((curTime - lastResponseCheckTime) > LnkPrt.ResponseInterval) {
                                lastResponseCheckTime = curTime;
                                if (LnkPrt.HasRead) {
                                    LnkPrt.HasRead = false;
                                    if (LnkPrt.IoNetIfDevice.GetComponentState() == ComponentState.Unresponsive) {
                                        LnkPrt.IoNetIfDevice.NotifyStateChange(ComponentState.Working);
                                    }
                                } else if (LnkPrt.IoNetIfDevice.GetComponentState() == ComponentState.Working) {
                                    LnkPrt.IoNetIfDevice.NotifyStateChange(ComponentState.Unresponsive);
                                }
                            }
                            if (LnkPrt.IoNetIfDevice.HasHeartBeat()) {
                                if (LnkPrt.GetWriteQueueSize() == 0) {
                                    LnkPrt.LinkPing();
                                }
                            }
                            if (LnkPrt.LinkMissingQueue.Size() != 0)
                                LnkPrt.Monitor.LinkProtocol();
                        }
                        Thread.Sleep(LnkPrt.SchedulerInterval);
                    } catch (ThreadInterruptedException) {
                        break;
                    } catch (Exception) {
                    }
                }
            }
        }

        // Convert raw byte stream into messages, put on read Q.
        public class LinkReadThread {

            LinkIOThreads Master;
            LinkDriver LnkPrt;
            public Thread Task;

            public LinkReadThread(LinkIOThreads master, LinkDriver lp) {
                Master = master;
                LnkPrt = lp;
                Task = new Thread(new ThreadStart(Run));
                Task.Name = "LinkReadThread:" + LnkPrt.NetIfIndex;
                Task.Priority = ThreadPriority.Highest;
            }

            public void Start() { if (Task != null) Task.Start(); }

            public void Run() {
                if (LnkPrt.Parser != null) {
                    LnkPrt.Parser.IoIndex = -1;
                }
                LnkPrt.vnoLastInput = (ProtocolConstants.VNO_SIZE - 1);
                LnkPrt.InputReset = false;
                LnkPrt.NumIOAttempts = 0;
                for (; ; ) {
                    if (!Master.Started) break;
                    try {
                        LnkPrt.LinkRead(true);
                    } catch (ThreadInterruptedException) {
                        break;
                    } catch (Exception e) {
                        if (Settings.DebugLevel > 6)
                            Log.d(TAG, "LinkReadThread " + e.Message + "\n");
                    }
                }
            }
        }

        // Pop messages off write Q and send via link.
        public class LinkWriteThread {

            LinkIOThreads Master;
            LinkDriver LnkPrt;
            public Thread Task;

            public LinkWriteThread(LinkIOThreads master, LinkDriver lp) {
                Master = master;
                LnkPrt = lp;
                Task = new Thread(new ThreadStart(Run));
                Task.Name = "LinkWriteThread:" + LnkPrt.NetIfIndex;
                Task.Priority = ThreadPriority.AboveNormal;
            }

            public void Start() { if (Task != null) Task.Start(); }

            public void Run() {
                LnkPrt.NumIOAttempts = 0;
                LnkPrt.vnoLastOutput = 0;
                for (; ; ) {
                    if (!Master.Started) break;
                    try {
                        LnkPrt.LinkWriteQueue.WaitWhileEmpty();
                        LnkPrt.LinkWrite();
                    } catch (ThreadInterruptedException) {
                        break;
                    } catch (Exception) {
                    }
                }
            }
        }
    }
}