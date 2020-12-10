using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace RegExtract
{
    // If not for .NET 4.0 support, I'd just use IReadOnlyCollection<T>.
    internal class ReadOnlySlice<T> : IEnumerable<T>
    {
        T[] _storage;
        int _start;
        int _length;

        internal ReadOnlySlice(T[] storage)
        {
            _storage = storage;
            _start = 0;
            _length = storage.Length;
        }

        internal ReadOnlySlice(ReadOnlySlice<T> source, int start, int length)
        {
            _storage = source._storage;
            _start = source._start + start;
            _length = length;
            Debug.Assert(length <= source._length - start);
        }

        public T this[int i] => _storage[i + _start];

        public int Count => _length;

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = _start; i < _start + _length; ++i)
            {
                yield return _storage[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
