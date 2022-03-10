using MoonSharp.Interpreter.Debugging;

namespace MoonSharp.Interpreter.Execution.VM
{
	internal class CallStackItem
	{
		public int Debug_EntryPoint;
		public SymbolRef[] Debug_Symbols;

		public SourceRef CallingSourceRef;

		public CallbackFunction ClrFunction;
		public CallbackFunction Continuation;
		public CallbackFunction ErrorHandler;
		public DynValue ErrorHandlerBeforeUnwind;

		public int BasePointer;
		public int ReturnAddress;

		public DynValue[] LatestBlock;
		public int StorePtr;
		
		public Upvalue[] LocalScope;
		public ClosureContext ClosureScope;

		public CallStackItemFlags Flags;
	}

}
