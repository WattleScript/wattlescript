#if !USE_DYNAMIC_STACKS

using System;
using System.Collections.Generic;
using System.Linq;

namespace WattleScript.Interpreter.DataStructs
{
	/// <summary>
	/// A preallocated, non-resizable, stack
	/// </summary>
	/// <typeparam name="T"></typeparam>
	internal class FastStack<T> : IList<T>
	{
		T[] m_Storage;
		int m_HeadIdx = 0;
		private int maxCapacity;

		public int MaxCapacity => maxCapacity;

		public FastStack(int initialCapacity, int maxCapacity)
		{
			m_Storage = new T[initialCapacity];
			this.maxCapacity = maxCapacity;
		}

		public ref T this[int index] => ref m_Storage[index];
		
		

		void Grow(int newSize)
		{
			if (newSize < m_Storage.Length) return;
			if (newSize > maxCapacity)
			{
				throw new OutOfMemoryException($"Trying to grow stack beyond Max Capacity ({newSize} > {maxCapacity}).");
			}
			int sz = m_Storage.Length;
			while (newSize > sz) {
				sz *= 2;
			}
			if (sz >= maxCapacity) sz = maxCapacity;
			Array.Resize(ref m_Storage, sz);
		}

		public T Push(T item)
		{
			Grow(m_HeadIdx + 1);
			m_Storage[m_HeadIdx++] = item;
			return item;
		}

		public int Reserve(int size)
		{
			Grow(m_HeadIdx + size);
			var retval = m_HeadIdx;
			m_HeadIdx += size;
			return retval;
		}

		private void Zero(int from, int to)
		{
			Array.Clear(m_Storage, from, to - from + 1);
		}

		private void Zero(int index)
		{
			m_Storage[index] = default(T);
		}

		public ref T Peek(int idxofs = 0)
		{
			ref T item = ref m_Storage[m_HeadIdx - 1 - idxofs];
			return ref item;
		}

		public void Set(int idxofs, T item)
		{
			m_Storage[m_HeadIdx - 1 - idxofs] = item;
		}

		public void CropAtCount(int p)
		{
			RemoveLast(Count - p);
		}

		public void RemoveLast( int cnt = 1)
		{
			if (cnt == 1)
			{
				--m_HeadIdx;
				m_Storage[m_HeadIdx] = default(T);
			}
			else
			{
				int oldhead = m_HeadIdx;
				m_HeadIdx -= cnt;
				Zero(m_HeadIdx, oldhead);
			}
		}

		public T Pop()
		{
			--m_HeadIdx;
			T retval = m_Storage[m_HeadIdx];
			m_Storage[m_HeadIdx] = default(T);
			return retval;
		}

		public void Clear()
		{
			Array.Clear(m_Storage, 0, m_Storage.Length);
			m_HeadIdx = 0;
		}

		public void ClearSection(int index, int length)
		{
			Array.Clear(m_Storage, index, length);
		}
		

		public int Count
		{
			get { return m_HeadIdx; }
		}


		#region IList<T> Impl.

		int IList<T>.IndexOf(T item)
		{
			throw new NotImplementedException();
		}

		void IList<T>.Insert(int index, T item)
		{
			throw new NotImplementedException();
		}

		void IList<T>.RemoveAt(int index)
		{
			throw new NotImplementedException();
		}

		T IList<T>.this[int index]
		{
			get
			{
				return this[index];
			}
			set
			{
				this[index] = value;
			}
		}

		void ICollection<T>.Add(T item)
		{
			Push(item);
		}

		void ICollection<T>.Clear()
		{
			Clear();
		}

		bool ICollection<T>.Contains(T item)
		{
			throw new NotImplementedException();
		}

		void ICollection<T>.CopyTo(T[] array, int arrayIndex)
		{
			m_Storage.Take(m_HeadIdx).ToArray().CopyTo(array, arrayIndex);
		}

		int ICollection<T>.Count
		{
			get { return this.Count; }
		}

		bool ICollection<T>.IsReadOnly
		{
			get { return false; }
		}

		bool ICollection<T>.Remove(T item)
		{
			throw new NotImplementedException();
		}

		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			return (IEnumerator<T>) m_Storage.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			throw new NotImplementedException();
		}

		#endregion

	}
}

#endif