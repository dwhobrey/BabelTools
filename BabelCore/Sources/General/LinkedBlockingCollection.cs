using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

/* Original (C) from Java file LinkedBlockingDeque.cs:
 * DO NOT ALTER OR REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
 *
 * This code is free software; you can redistribute it and/or modify it
 * under the terms of the GNU General Public License version 2 only, as
 * published by the Free Software Foundation.  Oracle designates this
 * particular file as subject to the "Classpath" exception as provided
 * by Oracle in the LICENSE file that accompanied this code.
 *
 * This code is distributed in the hope that it will be useful, but WITHOUT
 * ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or
 * FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License
 * version 2 for more details (a copy is included in the LICENSE file that
 * accompanied this code).
 *
 * You should have received a copy of the GNU General Public License version
 * 2 along with this work; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin St, Fifth Floor, Boston, MA 02110-1301 USA.
 *
 * Please contact Oracle, 500 Oracle Parkway, Redwood Shores, CA 94065 USA
 * or visit www.oracle.com if you need additional information or have any
 * questions.
 */

/*
 * This file is available under and governed by the GNU General Public
 * License version 2 only, as published by the Free Software Foundation.
 * However, the following notice accompanied the original version of this
 * file:
 *
 * Written by Doug Lea with assistance from members of JCP JSR-166
 * Expert Group and released to the public domain, as explained at
 * http://creativecommons.org/publicdomain/zero/1.0/
 */

namespace Babel.Core {

    public class LinkedBlockingCollection<T> {

        /** Doubly-linked list node class */
        protected sealed class Node<E> {
            /**
             * The item, or null if this node has been removed.
             */
            public E Item;

            /**
             * One of:
             * - the real predecessor Node
             * - this Node, meaning the predecessor is tail
             * - null, meaning there is no predecessor
             */
            public Node<E> Prev;

            /**
             * One of:
             * - the real successor Node
             * - this Node, meaning the successor is head
             * - null, meaning there is no successor
             */
            public Node<E> Next;

            public Node(E v, Node<E> p, Node<E> n) {
                Item = v;
                Prev = p;
                Next = n;
            }
        }

        /**
         * Pointer to first node.
         * Invariant: (first == null && last == null) ||
         *            (first.prev == null && first.item != null)
         */
        protected Node<T> First;

        /**
         * Pointer to last node.
         * Invariant: (first == null && last == null) ||
         *            (last.next == null && last.item != null)
         */
        protected Node<T> Last;

        protected T Default;

        /** Number of items in the deque */
        protected int Count;

        /** Maximum number of items in the deque */
        protected int MaxCapacity;

        /** Main lock guarding all access */
        protected Mutex AccessLock;

        /** Condition for waiting takes */
        protected ConditionVariable NotEmpty;

        /** Condition for waiting puts */
        protected ConditionVariable NotFull;

        /**
         * Creates a {@code LinkedBlockingDeque} with a capacity of
         * {@link Integer#MAX_VALUE}.
         */
        public LinkedBlockingCollection()
            : this(Int32.MaxValue, default(T)) {
        }

        /**
         * Creates a {@code LinkedBlockingDeque} with the given (fixed) capacity.
         *
         * @param capacity the capacity of this deque
         * @throws IllegalArgumentException if {@code capacity} is less than 1
         */
        public LinkedBlockingCollection(int capacity, T defaultValue = default(T)) {
            if (capacity <= 0) throw new System.ArgumentException("Capacity cannot be negative.", "capacity");
            Count = 0;
            MaxCapacity = capacity;
            Default = defaultValue;
            AccessLock = new Mutex();
            NotEmpty = new ConditionVariable();
            NotFull = new ConditionVariable();
        }

        /**
         * Creates a {@code LinkedBlockingDeque} with a capacity of
         * {@link Integer#MAX_VALUE}, initially containing the elements of
         * the given collection, added in traversal order of the
         * collection's iterator.
         *
         * @param c the collection of elements to initially contain
         * @throws NullPointerException if the specified collection or any
         *         of its elements are null
         * throws ThreadInterruptedException.
         */
        public LinkedBlockingCollection(ICollection<T> c)
            : this(Int32.MaxValue) {
            AccessLock.WaitOne();
            try {
                foreach (T x in c) {
                    if (x == null)
                        throw new System.NullReferenceException();
                    if (!LinkLast(x))
                        throw new System.ArgumentOutOfRangeException("Deque full.");
                }
            } finally {
                AccessLock.ReleaseMutex();
            }
        }


