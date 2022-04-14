using System.Collections.Generic;
using WattleScript.Interpreter.Debugging;

namespace WattleScript.Interpreter.Execution.VM
{
	internal struct CallStackItem
	{
		public bool IsNil => BasePointer == 0;

		public FunctionProto Function;

		public SourceRef CallingSourceRef;

		public CallbackFunction ClrFunction;
		public CallbackFunction Continuation;
		public CallbackFunction ErrorHandler;
		public DynValue ErrorHandlerBeforeUnwind;

		public int BasePointer;
		public int ReturnAddress;

		public List<Upvalue> OpenClosures;
		public ClosureContext ClosureScope;

		public CallStackItemFlags Flags;
	}

}
