using System;
using System.Threading;
using System.Threading.Tasks;

namespace Babel.Core {

    /// <summary>
    /// Tracks token usage.
    /// For example, used for allocating and releasing list elements.
    /// </summary>
    public class TokenAllocator {
        volatile int FreeIndex;
        volatile int Count;
        int MaxCapacity;
        int Default;
        public int[] ContainerList;
        public Mutex AccessLock;
        ConditionVariable NotEmpty;
        ConditionVariable NotFull;

        public TokenAllocator(int capacity, int defaultValue = -1) {
            if (capacity <= 0) throw new System.ArgumentException("Capacity cannot be negative.", "capacity");
            MaxCapacity = capacity;
            Default = defaultValue;
            AccessLock = new Mutex();
            NotEmpty = new ConditionVariable();
            NotFull = new ConditionVariable();
            ContainerList = new int[capacity];
            Reset();
        }

        private void Reset() {
            int k;
            Count = 0;
            FreeIndex = Default;
            for (k = 0; k < MaxCapacity; k++) {
                ContainerList[k] = FreeIndex;
                FreeIndex = k;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// throws ThreadInterruptedException.
        public void Clear() {
            AccessLock.WaitOne();
            try {
                Reset();
                NotFull.BroadcastCondition();
            } finally {
                AccessLock.ReleaseMutex();
            }
        }

        public int Capacity() {
            return MaxCapacity;
        }

        /// <summary>
        /// Returns a count of the number of tokens allocated.
        /// </summary>
        /// <returns></returns>
        /// throws ThreadInterruptedException.
        public int Size() {
            return Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// throws ThreadInterruptedException.
        public int RemainingCapacity() {
            AccessLock.WaitOne();
            try {
                return MaxCapacity - Count;
            } finally {
                AccessLock.ReleaseMutex();
            }
        }

        /// <summary>
        /// Fetch a free token from list.
        /// </summary>
        /// <returns>Returns token, or Default if none free.</returns>
        /// throws ThreadInterruptedException.
        public int Allocate() {
            int idx = Default;
            AccessLock.WaitOne();
            try {
                idx = FreeIndex;
                if (idx != Default) {
                    FreeIndex = ContainerList[idx];
                    if(Count<MaxCapacity) ++Count;
                    NotEmpty.SignalCondition();
                }
            } finally {
                AccessLock.ReleaseMutex();
            }
            return idx;
        }

        /// <summary>
        /// Release token to free list.
        /// </summary>
        /// <param name="idx"></param>
        /// throws ThreadInterruptedException.
        public void Release(int idx) {
            AccessLock.WaitOne();
            try {
                if (idx < MaxCapacity) {
                    ContainerList[idx] = FreeIndex;
                    FreeIndex = idx;
                    if(Count>0) --Count;
                    NotFull.SignalCondition();
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
                    long diff = DateTime.UtcNow.Ticks -start;
                    millisecondsTimeout -= (int)(diff / 10000);
                    if (millisecondsTimeout < 0) millisecondsTimeout = 0;
                }
            }
            return millisecondsTimeout;
        }

        /// <summary>
        /// Blocks until a free token is available.
        /// Then allocates this to calling thread and returns it.
        /// </summary>
        /// <param name="millisecondsTimeout"></param>
        /// <returns></returns>
        /// throws ThreadInterruptedException.
        public int WaitUntilAllocated(int millisecondsTimeout) {
            int idx = Default;
            AccessLock.WaitOne();
            try {
                while ((idx = Allocate()) == Default) {
                    if (millisecondsTimeout == 0) break;
                    millisecondsTimeout = Wait(NotFull, AccessLock, millisecondsTimeout);
                }               
            } finally {
                try {
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
            return idx;
        }
    }
}
