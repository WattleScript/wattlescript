using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MoonSharp.Interpreter.IO
{
	/// <summary>
	/// "Optimized" BinaryWriter which shares strings and use a dumb compression for integers
	/// </summary>
	public class BinDumpWriter
	{
		private uint m_stringCount = 2;
		private Dictionary<string, uint> m_stringMap = new Dictionary<string, uint>();
		
		private Stream stream;

		public BinDumpWriter(Stream s)
		{
			stream = s;
		}
		
		public void WriteByte(byte b)
		{
			stream.WriteByte(b);
		}

		public void WriteBoolean(bool b)
		{
			stream.WriteByte(b ? (byte)1 : (byte)0);
		}

		public void WriteUInt64(ulong u)
		{
			var bytes = BitConverter.GetBytes(u);
			stream.Write(bytes, 0, bytes.Length);
		}

		public void WriteDouble(double d)
		{
			var bytes = BitConverter.GetBytes(d);
			stream.Write(bytes, 0, bytes.Length);
		}

		public void WriteVarUInt32(uint u)
		{
			do
			{
				var b = (byte) (u & 0x7f);
				u >>= 7;
				if (u != 0) {
					b |= 0x80;
				}
				stream.WriteByte(b);
			} while (u != 0);
		}

		public void WriteVarInt32(int i)
		{
			WriteVarUInt32((uint)((i >> 31) ^ (i << 1)));
		}

		public void WriteString(string s)
		{
			if (s == null) 
			{
				WriteVarUInt32(0);	
			} 
			else if (s == string.Empty)
			{
				WriteVarUInt32(1);
			}
			else if (m_stringMap.TryGetValue(s, out uint index))
			{
				WriteVarUInt32(index);
			}
			else
			{
				WriteVarUInt32(m_stringCount);
				m_stringMap[s] = m_stringCount;
				m_stringCount++;
				var bytes = Encoding.UTF8.GetBytes(s);
				WriteVarUInt32((uint)bytes.Length);
				stream.Write(bytes, 0, bytes.Length);
			}
		}
	}
}
