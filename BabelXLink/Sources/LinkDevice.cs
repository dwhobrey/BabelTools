using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

using Babel.Core;
using Babel.XLink;

namespace Babel.XLink {

    /// <summary>
    /// Base class for wrapping raw device drivers.
    /// It performs the following functions:
    /// a) Defines the device driver interface.
    /// b) Provides pinned buffers.
    /// c) Sets up read/write queues.
    /// d) Provides dispatch threads for queues.
    /// e) Manages device event notificaton.
    /// 
    /// Link Devices are typically attached to a transport protocol handler.
    /// </summary>
    public class LinkDevice : Component, ILinkNetIf {

        protected bool IsReading;
        protected bool IsWriting;
        protected bool IsInterrupted;
        protected bool IsClosing;
        protected bool EnableListeners;
        protected int ReadsCount;
        protected int WritesCount;
        protected Object DeviceLock;
        protected Gate SuspendGate;
        LinkedBlockingCollection<byte[]> WriteQueue;
        BlockingCollection<byte[]> ReadQueue;
        LinkDeviceReadThread ReadThread;
        LinkDeviceWriteThread WriteThread;
        LinkDeviceListenerThread ListenerThread;
        InterruptSource Interrupt;

        public override string ToString() {
            return Id + ",State=" + State.Name;
        }

        // Clears the r/w queues.
        public void Clear(bool readQueue = true, bool writeQueue = true) {
            byte[] p = null;
            ReadsCount = 0;
            WritesCount = 0;
            if (writeQueue) 
                WriteQueue.Clear();
            if (readQueue) 
                while (ReadQueue.TryTake(out p)) ;
        }
        public bool Compare(LinkDevice d) {
            return this == d;
        }
        public virtual bool HasHeartBeat() {
            return true; // Masters always have a heart beat.
        }
        public int GetWriteQueueSize() {
            return WriteQueue.Size();
        }
        public int GetIOCount(bool reads) {
            return reads ? ReadsCount : WritesCount;
        }
        public void ResetIOCounters() {
            ReadsCount = 0;
            WritesCount = 0;
        }

        protected LinkDevice() {
            Interrupt = new InterruptSource();
            IsClosing = false;
            IsReading = false;
            IsWriting = false;
            IsInterrupted = false;
            EnableListeners = true;
            ReadsCount = 0;
            WritesCount = 0;
            DeviceLock = new Object();
            SuspendGate = new Gate(true);
            WriteQueue = new LinkedBlockingCollection<byte[]>();
            ReadQueue = new BlockingCollection<byte[]>();
            ReadThread = null;
            WriteThread = null;
            ListenerThread = null;
        }

        protected void InitThreads() {
            IsInterrupted = false;
            SuspendGate.SetState(true);
            if (EnableListeners && ListenerThread == null) {
                ListenerThread = new LinkDeviceListenerThread(this);
                ListenerThread.Start();
            }
            if (ReadThread == null) {
                ReadThread = new LinkDeviceReadThread(this);
                ReadThread.Start();
            }
            if (WriteThread == null) {
                WriteThread = new LinkDeviceWriteThread(this);
                WriteThread.Start();
            }
        }

        public virtual void Close() {
            lock (DeviceLock) {
                IsClosing = true;
                IsInterrupted = true;
            }
            Suspend();
            NotifyStateChange(ComponentState.Closing);
            if (WriteThread != null) {
                WriteThread.Task.Abort();
                WriteThread = null;
            }
            if (ReadThread != null) {
                ReadThread.Task.Abort();
                ReadThread = null;
            }
            if (ListenerThread != null) {
                ListenerThread.Task.Abort();
                ListenerThread = null;
            }
            // LinkManager.Manager.DeleteDevice(Id);
            DriverClose();
        }

        protected virtual void DriverClose() {
        }

        public virtual void Suspend() {
            lock (DeviceLock) {
                SuspendGate.SetState(false);
                if(State!=ComponentState.Suspended) {
                    NotifyStateChange(ComponentState.Suspended);
                    LinkManager.Manager.ComponentEventListener(ComponentEvent.ComponentSuspend, this, null);
                }
                try {
                    InterruptWrite();
                } catch (Exception) {
                }
                try { 
                    InterruptRead();
                } catch (Exception) {
                }
            }
        }

        public virtual void Resume() {
		    lock (DeviceLock) {
                if (!IsClosing) {
                    if (State != ComponentState.Working) {
                        LinkManager.Manager.ComponentEventListener(ComponentEvent.ComponentResume, this, null);
                        NotifyStateChange(ComponentState.Working);
                    }
                    InitThreads();
                }
		    } 
	    }

        protected virtual void InterruptRead() {
        }
        protected virtual void InterruptWrite() {
        }

        /// <summary>
        /// Low level blocking read.
        /// </summary>
        /// <returns>Returns byte array on success.</returns>
        protected virtual byte[] DriverRead() {
            return null;
        }

        /// <summary>
        /// Low level blocking write.
        /// </summary>
        /// <returns>Returns number of bytes written on success.</returns>
        protected virtual int DriverWrite(byte[] p) {
            return 0;
        }

        // Non blocking read.
        // Returns true if successfully read.
        public byte[] Read() {
            byte[] p = null;
            ReadQueue.TryTake(out p);
            return p;
        }

