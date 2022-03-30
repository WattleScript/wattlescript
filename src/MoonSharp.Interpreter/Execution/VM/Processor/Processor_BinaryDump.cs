using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.IO;

namespace MoonSharp.Interpreter.Execution.VM
{
	sealed partial class Processor
	{
		const ulong DUMP_CHUNK_MAGIC = 0x504D5544234E4D01; // "\x1MN#DUMP"
		const byte DUMP_CHUNK_VERSION = 0x01; //version

		internal static bool IsDumpStream(Stream stream)
		{
			if (stream.Length >= 8)
			{
				using BinaryReader br = new BinaryReader(stream, Encoding.UTF8, true);
				ulong magic = br.ReadUInt64();
				stream.Seek(-8, SeekOrigin.Current);
				return magic == DUMP_CHUNK_MAGIC;
			}
			return false;
		}

		internal string DumpString(int baseAddress)
		{
			Instruction? meta = FindMeta(ref baseAddress);
			if (meta == null)
				throw new ArgumentException("baseAddress");
			var builder = new StringBuilder();
			for (int i = 0; i <= meta.Value.NumVal; i++)
			{
				builder.AppendLine(m_RootChunk.Code[baseAddress + i].ToString());
			}
			return builder.ToString();
		}
		
		internal int Dump(Stream stream, int baseAddress, bool hasUpvalues, bool writeSourceRefs)
		{
			var bw = new BinDumpWriter(stream);

			Dictionary<SymbolRef, int> symbolMap = new Dictionary<SymbolRef, int>();

			Instruction? meta = FindMeta(ref baseAddress);

			if (meta == null)
				throw new ArgumentException("baseAddress");

			bw.WriteUInt64(DUMP_CHUNK_MAGIC);
			bw.WriteByte(DUMP_CHUNK_VERSION);
			bw.WriteBoolean(hasUpvalues);
			bw.WriteVarUInt32((uint) meta.Value.NumVal);

			for (int i = 0; i <= meta.Value.NumVal; i++)
			{
				m_RootChunk.Code[baseAddress + i].GetSymbolReferences(out SymbolRef[] symbolList, out SymbolRef symbol);

				if (symbol != null)
					AddSymbolToMap(symbolMap, symbol);

				if (symbolList != null)
					foreach (var s in symbolList)
						AddSymbolToMap(symbolMap, s);
			}

			foreach (SymbolRef sr in symbolMap.Keys.ToArray())
			{
				if (sr.i_Env != null)
					AddSymbolToMap(symbolMap, sr.i_Env);
			}

			SymbolRef[] allSymbols = new SymbolRef[symbolMap.Count];

			foreach (KeyValuePair<SymbolRef, int> pair in symbolMap)
			{
				allSymbols[pair.Value] = pair.Key;
			}

			bw.WriteVarUInt32((uint) symbolMap.Count);

			foreach (SymbolRef sym in allSymbols)
				sym.WriteBinary(bw);

			foreach (SymbolRef sym in allSymbols)
				sym.WriteBinaryEnv(bw, symbolMap);

			for (int i = 0; i <= meta.Value.NumVal; i++)
				m_RootChunk.Code[baseAddress + i].WriteBinary(bw, baseAddress, symbolMap);
			for (int i = 0; i <= meta.Value.NumVal; i++)
			{
				if(m_RootChunk.SourceRefs[baseAddress + i] == null || !writeSourceRefs) bw.WriteByte(0);
				else if(i != 0 && m_RootChunk.SourceRefs[baseAddress + i] == m_RootChunk.SourceRefs[baseAddress + i - 1]) bw.WriteByte(1);
				else
				{
					bw.WriteByte(2);
					m_RootChunk.SourceRefs[baseAddress + i].WriteBinary(bw);
				}
			}


			return meta.Value.NumVal + baseAddress + 1;
		}

		private void AddSymbolToMap(Dictionary<SymbolRef, int> symbolMap, SymbolRef s)
		{
			if (!symbolMap.ContainsKey(s))
				symbolMap.Add(s, symbolMap.Count);
		}

		internal int Undump(Stream stream, int sourceID, Table envTable, out bool hasUpvalues)
		{
			int baseAddress = m_RootChunk.Code.Count;
			SourceRef sourceRef = new SourceRef(sourceID, 0, 0, 0, 0, false);

			var br = new BinDumpReader(stream);
			ulong headerMark = br.ReadUInt64();

			if (headerMark != DUMP_CHUNK_MAGIC)
				throw new ArgumentException("Not a MoonSharp chunk");

			int version = br.ReadByte();

			if (version != DUMP_CHUNK_VERSION)
				throw new ArgumentException("Invalid version");

			hasUpvalues = br.ReadBoolean();

			int len = (int)br.ReadVarUInt32();

			int numSymbs = (int)br.ReadVarUInt32();
			SymbolRef[] allSymbs = new SymbolRef[numSymbs];

			for (int i = 0; i < numSymbs; i++)
				allSymbs[i] = SymbolRef.ReadBinary(br);

			for (int i = 0; i < numSymbs; i++)
				allSymbs[i].ReadBinaryEnv(br, allSymbs);

			for (int i = 0; i <= len; i++) {
				Instruction I = Instruction.ReadBinary(br, baseAddress, envTable, allSymbs);
				m_RootChunk.Code.Add(I);
			}
			for (int i = 0; i <= len; i++)
			{
				var c = br.ReadByte();
				switch (c)
				{
					case 0:
						m_RootChunk.SourceRefs.Add(sourceRef);
						break;
					case 1:
						m_RootChunk.SourceRefs.Add(m_RootChunk.SourceRefs[m_RootChunk.SourceRefs.Count - 1]);
						break;
					default:
						m_RootChunk.SourceRefs.Add(SourceRef.ReadBinary(br, sourceID));
						break;
				}
			}

			return baseAddress;
		}
	}
}
