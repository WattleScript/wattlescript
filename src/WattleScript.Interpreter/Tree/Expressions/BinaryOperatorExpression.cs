using System;
using System.Collections.Generic;
using WattleScript.Interpreter.DataStructs;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;

namespace WattleScript.Interpreter.Tree.Expressions
{
	[Flags]
	public enum Operator : ulong
	{
		NotAnOperator = 0,
		Or = 0x1, 
		And = 0x2,
		Less = 0x4,
		Greater = 0x8,
		LessOrEqual = 0x10,
		GreaterOrEqual = 0x20,
		NotEqual = 0x40,
		Equal = 0x80,
		StrConcat = 0x100,
		Add = 0x200,
		Sub = 0x400,
		Mul = 0x1000,
		Div = 0x2000,
		Mod = 0x4000,
		Power = 0x8000,
		AddConcat = 0x10000,
		NilCoalescing = 0x20000,
		BitAnd = 0x40000,
		BitOr = 0x80000,
		BitXor = 0x100000,
		BitLShift = 0x200000,
		BitRShiftA = 0x400000,
		BitRShiftL = 0x4800000,
		NilCoalescingInverse = 0x9000000,
		InclusiveRange = 0x12000000,
		LeftExclusiveRange = 0x24000000,
		RightExclusiveRange = 0x48000000,
		ExclusiveRange = 0x98000000,
	}
	
	class BinaryOperatorExpression : Expression
	{
		class Node
		{
			public Expression Expr;
			public Operator Op;
			public Node Prev;
			public Node Next;
		}

		class LinkedList
		{
			public Node Nodes;
			public Node Last;
			public Operator OperatorMask;
		}

		const Operator POWER = Operator.Power;
		const Operator MUL_DIV_MOD = Operator.Mul | Operator.Div | Operator.Mod;
		const Operator ADD_SUB = Operator.Add | Operator.Sub | Operator.AddConcat;
		const Operator STRCAT = Operator.StrConcat;
		const Operator COMPARES = Operator.Less | Operator.Greater | Operator.GreaterOrEqual | Operator.LessOrEqual | Operator.Equal | Operator.NotEqual;
		const Operator LOGIC_AND = Operator.And;
		const Operator LOGIC_OR = Operator.Or;
		const Operator NIL_COAL_ASSIGN = Operator.NilCoalescing;
		const Operator SHIFTS = Operator.BitLShift | Operator.BitRShiftA | Operator.BitRShiftL;
		const Operator NIL_COAL_INVERSE = Operator.NilCoalescingInverse;
		const Operator RANGES = Operator.InclusiveRange | Operator.ExclusiveRange | Operator.LeftExclusiveRange | Operator.RightExclusiveRange;

		public static object BeginOperatorChain()
		{
			return new LinkedList();
		}

		public static void AddExpressionToChain(object chain, Expression exp)
		{
			LinkedList list = (LinkedList)chain;
			Node node = new Node() { Expr = exp };
			AddNode(list, node);
		}


		public static void AddOperatorToChain(object chain, Token op)
		{
			LinkedList list = (LinkedList)chain;
			Node node = new Node() { Op = ParseBinaryOperator(op) };
			AddNode(list, node);
		}

		public static Expression CommitOperatorChain(object chain, ScriptLoadingContext lcontext)
		{
			return CreateSubTree((LinkedList)chain, lcontext);
		}

		public static Expression CreatePowerExpression(Expression op1, Expression op2, ScriptLoadingContext lcontext)
		{
			return new BinaryOperatorExpression(op1, op2, Operator.Power, lcontext);
		}


		private static void AddNode(LinkedList list, Node node)
		{
			list.OperatorMask |= node.Op;

			if (list.Nodes == null)
			{
				list.Nodes = list.Last = node;
			}
			else
			{
				list.Last.Next = node;
				node.Prev = list.Last;
				list.Last = node;
			}
		}


