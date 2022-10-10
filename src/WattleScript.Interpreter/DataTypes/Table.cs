using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace WattleScript.Interpreter
{
	/// <summary>
	/// A class representing a Lua table.
	/// </summary>
	public class Table : RefIdObject, IScriptPrivateResource
	{
		private const int ARRAY_PART_THRESHOLD = 5;
		
		private readonly LinkedList<TablePair> valueList = new LinkedList<TablePair>();
		readonly Dictionary<DynValue, LinkedListNode<TablePair>> valueMap = new Dictionary<DynValue, LinkedListNode<TablePair>>();
		private DynValue[] arrayPart = null;
		int arrayLength = 0;
		
		//Bit 31 = ReadOnly
		//Bit 30 = ContainsNilEntries
		//Other bits = TableKind.
		private uint kindVal = 0;

		private bool containsNilEntries
		{
			get => (kindVal & 0x40000000) != 0;
			set
			{
				if (value) kindVal |= 0x40000000;
				else kindVal &= ~0x40000000U;
			}
		}

		
		int indexFrom => OwnerScript?.Options.IndexTablesFrom ?? 1;

		/// <summary>
		/// Initializes a new instance of the <see cref="Table"/> class.
		/// </summary>
		/// <param name="owner">The owner script.</param>
		public Table(Script owner)
		{
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
				Set(DynValue.NewNumber(i + indexFrom), arrayValues[i]);
			}
		}

		/// <summary>
		/// Gets the script owning this resource.
		/// </summary>
		public Script OwnerScript { get; }

		/// <summary>
		/// Gets/sets if this is a ReadOnly table.
		/// Writing to a ReadOnly table will throw an exception
		/// </summary>
		public bool ReadOnly
		{
			get => (kindVal & 0x80000000) != 0;
			set
			{
				if (value) kindVal |= 0x80000000;
				else kindVal &= ~0x80000000U;
			}
		}

		/// <summary>
		/// Gets/sets the kind of table.
		/// This is only for metadata purposes, and does not affect execution.
		/// </summary>
		public TableKind Kind
		{
			get => (TableKind)(kindVal & 0x3FFFFFFF);
			set => kindVal = kindVal & 0xC0000000 | (uint)value & 0x3FFFFFFF;
		}
		
		/// <summary>
		/// Gets/sets the modifiers of table.
		/// This is only for metadata purposes, and does not affect execution.
		/// </summary>
		public MemberModifierFlags ModifierFlags { get; set; }
		
		/// <summary>
		/// Holds modifiers of declared fields & functions in case <see cref="TableKind"/> is Class/Mixin. Can be null.
		/// </summary>
		public WattleMembersInfo Members { get; set; }

		/// <summary>
		/// Removes all items from the Table.
		/// </summary>
		public void Clear()
		{
			valueList.Clear();
			valueMap.Clear();
			arrayLength = 0;
			arrayPart = null;
		}

		/// <summary>
		/// Gets the integral key from a double.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool TryGetIntegralKey(ref DynValue dv, out int k)
		{
			if (dv.Type != DataType.Number)
			{
				k = -1;
				return false;
			}
			k = (int)dv.Number;
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if (dv.Number == k)
				return true;
			return false;
		}
		
		/// <summary>
		/// Gets or sets the 
		/// <see cref="System.Object" /> with the specified key(s).
		/// This will marshall CLR and WattleScript objects in the best possible way.
		/// Multiple keys can be used to access subtables.
		/// </summary>
		/// <value>
		/// The <see cref="System.Object" />.
		/// </value>
		/// <param name="keys">The keys to access the table and subtables</param>
		public object this[params object[] keys]
		{
			get
			{
				return Get(keys).ToObject();
			}
			set
			{
				Set(keys, DynValue.FromObject(OwnerScript, value));
			}
		}

		/// <summary>
		/// Gets or sets the <see cref="System.Object"/> with the specified key(s).
		/// This will marshall CLR and WattleScript objects in the best possible way.
		/// </summary>
		/// <value>
		/// The <see cref="System.Object"/>.
		/// </value>
		/// <param name="key">The key.</param>
		/// <returns></returns>
		public object this[object key]
		{
			get
			{
				return Get(key).ToObject();
			}
			set
			{
				Set(key, DynValue.FromObject(OwnerScript, value));
			}
		}

		private Table ResolveMultipleKeys(object[] keys, out object key)
		{
			Table t = this;
			key = (keys.Length > 0) ? keys[0] : null;

			for (int i = indexFrom; i < keys.Length; ++i)
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
		/// Append the values to the table using the next available integer indexes.
		/// </summary>
		/// <param name="value">The value.</param>
		public void Append(IEnumerable<DynValue> values)
		{
			foreach (DynValue value in values)
			{
				Append(value);
			}
		}

		/// <summary>
		/// Append the value to the table using the next available integer index.
		/// </summary>
		/// <param name="value">The value.</param>
		public void Append(DynValue value)
		{
			this.CheckScriptOwnership(value);
			PerformTableSet(DynValue.NewNumber(Length + indexFrom), value);
		}
		
		/// <summary>
		/// Append the value to the table using the next available integer index.
		/// </summary>
		/// <param name="value">The value.</param>
		public void Append(object value)
		{
			Append(DynValue.FromObject(OwnerScript, value));
		}

		/// <summary>
		/// Append the values to the table using the next available integer indexes.
		/// </summary>
		/// <param name="values">The value.</param>
		public void Append(IEnumerable<object> values)
		{
			foreach (object value in values)
			{
				Append(DynValue.FromObject(OwnerScript, value));	
			}
		}

		#region Set

		LinkedListNode<TablePair> MapFind(DynValue key)
		{
			valueMap.TryGetValue(key, out var pair);
			return pair;
		}
		
		void MapAdd(DynValue key, DynValue value)
		{
			var node = valueList.AddLast(new TablePair(key, value));
			valueMap.Add(key, node);
		}
		
		TablePair MapSet(DynValue key, DynValue value)
		{
			LinkedListNode<TablePair> node = MapFind(key);

			if (node == null)
			{
				MapAdd(key, value);
				return default;
			}
			else
			{
				TablePair val = node.Value;
				node.Value = new TablePair(key, value);
				return val;
			}
		}

		bool KeyInArray(int ik)
		{
			return (ik >= 0 && ik <= (arrayPart?.Length ?? 0) + ARRAY_PART_THRESHOLD);
		}

		int GetNewLength(int minLength)
		{
			int i = arrayPart == null ? ARRAY_PART_THRESHOLD : arrayPart.Length * 2;
			while (valueMap.TryGetValue(DynValue.NewNumber(i), out var node) &&
			       node.Value.Value.IsNotNil())
				i++;
			if (i < minLength) return minLength;
			return i;
		}

		void MapToArray(int oldLength)
		{
			if (valueMap.Count == 0) return; //Pure array
			for (int i = oldLength; i < arrayPart.Length; i++)
			{
				var k = DynValue.NewNumber(i);
				if (valueMap.TryGetValue(k, out var node)) {
					arrayPart[i] = node.Value.Value;
					valueList.Remove(node);
					valueMap.Remove(k);
					if (arrayLength < i && arrayPart[i].IsNotNil()) arrayLength = i + 1;
				}
			}
		}

		private void PerformTableSet(DynValue key, DynValue value)
		{
			if (TryGetIntegralKey(ref key, out var ik) &&
			    KeyInArray(ik))
			{
				if (arrayPart == null) {
					arrayPart = new DynValue[GetNewLength(ik + 1)];
					MapToArray(0);
				}
				else if (arrayPart.Length <= ik) {
					int oldLength = arrayPart.Length;
					Array.Resize(ref arrayPart, GetNewLength(ik + 1));
					MapToArray(oldLength);
				}
				
				arrayPart[ik] = value;
				if (value.IsNil() && arrayLength > ik)
					arrayLength = ik;
				else if (value.IsNotNil() && arrayLength <= ik)
				{
					//Scan to find new array length
					for(; ik < arrayPart.Length; ik++) {
						if (arrayPart[ik].IsNil()) break;
					}
					arrayLength = ik;
				}
			}
			else
			{
				TablePair prev = MapSet(key, value);
				// If this is an insert, we can invalidate all iterators and collect dead keys
				if (containsNilEntries && value.IsNotNil() && prev.Value.IsNil())
				{
					CollectDeadKeys();
				}
				// If this value is nil (and we didn't collect), set that there are nil entries
				if (value.IsNil())
					containsNilEntries = true;
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
			PerformTableSet(DynValue.NewString(key), value);
		}

		/// <summary>
		/// Sets the value associated to the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		public void Set(int key, DynValue value)
		{
			this.CheckScriptOwnership(value);
			PerformTableSet(DynValue.NewNumber(key), value);
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
				else
					throw ScriptRuntimeException.TableIndexIsNaN();
			}

			this.CheckScriptOwnership(key);
			this.CheckScriptOwnership(value);

			PerformTableSet(key, value);
		}

		/// <summary>
		/// Sets the value associated with the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="value">The value.</param>
		public void Set(object key, DynValue value)
		{
			if (ReadOnly) throw ScriptRuntimeException.TableIsReadonly();
			if (key == null)
				throw ScriptRuntimeException.TableIndexIsNil();
			Set(DynValue.FromObject(OwnerScript, key), value);
		}

		/// <summary>
		/// Sets the value associated with the specified keys.
		/// Multiple keys can be used to access subtables.
		/// </summary>
		/// <param name="key">The keys.</param>
		/// <param name="value">The value.</param>
		public void Set(object[] keys, DynValue value)
		{
			if (ReadOnly) throw ScriptRuntimeException.TableIsReadonly();
			if (keys == null || keys.Length <= 0)
				throw ScriptRuntimeException.TableIndexIsNil();

			object key;
			ResolveMultipleKeys(keys, out key).Set(key, value);
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
		/// This will marshall CLR and WattleScript objects in the best possible way.
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

		/// <summary>
		/// Gets the value associated with the specified key,
		/// without bringing to Nil the non-existant values.
		/// </summary>
		/// <param name="key">The key.</param>
		public DynValue RawGet(string key) => RawGet(DynValue.NewString(key));

		/// <summary>
		/// Gets the value associated with the specified key,
		/// without bringing to Nil the non-existant values.
		/// </summary>
		/// <param name="key">The key.</param>
		public DynValue RawGet(int key) => RawGet(DynValue.NewNumber(key));

		/// <summary>
		/// Gets the value associated with the specified key,
		/// without bringing to Nil the non-existant values.
		/// </summary>
		/// <param name="key">The key.</param>
		public DynValue RawGet(DynValue key)
		{
			if (TryGetIntegralKey(ref key, out int ik) && KeyInArray(ik))
			{
				if (arrayPart == null || arrayPart.Length <= ik) return DynValue.Nil;
				return arrayPart[ik];
			}
			var node = MapFind(key);
			return (node != null) ? node.Value.Value : DynValue.Nil;
		}

		/// <summary>
		/// Gets the value associated with the specified key,
		/// without bringing to Nil the non-existant values.
		/// </summary>
		/// <param name="key">The key.</param>
		public DynValue RawGet(object key) => RawGet(DynValue.FromObject(OwnerScript, key));

		/// <summary>
		/// Gets the value associated with the specified keys (expressed as an
		/// array of <see cref="System.Object"/>).
		/// This will marshall CLR and WattleScript objects in the best possible way.
		/// Multiple keys can be used to access subtables.
		/// </summary>
		/// <param name="keys">The keys to access the table and subtables</param>
		public DynValue RawGet(params object[] keys)
		{
			if (keys == null || keys.Length <= 0)
				return DynValue.Nil;

			object key;
			return ResolveMultipleKeys(keys, out key).RawGet(key);
		}

		#endregion

		#region Remove
		
		bool MapRemove(DynValue key)
		{
			LinkedListNode<TablePair> node = MapFind(key);
			if (node != null)
			{
				valueList.Remove(node);
				return valueMap.Remove(key);
			}
			return false;
		}

		private bool PerformTableRemove(DynValue key)
		{
			if (TryGetIntegralKey(ref key, out int ik) && KeyInArray(ik))
			{
				if (arrayPart == null || ik >= arrayPart.Length) return false;
				var retval = arrayPart[ik].IsNotNil();
				arrayPart[ik] = DynValue.Nil;
				if (ik < arrayLength) arrayLength = ik;
				return retval;
			}
			return MapRemove(key);
		}

		/// <summary>
		/// Remove the value associated with the specified key from the table.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns><c>true</c> if values was successfully removed; otherwise, <c>false</c>.</returns>
		public bool Remove(string key) => PerformTableRemove(DynValue.NewString(key));

		/// <summary>
		/// Remove the value associated with the specified key from the table.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns><c>true</c> if values was successfully removed; otherwise, <c>false</c>.</returns>
		public bool Remove(int key) => PerformTableRemove(DynValue.NewNumber(key));
		

		/// <summary>
		/// Remove the value associated with the specified key from the table.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns><c>true</c> if values was successfully removed; otherwise, <c>false</c>.</returns>
		public bool Remove(DynValue key) => PerformTableRemove(key);
		

		/// <summary>
		/// Remove the value associated with the specified key from the table.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns><c>true</c> if values was successfully removed; otherwise, <c>false</c>.</returns>
		public bool Remove(object key)
		{
			return Remove(DynValue.FromObject(OwnerScript, key));
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

			object key;
			return ResolveMultipleKeys(keys, out key).Remove(key);
		}

		#endregion

		/// <summary>
		/// Collects the dead keys. This frees up memory but invalidates pending iterators.
		/// It's called automatically internally when the semantics of Lua tables allow, but can be forced
		/// externally if it's known that no iterators are pending.
		/// </summary>
		public void CollectDeadKeys()
		{
			for (LinkedListNode<TablePair> node = valueList.First; node != null; node = node.Next)
			{
				if (node.Value.Value.IsNil())
				{
					Remove(node.Value.Key);
				}
			}

			containsNilEntries = false;
		}


		/// <summary>
		/// Returns the next pair from a value
		/// </summary>

		TablePair? FirstMapNode()
		{
			LinkedListNode<TablePair> node = valueList.First;
			if (node == null)
				return TablePair.Nil;
			else
			{
				if (node.Value.Value.IsNil())
					return NextKey(node.Value.Key);
				else
					return node.Value;
			}
		}
		public TablePair? NextKey(DynValue v)
		{
			if (v.IsNil())
			{
				if (arrayPart != null)
				{
					for (int i = 0; i < arrayPart.Length; i++)
					{
						if (arrayPart[i].IsNotNil())
							return new TablePair(DynValue.NewNumber(i), arrayPart[i]);
					}
				}
				return FirstMapNode();
			}

			if (arrayPart != null && TryGetIntegralKey(ref v, out int ik) &&
			    KeyInArray(ik))
			{
				//Invalid array index
				if (ik >= arrayPart.Length || arrayPart[ik].IsNil())
					return null;
				//Search
				for (int i = ik + 1; i < arrayPart.Length; i++)
				{
					if (arrayPart[i].IsNotNil())
						return new TablePair(DynValue.NewNumber(i), arrayPart[i]);
				}
				return FirstMapNode();
			}

			return GetNextOf(MapFind(v));
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
		public int Length => arrayLength - indexFrom < 0 ? 0 : arrayLength - indexFrom;
		
		/// <summary>
		/// Gets the meta-table associated with this instance.
		/// </summary>
		public Table MetaTable
		{
			get { return m_MetaTable; }
			set { this.CheckScriptOwnership(m_MetaTable); m_MetaTable = value; }
		}
		private Table m_MetaTable;
		
		/// <summary>
		/// Gets/sets the annotations attached to this table.
		/// </summary>
		public List<Annotation> Annotations { get; set; }

		IEnumerable<TablePair> IteratePairs()
		{
			if (arrayPart != null) {
				for (int i = 0; i < arrayPart.Length; i++)
				{
					if (arrayPart[i].IsNotNil())
						yield return new TablePair(DynValue.NewNumber(i), arrayPart[i]);
				}
			}
			foreach (var x in valueList)
				yield return x;
		}

		/// <summary>
		/// Enumerates the key/value pairs.
		/// </summary>
		/// <returns></returns>
		public IEnumerable<TablePair> Pairs => IteratePairs();

		
		IEnumerable<DynValue> IterateKeys()
		{
			if (arrayPart != null) {
				for (int i = 0; i < arrayPart.Length; i++)
				{
					if (arrayPart[i].IsNotNil())
						yield return DynValue.NewNumber(i);
				}
			}
			foreach (var x in valueList)
				yield return x.Key;
		}

		/// <summary>
		/// Enumerates the keys.
		/// </summary>
		/// <returns></returns>
		public IEnumerable<DynValue> Keys => IterateKeys();

		
		IEnumerable<DynValue> IterateValues()
		{
			if (arrayPart != null) {
				for (int i = 0; i < arrayPart.Length; i++)
				{
					if (arrayPart[i].IsNotNil())
						yield return arrayPart[i];
				}
			}
			foreach (var x in valueList)
				yield return x.Value;
		}

		/// <summary>
		/// Enumerates the values
		/// </summary>
		/// <returns></returns>
		public IEnumerable<DynValue> Values => IterateValues();


		IEnumerable<DynValue> IteratePairsReverse()
		{
			if (arrayPart != null) {
				for (int i = 0; i < arrayPart.Length; i++)
				{
					if (arrayPart[i].IsNotNil())
						yield return DynValue.NewTuple( arrayPart[i], DynValue.NewNumber(i));
				}
			}
			foreach (var x in valueList)
				yield return DynValue.NewTuple(x.Value, x.Key);
		}

		/// <summary>
		/// Enumerates value, key
		/// </summary>

		public IEnumerable<DynValue> ReversePair => IteratePairsReverse();
	}
}