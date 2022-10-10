
namespace WattleScript.Interpreter.Tree
{
	enum TokenType
	{
		Eof,
		HashBang,
		Name,
		And,
		Break,
		Continue,
		Do,
		Else,
		ElseIf,
		End,
		False,
		For,
		Function,
		Lambda,
		Goto,
		If,
		In,
		Local,
		Nil,
		Not,
		Or,
		Repeat,
		Return,
		Then,
		True,
		Until,
		While,
		Op_Equal,
		Op_Assignment,
		Op_LessThan,
		Op_LessThanEqual,
		Op_GreaterThanEqual,
		Op_GreaterThan,
		Op_NotEqual,
		Op_Concat,
		VarArgs,
		Dot,
		Colon,
		DoubleColon,
		Comma,
		Brk_Close_Curly,
		Brk_Open_Curly,
		Brk_Close_Round,
		Brk_Open_Round,
		Brk_Close_Square,
		Brk_Open_Square,
		Op_Len,
		//Number arithmetic
		Op_Pwr,
		Op_Mod,
		Op_Div,
		Op_Mul,
		Op_MinusOrSub,
		Op_Add,
		Op_AddEq,
		Op_SubEq,
		Op_DivEq,
		Op_ModEq,
		Op_MulEq,
		Op_PwrEq,
		Op_ConcatEq,
		Op_Inc,
		Op_Dec,
		//Bitwise ops
		Op_Not,
		Op_LShift,
		Op_RShiftArithmetic,
		Op_RShiftLogical,
		Op_And,
		Op_Xor,
		Op_Or,
		Op_LShiftEq,
		Op_RShiftArithmeticEq,
		Op_RShiftLogicalEq,
		Op_AndEq,
		Op_XorEq,
		Op_OrEq,
		//Nil operators
		Op_NilCoalesce,
		Op_NilCoalesceInverse,
		Op_NilCoalescingAssignment,
		Op_NilCoalescingAssignmentInverse,
		//Nil Accessing
		DotNil,
		BrkOpenSquareNil,
		
		Ternary,
		Comment,

		String,
		String_Long,
		String_TemplateFragment,
		String_EndTemplate,
		Number,
		Number_HexFloat,
		Number_Hex,
		SemiColon,
		Invalid,

		Brk_Open_Curly_Shared,
		Op_Dollar,
		
		Arrow,
		ChunkAnnotation,
		FunctionAnnotation,
		Directive,
		Switch,
		Case,
		Op_ExclusiveRange, // >..<
		Op_InclusiveRange, // ..
		Op_LeftExclusiveRange, // >..
		Op_RightExclusiveRange, // ..<
		
		Enum,
		Class,
		New,
		Mixin,
		
		Public,
		Static,
		Private,
		Sealed,
		
		Preprocessor_Defined //Reserved only in preprocessor
	}



}
