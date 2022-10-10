
namespace WattleScript.Interpreter.Execution.VM
{
	internal enum OpCode
	{
		// Meta-opcodes
		Nop,		// Does not perform any operation.
		Debug,		// Does not perform any operation. Used to help debugging.

		// Stack ops and assignment
		Pop,		// Discards the topmost n elements from the v-stack. 
		Copy,		// Copies the n-th value of the stack on the top
		CopyValue,  // Copies the n-th value of the stack on the top, fetching tupleidx from NumVal2
		Swap,		// Swaps two entries relative to the v-stack
		PushNil,	// Pushes nil to the v-stack
		PushTrue,	// Pushes true to the v-stack
		PushFalse,	// Pushes false to the v-stack
		PushInt,    // Pushes an integer to the v-stack
		PushNumber, // Pushes a number to the v-stack
		PushString, // Pushes a string to the v-stack
		Closure,	// Creates a closure on the top of the v-stack, using the symbols for upvalues and num-val for entry point of the function.
		TblInitN,	// Initializes NumVal named entries, NumVal2 0 = don't create, 1 = create normal, 2 = create shared
		TblInitI,	// Initializes NumVal2 table positional entries, starting at pos NumVal (modifed by IndexFrom)
            // (NumVal3 & 0x7F) 0 = don't create, 1 = create normal, 2 = create shared
            // NumVal3 & 0x80 == expand tuple
		NewRange,   // Creates a range from the v-stack
		TabProps,	// Sets v-stack top table modifier flags, kind and readonly flag. Does not pop
		SetMetaTab, // Sets v-stack - 1 table's metatable to vstack top & pops once.
		
		StoreLcl, Local,
		StoreUpv, Upvalue,
		IndexSet, Index,
		IndexSetN, IndexN,
		IndexSetL, IndexL,
		

		// Stack-frame ops and calls
		Clean,		// Cleansup locals setting them as null
		CloseUp,	// Close a specific upvalue
		
		Args,		// Takes the arguments passed to a function and sets the appropriate symbols in the local scope
		Call,		// Calls the function specified on the specified element from the top of the v-stack. If the function is a WattleScript function, it pushes its numeric value on the v-stack, then pushes the current PC onto the x-stack, enters the function closure and jumps to the function first instruction. If the function is a CLR function, it pops the function value from the v-stack, then invokes the function synchronously and finally pushes the result on the v-stack.
		ThisCall,	// Same as call, but the call is a ':' method invocation
		Ret,		// Pops the top n values of the v-stack. Then pops an X value from the v-stack. Then pops X values from the v-stack. Afterwards, it pushes the top n values popped in the first step, pops the top of the x-stack and jumps to that location.

		// Jumps
		Jump,		// Jumps to the specified PC
		Jf,			// Pops the top of the v-stack and jumps to the specified location if it's false
		Jt,			// Pops the top of the v-stack and jumps to the specified location if it's true
		JNil,		// Jumps if the top of the stack is nil (pops stack)
		JNilChk,	// Jumps if the top of the stack is nil (does not pop stack)
		JFor,		// Peeks at the top, top-1 and top-2 values of the v-stack which it assumes to be numbers. Then if top-1 is less than zero, checks if top is <= top-2, otherwise it checks that top is >= top-2. Then if the condition is false, it jumps.
		JtOrPop,	// Peeks at the topmost value of the v-stack as a boolean. If true, it performs a jump, otherwise it removes the topmost value from the v-stack.
		JfOrPop,	// Peeks at the topmost value of the v-stack as a boolean. If false, it performs a jump, otherwise it removes the topmost value from the v-stack.
		
		// This instruction compares the top of the v-stack against the
		// values in the following jump table.
		// The 3 high bits of NumVal indicate the presence of nil, true, false
		// The rest of the bits of NumVal == the count of SString
		// NumValB contains the combined count of SNumber and SInteger entries
		// Default case is stored directly after the jump table
		Switch,
		// These ops contain jump offsets + data for switch statements, they cannot be
		// directly executed
		SString, // NumVal = string table entry, NumValB = offset
		SNumber, // NumVal = int32, NumValB = offset
		SInteger, // NumVal = number table entry, NumValB = offset
		// Encodes jump offsets for fixed position entries in the jump table: nil, true, false
		// NumVal == 0
		SSpecial,
		//
		StrFormat,  // Format using string.Format
		// Operators
		Concat,		// Concatenation of the two topmost operands on the v-stack
		LessEq,		// Compare <= of the two topmost operands on the v-stack
		Less,		// Compare < of the two topmost operands on the v-stack
		Eq,			// Compare == of the two topmost operands on the v-stack
		Add,		// Addition of the two topmost operands on the v-stack
		AddStr,		// Addition of the two topmost operands on the v-stack, will concat strings
		Sub,		// Subtraction of the two topmost operands on the v-stack
		Mul,		// Multiplication of the two topmost operands on the v-stack
		Div,		// Division of the two topmost operands on the v-stack
		Mod,		// Modulus of the two topmost operands on the v-stack
		Not,		// Logical inversion of the topmost operand on the v-stack
		Len,		// Size operator of the topmost operand on the v-stack
		Neg,		// Negation (unary minus) operator of the topmost operand on the v-stack
		Power,		// Power of the two topmost operands on the v-stack
		CNot,		// Conditional NOT - takes second operand from the v-stack (must be bool), if true execs a NOT otherwise execs a TOBOOL.
					// If NumVal != 0, then execute a second NOT
		//Bit Operators
		BAnd,
		BOr,
		BXor,
		BLShift,
		BRShiftA,
		BRShiftL,
		BNot,

		// Type conversions and manipulations
		MkTuple,	// Creates a tuple from the topmost n values
		Scalar,		// Converts the topmost tuple to a scalar
		Incr,		// Performs an add operation, without extracting the operands from the v-stack and assuming the operands are numbers.
		ToNum,		// Converts the top of the stack to a number
		ToBool,		// Converts the top of the stack to a boolean
		ExpTuple,	// Expands a tuple on the stack


		// Iterators
		IterPrep,   // Prepares an iterator for execution 
		IterUpd,	// Updates the var part of an iterator
		
		// Nil coalescing
		NilCoalescing,
		NilCoalescingInverse,

		JLclInit, // Inits a param value if a default one is specified and not provided at callsite.

		// OOP
		// AnnotX instructions, add annotation to table
		//NumValB = annotation name string
		AnnotI, //NumVal = int
		AnnotN, //NumVal = number
		AnnotS, //NumVal = string or nil
		AnnotB, //NumVal = bool
		AnnotT, //pop table from v-stack
		LoopChk, //Checks if local in NumVal is < threshold. If not, throw error using NumValB as the class name
		BaseChk, //Checks if v-stack top is a class. If not, throw error using NumVal as the base class name
		NewCall, //Calls the new() function stored in table at v-stack offset NumVal with NumVal arguments.
				 //Throws error using class name in NumValB if type check fails
		MixInit, //Checks type of mixin on v-stack top, stores init to v-stack + 1, adds functions to v-stack + 2, pops top
		         //Error check uses NumVal for mixin name
		SetFlags, //Sets WattleFieldsInfo for NumVal keys, using modifier in NumVal2 (pops NumVal Items)
		MergeFlags, //Merges the WattleFieldsInfo of v-stack(NumVal) into v-stack(NumVal2)
		CopyFlags, //Copies the WattleFieldsInfo of v-stack top into v-stack +1, pops 1 value
		// Meta
		Invalid,	// Crashes the executor with an unrecoverable NotImplementedException. This MUST always be the last opcode in enum
	}
}
