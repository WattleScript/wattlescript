using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;

namespace WattleScript.Interpreter
{
	/// <summary>
	/// A class representing a script function
	/// </summary>
	public class Closure : RefIdObject, IScriptPrivateResource
	{
		/// <summary>
		/// Type of closure based on upvalues
		/// </summary>
		public enum UpvaluesType
		{
			/// <summary>
			/// The closure has no upvalues (thus, technically, it's a function and not a closure!)
			/// </summary>
			None,
			/// <summary>
			/// The closure has _ENV as its only upvalue
			/// </summary>
			Environment,
			/// <summary>
			/// The closure is a "real" closure, with multiple upvalues
			/// </summary>
			Closure
		}

		/// <summary>
		/// Gets the annotations made on this function.
		/// </summary>
		public IReadOnlyList<Annotation> Annotations => Function.Annotations;

		/// <summary>
		/// Gets the script owning this function
		/// </summary>
		public Script OwnerScript { get; private set; }


		/// <summary>
		/// Shortcut for an empty closure
		/// </summary>
		private static ClosureContext emptyClosure = new ClosureContext();

		/// <summary>
		/// The current closure context
		/// </summary>
		internal ClosureContext ClosureContext { get; private set; }
		
		/// <summary>
		/// Provides information about this closure definition
		/// </summary>
		public FunctionProto Function { get; private set; }


		/// <summary>
		/// Initializes a new instance of the <see cref="Closure"/> class.
		/// </summary>
		/// <param name="script">The script.</param>
		/// <param name="idx">The index.</param>
		/// <param name="symbols">The symbols.</param>
		/// <param name="resolvedLocals">The resolved locals.</param>
		internal Closure(Script script, FunctionProto proto, SymbolRef[] symbols, IEnumerable<Upvalue> resolvedLocals)
		{
			OwnerScript = script;
			
			Function = proto;
			
			if (symbols.Length > 0)
				ClosureContext = new ClosureContext(symbols, resolvedLocals);
			else
				ClosureContext = emptyClosure;
		}

		/// <summary>
		/// Calls this function with the specified args
		/// </summary>
		/// <returns></returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public DynValue Call()
		{
			return OwnerScript.Call(this);
		}

		/// <summary>
		/// Calls this function with the specified args
		/// </summary>
		/// <param name="args">The arguments to pass to the function.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public DynValue Call(params object[] args)
		{
			return OwnerScript.Call(this, args);
		}

		/// <summary>
		/// Calls this function with the specified args
		/// </summary>
		/// <param name="args">The arguments to pass to the function.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public DynValue Call(params DynValue[] args)
		{
			return OwnerScript.Call(this, args);
		}
		
		public Task<DynValue> CallAsync()
		{
			return OwnerScript.CallAsync(this);
		}

		public Task<DynValue> CallAsync(params object[] args)
		{
			return OwnerScript.CallAsync(this, args);
		}

		public Task<DynValue> CallAsync(params DynValue[] args)
		{
			return OwnerScript.CallAsync(this, args);
		}

		public DynValue ThisCall(DynValue self)
		{
			return OwnerScript.ThisCall(this, self);
		}
		
		public DynValue ThisCall(params DynValue[] args)
		{
			return OwnerScript.ThisCall(this, args);
		}
		
		public Task<DynValue> ThisCallAsync(DynValue self)
		{
			return OwnerScript.ThisCallAsync(this, self);
		}
		
		public Task<DynValue> ThisCallAsync(params DynValue[] args)
		{
			return OwnerScript.ThisCallAsync(this, args);
		}

		/// <summary>
		/// Gets a delegate wrapping calls to this scripted function
		/// </summary>
		/// <returns></returns>
		public ScriptFunctionDelegate GetDelegate()
		{
			return args => this.Call(args).ToObject();
		}

		/// <summary>
		/// Gets a delegate wrapping calls to this scripted function
		/// </summary>
		/// <typeparam name="T">The type of return value of the delegate.</typeparam>
		/// <returns></returns>
		public ScriptFunctionDelegate<T> GetDelegate<T>()
		{
			return args => this.Call(args).ToObject<T>();
		}

		/// <summary>
		/// Gets the number of upvalues in this closure
		/// </summary>
		/// <returns>The number of upvalues in this closure</returns>
		public int GetUpvaluesCount()
		{
			return ClosureContext.Count;
		}

		/// <summary>
		/// Gets the name of the specified upvalue.
		/// </summary>
		/// <param name="idx">The index of the upvalue.</param>
		/// <returns>The upvalue name</returns>
		public string GetUpvalueName(int idx)
		{
			return ClosureContext.Symbols[idx];
		}

		/// <summary>
		/// Gets the value of an upvalue. To set the value, use GetUpvalue(idx) = ...;
		/// </summary>
		/// <param name="idx">The index of the upvalue.</param>
		/// <returns>The value of an upvalue </returns>
		public ref DynValue GetUpvalue(int idx)
		{
			return ref ClosureContext[idx].Value();
		}

		/// <summary>
		/// Gets the type of the upvalues contained in this closure
		/// </summary>
		/// <returns></returns>
		public UpvaluesType GetUpvaluesType()
		{
			int count = GetUpvaluesCount();

			if (count == 0)
				return UpvaluesType.None;
			else if (count == 1 && GetUpvalueName(0) == WellKnownSymbols.ENV)
				return UpvaluesType.Environment;
			else
				return UpvaluesType.Closure;
		}


	}
}