		/// <summary>
		/// Creates a sub tree of binary expressions
		/// </summary>
		private static Expression CreateSubTree(LinkedList list, ScriptLoadingContext lcontext)
		{
			Operator opfound = list.OperatorMask;

			Node nodes = list.Nodes;

			if ((opfound & POWER) != 0)
				nodes = PrioritizeRightAssociative(nodes, lcontext, POWER);

			if ((opfound & MUL_DIV_MOD) != 0)
				nodes = PrioritizeLeftAssociative(nodes, lcontext, MUL_DIV_MOD);

			if ((opfound & ADD_SUB) != 0)
				nodes = PrioritizeLeftAssociative(nodes, lcontext, ADD_SUB);
			
			if ((opfound & RANGES) != 0)
				nodes = PrioritizeLeftAssociative(nodes, lcontext, RANGES);

			if ((opfound & STRCAT) != 0)
				nodes = PrioritizeRightAssociative(nodes, lcontext, STRCAT);

			if ((opfound & SHIFTS) != 0)
				nodes = PrioritizeLeftAssociative(nodes, lcontext, SHIFTS);

			if ((opfound & COMPARES) != 0)
				nodes = PrioritizeLeftAssociative(nodes, lcontext, COMPARES);

			if ((opfound & Operator.BitAnd) != 0)
				nodes = PrioritizeLeftAssociative(nodes, lcontext, Operator.BitAnd);
			
			if ((opfound & Operator.BitXor) != 0)
				nodes = PrioritizeLeftAssociative(nodes, lcontext, Operator.BitXor);
			
			if ((opfound & Operator.BitOr) != 0)
				nodes = PrioritizeLeftAssociative(nodes, lcontext, Operator.BitOr);

			if ((opfound & LOGIC_AND) != 0)
				nodes = PrioritizeLeftAssociative(nodes, lcontext, LOGIC_AND);

			if ((opfound & LOGIC_OR) != 0)
				nodes = PrioritizeLeftAssociative(nodes, lcontext, LOGIC_OR);

			if ((opfound & NIL_COAL_ASSIGN) != 0)
				nodes = PrioritizeLeftAssociative(nodes, lcontext, NIL_COAL_ASSIGN);
			
			if ((opfound & NIL_COAL_INVERSE) != 0)
				nodes = PrioritizeLeftAssociative(nodes, lcontext, NIL_COAL_INVERSE);

			if (nodes.Next != null || nodes.Prev != null)
				throw new InternalErrorException("Expression reduction didn't work! - 1");
			if (nodes.Expr == null)
				throw new InternalErrorException("Expression reduction didn't work! - 2");
			
			return nodes.Expr;
		}

		private static Node PrioritizeLeftAssociative(Node nodes, ScriptLoadingContext lcontext, Operator operatorsToFind)
		{
			for (Node N = nodes; N != null; N = N.Next)
			{
				Operator o = N.Op;

				if ((o & operatorsToFind) != 0)
				{
					N.Op = Operator.NotAnOperator;
					N.Expr = new BinaryOperatorExpression(N.Prev.Expr, N.Next.Expr, o, lcontext);
					N.Prev = N.Prev.Prev;
					N.Next = N.Next.Next;

					if (N.Next != null)
						N.Next.Prev = N;

					if (N.Prev != null)
						N.Prev.Next = N;
					else
						nodes = N;
				}
			}

			return nodes;
		}

		private static Node PrioritizeRightAssociative(Node nodes, ScriptLoadingContext lcontext, Operator operatorsToFind)
		{
			Node last;
			for (last = nodes; last.Next != null; last = last.Next)
			{
			}

			for (Node N = last; N != null; N = N.Prev)
			{
				Operator o = N.Op;

				if ((o & operatorsToFind) != 0)
				{
					N.Op = Operator.NotAnOperator;
					N.Expr = new BinaryOperatorExpression(N.Prev.Expr, N.Next.Expr, o, lcontext);
					N.Prev = N.Prev.Prev;
					N.Next = N.Next.Next;

					if (N.Next != null)
						N.Next.Prev = N;

					if (N.Prev != null)
						N.Prev.Next = N;
					else
						nodes = N;
				}
			}

			return nodes;
		}