        // Queue bytes for output.
        // Returns true if successfully queued.
        public bool Write(byte[] p, int offset, int length) {
            if (offset == 0 && length < 0) {
                return WriteQueue.Add(p);
            } else {
                int len = length - offset;
                if (len <= 0 || len > p.Length) return true;
                byte[] b = new byte[len];
                Array.Copy(p, offset, b, 0, len);
                return WriteQueue.Add(b);
            }
        }

        public byte[] BlockingRead() {
            byte[] p = null;
            ReadQueue.TryTake(out p, System.Threading.Timeout.Infinite, Interrupt.GetToken);
            return p;
        }

        // Read & Write need to be interruptable.

        /// <summary>
        /// Blocking read. Used by read thread.
        /// </summary>
        /// <returns>Returns null on failure.</returns>
        protected virtual byte[] BlockingDriverRead() {
            byte[] p = null;
            try {
                IsReading = true;
                if (State == ComponentState.Working || State == ComponentState.Unresponsive) {
                    p = DriverRead();
                }
            } catch (Exception) {
                // May arrive here if unplugged during read.
            } finally {
                IsReading = false;
            }
            return p;
        }

        /// <summary>
        /// Blocking write. Used by write thread.
        /// </summary>
        /// <param name="p"></param>
        /// <returns>Returns non-zero on failure.</returns>
        protected virtual int BlockingDriverWrite(byte[] p) {
            int result = 1;
            try {
                IsWriting = true;
                if (State == ComponentState.Working || State == ComponentState.Unresponsive) {
                    if (DriverWrite(p) > 0) {
                        result = 0;
                    }
                }
            } catch (Exception) {
                // May arrive here if unplugged during write.
                result = 2;
            } finally {
                IsWriting = false;
            }
            return result;
        }

        // This should only return true once the write buffer is empty: i.e. actually transferred.
        public bool WriteBufferEmpty(byte deviceContextIndex) {
            return !IsWriting && WriteQueue.Size() == 0;
        }

        public void SetDuplexKind(byte deviceContextIndex, byte duplexKind) {

        }
        public void PerformBaudAction(byte deviceContextIndex, byte rateIndex, byte baudAction) {

        }

        /// <summary>
        /// Thread method for reading and queueing read data.
        /// </summary>
        public class LinkDeviceReadThread {
            LinkDevice Dev;
            public Thread Task;
            public LinkDeviceReadThread(LinkDevice d) {
                Dev = d;
                Task = new Thread(new ThreadStart(Run));
                Task.Name = "LinkDeviceReadThread:" + d.Id;
                Task.Priority = ThreadPriority.AboveNormal;

            }
            public void Start() {
                if (Task != null) Task.Start();
            }
            public void Run() {
                byte[] p;
                while (!Dev.IsInterrupted) {
                    try {
                        while (!Dev.SuspendGate.WaitUntilOpen(-1)) ;
                        if (Dev.IsInterrupted) break;
                        p = Dev.BlockingDriverRead();
                        if (p != null && p.Length > 0) {
                            Dev.ReadQueue.Add(p);
                        }
                    } catch (ThreadInterruptedException) {
                        if (Dev.SuspendGate.GetState()) break;
                    } catch (Exception) {
                        // ignore.
                    }
                }
            }
        }

        // Thread method for writing queued data.
        public class LinkDeviceWriteThread {
            LinkDevice Dev;
            public Thread Task;
            public LinkDeviceWriteThread(LinkDevice d) {
                Dev = d;
                Task = new Thread(new ThreadStart(Run));
                Task.Name = "LinkDeviceWriteThread:" + d.Id;
                Task.Priority = ThreadPriority.AboveNormal;
            }
            public void Start() {
                if (Task != null) Task.Start();
            }
            public void Run() {
                byte[] p;
                while (!Dev.IsInterrupted) {
                    try {
                        while (!Dev.SuspendGate.WaitUntilOpen(-1)) ;
                        if (Dev.IsInterrupted) break;
                        p = Dev.WriteQueue.Take();
                        if (p != null && p.Length > 0) {
                            if (Dev.BlockingDriverWrite(p) == 0) {
                                Dev.NotifyListeners(ComponentEvent.WriteComplete, p);
                            } else {
                                Dev.WriteQueue.AddFirst(p);
                            }
                        }
                    } catch (ThreadInterruptedException) {
                        if (Dev.SuspendGate.GetState()) break;
                    } catch (Exception) {
                        // ignore.
                    }
                }
            }
        }

        /// <summary>
        /// Thread method for notifying listeners of reads.
        /// </summary>
        public class LinkDeviceListenerThread {
            LinkDevice Dev;
            public Thread Task;
            public LinkDeviceListenerThread(LinkDevice d) {
                Dev = d;
                Task = new Thread(new ThreadStart(Run));
                Task.Name = "LinkDeviceListenerThread:" + d.Id;
                Task.Priority = ThreadPriority.AboveNormal;
            }
            public void Start() {
                if (Task != null) Task.Start();
            }
            public void Run() {
                byte[] p;
                while (!Dev.IsInterrupted) {
                    try {
                        if ((p = Dev.ReadQueue.Take()) != null) {
                            Dev.NotifyListeners(ComponentEvent.ReadComplete, p);
                        }
                    } catch (ThreadInterruptedException) {
                        break;
                    } catch (Exception) {
                        // ignore.
                    }
                }
            }
        }
    }
}
