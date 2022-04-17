﻿using System;
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

		public OpCode OpCode => (OpCode) (_data & 0x7F); //7 bits
		//32 bits
		public int NumVal
		{
			get
			{
				return (int) (_data >> 7);
			}
			set
			{
				_data = (_data & ~0x7fffffff80UL) |
				        ((ulong) (uint) value) << 7;
			}
		}

		public int NumVal2 => ((int) ((_data >> 39) & 0x1FFF)) - 0xFFF; //13 bits, signed -4095 to 4095
		public uint NumVal3 => (uint) ((_data >> 52) & 0xFFF); //12 bits, unsigned 0 to 4095

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
			if (numval2 > 4096 || numval2 < -4095)
				throw new ArgumentOutOfRangeException(nameof(numval2));
			numval2 += 0xFFF;
			_data = (ulong) op |
			        ((ulong) (uint) numval) << 7 |
			        ((ulong) (uint) numval2 & 0x1FFF) << 39;
		}
		
		public Instruction(OpCode op, int numval, int numval2, uint numval3)
		{
			if (numval2 > 4096 || numval2 < -4095)
				throw new ArgumentOutOfRangeException(nameof(numval2));
			numval2 += 0xFFF;
			if (numval3 < 0 || numval3 > 4095)
				throw new ArgumentOutOfRangeException(nameof(numval3));
			_data = (ulong) op |
			        ((ulong) (uint) numval) << 7 |
			        ((ulong) (uint) numval2 & 0x1FFF) << 39  |
					((ulong) numval3 & 0xFFF) << 52;
		}

		
		public override string ToString()
		{
			string append = this.OpCode.ToString().ToUpperInvariant();

			int usage = (int)OpCode.GetFieldUsage();

			if (usage != 0)
				append += GenSpaces();

			if ((usage & ((int)InstructionFieldUsage.NumVal)) != 0)
				append += " " + NumVal.ToString();
			if ((usage & ((int)InstructionFieldUsage.NumVal2)) != 0)
				append += " " + NumVal2.ToString();
			if ((usage & ((int)InstructionFieldUsage.NumVal3)) != 0)
				append += " " + NumVal3.ToString();

			return append;
		}

		private string GenSpaces()
		{
			return new string(' ', 10 - this.OpCode.ToString().Length);
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