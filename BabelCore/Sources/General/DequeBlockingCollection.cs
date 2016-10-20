using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Babel.Core {

    /// <summary>
    /// Provides a thread safe blocking collection based on an array.
    /// Allows additions to head and tail. 
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    public class DequeBlockingCollection<T> {
        protected volatile int Head;
        protected volatile int Tail;
        protected volatile int Count;
        protected int MaxCapacity;
        protected T Default;
        protected T[] ContainerList;
        protected Mutex AccessLock;
        protected ConditionVariable NotEmpty;
        protected ConditionVariable NotFull;

        public DequeBlockingCollection(int capacity, T defaultValue = default(T)) {
            if (capacity <= 0) throw new System.ArgumentException("Capacity cannot be negative.", "capacity");
            Head = 0;
            Tail = 0;
            Count = 0;
            MaxCapacity = capacity;
            Default = defaultValue;
            AccessLock = new Mutex();
            NotEmpty = new ConditionVariable();
            NotFull = new ConditionVariable();
            ContainerList = new T[capacity];
        }

        public DequeBlockingCollection(DequeBlockingCollection<T> q) {
            q.AccessLock.WaitOne();
            try {
                Head = q.Head;
                Tail = q.Tail;
                Count = q.Count;
                MaxCapacity = q.MaxCapacity;
                Default = q.Default;
                AccessLock = new Mutex();
                NotEmpty = new ConditionVariable();
                NotFull = new ConditionVariable();
                ContainerList = new T[MaxCapacity];
                for (int k = 0; k < MaxCapacity; k++) ContainerList[k] = q.ContainerList[k];
            } finally {
                q.AccessLock.ReleaseMutex();
            }
        }

        /// <summary>
        /// Resets the collection.
        /// </summary>
        /// throws ThreadInterruptedException.
        public void Clear() { 
            AccessLock.WaitOne();
            try {
                // Set each cell to null for gc.
                for (int k = 0; k < MaxCapacity; k++) ContainerList[k] = default(T);
                Head = 0;
                Tail = 0;
                Count = 0;
                NotFull.BroadcastCondition();
            } finally {
                AccessLock.ReleaseMutex();
            }
        }

        public int Capacity() {
            return MaxCapacity;
        }

        /// <summary>
        /// The current number of elements in the collection.
        /// </summary>
        /// <returns>Returns a count of the number of elements in the Collection.</returns>
        public int Size() {
            return Count;
        }

        /// <summary>
        /// Waits on Condition Variable and Access Lock.
        /// Precondition: AccessLock must be held on entry.
        /// </summary>
        /// <param name="cv"></param>
        /// <param name="cvLock"></param>
        /// <param name="millisecondsTimeout"></param>
        /// <returns>Returns the time remaining.</returns>
        /// throws ThreadInterruptedException.
        private int Wait(ConditionVariable cv, Mutex cvLock, int millisecondsTimeout) {
            long start = DateTime.UtcNow.Ticks;
            if (cv.WaitCondition(cvLock, millisecondsTimeout)) {
                if (millisecondsTimeout > 0) {
                    long diff = DateTime.UtcNow.Ticks - start;
                    millisecondsTimeout -= (int)(diff / 10000);
                    if (millisecondsTimeout < 0) millisecondsTimeout = 0;
                }
            } else return 0;
            return millisecondsTimeout;
        }

        /// <summary>
        /// Waits until collection contains an item.
        /// </summary>
        /// <param name="millisecondsTimeout"></param>
        /// <returns>Returns true when not empty, otherwise false if timeout occurred.</returns>
        /// throws ThreadInterruptedException.
        public bool WaitWhileEmpty(int millisecondsTimeout=-1) {
            AccessLock.WaitOne();
            try {
                while (Head == Tail) {
                    if (millisecondsTimeout == 0)
                        return false;
                    millisecondsTimeout = Wait(NotEmpty,AccessLock,millisecondsTimeout);
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

        /// <summary>
        /// Get & remove element from head of Q.
        /// </summary>
        /// <returns>Returns head element, or Default if queue is empty.</returns>
        /// throws ThreadInterruptedException.
        public T Pop() {
            T v = Default;
            AccessLock.WaitOne();
            try {
                if (Head != Tail) {
                    v = ContainerList[Head++];
                    Head %= MaxCapacity;
                    if (Count > 0) --Count;
                    NotFull.SignalCondition();
                }
            } finally {
                AccessLock.ReleaseMutex();
            }
            return v;
        }

        /// <summary>
        /// Only pops value if it equals v.
        /// </summary>
        /// <param name="v"></param>
        /// <returns>Returns v, or Default if not equal.</returns>
        /// throws ThreadInterruptedException.
        public T PopIfEqual(T v) {
            T a = Default;
            AccessLock.WaitOne();
            try {
                if (Head != Tail) {
                    if (EqualityComparer<T>.Default.Equals(ContainerList[Head],v)) {
                        a = ContainerList[Head++];
                        Head %= MaxCapacity;
                        if (Count > 0) --Count;
                        NotFull.SignalCondition();
                    }
                }
            } finally {
                AccessLock.ReleaseMutex();
            }
            return a;
        }

        /// <summary>
        /// Get, without removal, element at head of Q.
        /// </summary>
        /// <returns>Returns head element, or Default if queue is empty.</returns>
        /// throws ThreadInterruptedException.
        public T Peek() {
            T v = Default;
            AccessLock.WaitOne();
            try {
                if (Head != Tail) {
                    v = ContainerList[Head];
                }
            } finally {
                AccessLock.ReleaseMutex();
            }
            return v;
        }

        /// <summary>
        /// Add v to tail of Q.
        /// </summary>
        /// <param name="v"></param>
        /// <returns>Returns v, or Default if queue is full.</returns>
        /// throws ThreadInterruptedException.
        public T Push(T v) {
            AccessLock.WaitOne();
            try {
                if (((Tail + 1) % MaxCapacity) == Head)
                    v = Default;
                else {
                    ContainerList[Tail++] = v;
                    Tail %= MaxCapacity;
                    if (Count < MaxCapacity) ++Count;
                    NotEmpty.SignalCondition();
                }
            } finally {
                AccessLock.ReleaseMutex();
            }
            return v;
        }

        /// <summary>
        /// Add v to head rather than tail.
        /// </summary>
        /// <param name="v"></param>
        /// <returns>Returns v, or Default if queue is full.</returns>
        /// throws ThreadInterruptedException.
        public T PushFirst(T v) {
            AccessLock.WaitOne();
            try {
                if (((Tail + 1) % MaxCapacity) == Head)
                    v = Default;
                else {
                    if (Head == Tail) {
                        ContainerList[Tail++] = v;
                        Tail %= MaxCapacity;
                    } else {
                        if (Head == 0)
                            Head = MaxCapacity;
                        ContainerList[--Head] = v;
                    }
                    if (Count < MaxCapacity) ++Count;
                    NotEmpty.SignalCondition();
                }
            } finally {
                AccessLock.ReleaseMutex();
            }
            return v;
        }

        /// <summary>
        /// Add to tail, removes head element if full.
        /// </summary>
        /// <param name="v"></param>
        /// <returns>Returns head element if removed, or Default.</returns>
        /// throws ThreadInterruptedException.
        public T PushForce(T v) {
            T w = Default;
            AccessLock.WaitOne();
            try {
                if (((Tail + 1) % MaxCapacity) == Head) {
                    w = ContainerList[Head++];
                    Head %= MaxCapacity;
                    --Count;
                }
                ContainerList[Tail++] = v;
                Tail %= MaxCapacity;
                if (Count < MaxCapacity) ++Count;
                NotEmpty.SignalCondition();
            } finally {
                AccessLock.ReleaseMutex();
            }
            return w;
        }

        /// <summary>
        /// Finds element index in Q.
        /// </summary>
        /// <param name="v"></param>
        /// <returns>Returns idx, or -1 if not found.</returns>
        /// throws ThreadInterruptedException.
        public int FindFirstOccurrenceIndex(T v) {
            int k, idx = -1;
            AccessLock.WaitOne();
            try {
                k = Head;
                while (k != Tail) {
                    if (EqualityComparer<T>.Default.Equals(ContainerList[k],v)) {
                        idx = k;
                        break;
                    }
                    ++k;
                    k %= MaxCapacity;
                }
            } finally {
                AccessLock.ReleaseMutex();
            }
            return idx;
        }

        /// <summary>
        /// Returns true if collection element v equals w.
        /// </summary>
        /// <param name="v"></param>
        /// <param name="w"></param>
        /// <returns></returns>
        public delegate bool ComparisonDelegate(T v, object w);

        /// <summary>
        /// Finds the element that satisfies the comparison delegate.
        /// </summary>
        /// <param name="d">Comparison delegate.</param>
        /// <param name="w">Parameter passed to delegate for comparison.</param>
        /// <returns>Returns the found element or Default if not found.</returns>
        /// throws ThreadInterruptedException.
        public T FindFirstOccurrence(ComparisonDelegate d,object w) {
            int k; T v = Default;
            AccessLock.WaitOne();
            try {
                k = Head;
                while (k != Tail) {
                    if (d(ContainerList[k],w)) {
                        v = ContainerList[k];
                        break;
                    }
                    ++k;
                    k %= MaxCapacity;
                }
            } finally {
                AccessLock.ReleaseMutex();
            }
            return v;
        }

        /// <summary>
        /// Remove item from Q, close gap.
        /// </summary>
        /// <param name="v"></param>
        /// <returns>Returns true if removed or false if not found.</returns>
        /// throws ThreadInterruptedException.
        public bool RemoveFirstOccurrence(T v) {
            int t, k, j;
            bool n = false;
            AccessLock.WaitOne();
            try {
                k = t = Head;
                while (k != Tail) {
                    if (EqualityComparer<T>.Default.Equals(ContainerList[k],v)) {
                        if (k >= t) {
                            if (k > t) {
                                for (j = k; j > t; j--) {
                                    ContainerList[j] = ContainerList[j - 1];
                                }
                            }
                            ++Head;
                            Head %= MaxCapacity;
                        } else {
                            t = Tail;
                            for (j = k; j < t; j++) {
                                ContainerList[j] = ContainerList[j + 1];
                            }
                            --Tail;
                        }
                        n = true;
                        --Count;
                        NotFull.SignalCondition();
                        break;
                    }
                    ++k;
                    k %= MaxCapacity;
                }
            } finally {
                AccessLock.ReleaseMutex();
            }
            return n;
        }
    }
}