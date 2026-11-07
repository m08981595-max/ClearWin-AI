using System;
using System.Collections;
using System.Collections.Generic;

namespace ClearWin
{
    /// <summary>
    /// Fixed-capacity circular buffer for sliding-window telemetry.
    /// Add() is O(1) — when full it overwrites the oldest slot instead of
    /// shifting every element, eliminating the List&lt;T&gt;.RemoveAt(0) memmove
    /// pattern that appears across the hot paths (tick loops, sparklines, replay
    /// buffers, training samples).
    ///
    /// Ordering contract:
    ///   index 0            = oldest stored item
    ///   index Count - 1    = newest stored item
    ///   enumeration walks oldest → newest (matches List semantics)
    ///
    /// Thread-safety: pass threadSafe:true for buffers shared across threads.
    /// The thread-safe mode guards Add/Count/iteration with an internal lock;
    /// for a read-only iteration call CopyToArray() to get a stable snapshot.
    /// </summary>
    public class RingBuffer<T> : IEnumerable<T>
    {
        private readonly T[] _items;
        private readonly int _capacity;
        private readonly bool _threadSafe;
        private readonly object _sync = new object();
        private int _head;   // next write slot
        private int _count;  // number of valid items stored

        public RingBuffer(int capacity, bool threadSafe = false)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException("capacity");
            _capacity = capacity;
            _threadSafe = threadSafe;
            _items = new T[capacity];
        }

        public int Capacity { get { return _capacity; } }

        public int Count
        {
            get
            {
                if (_threadSafe) { lock (_sync) { return _count; } }
                return _count;
            }
        }

        public bool IsFull
        {
            get
            {
                if (_threadSafe) { lock (_sync) { return _count == _capacity; } }
                return _count == _capacity;
            }
        }

        /// <summary>Appends an item; evicts the oldest when the buffer is full.</summary>
        public void Add(T item)
        {
            if (_threadSafe) { lock (_sync) { AddLocked(item); } }
            else { AddLocked(item); }
        }

        private void AddLocked(T item)
        {
            _items[_head] = item;
            _head = (_head + 1) % _capacity;
            if (_count < _capacity) _count++;
        }

        /// <summary>Chronological indexer: 0 = oldest, Count-1 = newest.</summary>
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException("index");
                int slot = (_head - _count + index) % _capacity;
                if (slot < 0) slot += _capacity;
                return _items[slot];
            }
        }

        /// <summary>Newest item, or default(T) when the buffer is empty.</summary>
        public T Last
        {
            get
            {
                if (_count == 0) return default(T);
                return this[_count - 1];
            }
        }

        /// <summary>Oldest item, or default(T) when the buffer is empty.</summary>
        public T First
        {
            get
            {
                if (_count == 0) return default(T);
                return this[0];
            }
        }

        public void Clear()
        {
            if (_threadSafe) { lock (_sync) { ClearLocked(); } }
            else { ClearLocked(); }
        }

        private void ClearLocked()
        {
            Array.Clear(_items, 0, _items.Length);
            _head = 0;
            _count = 0;
        }

        /// <summary>Chronological snapshot (oldest → newest). Thread-safe read.</summary>
        public T[] ToArray()
        {
            if (_threadSafe)
            {
                lock (_sync) { return ToArrayLocked(); }
            }
            return ToArrayLocked();
        }

        private T[] ToArrayLocked()
        {
            var result = new T[_count];
            for (int i = 0; i < _count; i++)
            {
                int slot = (_head - _count + i) % _capacity;
                if (slot < 0) slot += _capacity;
                result[i] = _items[slot];
            }
            return result;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < _count; i++)
                yield return this[i];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
