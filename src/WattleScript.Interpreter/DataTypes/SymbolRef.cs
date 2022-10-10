using System.Collections.Generic;
using System.IO;
using WattleScript.Interpreter.IO;

namespace WattleScript.Interpreter
{
	/// <summary>
	/// This class stores a possible l-value (that is a potential target of an assignment)
	/// </summary>
	public class SymbolRef
	{
		private static SymbolRef s_DefaultEnv = new SymbolRef() { i_Type = SymbolRefType.DefaultEnv };

		// Fields are internal - direct access by the executor was a 10% improvement at profiling here!
		internal SymbolRefType i_Type;
		internal SymbolRef i_Env;
		internal int i_Index;
		internal string i_Name;

		/// <summary>
		/// Gets the type of this symbol reference
		/// </summary>
		public SymbolRefType Type { get { return i_Type; } }
		/// <summary>
		/// Gets the index of this symbol in its scope context
		/// </summary>
		public int Index { get { return i_Index; } }
		/// <summary>
		/// Gets the name of this symbol
		/// </summary>
		public string Name { get { return i_Name; } }
		/// <summary>
		/// Gets the environment this symbol refers to (for global symbols only)
		/// </summary>
		public SymbolRef Environment { get { return i_Env; } }
		
		
		/// <summary>
		/// Sets whether or not this symbol refers to a class's base class.
		/// This option is not serialized.
		/// </summary>
		public bool IsBaseClass { get; set; }
		
		/// <summary>
		/// Sets whether or not this symbol is the `this` argument of a function
		/// </summary>
		public bool IsThisArgument { get; set; }
		
		/// <summary>
		/// Set to true if this symbol is a placeholder (no allocation)
		/// </summary>
		public bool Placeholder { get; set; }


		/// <summary>
		/// Gets the default _ENV.
		/// </summary>
		public static SymbolRef DefaultEnv { get { return s_DefaultEnv; } }

		/// <summary>
		/// Creates a new symbol reference pointing to a global var
		/// </summary>
		/// <param name="name">The name.</param>
		/// <param name="envSymbol">The _ENV symbol.</param>
		/// <returns></returns>
		public static SymbolRef Global(string name, SymbolRef envSymbol)
		{
			return new SymbolRef() { i_Index = -1, i_Type = SymbolRefType.Global, i_Env = envSymbol, i_Name = name };
		}

		/// <summary>
		/// Creates a new symbol reference pointing to a local var
		/// </summary>
		/// <param name="name">The name.</param>
		/// <param name="index">The index of the var in local scope.</param>
		/// <returns></returns>
		internal static SymbolRef Local(string name, int index)
		{
			//Debug.Assert(index >= 0, "Symbol Index < 0");
			return new SymbolRef() { i_Index = index, i_Type = SymbolRefType.Local, i_Name = name };
		}

		/// <summary>
		/// Creates a new symbol reference pointing to an upvalue var
		/// </summary>
		/// <param name="name">The name.</param>
		/// <param name="index">The index of the var in closure scope.</param>
		/// <returns></returns>
		internal static SymbolRef Upvalue(string name, int index)
		{
			//Debug.Assert(index >= 0, "Symbol Index < 0");
			return new SymbolRef() { i_Index = index, i_Type = SymbolRefType.Upvalue, i_Name = name };
		}

		/// <summary>
		/// Returns a <see cref="System.String" /> that represents this instance.
		/// </summary>
		/// <returns>
		/// A <see cref="System.String" /> that represents this instance.
		/// </returns>
		public override string ToString()
		{
			if (i_Type == SymbolRefType.DefaultEnv)
				return "(default _ENV)";
			else
			if (i_Type == SymbolRefType.Global)
				return string.Format("{2} : {0} / {1}", i_Type, i_Env, i_Name);
			else
				return string.Format("{2} : {0}[{1}]", i_Type, i_Index, i_Name);
		}

		/// <summary>
		/// Writes this instance to a binary stream
		/// </summary>
		internal void WriteBinary(BinDumpWriter bw)
		{
			bw.WriteByte((byte)this.i_Type);
			bw.WriteVarInt32(i_Index);
			bw.WriteString(i_Name);
		}

		/// <summary>
		/// Reads a symbolref from a binary stream 
		/// </summary>
		internal static SymbolRef ReadBinary(BinDumpReader br)
		{
			SymbolRef that = new SymbolRef();
			that.i_Type = (SymbolRefType)br.ReadByte();
			that.i_Index = br.ReadVarInt32();
			that.i_Name = br.ReadString();
			return that;
		}

		internal void WriteBinaryEnv(BinDumpWriter bw, Dictionary<SymbolRef, int> symbolMap)
		{
			if (this.i_Env != null)
				bw.WriteVarUInt32((uint)(symbolMap[i_Env] + 1));
			else
				bw.WriteVarUInt32(0);
		}

		internal void ReadBinaryEnv(BinDumpReader br, SymbolRef[] symbolRefs)
		{
			uint idx = br.ReadVarUInt32();

			if (idx >= 1)
				i_Env = symbolRefs[idx - 1];
		}
	}
}
