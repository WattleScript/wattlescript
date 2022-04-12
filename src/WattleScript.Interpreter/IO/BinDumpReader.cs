using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WattleScript.Interpreter.IO
{
	/// <summary>
	/// "Optimized" BinaryReader which shares strings and use a dumb compression for integers
	/// </summary>
	public class BinDumpReader
	{
		private Stream stream;
		public BinDumpReader(Stream s)
		{
			this.stream = s;
		}
		

		List<string> m_Strings = new List<string>();

		public byte ReadByte()
		{
			int a = stream.ReadByte();
			if (a == -1) throw new EndOfStreamException();
			return (byte)a;
		}

		public bool ReadBoolean()
		{
			return ReadByte() != 0;
		}

		public ulong ReadUInt64()
		{
			var buf = new byte[8];
			if (stream.Read(buf, 0, 8) != 8) throw new EndOfStreamException();
			return BitConverter.ToUInt64(buf, 0);
		}

		public double ReadDouble()
		{
			var buf = new byte[8];
			if (stream.Read(buf, 0, 8) != 8) throw new EndOfStreamException();
			return BitConverter.ToDouble(buf, 0);
		}

		public uint ReadVarUInt32()
		{
			uint a = 0;
			int b = stream.ReadByte();
			if (b == -1) throw new EndOfStreamException();
			a = (uint) (b & 0x7f);
			int extraCount = 0;
			//first extra
			if ((b & 0x80) == 0x80) {
				b = stream.ReadByte();
				if (b == -1) throw new EndOfStreamException();
				a |= (uint) ((b & 0x7f) << 7);
				extraCount++;
			}
			//second extra
			if ((b & 0x80) == 0x80) {
				b = stream.ReadByte();
				if (b == -1) throw new EndOfStreamException();
				a |= (uint) ((b & 0x7f) << 14);
				extraCount++;
			}
			//third extra
			if ((b & 0x80) == 0x80) {
				b = stream.ReadByte();
				if (b == -1) throw new EndOfStreamException();
				a |= (uint) ((b & 0x7f) << 21);
				extraCount++;
			}
			//fourth extra
			if ((b & 0x80) == 0x80) {
				b = stream.ReadByte();
				if (b == -1) throw new EndOfStreamException();
				a |= (uint) ((b & 0xf) << 28);
				extraCount++;
			}
			switch (extraCount) {
				case 1: a += 128; break;
				case 2: a += 16512; break;
				case 3: a += 2113663; break;
			}
			return a;
		}

		public int ReadVarInt32()
		{
			var i = (int)ReadVarUInt32();
			return ((i >>  1) ^ -(i & 1));
		}

		public string ReadString()
		{
			var pos = ReadVarUInt32();
			if (pos == 0) return null;
			if (pos == 1) return string.Empty;
			pos -= 2;
			if (pos < m_Strings.Count)
			{
				return m_Strings[(int)pos];
			}
			else if (pos == m_Strings.Count)
			{
				var len = ReadVarUInt32();
				var bytes = new byte[len];
				if (stream.Read(bytes,0,(int)len) < len) throw new EndOfStreamException();
				var str = Encoding.UTF8.GetString(bytes);
				m_Strings.Add(str);
				return str;
			}
			else
			{
				throw new IOException("string map failure");
			}
		}
	}
}