		private static Operator ParseBinaryOperator(Token token)
		{
			switch (token.Type)
			{
				case TokenType.Or:
					return Operator.Or;
				case TokenType.And:
					return Operator.And;
				case TokenType.Op_LessThan:
					return Operator.Less;
				case TokenType.Op_GreaterThan:
					return Operator.Greater;
				case TokenType.Op_LessThanEqual:
					return Operator.LessOrEqual;
				case TokenType.Op_GreaterThanEqual:
					return Operator.GreaterOrEqual;
				case TokenType.Op_NotEqual:
					return Operator.NotEqual;
				case TokenType.Op_Equal:
					return Operator.Equal;
				case TokenType.Op_Concat:
					return Operator.StrConcat;
				case TokenType.Op_Add:
					return Operator.Add;
				case TokenType.Op_MinusOrSub:
					return Operator.Sub;
				case TokenType.Op_Mul:
					return Operator.Mul;
				case TokenType.Op_Div:
					return Operator.Div;
				case TokenType.Op_Mod:
					return Operator.Mod;
				case TokenType.Op_Pwr:
					return Operator.Power;
				case TokenType.Op_NilCoalesce:
					return Operator.NilCoalescing;
				case TokenType.Op_NilCoalesceInverse:
					return Operator.NilCoalescingInverse;
				case TokenType.Op_Or:
					return Operator.BitOr;
				case TokenType.Op_And:
					return Operator.BitAnd;
				case TokenType.Op_Xor:
					return Operator.BitXor;
				case TokenType.Op_LShift:
					return Operator.BitLShift;
				case TokenType.Op_RShiftArithmetic:
					return Operator.BitRShiftA;
				case TokenType.Op_RShiftLogical:
					return Operator.BitRShiftL;
				case TokenType.Op_InclusiveRange:
					return Operator.InclusiveRange;
				case TokenType.Op_ExclusiveRange:
					return Operator.ExclusiveRange;
				case TokenType.Op_LeftExclusiveRange:
					return Operator.LeftExclusiveRange;
				case TokenType.Op_RightExclusiveRange:
					return Operator.RightExclusiveRange;
				default:
					throw new InternalErrorException("Unexpected binary operator '{0}'", token.Text);
			}
		}


		private readonly Expression m_Exp1;
		private readonly Expression m_Exp2;
		private readonly Operator m_Operator;
		private readonly ScriptLoadingContext lcontext;

		internal Expression Exp1 => m_Exp1;
		internal Expression Exp2 => m_Exp2;
		internal Operator Operator => m_Operator;

		private BinaryOperatorExpression(Expression exp1, Expression exp2, Operator op, ScriptLoadingContext lcontext)
			: base (lcontext)
		{
			this.lcontext = lcontext;
			m_Exp1 = exp1;
			m_Exp2 = exp2;
			m_Operator = op;
			if (op == Operator.Add && lcontext.Syntax == ScriptSyntax.Wattle)
				m_Operator = Operator.AddConcat;
		}

		private static bool ShouldInvertBoolean(Operator op)
		{
			return (op == Operator.NotEqual)
				|| (op == Operator.GreaterOrEqual)
				|| (op == Operator.Greater);
		}

		public static OpCode OperatorToOpCode(Operator op)
		{
			switch (op)
			{
				case Operator.Less:
				case Operator.GreaterOrEqual:
					return OpCode.Less;
				case Operator.LessOrEqual:
				case Operator.Greater:
					return OpCode.LessEq;
				case Operator.Equal:
				case Operator.NotEqual:
					return OpCode.Eq;
				case Operator.StrConcat:
					return OpCode.Concat;
				case Operator.Add:
					return OpCode.Add;
				case Operator.Sub:
					return OpCode.Sub;
				case Operator.Mul:
					return OpCode.Mul;
				case Operator.Div:
					return OpCode.Div;
				case Operator.Mod:
					return OpCode.Mod;
				case Operator.Power:
					return OpCode.Power;
				case Operator.AddConcat:
					return OpCode.AddStr;
				case Operator.NilCoalescing:
					return OpCode.NilCoalescing;
				case Operator.BitAnd:
					return OpCode.BAnd;
				case Operator.BitOr:
					return OpCode.BOr;
				case Operator.BitXor:
					return OpCode.BXor;
				case Operator.BitLShift:
					return OpCode.BLShift;
				case Operator.BitRShiftA:
					return OpCode.BRShiftA;
				case Operator.BitRShiftL:
					return OpCode.BRShiftL;
				case Operator.NilCoalescingInverse:
					return OpCode.NilCoalescingInverse;
				case Operator.InclusiveRange:
					return OpCode.NewRange;
				case Operator.ExclusiveRange:
					return OpCode.NewRange;
				case Operator.LeftExclusiveRange:
					return OpCode.NewRange;
				case Operator.RightExclusiveRange:
					return OpCode.NewRange;
				default:
					throw new InternalErrorException("Unsupported operator {0}", op);
			}
		}

