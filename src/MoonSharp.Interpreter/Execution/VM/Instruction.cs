using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

		internal Annotation[] Annotations
		{
			get => _object as Annotation[];
			set => _object = value;
		}
		
		
		public override string ToString()
		{
			string append = this.OpCode.ToString().ToUpperInvariant();

			int usage = (int)OpCode.GetFieldUsage();

			if (usage != 0)
				append += GenSpaces();

			if ((this.OpCode == VM.OpCode.Meta) ||((usage & ((int)InstructionFieldUsage.NumValAsCodeAddress)) == (int)InstructionFieldUsage.NumValAsCodeAddress))
				append += " " + NumVal.ToString("X8");
			else if ((usage & ((int)InstructionFieldUsage.NumVal)) != 0)
				append += " " + NumVal.ToString();
			else if ((usage & ((int)InstructionFieldUsage.Number)) != 0)
				append += " " + Number.ToString();
			if ((usage & ((int)InstructionFieldUsage.NumVal2)) != 0)
				append += " " + NumVal2.ToString();

			if ((usage & ((int)InstructionFieldUsage.String)) != 0)
				append += " " + (String == null ? "<no string arg>" : DynValue.NewString(String).SerializeValue());

			if ((usage & ((int)InstructionFieldUsage.Symbol)) != 0)
				append += " " + Symbol;

			if (((usage & ((int)InstructionFieldUsage.SymbolList)) != 0) && (SymbolList != null))
				append += " " + string.Join(",", SymbolList.Select(s => s.ToString()).ToArray());

			if (((usage & ((int) InstructionFieldUsage.Annotations)) != 0) && (Annotations != null))
				append += " " + string.Join(",", Annotations.Select((x => x.ToString())));

			return append;
		}

		private string GenSpaces()
		{
			return new string(' ', 10 - this.OpCode.ToString().Length);
		}

		internal void WriteBinary(BinDumpWriter wr, int baseAddress, Dictionary<SymbolRef, int> symbolMap)
		{
			wr.WriteByte((byte)this.OpCode);

			int usage = (int)OpCode.GetFieldUsage();

			if ((usage & (int) InstructionFieldUsage.Number) == 0
			    && (usage & (int) InstructionFieldUsage.NumVal2) == 0 &&
			    (usage & (int) InstructionFieldUsage.NumVal) == 0 &&
			    (usage & (int) InstructionFieldUsage.NumValAsCodeAddress) == 0 &&
			    (NumVal != 0 || NumVal2 != 0))
				throw new Exception("NumVal usage");
			
			if ((usage & ((int)InstructionFieldUsage.String)) == 0 &&
				(usage & ((int)InstructionFieldUsage.Symbol)) == 0 && 
			    (usage & ((int)InstructionFieldUsage.SymbolList)) == 0 &&
				(usage & ((int)InstructionFieldUsage.Annotations)) == 0 && 
				_object != null)
			{
				throw new Exception("Object usage");
			}
						
			if ((usage & ((int) InstructionFieldUsage.Number)) == (int) InstructionFieldUsage.Number)
			{
				wr.WriteDouble(this.Number);
			}
			else
			{
				if ((usage & ((int) InstructionFieldUsage.NumValAsCodeAddress)) ==
				    (int) InstructionFieldUsage.NumValAsCodeAddress)
					wr.WriteVarInt32(this.NumVal - baseAddress);
				else if ((usage & ((int) InstructionFieldUsage.NumVal)) != 0)
					wr.WriteVarInt32(this.NumVal);
				if ((usage & ((int) InstructionFieldUsage.NumVal2)) != 0)
					wr.WriteVarInt32(this.NumVal2);
			}

			if ((usage & ((int)InstructionFieldUsage.String)) != 0)
				wr.WriteString(String);

			if ((usage & ((int)InstructionFieldUsage.Symbol)) != 0)
				WriteSymbol(wr, Symbol, symbolMap);

			if ((usage & ((int)InstructionFieldUsage.SymbolList)) != 0)
			{
				wr.WriteVarUInt32((uint)this.SymbolList.Length);
				for (int i = 0; i < this.SymbolList.Length; i++)
					WriteSymbol(wr, SymbolList[i], symbolMap);
			}
			
			if ((usage & ((int) InstructionFieldUsage.Annotations)) != 0)
			{
				wr.WriteVarUInt32((uint)this.Annotations.Length);
				for (int i = 0; i < Annotations.Length; i++)
				{
					wr.WriteString(Annotations[i].Name);
					WriteDynValue(wr, Annotations[i].Value, true);
				}
			}
		}

		private static void WriteSymbol(BinDumpWriter wr, SymbolRef symbolRef, Dictionary<SymbolRef, int> symbolMap)
		{
			int id = (symbolRef == null) ? 0 : symbolMap[symbolRef] + 1;
			wr.WriteVarUInt32((uint)id);
		}

		private static SymbolRef ReadSymbol(BinDumpReader rd, SymbolRef[] deserializedSymbols)
		{
			uint id = rd.ReadVarUInt32();

			if (id < 1) return null;
			return deserializedSymbols[id - 1];
		}

		static void WriteDynValue(BinDumpWriter wr, DynValue d, bool allowTable)
		{
			switch (d.Type) {
				case DataType.Nil:
					wr.WriteByte((byte)d.Type);
					break;
				case DataType.Void:
					wr.WriteByte((byte)d.Type);
					break;
				case DataType.Boolean:
					if(d.Boolean) wr.WriteByte((byte)DataType.Boolean | 0x80);
					else wr.WriteByte((byte)DataType.Boolean);
					break;
				case DataType.String:
					wr.WriteByte((byte)d.Type);
					wr.WriteString(d.String);
					break;
				case DataType.Number:
					wr.WriteByte((byte)d.Type);
					wr.WriteDouble(d.Number);
					break;
				case DataType.Table when allowTable:
					wr.WriteByte((byte)d.Type);
					WriteTable(wr, d.Table);
					break;
				case DataType.Table when !allowTable:
					throw new Exception("Stored table key cannot be table"); 
				default:
					throw new Exception("Can only store DynValue of string/number/bool/nil/table");
			}
		}

		static void WriteTable(BinDumpWriter wr, Table t)
		{
			//this enumerates twice. not ideal
			wr.WriteVarInt32(t.Pairs.Count());
			foreach (var p in t.Pairs)
			{
				WriteDynValue(wr, p.Key, false);
				WriteDynValue(wr, p.Value, true);
			}
		}

		static DynValue ReadDynValue(BinDumpReader rd, bool allowTable)
		{
			var b = rd.ReadByte();
			var type = (DataType) (b & 0x7f);
			switch (type) {
				case DataType.Nil:
					return DynValue.Nil;
				case DataType.Void:
					return DynValue.Void;
				case DataType.Boolean:
					if ((b & 0x80) == 0x80) return DynValue.True;
					return DynValue.False;
				case DataType.String:
					return DynValue.NewString(rd.ReadString());
				case DataType.Number:
					return DynValue.NewNumber(rd.ReadDouble());
				case DataType.Table when allowTable:
					return ReadTable(rd);
				default:
					throw new InternalErrorException("Invalid DynValue storage in bytecode");
			}
		}

		static DynValue ReadTable(BinDumpReader rd)
		{
			var d = DynValue.NewPrimeTable();
			var table = d.Table;
			var c = rd.ReadVarInt32();
			for (int i = 0; i < c; i++) {
				table.Set(ReadDynValue(rd, false), ReadDynValue(rd, true));
			}
			return d;
		}

		internal static Instruction ReadBinary(BinDumpReader rd, int baseAddress, Table envTable, SymbolRef[] deserializedSymbols)
		{
			Instruction that = new Instruction();

			that.OpCode = (OpCode)rd.ReadByte();

			int usage = (int)that.OpCode.GetFieldUsage();

			if ((usage & ((int) InstructionFieldUsage.Number)) == (int)InstructionFieldUsage.Number)
			{
				that.Number = rd.ReadDouble();
			}
			else
			{
				if ((usage & ((int) InstructionFieldUsage.NumValAsCodeAddress)) ==
				    (int) InstructionFieldUsage.NumValAsCodeAddress)
					that.NumVal = rd.ReadVarInt32() + baseAddress;
				else if ((usage & ((int) InstructionFieldUsage.NumVal)) != 0)
					that.NumVal = rd.ReadVarInt32();

				if ((usage & ((int) InstructionFieldUsage.NumVal2)) != 0)
					that.NumVal2 = rd.ReadVarInt32();
			}

			if ((usage & ((int)InstructionFieldUsage.String)) != 0)
				that.String = rd.ReadString();

			if ((usage & ((int)InstructionFieldUsage.Symbol)) != 0)
				that.Symbol = ReadSymbol(rd, deserializedSymbols);

			if ((usage & ((int)InstructionFieldUsage.SymbolList)) != 0)
			{
				int len = (int)rd.ReadVarUInt32();
				that.SymbolList = new SymbolRef[len];

				for (int i = 0; i < that.SymbolList.Length; i++)
					that.SymbolList[i] = ReadSymbol(rd, deserializedSymbols);
			}

			if ((usage & ((int) InstructionFieldUsage.Annotations)) != 0)
			{
				int len = (int) rd.ReadVarUInt32();
				that.Annotations = new Annotation[len];
				for (int i = 0; i < that.Annotations.Length; i++)
				{
					that.Annotations[i] = new Annotation(rd.ReadString(), ReadDynValue(rd, true));
				}
			}
			

			return that;
		}

		internal void GetSymbolReferences(out SymbolRef[] symbolList, out SymbolRef symbol)
		{
			int usage = (int)OpCode.GetFieldUsage();

			symbol = null;
			symbolList = null;

			if ((usage & ((int)InstructionFieldUsage.Symbol)) != 0)
				symbol = this.Symbol;

			if ((usage & ((int)InstructionFieldUsage.SymbolList)) != 0)
				symbolList = this.SymbolList;
		}
	}
}
