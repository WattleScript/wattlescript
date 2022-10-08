using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.IO;
using WattleScript.Interpreter.Serialization;

namespace WattleScript.Interpreter.Execution.VM
{
	internal struct Instruction
	{
		private ulong _data;
		internal const int NumVal2Min = -4095;
		internal const int NumVal2Max = 4096;

		public OpCode OpCode => (OpCode) (_data & 0x7F); //7 bits
		//32 bits
		public int NumVal
		{
			get => (int) (_data >> 7);
			set => _data = (_data & ~0x7fffffff80UL) | ((ulong) (uint) value) << 7;
		}

		public int NumVal2 => ((int) ((_data >> 39) & 0x1FFF)) - 0xFFF; //13 bits, signed -4095 to 4095
		public uint NumVal3 => (uint) ((_data >> 52) & 0xFFF); //12 bits, unsigned 0 to 4095

		//NumVal2 + NumVal3
		public uint NumValB
		{
			get => (uint) (_data >> 39); //25 bits unsigned
			set
			{
				if (value > 0x1FFFFFF) throw new ArgumentOutOfRangeException("NumValB");
				_data = (_data & ~0xFFFFFF8000000000) |
				        ((ulong) value) << 39;
			}
		}

		public Instruction(OpCode op)
		{
			_data = (ulong) op;
		}

		public Instruction(OpCode op, int numval)
		{
			_data = (ulong) op |
			        ((ulong) (uint) numval) << 7;
		}

		public Instruction(OpCode op, int numval, int numval2)
		{
			if (numval2 > NumVal2Max || numval2 < NumVal2Min)
				throw new ArgumentOutOfRangeException(nameof(numval2));
			numval2 += 0xFFF;
			_data = (ulong) op |
			        ((ulong) (uint) numval) << 7 |
			        ((ulong) (uint) numval2 & 0x1FFF) << 39;
		}
		
		public Instruction(OpCode op, int numval, int numval2, uint numval3)
		{
			if (numval2 > NumVal2Max || numval2 < NumVal2Min)
				throw new ArgumentOutOfRangeException(nameof(numval2));
			numval2 += 0xFFF;
			if (numval3 > 4095)
				throw new ArgumentOutOfRangeException(nameof(numval3));
			_data = (ulong) op |
			        ((ulong) (uint) numval) << 7 |
			        ((ulong) (uint) numval2 & 0x1FFF) << 39  |
					((ulong) numval3 & 0xFFF) << 52;
		}

		
		public override string ToString()
		{
			string append = OpCode.ToString().ToUpperInvariant();

			int usage = (int)OpCode.GetFieldUsage();

			if (usage != 0)
				append += GenSpaces();

			if ((usage & ((int) InstructionFieldUsage.NumVal)) != 0)
				append += " " + NumVal;
			else if ((usage & ((int) InstructionFieldUsage.NumVal1Hex)) != 0)
				append += " 0x" + NumVal.ToString("X");
			if ((usage & ((int) InstructionFieldUsage.NumVal2)) != 0)
				append += " " + NumVal2;
			if ((usage & ((int) InstructionFieldUsage.NumVal3)) != 0)
				append += " " + NumVal3;
			if ((usage & ((int) InstructionFieldUsage.NumValB)) != 0)
				append += " " + NumValB;
			
			return append;
		}

		private string GenSpaces()
		{
			return new string(' ', 10 - OpCode.ToString().Length);
		}

		internal void WriteBinary(BinDumpWriter wr)
		{
			wr.WriteUInt64(_data);
		}

		internal static Instruction ReadBinary(BinDumpReader rd)
		{
			return new Instruction() {_data = rd.ReadUInt64()};
		}
	}
}
