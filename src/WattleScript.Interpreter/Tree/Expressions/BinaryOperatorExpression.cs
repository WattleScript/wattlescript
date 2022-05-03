using System;
using WattleScript.Interpreter.DataStructs;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;

namespace WattleScript.Interpreter.Tree.Expressions
{
	[Flags]
	public enum Operator
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
	}
	
	/// <summary>
	/// 
	/// </summary>
	class BinaryOperatorExpression : Expression
	{
		internal class Node
		{
			public Expression Expr;
			public Operator Op;
			public Node Prev;
			public Node Next;
			
			public Node GetLast()
			{
				return Next?.GetLast() ?? this;
			}
		}

		internal class LinkedList
		{
			public Node Nodes;
			public Node Last;
			public Operator OperatorMask;
		}

		private enum Associativity
		{
			Left,
			Right
		}

		private const Operator POWER = Operator.Power;
		private const Operator MUL_DIV_MOD = Operator.Mul | Operator.Div | Operator.Mod;
		private const Operator ADD_SUB = Operator.Add | Operator.Sub | Operator.AddConcat;
		private const Operator STRCAT = Operator.StrConcat;
		private const Operator COMPARES = Operator.Less | Operator.Greater | Operator.GreaterOrEqual | Operator.LessOrEqual | Operator.Equal | Operator.NotEqual;
		private const Operator LOGIC_AND = Operator.And;
		private const Operator LOGIC_OR = Operator.Or;
		private const Operator NIL_COAL_ASSIGN = Operator.NilCoalescing;
		private const Operator SHIFTS = Operator.BitLShift | Operator.BitRShiftA | Operator.BitRShiftL;
		private const Operator NIL_COAL_INVERSE = Operator.NilCoalescingInverse;

		public static LinkedList BeginOperatorChain()
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
				nodes = PrioritizeAssociative(Associativity.Right, nodes, lcontext, POWER);

			if ((opfound & MUL_DIV_MOD) != 0)
				nodes = PrioritizeAssociative(Associativity.Left, nodes, lcontext, MUL_DIV_MOD);

			if ((opfound & ADD_SUB) != 0)
				nodes = PrioritizeAssociative(Associativity.Left, nodes, lcontext, ADD_SUB);

			if ((opfound & STRCAT) != 0)
				nodes = PrioritizeAssociative(Associativity.Right, nodes, lcontext, STRCAT);

			if ((opfound & SHIFTS) != 0)
				nodes = PrioritizeAssociative(Associativity.Left, nodes, lcontext, SHIFTS);

			if ((opfound & COMPARES) != 0)
				nodes = PrioritizeAssociative(Associativity.Left, nodes, lcontext, COMPARES);

			if ((opfound & Operator.BitAnd) != 0)
				nodes = PrioritizeAssociative(Associativity.Left, nodes, lcontext, Operator.BitAnd);
			
			if ((opfound & Operator.BitXor) != 0)
				nodes = PrioritizeAssociative(Associativity.Left, nodes, lcontext, Operator.BitXor);
			
			if ((opfound & Operator.BitOr) != 0)
				nodes = PrioritizeAssociative(Associativity.Left, nodes, lcontext, Operator.BitOr);

			if ((opfound & LOGIC_AND) != 0)
				nodes = PrioritizeAssociative(Associativity.Left, nodes, lcontext, LOGIC_AND);

			if ((opfound & LOGIC_OR) != 0)
				nodes = PrioritizeAssociative(Associativity.Left, nodes, lcontext, LOGIC_OR);

			if ((opfound & NIL_COAL_ASSIGN) != 0)
				nodes = PrioritizeAssociative(Associativity.Left, nodes, lcontext, NIL_COAL_ASSIGN);
			
			if ((opfound & NIL_COAL_INVERSE) != 0)
				nodes = PrioritizeAssociative(Associativity.Left, nodes, lcontext, NIL_COAL_INVERSE);

			if (nodes.Next != null || nodes.Prev != null)
				throw new InternalErrorException("Expression reduction didn't work! - 1");
			if (nodes.Expr == null)
				throw new InternalErrorException("Expression reduction didn't work! - 2");
			
			return nodes.Expr;
		}

		private static Node PrioritizeAssociative(Associativity side, Node node, ScriptLoadingContext lcontext, Operator operatorsToFind)
		{
			for (Node n = side == Associativity.Left ? node : node.GetLast(); n != null; n = side == Associativity.Left ? n.Next : n.Prev)
			{
				if ((n.Op & operatorsToFind) != 0)
				{
					Operator prevOp = n.Op;
					n.Op = Operator.NotAnOperator;
					n.Expr = new BinaryOperatorExpression(n.Prev.Expr, n.Next.Expr, prevOp, lcontext);
					n.Prev = n.Prev.Prev;
					n.Next = n.Next.Next;

					if (n.Next != null)
						n.Next.Prev = n;

					if (n.Prev != null)
						n.Prev.Next = n;
					else
						node = n;
				}
			}

			return node;
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
				default:
					throw new InternalErrorException("Unexpected binary operator '{0}'", token.Text);
			}
		}

		readonly Expression m_Exp1;
		readonly Expression m_Exp2;
		readonly Operator m_Operator;

		private BinaryOperatorExpression(Expression exp1, Expression exp2, Operator op, ScriptLoadingContext lcontext)
			: base (lcontext)
		{
			m_Exp1 = exp1;
			m_Exp2 = exp2;
			m_Operator = op;
			if (op == Operator.Add && lcontext.Syntax == ScriptSyntax.WattleScript)
				m_Operator = Operator.AddConcat;
		}

		private static bool ShouldInvertBoolean(Operator op)
		{
			return op == Operator.NotEqual
				|| op == Operator.GreaterOrEqual
				|| op == Operator.Greater;
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
				default:
					throw new InternalErrorException("Unsupported operator {0}", op);
			}
		}

		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			m_Exp1.ResolveScope(lcontext);
			m_Exp2.ResolveScope(lcontext);
		}
		
		public override void Compile(FunctionBuilder bc)
		{
			m_Exp1.CompilePossibleLiteral(bc);

			switch (m_Operator)
			{
				case Operator.Or:
				{
					int i = bc.Emit_Jump(OpCode.JtOrPop, -1);
					m_Exp2.CompilePossibleLiteral(bc);
					bc.SetNumVal(i, bc.GetJumpPointForNextInstruction());
					return;
				}
				case Operator.And:
				{
					int i = bc.Emit_Jump(OpCode.JfOrPop, -1);
					m_Exp2.CompilePossibleLiteral(bc);
					bc.SetNumVal(i, bc.GetJumpPointForNextInstruction());
					return;
				}
			}

			m_Exp2?.CompilePossibleLiteral(bc);
			bc.Emit_Operator(OperatorToOpCode(m_Operator));

			if (ShouldInvertBoolean(m_Operator))
				bc.Emit_Operator(OpCode.Not);
		}

		public override bool EvalLiteral(out DynValue dv)
		{
			dv = DynValue.Nil;
			if (!m_Exp1.EvalLiteral(out var v1))
				return false;
			bool t1Neg = m_Exp1 is UnaryOperatorExpression uo && uo.IsNegativeNumber;
			v1 = v1.ToScalar();
			if (!m_Exp2.EvalLiteral(out var v2))
				return false;
			v2 = v2.ToScalar();
			
			switch (m_Operator)
			{
				case Operator.NilCoalescing:
					dv = v1.IsNil() ? v2 : v1;
					return true;
				case Operator.NilCoalescingInverse:
					dv = v1.IsNotNil() ? v2 : v1;
					return true;
				case Operator.Or:
					dv = v1.CastToBool() ? v1 : v2;
					break;
				case Operator.And when !v1.CastToBool():
					dv = v1;
					break;
				case Operator.And:
					dv = v2;
					break;
				default:
				{
					if ((m_Operator & COMPARES) != 0)
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

					break;
				}
			}

			return true;
		}

		public override DynValue Eval(ScriptExecutionContext context)
		{
			DynValue v1 = m_Exp1.Eval(context).ToScalar();

			switch (m_Operator)
			{
				case Operator.NilCoalescing:
					return v1.IsNil() ? m_Exp2.Eval(context) : v1;
				case Operator.NilCoalescingInverse:
					return v1.IsNotNil() ? m_Exp2.Eval(context) : v1;
				case Operator.Or:
					return v1.CastToBool() ? v1 : m_Exp2.Eval(context).ToScalar();
				case Operator.And:
					return !v1.CastToBool() ? v1 : m_Exp2.Eval(context).ToScalar();
			}

			DynValue v2 = m_Exp2.Eval(context).ToScalar();

			if ((m_Operator & COMPARES) != 0)
			{
				return DynValue.NewBoolean(EvalComparison(v1, v2, m_Operator));				
			}
			
			if (m_Operator == Operator.StrConcat)
			{
				string s1 = v1.CastToString();
				string s2 = v2.CastToString();

				if (s1 == null || s2 == null)
					throw new DynamicExpressionException("Attempt to perform concatenation on non-strings.");

				return DynValue.NewString(s1 + s2);
			}

			return DynValue.NewNumber(EvalArithmetic(v1, v2));
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
					return d1 - Math.Floor(d1 / d2) * d2;
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
					return l.Type switch
					{
						DataType.Number when r.Type == DataType.Number => l.Number < r.Number,
						DataType.String when r.Type == DataType.String => string.Compare(l.String, r.String, StringComparison.Ordinal) < 0,
						_ => throw new DynamicExpressionException("Attempt to compare non-numbers, non-strings.")
					};
				case Operator.LessOrEqual:
					return l.Type switch
					{
						DataType.Number when r.Type == DataType.Number => l.Number <= r.Number,
						DataType.String when r.Type == DataType.String => string.Compare(l.String, r.String, StringComparison.Ordinal) <= 0,
						_ => throw new DynamicExpressionException("Attempt to compare non-numbers, non-strings.")
					};
				case Operator.Equal:
					if (r.Type != l.Type)
					{
						return (l.Type == DataType.Nil && r.Type == DataType.Void)
						       || (l.Type == DataType.Void && r.Type == DataType.Nil);
					}
					return r.Equals(l);
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