        #region // Basic linking and unlinking operations, called only while holding lock.

        /**
         * Links node as first element, or returns false if full.
         */
        private bool LinkFirst(T v) {
            // assert lock.isHeldByCurrentThread();
            if (Count >= MaxCapacity) return false;
            Node<T> f = First;
            Node<T> x = new Node<T>(v, null, f);
            First = x;
            if (Last == null) {
                Last = x;
            } else {
                f.Prev = x;
            }
            ++Count;
            NotEmpty.SignalCondition();
            return true;
        }

        /**
         * Links node as last element, or returns false if full.
         */
        private bool LinkLast(T v) {
            // assert lock.isHeldByCurrentThread();
            if (Count >= MaxCapacity) return false;
            Node<T> l = Last;
            Node<T> x = new Node<T>(v, l, null);
            Last = x;
            if (First == null) {
                First = x;
            } else {
                l.Next = x;
            }
            ++Count;
            NotEmpty.SignalCondition();
            return true;
        }

        /**
         * Removes and returns first element, or Default if empty.
         */
        private T UnlinkFirst() {
            Node<T> f = First;
            if (f == null)
                return Default;
            Node<T> n = f.Next;
            T item = f.Item;
            f.Item = Default;
            f.Next = f; // help GC
            First = n;
            if (n == null)
                Last = null;
            else
                n.Prev = null;
            --Count;
            NotFull.SignalCondition();
            return item;
        }

        /**
         * Removes and returns last element, or Default if empty.
         */
        private T UnlinkLast() {
            Node<T> l = Last;
            if (l == null)
                return Default;
            Node<T> p = l.Prev;
            T item = l.Item;
            l.Item = Default;
            l.Prev = l; // help GC
            Last = p;
            if (p == null)
                First = null;
            else
                p.Next = null;
            --Count;
            NotFull.SignalCondition();
            return item;
        }

        /**
         * Unlinks x.
         */
        void Unlink(Node<T> x) {
            Node<T> p = x.Prev;
            Node<T> n = x.Next;
            if (p == null) {
                UnlinkFirst();
            } else if (n == null) {
                UnlinkLast();
            } else {
                p.Next = n;
                n.Prev = p;
                x.Item = Default;
                // Don't mess with x's links.  They may still be in use by
                // an iterator.
                --Count;
                NotFull.SignalCondition();
            }
        }
        #endregion

        // BlockingDeque methods

        /**
         * Add to head of queue if space.
         * Throws exception if full.
         * @throws IllegalStateException {@inheritDoc}
         * @throws NullPointerException  {@inheritDoc}
         * throws ThreadInterruptedException.
         */
        public void AddFirst(T x) {
            if (!OfferFirst(x))
                throw new System.ArgumentOutOfRangeException("Deque full.");
        }

        /**
         * Add to tail of queue if space.
         * Throws exception if full.
         * @throws IllegalStateException {@inheritDoc}
         * @throws NullPointerException  {@inheritDoc}
         * throws ThreadInterruptedException.
         */
        public void AddLast(T x) {
            if (!OfferLast(x))
                throw new System.ArgumentOutOfRangeException("Deque full.");
        }

