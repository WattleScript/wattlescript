using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WattleScript.Interpreter.IO
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
			if (u <= 127) 
			{
				stream.WriteByte((byte)u);
			} 
			else if (u <= 16511) 
			{
				u -= 128;
				stream.WriteByte((byte)((u & 0x7f) | 0x80));
				stream.WriteByte((byte)((u >> 7) & 0x7f));
			} 
			else if (u <= 2113662) 
			{
				u -= 16512;
				stream.WriteByte((byte)((u & 0x7f) | 0x80));
				stream.WriteByte((byte) (((u >> 7) & 0x7f) | 0x80));
				stream.WriteByte((byte)((u >> 14) & 0x7f));
			} 
			else if (u <= 270549118)
			{
				u -= 2113663;
				stream.WriteByte((byte)((u & 0x7f) | 0x80));
				stream.WriteByte((byte)(((u >> 7) & 0x7f) | 0x80));
				stream.WriteByte((byte)(((u >> 14) & 0x7f) | 0x80));
				stream.WriteByte((byte)((u >> 21) & 0x7f));
			}
			else
			{
				stream.WriteByte((byte)((u & 0x7f) | 0x80));
				stream.WriteByte((byte)(((u >> 7) & 0x7f) | 0x80));
				stream.WriteByte((byte)(((u >> 14) & 0x7f) | 0x80));
				stream.WriteByte((byte)(((u >> 21) & 0x7f) | 0x80));
				stream.WriteByte((byte)((u >> 28) & 0x7f));
			}
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