		public bool IsRangeCtor()
		{
			return m_Exp1 != null && m_Exp2 != null && (m_Operator & RANGES) != 0;
		}

		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			m_Exp1.ResolveScope(lcontext);
			m_Exp2.ResolveScope(lcontext);
		}


		public override void Compile(Execution.VM.FunctionBuilder bc)
		{
			m_Exp1.CompilePossibleLiteral(bc);

			if (m_Operator == Operator.Or)
			{
				int i = bc.Emit_Jump(OpCode.JtOrPop, -1);
				m_Exp2.CompilePossibleLiteral(bc);
				bc.SetNumVal(i, bc.GetJumpPointForNextInstruction());
				return;
			}

			if (m_Operator == Operator.And)
			{
				int i = bc.Emit_Jump(OpCode.JfOrPop, -1);
				m_Exp2.CompilePossibleLiteral(bc);
				bc.SetNumVal(i, bc.GetJumpPointForNextInstruction());
				return;
			}


			if (m_Exp2 != null)
			{
				m_Exp2.CompilePossibleLiteral(bc);
			}

			bc.Emit_Operator(OperatorToOpCode(m_Operator), ShouldInvertBoolean(m_Operator));
		}

		public override bool EvalLiteral(out DynValue dv, IDictionary<string, DynValue> symbols = null)
		{
			dv = DynValue.Nil;
			if (!m_Exp1.EvalLiteral(out var v1, symbols))
				return false;
			bool t1Neg = m_Exp1 is UnaryOperatorExpression uo &&
			             uo.IsNegativeNumber;
			v1 = v1.ToScalar();
			if (!m_Exp2.EvalLiteral(out var v2, symbols))
				return false;
			v2 = v2.ToScalar();
			if (m_Operator == Operator.NilCoalescing)
			{
				if (v1.IsNil()) dv = v2;
				else dv = v1;
				return true;
			}
			if (m_Operator == Operator.NilCoalescingInverse)
			{
				if (v1.IsNotNil()) dv = v2;
				else dv = v1;
				return true;
			}
			if (m_Operator == Operator.Or)
			{
				if (v1.CastToBool())
					dv = v1;
				else 
					dv = v2;
			}
			else if (m_Operator == Operator.And)
			{
				if (!v1.CastToBool())
					dv = v1;
				else
					dv = v2;
			}
			else if ((m_Operator & COMPARES) != 0)
			{
				if (v1.Type == DataType.Number && v2.Type == DataType.Number ||
				    v1.Type == DataType.String && v2.Type == DataType.String)
					dv = DynValue.NewBoolean(EvalComparison(v1, v2, m_Operator));
				else
					return false;
			}
			else if (m_Operator == Operator.StrConcat)
			{
				string s1 = v1.CastToString();
				string s2 = v2.CastToString();

				if (s1 == null || s2 == null)
					return false;

				dv = DynValue.NewString(s1 + s2);
			}
			else if (m_Operator == Operator.InclusiveRange || m_Operator == Operator.ExclusiveRange || m_Operator == Operator.LeftExclusiveRange || m_Operator == Operator.RightExclusiveRange)
			{
				int? nd1 = v1.CastToInt();
				int? nd2 = v2.CastToInt();
				if (nd1 == null || nd2 == null)
					return false;

				int from = nd1.Value;
				int to = nd2.Value;

				switch (m_Operator)
				{
					case Operator.ExclusiveRange:
						from++;
						to--;
						break;
					case Operator.LeftExclusiveRange:
						from++;
						break;
					case Operator.RightExclusiveRange:
						to--;
						break;
				}
				
				dv = DynValue.NewRange(new Range(lcontext.Script, from, to));
			}
			else
			{
				//Check correct casts
				double? nd1 = v1.CastToNumber();
				double? nd2 = v2.CastToNumber();
				if (nd1 == null || nd2 == null)
					return false;	
				//Literal evaluation
				dv = DynValue.NewNumber(EvalArithmetic(v1, v2, t1Neg));
			}
			return true;
		}