        /**
         * Add to head of queue if space.
         * Returns true if added.
         * @throws NullPointerException {@inheritDoc}
         * throws ThreadInterruptedException.
         */
        public bool OfferFirst(T x) {
            if (x == null) throw new System.NullReferenceException();
            AccessLock.WaitOne();
            try {
                return LinkFirst(x);
            } finally {
                try {
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
        }

        /**
         * Add to tail of queue if space.
         * Returns true if added.
         * @throws NullPointerException {@inheritDoc}
         * throws ThreadInterruptedException.
         */
        public bool OfferLast(T x) {
            if (x == null) throw new System.NullReferenceException();
            AccessLock.WaitOne();
            try {
                return LinkLast(x);
            } finally {
                try {
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
        }

        /**
         * Add to head of queue blocking until space if necessary.
         * @throws NullPointerException {@inheritDoc}
         * @throws InterruptedException {@inheritDoc}
         * throws ThreadInterruptedException.
         */
        public void PutFirst(T x) {
            if (x == null) throw new System.NullReferenceException();
            AccessLock.WaitOne();
            try {
                while (!LinkFirst(x))
                    NotFull.WaitCondition(AccessLock);
            } finally {
                try {
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
        }

        /**
         * Add to tail of queue blocking until space if necessary.
         * @throws NullPointerException {@inheritDoc}
         * @throws InterruptedException {@inheritDoc}
         * throws ThreadInterruptedException.
         */
        public void PutLast(T x) {
            if (x == null) throw new System.NullReferenceException();
            AccessLock.WaitOne();
            try {
                while (!LinkLast(x))
                    NotFull.WaitCondition(AccessLock);
            } finally {
                try {
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
        }

        /// <summary>
        /// Waits on the Condition Variable up to the timeout period.
        /// AccessLock must be held on entry.
        /// </summary>
        /// <param name="millisecondsTimeout">Timeout period.</param>
        /// <returns>Returns the time remaining.</returns>
        /// throws ThreadInterruptedException.
        private int Wait(ConditionVariable cv, Mutex cvLock, int millisecondsTimeout) {
            long start = DateTime.UtcNow.Ticks;
            if (cv.WaitCondition(cvLock, millisecondsTimeout)) {
                if (millisecondsTimeout > 0) {
                    long diff = DateTime.UtcNow.Ticks -start;
                    millisecondsTimeout -= (int)(diff / 10000);
                    if (millisecondsTimeout < 0) millisecondsTimeout = 0;
                }
            } else return 0;
            return millisecondsTimeout;
        }

        /**
         * @throws NullPointerException {@inheritDoc}
         * @throws InterruptedException {@inheritDoc}
         * throws ThreadInterruptedException.
         */
        public bool OfferFirst(T x, int millisecondsTimeout) {
            if (x == null) throw new System.NullReferenceException();
            AccessLock.WaitOne();
            try {
                while (!LinkFirst(x)) {
                    if (millisecondsTimeout == 0)
                        return false;
                    millisecondsTimeout = Wait(NotFull, AccessLock, millisecondsTimeout);
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

        /**
         * @throws NullPointerException {@inheritDoc}
         * @throws InterruptedException {@inheritDoc}
         * throws ThreadInterruptedException.
         */
        public bool OfferLast(T x, int millisecondsTimeout) {
            if (x == null) throw new System.NullReferenceException();
            AccessLock.WaitOne();
            try {
                while (!LinkLast(x)) {
                    if (millisecondsTimeout == 0)
                        return false;
                    millisecondsTimeout = Wait(NotFull, AccessLock, millisecondsTimeout);
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

        /**
         * @throws NoSuchElementException {@inheritDoc}
         * throws ThreadInterruptedException.
         */
        public T RemoveFirst() {
            T x = PollFirst();
            if (x == null) throw new ArgumentOutOfRangeException("Deque Empty.");
            return x;
        }

        /**
         * @throws NoSuchElementException {@inheritDoc}
         * throws ThreadInterruptedException.
         */
        public T RemoveLast() {
            T x = PollLast();
            if (x == null) throw new ArgumentOutOfRangeException("Deque Empty.");
            return x;
        }

        public T PollFirst() {
            AccessLock.WaitOne();
            try {
                return UnlinkFirst();
            } finally {
                try {
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// throws ThreadInterruptedException.
        public T PollLast() {
            AccessLock.WaitOne();
            try {
                return UnlinkLast();
            } finally {
                try {
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// throws ThreadInterruptedException.
        public T TakeFirst() {
            AccessLock.WaitOne();
            try {
                T x;
                while ((x = UnlinkFirst()) == null)
                    NotEmpty.WaitCondition(AccessLock);
                return x;
            } finally {
                try {
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// throws ThreadInterruptedException.
        public T TakeLast() {
            AccessLock.WaitOne();
            try {
                T x;
                while ((x = UnlinkLast()) == null)
                    NotEmpty.WaitCondition(AccessLock);
                return x;
            } finally {
                try { 
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="millisecondsTimeout"></param>
        /// <returns></returns>
        /// throws ThreadInterruptedException.
        public T PollFirst(int millisecondsTimeout) {
            AccessLock.WaitOne();
            try {
                T x;
                while ((x = UnlinkFirst()) == null) {
                    if (millisecondsTimeout == 0)
                        return Default;
                    millisecondsTimeout = Wait(NotEmpty, AccessLock, millisecondsTimeout);
                }
                return x;
            } finally {
                try { 
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="millisecondsTimeout"></param>
        /// <returns></returns>
        /// throws ThreadInterruptedException.
        public T PollLast(int millisecondsTimeout) {
            AccessLock.WaitOne();
            try {
                T x;
                while ((x = UnlinkLast()) == null) {
                    if (millisecondsTimeout == 0)
                        return Default;
                    millisecondsTimeout = Wait(NotEmpty, AccessLock, millisecondsTimeout);
                }
                return x;
            } finally {
                try {
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
        }

        /**
         * @throws NoSuchElementException {@inheritDoc}
         * throws ThreadInterruptedException.
         */
        public T GetFirst() {
            T x = PeekFirst();
            if (EqualityComparer<T>.Default.Equals(x, Default))
                throw new ArgumentOutOfRangeException("Deque Empty.");
            return x;
        }

        /**
         * @throws NoSuchElementException {@inheritDoc}
         * throws ThreadInterruptedException.
         */
        public T GetLast() {
            T x = PeekLast();
            if (EqualityComparer<T>.Default.Equals(x, Default))
                throw new ArgumentOutOfRangeException("Deque Empty.");
            return x;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// throws ThreadInterruptedException.
        public T PeekFirst() {
            AccessLock.WaitOne();
            try {
                return (First == null) ? Default : First.Item;
            } finally {
                try {
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// throws ThreadInterruptedException.
        public T PeekLast() {
            AccessLock.WaitOne();
            try {
                return (Last == null) ? Default : Last.Item;
            } finally {
                try {
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
        }

        /// <summary>
        /// Peek, waiting for item if necessary.
        /// </summary>
        /// <param name="millisecondsTimeout"></param>
        /// <returns>Returns item or Default if timeout occurred.</returns>
        /// throws ThreadInterruptedException.
        public T PeekFirst(int millisecondsTimeout) {
            AccessLock.WaitOne();
            try {
                while (First == null) {
                    if (millisecondsTimeout == 0)
                        return Default;
                    millisecondsTimeout = Wait(NotEmpty, AccessLock, millisecondsTimeout);
                }
                return First.Item;
            } finally {
                try {
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
        }

        /// <summary>
        /// Waits until collection contains an item.
        /// </summary>
        /// <param name="millisecondsTimeout"></param>
        /// <returns>Returns true when not emtpy, otherwise false if timeout occurred.</returns>
        /// throws ThreadInterruptedException.
        public bool WaitWhileEmpty(int millisecondsTimeout) {
            AccessLock.WaitOne();
            try {
                while (First == null) {
                    if (millisecondsTimeout == 0)
                        return false;
                    millisecondsTimeout = Wait(NotEmpty, AccessLock, millisecondsTimeout);
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
        /// 
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        /// throws ThreadInterruptedException.
        public bool RemoveFirstOccurrence(Object o) {
            if (o == null) return false;
            AccessLock.WaitOne();
            try {
                for (Node<T> p = First; p != null; p = p.Next) {
                    if (o.Equals(p.Item)) {
                        Unlink(p);
                        return true;
                    }
                }
                return false;
            } finally {
                try {
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        /// throws ThreadInterruptedException.
        public bool RemoveLastOccurrence(Object o) {
            if (o == null) return false;
            AccessLock.WaitOne();
            try {
                for (Node<T> p = Last; p != null; p = p.Prev) {
                    if (o.Equals(p.Item)) {
                        Unlink(p);
                        return true;
                    }
                }
                return false;
            } finally {
                try {
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
        }

        // BlockingQueue methods

        /**
         * Inserts the specified element at the end of this deque unless it would
         * violate capacity restrictions.  When using a capacity-restricted deque,
         * it is generally preferable to use method {@link #offer(Object) offer}.
         *
         * This method is equivalent to {@link #addLast}.
         *
         * @throws IllegalStateException if the element cannot be added at this
         *         time due to capacity restrictions
         * @throws NullPointerException if the specified element is null
         * throws ThreadInterruptedException.
         */
        public bool Add(T x) {
            AddLast(x);
            return true;
        }

        /**
         * @throws NullPointerException if the specified element is null
         * throws ThreadInterruptedException.
         */
        public bool Offer(T x) {
            return OfferLast(x);
        }

        /**
         * @throws NullPointerException {@inheritDoc}
         * @throws InterruptedException {@inheritDoc}
         * throws ThreadInterruptedException.
         */
        public void Put(T x) {
            PutLast(x);
        }

        /**
         * @throws NullPointerException {@inheritDoc}
         * @throws InterruptedException {@inheritDoc}
         * throws ThreadInterruptedException.
         */
        public bool Offer(T x, int millisecondsTimeout) {
            return OfferLast(x, millisecondsTimeout);
        }

        /**
         * Retrieves and removes the head of the queue represented by this deque.
         * This method differs from {@link #poll poll} only in that it throws an
         * exception if this deque is empty.
         *
         * This method is equivalent to {@link #removeFirst() removeFirst}.
         *
         * @return the head of the queue represented by this deque
         * @throws NoSuchElementException if this deque is empty
         * throws ThreadInterruptedException.
         */
        public T Remove() {
            return RemoveFirst();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// throws ThreadInterruptedException.
        public T Poll() {
            return PollFirst();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// throws ThreadInterruptedException.
        public T Take() {
            return TakeFirst();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="millisecondsTimeout"></param>
        /// <returns></returns>
        /// throws ThreadInterruptedException.
        public T Poll(int millisecondsTimeout) {
            return PollFirst(millisecondsTimeout);
        }

        /**
         * Retrieves, but does not remove, the head of the queue represented by
         * this deque.  This method differs from {@link #peek peek} only in that
         * it throws an exception if this deque is empty.
         *
         * This method is equivalent to {@link #getFirst() getFirst}.
         *
         * @return the head of the queue represented by this deque
         * @throws NoSuchElementException if this deque is empty
         * throws ThreadInterruptedException.
         */
        public T Element() {
            return GetFirst();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// throws ThreadInterruptedException.
        public T Peek() {
            return PeekFirst();
        }

        /**
         * Returns the number of additional elements that this deque can ideally
         * (in the absence of memory or resource constraints) accept without
         * blocking. This is always equal to the initial capacity of this deque
         * less the current {@code size} of this deque.
         *
         * Note that you <em>cannot</em> always tell if an attempt to insert
         * an element will succeed by inspecting {@code remainingCapacity}
         * because it may be the case that another thread is about to
         * insert or remove an element.
         * throws ThreadInterruptedException.
         */
        public int RemainingCapacity() {
            AccessLock.WaitOne();
            try {
                return MaxCapacity - Count;
            } finally {
                try {
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
        }

        /**
         * @throws UnsupportedOperationException {@inheritDoc}
         * @throws ClassCastException            {@inheritDoc}
         * @throws NullPointerException          {@inheritDoc}
         * @throws IllegalArgumentException      {@inheritDoc}
         * throws ThreadInterruptedException.
         */
        public int DrainTo(ICollection<T> c) {
            return DrainTo(c, Int32.MaxValue);
        }

        /**
         * @throws UnsupportedOperationException {@inheritDoc}
         * @throws ClassCastException            {@inheritDoc}
         * @throws NullPointerException          {@inheritDoc}
         * @throws IllegalArgumentException      {@inheritDoc}
         * throws ThreadInterruptedException.
         */
        public int DrainTo(ICollection<T> c, int maxElements) {
            if (c == null)
                throw new System.NullReferenceException();
            if (c == this)
            throw new System.InvalidOperationException();
            AccessLock.WaitOne();
            try {
                int n = Math.Min(maxElements, Count);
                for (int i = 0; i < n; i++) {
                    c.Add(First.Item);   // In this order, in case add() throws.
                    UnlinkFirst();
                }
                return n;
            } finally {
                try {
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
        }

        // Stack methods

        /**
         * @throws IllegalStateException {@inheritDoc}
         * @throws NullPointerException  {@inheritDoc}
         * throws ThreadInterruptedException.
         */
        public void Push(T x) {
            AddFirst(x);
        }

        /**
         * @throws NoSuchElementException {@inheritDoc}
         * throws ThreadInterruptedException.
         */
        public T Pop() {
            return RemoveFirst();
        }

        // Collection methods

        /**
         * Removes the first occurrence of the specified element from this deque.
         * If the deque does not contain the element, it is unchanged.
         * More formally, removes the first element {@code x} such that
         * {@code o.equals(x)} (if such an element exists).
         * Returns {@code true} if this deque contained the specified element
         * (or equivalently, if this deque changed as a result of the call).
         *
         * This method is equivalent to
         * {@link #removeFirstOccurrence(Object) removeFirstOccurrence}.
         *
         * @param o element to be removed from this deque, if present
         * @return {@code true} if this deque changed as a result of the call
         * throws ThreadInterruptedException.
         */
        public bool Remove(Object o) {
            return RemoveFirstOccurrence(o);
        }

        public int Capacity() {
            return MaxCapacity;
        }

        /**
         * Returns the number of elements in this deque.
         *
         * @return the number of elements in this deque
         */
        public int Size() {
            return Count;
        }

        /**
         * Returns {@code true} if this deque contains the specified element.
         * More formally, returns {@code true} if and only if this deque contains
         * at least one element {@code x} such that {@code o.equals(x)}.
         *
         * @param o object to be checked for containment in this deque
         * @return {@code true} if this deque contains the specified element
         * throws ThreadInterruptedException.
         */
        public bool Contains(Object o) {
            if (o == null) return false;
            AccessLock.WaitOne();
            try {
                for (Node<T> p = First; p != null; p = p.Next)
                    if (o.Equals(p.Item))
                        return true;
                return false;
            } finally {
                try {
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
        }

        /**
         * Returns an array containing all of the elements in this deque, in
         * proper sequence (from first to last element).
         *
         * The returned array will be "safe" in that no references to it are
         * maintained by this deque.  (In other words, this method must allocate
         * a new array).  The caller is thus free to modify the returned array.
         *
         * This method acts as bridge between array-based and collection-based
         * APIs.
         *
         * @return an array containing all of the elements in this deque
         * throws ThreadInterruptedException.
         */
        public Object[] ToArray() {
            AccessLock.WaitOne();
            try {
                Object[] a = new Object[Count];
                int k = 0;
                for (Node<T> p = First; p != null; p = p.Next)
                    a[k++] = p.Item;
                return a;
            } finally {
                try {
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// throws ThreadInterruptedException.
        public override string ToString() {
            AccessLock.WaitOne();
            try {
                Node<T> p = First;
                if (p == null)
                    return "[]";
                string s = "[";
                for (; ; ) {
                    T x = p.Item;
                    s += x;
                    p = p.Next;
                    if (p == null)
                        return s + "]";
                    s += ", ";
                }
            } finally {
                try {
                    AccessLock.ReleaseMutex();
                } catch (Exception) {
                    throw new ThreadInterruptedException();
                }
            }
        }

        /**
         * Atomically removes all of the elements from this deque.
         * The deque will be empty after this call returns.
         * throws ThreadInterruptedException.
         */
        public void Clear() {
            AccessLock.WaitOne();
            try {
                for (Node<T> f = First; f != null; ) {
                    f.Item = Default;
                    Node<T> n = f.Next;
                    f.Prev = null;
                    f.Next = null;
                    f = n;
                }
                First = Last = null;
                Count = 0;
                NotFull.BroadcastCondition();
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
