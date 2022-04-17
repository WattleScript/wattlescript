using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.IO;

namespace WattleScript.Interpreter.Execution.VM
{
	sealed partial class Processor
	{
		const ulong DUMP_CHUNK_MAGIC = 0x0A04504D55444342; // "BCDUMP^D\n"
		const byte DUMP_CHUNK_VERSION = 0x02; //version

		internal static bool IsDumpStream(Stream stream)
		{
			if (stream.Length >= 8)
			{
				using (BinaryReader br = new BinaryReader(stream, Encoding.UTF8, true))
				{
					ulong magic = br.ReadUInt64();
					stream.Seek(-8, SeekOrigin.Current);
					return magic == DUMP_CHUNK_MAGIC;
				}
			}
			return false;
		}

		internal void DumpFuncString(FunctionProto function, StringBuilder builder)
		{
			builder.Append((function.Flags & FunctionFlags.IsChunk) == FunctionFlags.IsChunk
				? "CHUNK " : "FUNCTION ").AppendLine(function.Name);
			builder.AppendLine("-");
			if (function.Upvalues.Length > 0)
			{
				builder.AppendLine("Upvalues:");
			}
			for (int i = 0; i < function.Upvalues.Length; i++)
				builder.Append(i).Append(": ").AppendLine(function.Upvalues[i].ToString());
			if (function.Locals.Length > 0)
			{
				builder.AppendLine("Locals:");
			}
			foreach (var lcl in function.Locals) builder.AppendLine(lcl.ToString());
			builder.AppendLine("--");
			foreach (var c in function.Code) builder.AppendLine(c.ToString());
			builder.AppendLine("--");
			if (function.Functions.Length > 0) builder.AppendLine("Functions: ");
			builder.AppendLine("-");
			foreach (var p in function.Functions) {
				DumpFuncString(p, builder);
			}
		}

