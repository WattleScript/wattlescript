﻿using System;
using WattleScript.Interpreter.Execution.VM;

namespace WattleScript.Interpreter.Execution
{
	[Flags]
	internal enum InstructionFieldUsage
	{
		None = 0,
		NumVal = 0x1,
		NumVal2 = 0x2,
		NumVal3 = 0x4,
		NumVal1Hex = 0x8,
		NumValB = 0x10
	}

	internal static class InstructionFieldUsage_Extensions
	{
		internal static InstructionFieldUsage GetFieldUsage(this OpCode op)
		{
			switch (op)
			{
				case OpCode.Scalar:
				case OpCode.IterUpd:
				case OpCode.IterPrep:
				case OpCode.NewTable:
				case OpCode.Concat:
				case OpCode.Add:
				case OpCode.AddStr:
				case OpCode.Sub:
				case OpCode.Mul:
				case OpCode.Div:
				case OpCode.Mod:
				case OpCode.Not:
				case OpCode.Len:
				case OpCode.Neg:
				case OpCode.Power:
				case OpCode.ToBool:
				case OpCode.PushNil:
				case OpCode.PushTrue:
				case OpCode.PushFalse:
					return InstructionFieldUsage.None;
				case OpCode.Pop:
				case OpCode.Copy:
				case OpCode.ExpTuple:
				case OpCode.Incr:
				case OpCode.ToNum:
				case OpCode.Ret:
				case OpCode.MkTuple:
				case OpCode.CloseUp:
				case OpCode.StrFormat:
				case OpCode.TblInitN:
				case OpCode.LessEq:
				case OpCode.Less:
				case OpCode.Eq:
				case OpCode.CNot:
					return InstructionFieldUsage.NumVal;
				case OpCode.Jump:
				case OpCode.Jf:
				case OpCode.Jt:
				case OpCode.JNil:
				case OpCode.JNilChk:
				case OpCode.JFor:
				case OpCode.JtOrPop:
				case OpCode.JfOrPop:
					return InstructionFieldUsage.NumVal; //Address
				case OpCode.Swap:
				case OpCode.Clean:
				case OpCode.CopyValue:
				case OpCode.JLclInit:
				case OpCode.Args:
				case OpCode.TblInitI:
					return InstructionFieldUsage.NumVal | InstructionFieldUsage.NumVal2;
				case OpCode.Local:
				case OpCode.Upvalue:
					return InstructionFieldUsage.NumVal;
				case OpCode.IndexSet:
				case OpCode.IndexSetN:
				case OpCode.IndexSetL:
				case OpCode.StoreLcl:
				case OpCode.StoreUpv:
				case OpCode.NewRange:
					return InstructionFieldUsage.NumVal3 | InstructionFieldUsage.NumVal | InstructionFieldUsage.NumVal2;
				case OpCode.Index:
				case OpCode.IndexL:
				case OpCode.IndexN:
					return InstructionFieldUsage.NumVal; //string argument
				case OpCode.Closure:
					return InstructionFieldUsage.NumVal;
				case OpCode.Nop:
				case OpCode.Debug:
				case OpCode.Invalid:
				case OpCode.PushString:
				case OpCode.PushInt:
					return InstructionFieldUsage.NumVal;
				case OpCode.PushNumber:
					return InstructionFieldUsage.NumVal;
				case OpCode.Call:
				case OpCode.ThisCall:
					return InstructionFieldUsage.NumVal | InstructionFieldUsage.NumVal2;
				case OpCode.Switch:
					return InstructionFieldUsage.NumVal1Hex | InstructionFieldUsage.NumValB;
				case OpCode.SSpecial:
				case OpCode.SString:
				case OpCode.SInteger: 
				case OpCode.SNumber:
					return InstructionFieldUsage.NumVal | InstructionFieldUsage.NumValB;
				default:
					throw new NotImplementedException(string.Format("InstructionFieldUsage for instruction {0}", op));
			}
		}
	}















}
