using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MoonSharp.Interpreter.DataStructs;

namespace MoonSharp.Interpreter
{
	/// <summary>
	/// A class representing a Lua table.
	/// </summary>
	public class Table : RefIdObject, IScriptPrivateResource
	{
		readonly LinkedList<TablePair> m_Values;
		readonly LinkedListIndex<DynValue, TablePair> m_ValueMap;
		readonly LinkedListIndex<string, TablePair> m_StringMap;
		readonly LinkedListArrayIndex<TablePair> m_ArrayMap;

		private int IndexFrom => OwnerScript?.Options.IndexTablesFrom ?? 1;
		
		private int m_InitArray = 0;
		private int m_CachedLength = -1;
		private bool m_ContainsNilEntries = false;

		/// <summary>
		/// Initializes a new instance of the <see cref="Table"/> class.
		/// </summary>
		/// <param name="owner">The owner script.</param>
		public Table(Script owner)
		{
			m_Values = new LinkedList<TablePair>();
			m_StringMap = new LinkedListIndex<string, TablePair>(m_Values);
			m_ArrayMap = new LinkedListArrayIndex<TablePair>(m_Values);
			m_ValueMap = new LinkedListIndex<DynValue, TablePair>(m_Values);
			OwnerScript = owner;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Table"/> class.
		/// </summary>
		/// <param name="owner">The owner.</param>
		/// <param name="arrayValues">The values for the "array-like" part of the table.</param>
		public Table(Script owner, params DynValue[] arrayValues)
			: this(owner)
		{
			for (int i = 0; i < arrayValues.Length; i++)
			{
				Set(DynValue.NewNumber(i + IndexFrom), arrayValues[i]);
			}
		}

		/// <summary>
		/// Gets the script owning this resource.
		/// </summary>
		public Script OwnerScript { get; }

		/// <summary>
		/// Removes all items from the Table.
		/// </summary>
		public void Clear()
		{
			m_Values.Clear();
			m_StringMap.Clear();
			m_ArrayMap.Clear();
			m_ValueMap.Clear();
            m_CachedLength = -1;
		}

		/// <summary>
		/// Gets the integral key from a double.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int GetIntegralKey(int n) => n >= IndexFrom ? n : -1;

		/// <summary>
		/// Gets or sets the 
		/// <see cref="System.Object" /> with the specified key(s).
		/// This will marshall CLR and MoonSharp objects in the best possible way.
		/// Multiple keys can be used to access subtables.
		/// </summary>
		/// <value>
		/// The <see cref="System.Object" />.
		/// </value>
		/// <param name="keys">The keys to access the table and subtables</param>
		public object this[params object[] keys]
		{
			get => Get(keys).ToObject();
			set => Set(keys, DynValue.FromObject(OwnerScript, value));
		}

		/// <summary>
		/// Gets or sets the <see cref="System.Object"/> with the specified key(s).
		/// This will marshall CLR and MoonSharp objects in the best possible way.
		/// </summary>
		/// <value>
		/// The <see cref="System.Object"/>.
		/// </value>
		/// <param name="key">The key.</param>
		/// <returns></returns>
		public object this[object key]
		{
			get => Get(key).ToObject();
			set => Set(key, DynValue.FromObject(OwnerScript, value));
		}

		private Table ResolveMultipleKeys(object[] keys, out object key)
		{
			//Contract.Ensures(Contract.Result<Table>() != null);
			//Contract.Requires(keys != null);

			Table t = this;
			key = keys.Length > 0 ? keys[0] : null;

			for (int i = IndexFrom; i < keys.Length; ++i)
			{
				DynValue vt = t.RawGet(key);

				if (vt.IsNil())
					throw new ScriptRuntimeException("Key '{0}' did not point to anything");

				if (vt.Type != DataType.Table)
					throw new ScriptRuntimeException("Key '{0}' did not point to a table");

				t = vt.Table;
				key = keys[i];
			}

			return t;
		}

		/// <summary>
		/// Append the value to the table using the next available integer index.
		/// </summary>
		/// <param name="value">The value.</param>
		public void Append(IEnumerable<DynValue> values)
		{
			foreach (DynValue value in values)
			{
				this.CheckScriptOwnership(value);
				PerformTableSet(m_ArrayMap, Length + IndexFrom, DynValue.NewNumber(Length + IndexFrom), value, true, Length + IndexFrom);	
			}
		}
		
		/// <summary>
		/// Append the value to the table using the next available integer index.
		/// </summary>
		/// <param name="value">The value.</param>
		public void Append(DynValue value)
		{
			this.CheckScriptOwnership(value);
			PerformTableSet(m_ArrayMap, Length + IndexFrom, DynValue.NewNumber(Length + IndexFrom), value, true, Length + IndexFrom);
		}
		
		/// Append the value to the table using the next available integer index.
		/// </summary>
		/// <param name="value">The value.</param>
		public void Append(object value)
		{
			Append(DynValue.FromObject(OwnerScript, value));
		}

		/// Append the value to the table using the next available integer index.
		/// </summary>
		/// <param name="value">The value.</param>
		public void Append(IEnumerable<object> values)
		{
			foreach (object value in values)
			{
				Append(DynValue.FromObject(OwnerScript, value));	
			}
		}
		
		#region Set

		private void PerformTableSet<T>(LinkedListIndex<T, TablePair> listIndex, T key, DynValue keyDynValue, DynValue value, bool isNumber, int appendKey)
		{
			TablePair prev = listIndex.Set(key, new TablePair(keyDynValue, value));

			// If this is an insert, we can invalidate all iterators and collect dead keys
			if (m_ContainsNilEntries && value.IsNotNil() && prev.Value.IsNil())
			{
				CollectDeadKeys();
			}
			// If this value is nil (and we didn't collect), set that there are nil entries, and invalidate array len cache
			else if (value.IsNil())
			{
				m_ContainsNilEntries = true;

				if (isNumber)
					m_CachedLength = -1;
			}
			else if (isNumber)
			{
				// If this is an array insert, we might have to invalidate the array length
				if (!prev.Value.IsNilOrNan())
				{
					return;
				}
				
				// If this is an array append, let's check the next element before blindly invalidating
				if (appendKey >= 0)
				{
					LinkedListNode<TablePair> next = m_ArrayMap.Find(appendKey + 1);
					if (next == null ||  next.Value.Value.IsNil())
					{
						m_CachedLength += 1;
					}
					else
					{
						m_CachedLength = -1;
					}
				}
				else
				{
					m_CachedLength = -1;
				}
			}
		}

		/// <summary>
		/// Sets the value associated to the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		public void Set(string key, DynValue value)
		{
			if (key == null)
				throw ScriptRuntimeException.TableIndexIsNil();

			this.CheckScriptOwnership(value);
			PerformTableSet(m_StringMap, key, DynValue.NewString(key), value, false, -1);
		}

		/// <summary>
		/// Sets the value associated to the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		public void Set(int key, DynValue value)
		{
			this.CheckScriptOwnership(value);
			PerformTableSet(m_ArrayMap, key, DynValue.NewNumber(key), value, true, -1);
		}
		
		/// <summary>
		/// Sets the value associated to the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		public void Set(int key, object value)
		{
			Set(key, DynValue.FromObject(OwnerScript, value));
		}

		/// <summary>
		/// Sets the value associated to the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		public void Set(DynValue key, DynValue value)
		{
			if (key.IsNilOrNan())
			{
				if (key.IsNil())
					throw ScriptRuntimeException.TableIndexIsNil();
				
				throw ScriptRuntimeException.TableIndexIsNaN();
			}

			switch (key.Type)
			{
				case DataType.String:
					Set(key.String, value);
					return;
				case DataType.Number:
				{
					int idx = GetIntegralKey(key.Int);

					if (idx >= IndexFrom)
					{
						Set(idx, value);
						return;
					}

					break;
				}
			}

			this.CheckScriptOwnership(key);
			this.CheckScriptOwnership(value);

			PerformTableSet(m_ValueMap, key, key, value, false, -1);
		}

		/// <summary>
		/// Sets the value associated with the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		public void Set(object key, DynValue value)
		{
			switch (key)
			{
				case null:
					throw ScriptRuntimeException.TableIndexIsNil();
				case string s:
					Set(s, value);
					break;
				case int i:
					Set(i, value);
					break;
				default:
					Set(DynValue.FromObject(OwnerScript, key), value);
					break;
			}
		}

		/// <summary>
		/// Sets the value associated with the specified keys.
		/// Multiple keys can be used to access subtables.
		/// </summary>
		/// <param name="key">The keys.</param>
		/// <param name="value">The value.</param>
		public void Set(object[] keys, DynValue value)
		{
			if (keys == null || keys.Length <= 0)
				throw ScriptRuntimeException.TableIndexIsNil();

			ResolveMultipleKeys(keys, out object key).Set(key, value);
		}

		#endregion

		#region Get

		/// <summary>
		/// Gets the value associated with the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		public DynValue Get(string key)
		{
			//Contract.Ensures(Contract.Result<DynValue>() != null);
			return RawGet(key);
		}

		/// <summary>
		/// Gets the value associated with the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		public DynValue Get(int key)
		{
			//Contract.Ensures(Contract.Result<DynValue>() != null);
			return RawGet(key);
		}

		/// <summary>
		/// Gets the value associated with the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		public DynValue Get(DynValue key)
		{
			//Contract.Ensures(Contract.Result<DynValue>() != null);
			return RawGet(key);
		}

		/// <summary>
		/// Gets the value associated with the specified key.
		/// (expressed as a <see cref="System.Object"/>).
		/// </summary>
		/// <param name="key">The key.</param>
		public DynValue Get(object key)
		{
			//Contract.Ensures(Contract.Result<DynValue>() != null);
			return RawGet(key);
		}

		/// <summary>
		/// Gets the value associated with the specified keys (expressed as an 
		/// array of <see cref="System.Object"/>).
		/// This will marshall CLR and MoonSharp objects in the best possible way.
		/// Multiple keys can be used to access subtables.
		/// </summary>
		/// <param name="keys">The keys to access the table and subtables</param>
		public DynValue Get(params object[] keys)
		{
			//Contract.Ensures(Contract.Result<DynValue>() != null);
			return RawGet(keys);
		}

		#endregion

		#region RawGet

		private static DynValue RawGetValue(LinkedListNode<TablePair> linkedListNode)
		{
			return linkedListNode != null ? linkedListNode.Value.Value : DynValue.Nil;
		}

		/// <summary>
		/// Gets the value associated with the specified key,
		/// without bringing to Nil the non-existant values.
		/// </summary>
		/// <param name="key">The key.</param>
		public DynValue RawGet(string key)
		{
			return RawGetValue(m_StringMap.Find(key));
		}

		/// <summary>
		/// Gets the value associated with the specified key,
		/// without bringing to Nil the non-existant values.
		/// </summary>
		/// <param name="key">The key.</param>
		public DynValue RawGet(int key)
		{
			return RawGetValue(m_ArrayMap.Find(key));
		}

		/// <summary>
		/// Gets the value associated with the specified key,
		/// without bringing to Nil the non-existant values.
		/// </summary>
		/// <param name="key">The key.</param>
		public DynValue RawGet(DynValue key)
		{
			switch (key.Type)
			{
				case DataType.String:
					return RawGet(key.String);
				case DataType.Number:
				{
					int idx = GetIntegralKey(key.Int);
					if (idx >= IndexFrom)
						return RawGet(idx);
					break;
				}
			}

			return RawGetValue(m_ValueMap.Find(key));
		}

		/// <summary>
		/// Gets the value associated with the specified key,
		/// without bringing to Nil the non-existant values.
		/// </summary>
		/// <param name="key">The key.</param>
		public DynValue RawGet(object key)
		{
			return key switch
			{
				null => DynValue.Nil,
				string s => RawGet(s),
				int i => RawGet(i),
				_ => RawGet(DynValue.FromObject(OwnerScript, key))
			};
		}

		/// <summary>
		/// Gets the value associated with the specified keys (expressed as an
		/// array of <see cref="System.Object"/>).
		/// This will marshall CLR and MoonSharp objects in the best possible way.
		/// Multiple keys can be used to access subtables.
		/// </summary>
		/// <param name="keys">The keys to access the table and subtables</param>
		public DynValue RawGet(params object[] keys)
		{
			if (keys == null || keys.Length <= 0)
				return DynValue.Nil;

			return ResolveMultipleKeys(keys, out object key).RawGet(key);
		}

		#endregion

		#region Remove

		private bool PerformTableRemove<T>(LinkedListIndex<T, TablePair> listIndex, T key, bool isNumber)
		{
			var removed = listIndex.Remove(key);

			if (removed && isNumber)
			{
				m_CachedLength = -1;
			}

			return removed;
		}

		/// <summary>
		/// Remove the value associated with the specified key from the table.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns><c>true</c> if values was successfully removed; otherwise, <c>false</c>.</returns>
		public bool Remove(string key)
		{
			return PerformTableRemove(m_StringMap, key, false);
		}

		/// <summary>
		/// Remove the value associated with the specified key from the table.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns><c>true</c> if values was successfully removed; otherwise, <c>false</c>.</returns>
		public bool Remove(int key)
		{
			return PerformTableRemove(m_ArrayMap, key, true);
		}

		/// <summary>
		/// Remove the value associated with the specified key from the table.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns><c>true</c> if values was successfully removed; otherwise, <c>false</c>.</returns>
		public bool Remove(DynValue key)
		{
			switch (key.Type)
			{
				case DataType.String:
					return Remove(key.String);
				case DataType.Number:
				{
					int idx = GetIntegralKey(key.Int);
					if (idx >= IndexFrom)
						return Remove(idx);
					break;
				}
			}

			return PerformTableRemove(m_ValueMap, key, false);
		}

		/// <summary>
		/// Remove the value associated with the specified key from the table.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns><c>true</c> if values was successfully removed; otherwise, <c>false</c>.</returns>
		public bool Remove(object key)
		{
			return key switch
			{
				string s => Remove(s),
				int i => Remove(i),
				_ => Remove(DynValue.FromObject(OwnerScript, key))
			};
		}

		/// <summary>
		/// Remove the value associated with the specified keys from the table.
		/// Multiple keys can be used to access subtables.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns><c>true</c> if values was successfully removed; otherwise, <c>false</c>.</returns>
		public bool Remove(params object[] keys)
		{
			if (keys == null || keys.Length <= 0)
				return false;

			return ResolveMultipleKeys(keys, out object key).Remove(key);
		}

		#endregion

		/// <summary>
		/// Collects the dead keys. This frees up memory but invalidates pending iterators.
		/// It's called automatically internally when the semantics of Lua tables allow, but can be forced
		/// externally if it's known that no iterators are pending.
		/// </summary>
		public void CollectDeadKeys()
		{
			for (LinkedListNode<TablePair> node = m_Values.First; node != null; node = node.Next)
			{
				if (node.Value.Value.IsNil())
				{
					Remove(node.Value.Key);
				}
			}

			m_ContainsNilEntries = false;
			m_CachedLength = -1;
		}


		/// <summary>
		/// Returns the next pair from a value
		/// </summary>
		public TablePair? NextKey(DynValue v)
		{
			while (true)
			{
				if (v.IsNil())
				{
					LinkedListNode<TablePair> node = m_Values.First;

					if (node == null) return TablePair.Nil;

					if (!node.Value.Value.IsNil())
					{
						return node.Value;
					}
					
					v = node.Value.Key;
					continue;
				}

				switch (v.Type)
				{
					case DataType.String:
						return GetNextOf(m_StringMap.Find(v.String));
					case DataType.Number:
					{
						int idx = GetIntegralKey(v.Int);

						if (idx >= IndexFrom)
						{
							return GetNextOf(m_ArrayMap.Find(idx));
						}

						break;
					}
				}

				return GetNextOf(m_ValueMap.Find(v));
			}
		}

		private TablePair? GetNextOf(LinkedListNode<TablePair> linkedListNode)
		{
			while (true)
			{
				if (linkedListNode == null)
					return null;

				if (linkedListNode.Next == null)
					return TablePair.Nil;

				linkedListNode = linkedListNode.Next;

				if (!linkedListNode.Value.Value.IsNil())
					return linkedListNode.Value;
			}
		}


		/// <summary>
		/// Gets the length of the "array part".
		/// </summary>
		public int Length
		{
			get
			{
				if (m_CachedLength >= 0)
				{
					return m_CachedLength;
				}
				
				m_CachedLength = 0;

				for (int i = IndexFrom; m_ArrayMap.ContainsKey(i) && !m_ArrayMap.Find(i).Value.Value.IsNil(); i++)
					m_CachedLength++;

				return m_CachedLength;
			}
		}

		internal void InitNextArrayKeys(DynValue val, bool lastpos)
		{
			if (val.Type == DataType.Tuple && lastpos)
			{
				foreach (DynValue v in val.Tuple)
					InitNextArrayKeys(v, true);
			}
			else
			{
				if (IndexFrom > 0)
				{
					Set(++m_InitArray, val.ToScalar());	
				}
				else
				{
					Set(m_InitArray++, val.ToScalar());	
				}
			}
		}

		/// <summary>
		/// Gets the meta-table associated with this instance.
		/// </summary>
		public Table MetaTable
		{
			get => m_MetaTable;
			set { this.CheckScriptOwnership(m_MetaTable); m_MetaTable = value; }
		}
		private Table m_MetaTable;



		/// <summary>
		/// Enumerates the key/value pairs.
		/// </summary>
		/// <returns></returns>
		public IEnumerable<TablePair> Pairs
		{
			get
			{
				return m_Values.Select(n => new TablePair(n.Key, n.Value));
			}
		}

		/// <summary>
		/// Enumerates the keys.
		/// </summary>
		/// <returns></returns>
		public IEnumerable<DynValue> Keys
		{
			get
			{
				return m_Values.Select(n => n.Key);
			}
		}

		/// <summary>
		/// Enumerates the values
		/// </summary>
		/// <returns></returns>
		public IEnumerable<DynValue> Values
		{
			get
			{
				return m_Values.Select(n => n.Value);
			}
		}
	}
}