		public override DynValue Eval(ScriptExecutionContext context)
		{
			DynValue v1 = m_Exp1.Eval(context).ToScalar();

			if (m_Operator == Operator.NilCoalescing)
			{
				if (v1.IsNil()) return m_Exp2.Eval(context);
				return v1;
			}
			if (m_Operator == Operator.NilCoalescingInverse)
			{
				if (v1.IsNotNil()) return m_Exp2.Eval(context);
				return v1;
			}
			if (m_Operator == Operator.Or)
			{
				if (v1.CastToBool())
					return v1;
				else
					return m_Exp2.Eval(context).ToScalar();
			}

			if (m_Operator == Operator.And)
			{
				if (!v1.CastToBool())
					return v1;
				else
					return m_Exp2.Eval(context).ToScalar();
			}

			DynValue v2 = m_Exp2.Eval(context).ToScalar();

			if ((m_Operator & COMPARES) != 0)
			{
				return DynValue.NewBoolean(EvalComparison(v1, v2, m_Operator));				
			}
			else if (m_Operator == Operator.StrConcat)
			{
				string s1 = v1.CastToString();
				string s2 = v2.CastToString();

				if (s1 == null || s2 == null)
					throw new DynamicExpressionException("Attempt to perform concatenation on non-strings.");

				return DynValue.NewString(s1 + s2);
			}
			else
			{
				return DynValue.NewNumber(EvalArithmetic(v1, v2));
			}
		}

		private double EvalArithmetic(DynValue v1, DynValue v2, bool t1Neg = false)
		{
			double? nd1 = v1.CastToNumber();
			double? nd2 = v2.CastToNumber();

			if (nd1 == null || nd2 == null)
				throw new DynamicExpressionException("Attempt to perform arithmetic on non-numbers.");

			double d1 = nd1.Value;
			double d2 = nd2.Value;

			switch (m_Operator)
			{
				case Operator.BitAnd:
					return (int) d1 & (int) d2;
				case Operator.BitOr:
					return (int) d1 | (int) d2;
				case Operator.BitXor:
					return (int) d1 ^ (int) d2;
				case Operator.BitLShift:
					return (int) d1 << (int) d2;
				case Operator.BitRShiftA:
					return (int) d1 >> (int) d2;
				case Operator.BitRShiftL:
					return (int) ((uint) d1 >> (int) d2);
				case Operator.Add:
				case Operator.AddConcat:
					return d1 + d2;
				case Operator.Sub:
					return d1 - d2;
				case Operator.Mul:
					return d1 * d2;
				case Operator.Div:
					return d1 / d2;
				case Operator.Mod:
					return (d1) - Math.Floor((d1) / (d2)) * (d2);
				case Operator.Power:
					var res = Math.Pow(t1Neg ? -d1 : d1, d2);
					return t1Neg ? -res : res;
				default:
					throw new DynamicExpressionException("Unsupported operator {0}", m_Operator);
			}
		}

		private bool EvalComparison(DynValue l, DynValue r, Operator op)
		{
			switch (op)
			{
				case Operator.Less:
					if (l.Type == DataType.Number && r.Type == DataType.Number)
					{
						return (l.Number < r.Number);
					}
					else if (l.Type == DataType.String && r.Type == DataType.String)
					{
						return (l.String.CompareTo(r.String) < 0);
					}
					else
					{
						throw new DynamicExpressionException("Attempt to compare non-numbers, non-strings.");
					}
				case Operator.LessOrEqual:
					if (l.Type == DataType.Number && r.Type == DataType.Number)
					{
						return (l.Number <= r.Number);
					}
					else if (l.Type == DataType.String && r.Type == DataType.String)
					{
						return (l.String.CompareTo(r.String) <= 0);
					}
					else
					{
						throw new DynamicExpressionException("Attempt to compare non-numbers, non-strings.");
					}
				case Operator.Equal:
					if (object.ReferenceEquals(r, l))
					{
						return true;
					}
					else if (r.Type != l.Type)
					{
						if ((l.Type == DataType.Nil && r.Type == DataType.Void)
							|| (l.Type == DataType.Void && r.Type == DataType.Nil))
							return true;
						else
							return false;
					}
					else
					{
						return r.Equals(l);
					}
				case Operator.Greater:
					return !EvalComparison(l, r, Operator.LessOrEqual);
				case Operator.GreaterOrEqual:
					return !EvalComparison(l, r, Operator.Less);
				case Operator.NotEqual:
					return !EvalComparison(l, r, Operator.Equal);
				default:
					throw new DynamicExpressionException("Unsupported operator {0}", op);
			}
		}
	}
}
