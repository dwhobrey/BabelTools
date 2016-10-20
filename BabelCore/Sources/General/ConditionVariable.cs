using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

namespace Babel.Core {

    // The condition variables strategy used here is based on that proposed by
    // Douglas Schmidt and Irfan Pyarali, Dept Comp Sci, Washington Uni, Missouri.
    // See http://www.cs.wustl.edu/~schmidt/win32-cv-1.html.
    public class ConditionVariable {
        byte waitersCount;		// Number of waiting threads.
        bool wasBroadcast;		// Keeps track of whether we were broadcasting or signaling.
        // This allows us to optimize the code if we're just signaling.
        Mutex waitersCountLock;	// Serialize access to <waitersCount>.
        Semaphore sema;		// Semaphore used to queue up threads waiting for the condition to become signaled.
        AutoResetEvent waitersDone;	// An auto-reset event used by the broadcast/signal thread to wait
        // for all the waiting thread(s) to wake up and be released from the semaphore.


        // c# kludge to manage interrupting threads waiting on a WaitHandle.
        bool wasInterrupted;
        Mutex currentWaitMutex;
        Semaphore currentWaitSemaphore;
        static ConcurrentDictionary<Thread, ConditionVariable> Waiters = new ConcurrentDictionary<Thread, ConditionVariable>();

        public ConditionVariable() {
            waitersCount = 0;
            wasBroadcast = false;
            wasInterrupted = false;
            currentWaitMutex = null;
            currentWaitSemaphore = null;
            sema = new Semaphore(0, 1000);
            waitersCountLock = new Mutex();
            waitersDone = new AutoResetEvent(false);
        }

        private void RegisterWaiter(Mutex waitMutex,Semaphore waitSemaphore) {
            currentWaitMutex = waitMutex;
            currentWaitSemaphore = waitSemaphore;
            Waiters.TryAdd(Thread.CurrentThread, this);
        }

        private void UnregisterWaiter() {
            ConditionVariable v = null;
            currentWaitMutex = null;
            currentWaitSemaphore = null;
            Waiters.TryRemove(Thread.CurrentThread, out v);
        }

        // Checks if the specified thread is waiting on a condition variable and interrupts it if it is.
        // This is kludge to get round c# interrupt not being able to interrupt WaitHandles!
        public static void Interrupt(Thread t) {
            ConditionVariable v = null;
            if (Waiters.TryGetValue(t, out v)) {
                if (v != null) {
                    try {
                        v.wasInterrupted = true;
                        // This should throw an exception because we don't own the Mutex, but it doesn't?
                        if(v.currentWaitMutex != null) v.currentWaitMutex.ReleaseMutex(); 
                        if (v.currentWaitSemaphore != null) v.currentWaitSemaphore.Release(1);
                    } catch (Exception e) {
                        Log.w("ConditionVariable", "Exception interrupting thread:" + e.Message);
                    }
                }
            }
        }

        //  Wait for condition variable to be signaled. 
        //  The mutex must be owned before entry, i.e. call accessLock.WaitOne().
        //  throws ThreadInterruptedException.
        public bool WaitCondition(Mutex accessLock, int millisecondsTimeout = -1) { 
            bool lastWaiter,timedOut;
            try {
                // Avoid race conditions.
                waitersCountLock.WaitOne();
                waitersCount++;
                waitersCountLock.ReleaseMutex();

                // This call atomically releases the mutex and waits on the semaphore
                // until <SignalCondition> or <BroadcastCondition>
                // are called by another thread.
                RegisterWaiter(null, sema);
                timedOut = !WaitHandle.SignalAndWait(accessLock, sema, millisecondsTimeout, false);
                UnregisterWaiter();

                waitersCountLock.WaitOne();
                waitersCount--;
                lastWaiter = wasBroadcast && (waitersCount == 0);
                waitersCountLock.ReleaseMutex();

                if (wasInterrupted) throw new ThreadInterruptedException();
                if (timedOut) return false;

                // Check to see if we're the last waiter after <BroadcastCondition>.
                // If we're the last waiter thread during this particular broadcast
                // then let all the other threads proceed.
                if (lastWaiter) {
                    // This call atomically signals the <waitersDone> event and waits until
                    // it can acquire the mutex.  This is required to ensure fairness. 
                    RegisterWaiter(accessLock, null);
                    timedOut = !WaitHandle.SignalAndWait(waitersDone, accessLock, millisecondsTimeout, false);
                    UnregisterWaiter();
                    if (wasInterrupted) throw new ThreadInterruptedException();
                    if (timedOut) return false;
                } else {
                    // Always regain the external mutex since that's the guarantee we
                    // give to our callers. 
                    if (!accessLock.WaitOne(millisecondsTimeout)) return false;
                }
                return true;
            } finally {
                if (wasInterrupted) {
                    wasInterrupted = false;
                    UnregisterWaiter();
                }         
            }
        }

        // Signal the condition.
        // throws ThreadInterruptedException.
        public void SignalCondition() {
            bool haveWaiters;

            waitersCountLock.WaitOne();
            haveWaiters = (waitersCount > 0);
            waitersCountLock.ReleaseMutex();

            // If there aren't any waiters, then this is a no-op.  
            if (haveWaiters)
                sema.Release(1);
        }

        // Broadcast the condition to all waiting threads.
        // throws ThreadInterruptedException.
        public void BroadcastCondition() {
            bool haveWaiters = false;

            // This is needed to ensure that <waitersCount> and <wasBroadcast> are
            // consistent relative to each other.
            waitersCountLock.WaitOne();
            haveWaiters = false;
            if (waitersCount > 0) {
                // We are broadcasting, even if there is just one waiter...
                // Record that we are broadcasting, which helps optimize
                // <WaitCondition> for the non-broadcast case.
                wasBroadcast = true;
                haveWaiters = true;
            }

            if (haveWaiters) {
                // Wake up all the waiters atomically.
                sema.Release(waitersCount);
                waitersCountLock.ReleaseMutex();

                // Wait for all the awakened threads to acquire the counting semaphore. 
                waitersDone.WaitOne();
                // This assignment is okay, even without the <waitersCountLock> held 
                // because no other waiter threads can wake up to access it.
                wasBroadcast = false;
            } else
                waitersCountLock.ReleaseMutex();
        }
    }
}