		internal string DumpString(FunctionProto function)
		{
			var builder = new StringBuilder();
			DumpFuncString(function, builder);
			return builder.ToString();
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
		
		internal void DumpFunction(BinDumpWriter bw, FunctionProto function, bool writeSourceRefs)
		{
			bw.WriteString(function.Name);
			bw.WriteByte((byte)function.Flags);
			bw.WriteVarUInt32((uint)function.Annotations.Length);
			foreach (var ant in function.Annotations) {
				bw.WriteString(ant.Name);
				WriteDynValue(bw, ant.Value, true);
			}
			bw.WriteVarUInt32((uint)function.LocalCount);
			//Symbols
			Dictionary<SymbolRef, int> symbolMap = new Dictionary<SymbolRef, int>();
			bw.WriteVarUInt32((uint)function.Locals.Length);
			bw.WriteVarUInt32((uint)function.Upvalues.Length);
			for (int i = 0; i < function.Locals.Length; i++) {
				symbolMap[function.Locals[i]] = i;
				function.Locals[i].WriteBinary(bw);
			}
			for (int i = 0; i < function.Upvalues.Length; i++) {
				symbolMap[function.Upvalues[i]] = i + function.Locals.Length;
				function.Upvalues[i].WriteBinary(bw);
			}
			foreach (var s in function.Locals) s.WriteBinaryEnv(bw, symbolMap);
			foreach (var s in function.Upvalues) s.WriteBinaryEnv(bw, symbolMap);
			//Constants
			bw.WriteVarUInt32((uint)function.Functions.Length);
			foreach(var f in function.Functions) DumpFunction(bw, f, writeSourceRefs);
			bw.WriteVarUInt32((uint)function.Strings.Length);
			foreach(var str in function.Strings) bw.WriteString(str);
			bw.WriteVarUInt32((uint)function.Numbers.Length);
			foreach(var dbl in function.Numbers) bw.WriteDouble(dbl);
			//Code
			bw.WriteVarUInt32((uint)function.Code.Length);
			foreach(var c in function.Code) c.WriteBinary(bw);
			bw.WriteBoolean(writeSourceRefs);
			if (writeSourceRefs)
			{
				for (int i = 0; i < function.SourceRefs.Length; i++)
				{
					if(function.SourceRefs[i] == null) bw.WriteByte(0);
					else if(i != 0 && function.SourceRefs[i] == function.SourceRefs[i - 1]) bw.WriteByte(1);
					else
					{
						bw.WriteByte(2);
						function.SourceRefs[i].WriteBinary(bw);
					}
				}
			}
		}
		internal void Dump(Stream stream, FunctionProto function, bool writeSourceRefs)
		{
			var bw = new BinDumpWriter(stream);
			bw.WriteUInt64(DUMP_CHUNK_MAGIC);
			bw.WriteByte(DUMP_CHUNK_VERSION);
			DumpFunction(bw, function, writeSourceRefs);
		}


		internal FunctionProto UndumpProto(BinDumpReader br, int sourceID)
		{
			var proto = new FunctionProto();
			proto.Name = br.ReadString();
			proto.Flags = (FunctionFlags)br.ReadByte();
			proto.Annotations = new Annotation[br.ReadVarUInt32()];
			for (int i = 0; i < proto.Annotations.Length; i++) {
				proto.Annotations[i] = new Annotation(br.ReadString(), ReadDynValue(br, true));
			}
			proto.LocalCount = (int) br.ReadVarUInt32();
			//Symbols
			proto.Locals = new SymbolRef[br.ReadVarUInt32()];
			proto.Upvalues = new SymbolRef[br.ReadVarUInt32()];
			var allsyms = new SymbolRef[proto.Locals.Length + proto.Upvalues.Length];
			for (int i = 0; i < allsyms.Length; i++) allsyms[i] = SymbolRef.ReadBinary(br);
			for (int i = 0; i < allsyms.Length; i++) allsyms[i].ReadBinaryEnv(br, allsyms);
			Array.Copy(allsyms, proto.Locals, proto.Locals.Length);
			Array.Copy(allsyms, proto.Locals.Length, proto.Upvalues, 0, proto.Upvalues.Length);
			//Constants
			proto.Functions = new FunctionProto[br.ReadVarUInt32()];
			for (int i = 0; i < proto.Functions.Length; i++) proto.Functions[i] = UndumpProto(br, sourceID);
			proto.Strings = new string[br.ReadVarUInt32()];
			for (int i = 0; i < proto.Strings.Length; i++) proto.Strings[i] = br.ReadString();
			proto.Numbers = new double[br.ReadVarUInt32()];
			for (int i = 0; i < proto.Numbers.Length; i++) proto.Numbers[i] = br.ReadDouble();
			//Code
			proto.Code = new Instruction[br.ReadVarUInt32()];
			proto.SourceRefs = new SourceRef[proto.Code.Length];
			for (int i = 0; i < proto.Code.Length; i++) proto.Code[i] = Instruction.ReadBinary(br);
			SourceRef sourceRef = new SourceRef(sourceID, 0, 0, 0, 0, false);
			if (br.ReadBoolean())
			{
				//Debug info!
				for (int i = 0; i < proto.SourceRefs.Length; i++)
				{
					switch (br.ReadByte())
					{
						case 0:
							proto.SourceRefs[i] = sourceRef;
							break;
						case 1:
							proto.SourceRefs[i] = proto.SourceRefs[i - 1];
							break;
						case 2:
							proto.SourceRefs[i] = SourceRef.ReadBinary(br, sourceID);
							break;
					}
				}
			}
			else
			{
				//No debug info
				for (int i = 0; i < proto.SourceRefs.Length; i++) proto.SourceRefs[i] = sourceRef;
			}
			return proto;
		}
	
		

		internal FunctionProto Undump(Stream stream, int sourceID)
		{
			//int baseAddress = m_RootChunk.Code.Count;

			var br = new BinDumpReader(stream);
			ulong headerMark = br.ReadUInt64();

			if (headerMark != DUMP_CHUNK_MAGIC)
				throw new ArgumentException("Not a WattleScript chunk");

			int version = br.ReadByte();

			if (version != DUMP_CHUNK_VERSION)
				throw new ArgumentException("Invalid version");
			return UndumpProto(br, sourceID);
		}
	}
}
