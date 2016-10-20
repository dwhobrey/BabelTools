using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Babel.Core {

    public class Gate {
        volatile bool IsOpen; // Flag to indicate when gate is open.
        Mutex AccessLock;
        ConditionVariable Ready; // Indicates when threads are enabled.

        public Gate(bool isOpen=false) {
            IsOpen = isOpen;
            AccessLock = new Mutex();
            Ready = new ConditionVariable();
        }

        public bool GetState() {
            return IsOpen;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isOpen"></param>
        /// throws ThreadInterruptedException.
        public void SetState(bool isOpen) {
            AccessLock.WaitOne();
            try {
                IsOpen = isOpen;
                if (isOpen) {
                    Ready.BroadcastCondition();
                }
            } finally {
                AccessLock.ReleaseMutex();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cv"></param>
        /// <param name="cvLock"></param>
        /// <param name="millisecondsTimeout"></param>
        /// <returns></returns>
        /// throws ThreadInterruptedException.
        private int Wait(ConditionVariable cv, Mutex cvLock, int millisecondsTimeout) {
            long start = DateTime.UtcNow.Ticks;
            if (cv.WaitCondition(cvLock, millisecondsTimeout)) {
                if (millisecondsTimeout > 0) {
                    long diff = DateTime.UtcNow.Ticks - start;
                    millisecondsTimeout -= (int)(diff / 10000);
                    if (millisecondsTimeout < 0) millisecondsTimeout = 0;
                }
            }
            return millisecondsTimeout;
        }

        /// <summary>
        /// Waits until gate is open.
        /// </summary>
        /// <param name="millisecondsTimeout"></param>
        /// <returns>Returns true when open, otherwise false if timeout occurred.</returns>
        /// throws ThreadInterruptedException.
        public bool WaitUntilOpen(int millisecondsTimeout = -1) {
            AccessLock.WaitOne();
            try {
                while (!IsOpen) {
                    if (millisecondsTimeout == 0)
                        return false;
                    millisecondsTimeout = Wait(Ready, AccessLock, millisecondsTimeout);
                }
                return true;
            } finally {
                try {
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
        }
    }
}
