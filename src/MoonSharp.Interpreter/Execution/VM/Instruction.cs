using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.IO;
using MoonSharp.Interpreter.Serialization;

namespace MoonSharp.Interpreter.Execution.VM
{
	[StructLayout(LayoutKind.Explicit)]
	internal struct Instruction
	{
		[FieldOffset(0)] internal OpCode OpCode;
		[FieldOffset(4)] internal double Number;
		[FieldOffset(4)] internal int NumVal;
		[FieldOffset(8)] internal int NumVal2;
		[FieldOffset(16)] object _object;

		internal static Instruction OpCodeCNot = new Instruction() {OpCode = OpCode.CNot};
		internal static Instruction OpCodeToBool = new Instruction() {OpCode = OpCode.ToBool};
		internal static Instruction OpCodeNil = new Instruction() {OpCode = OpCode.PushNil};
		internal static Instruction OpCodeIterPrep = new Instruction() {OpCode = OpCode.IterPrep};
		internal static Instruction OpCodeIterUpd = new Instruction() {OpCode = OpCode.IterUpd};
		
		internal SymbolRef Symbol
		{
			get => _object as SymbolRef;
			set => _object = value;
		}

		internal SymbolRef[] SymbolList
		{
			get => _object as SymbolRef[];
			set => _object = value;
		}

		internal string String
		{
			get => _object as string;
			set => _object = value;
		}
		
		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(OpCode.ToString().ToUpperInvariant());

			int usage = (int)OpCode.GetFieldUsage();

			if (usage != 0)
				sb.Append(GenSpaces());

			if (OpCode == OpCode.Meta || (usage & (int)InstructionFieldUsage.NumValAsCodeAddress) == (int)InstructionFieldUsage.NumValAsCodeAddress)
				sb.Append($" {NumVal:X8}");
			else if ((usage & (int) InstructionFieldUsage.NumVal) != 0)
				sb.Append($" {NumVal}");
			else if ((usage & ((int) InstructionFieldUsage.Number)) != 0)
				sb.Append($" {Number}");
			if ((usage & (int) InstructionFieldUsage.NumVal2) != 0)
				sb.Append($" {NumVal2}");
			if ((usage & (int)InstructionFieldUsage.String) != 0)
				sb.Append($" {(String == null ? "<no string arg>" : DynValue.NewString(String).SerializeValue())}");
			if ((usage & (int)InstructionFieldUsage.Symbol) != 0)
				sb.Append($" {Symbol}");
			if ((usage & (int)InstructionFieldUsage.SymbolList) != 0 && SymbolList != null)
				sb.Append($" {string.Join(",", SymbolList.Select(s => s.ToString()).ToArray())}");

			return sb.ToString();
		}

		private string GenSpaces()
		{
			return new string(' ', 10 - OpCode.ToString().Length);
		}

		internal void WriteBinary(BinDumpWriter wr, int baseAddress, Dictionary<SymbolRef, int> symbolMap)
		{
			wr.WriteByte((byte)OpCode);

			int usage = (int)OpCode.GetFieldUsage();

			if ((usage & (int) InstructionFieldUsage.Number) == 0
			    && (usage & (int) InstructionFieldUsage.NumVal2) == 0 &&
			    (usage & (int) InstructionFieldUsage.NumVal) == 0 &&
			    (usage & (int) InstructionFieldUsage.NumValAsCodeAddress) == 0 &&
			    (NumVal != 0 || NumVal2 != 0))
				throw new Exception("NumVal usage");
			
			if ((usage & (int)InstructionFieldUsage.String) == 0 &&
				(usage & (int)InstructionFieldUsage.Symbol) == 0 && 
					(usage & (int)InstructionFieldUsage.SymbolList) == 0 &&
				_object != null)
			{
				throw new Exception("Object usage");
			}
						
			if ((usage & (int) InstructionFieldUsage.Number) == (int) InstructionFieldUsage.Number)
			{
				wr.WriteDouble(Number);
			}
			else
			{
				if ((usage & (int) InstructionFieldUsage.NumValAsCodeAddress) ==
				    (int) InstructionFieldUsage.NumValAsCodeAddress)
					wr.WriteVarInt32(NumVal - baseAddress);
				else if ((usage & (int) InstructionFieldUsage.NumVal) != 0)
					wr.WriteVarInt32(NumVal);
				if ((usage & (int) InstructionFieldUsage.NumVal2) != 0)
					wr.WriteVarInt32(NumVal2);
			}

			if ((usage & (int)InstructionFieldUsage.String) != 0)
				wr.WriteString(String);

			if ((usage & (int)InstructionFieldUsage.Symbol) != 0)
				WriteSymbol(wr, Symbol, symbolMap);

			if ((usage & (int)InstructionFieldUsage.SymbolList) != 0)
			{
				wr.WriteVarUInt32((uint)SymbolList.Length);
				for (int i = 0; i < SymbolList.Length; i++)
					WriteSymbol(wr, SymbolList[i], symbolMap);
			}
		}

		private static void WriteSymbol(BinDumpWriter wr, SymbolRef symbolRef, Dictionary<SymbolRef, int> symbolMap)
		{
			int id = symbolRef == null ? 0 : symbolMap[symbolRef] + 1;
			wr.WriteVarUInt32((uint)id);
		}

		private static SymbolRef ReadSymbol(BinDumpReader rd, SymbolRef[] deserializedSymbols)
		{
			uint id = rd.ReadVarUInt32();
			return id < 1 ? null : deserializedSymbols[id - 1];
		}

		internal static Instruction ReadBinary(BinDumpReader rd, int baseAddress, Table envTable, SymbolRef[] deserializedSymbols)
		{
			Instruction that = new Instruction
			{
				OpCode = (OpCode)rd.ReadByte()
			};

			int usage = (int)that.OpCode.GetFieldUsage();

			if ((usage & (int) InstructionFieldUsage.Number) == (int)InstructionFieldUsage.Number)
			{
				that.Number = rd.ReadDouble();
			}
			else
			{
				if ((usage & (int) InstructionFieldUsage.NumValAsCodeAddress) ==
				    (int) InstructionFieldUsage.NumValAsCodeAddress)
					that.NumVal = rd.ReadVarInt32() + baseAddress;
				else if ((usage & (int) InstructionFieldUsage.NumVal) != 0)
					that.NumVal = rd.ReadVarInt32();

				if ((usage & (int) InstructionFieldUsage.NumVal2) != 0)
					that.NumVal2 = rd.ReadVarInt32();
			}

			if ((usage & (int)InstructionFieldUsage.String) != 0)
				that.String = rd.ReadString();

			if ((usage & (int)InstructionFieldUsage.Symbol) != 0)
				that.Symbol = ReadSymbol(rd, deserializedSymbols);

			if ((usage & (int)InstructionFieldUsage.SymbolList) != 0)
			{
				int len = (int)rd.ReadVarUInt32();
				that.SymbolList = new SymbolRef[len];

				for (int i = 0; i < that.SymbolList.Length; i++)
					that.SymbolList[i] = ReadSymbol(rd, deserializedSymbols);
			}

			return that;
		}

		internal void GetSymbolReferences(out SymbolRef[] symbolList, out SymbolRef symbol)
		{
			int usage = (int)OpCode.GetFieldUsage();

			symbol = null;
			symbolList = null;

			if ((usage & (int)InstructionFieldUsage.Symbol) != 0)
				symbol = Symbol;

			if ((usage & (int)InstructionFieldUsage.SymbolList) != 0)
				symbolList = SymbolList;
		}
	}
}
