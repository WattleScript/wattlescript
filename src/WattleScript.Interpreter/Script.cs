using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WattleScript.Interpreter.CoreLib;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Diagnostics;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;
using WattleScript.Interpreter.Interop;
using WattleScript.Interpreter.IO;
using WattleScript.Interpreter.Platforms;
using WattleScript.Interpreter.Tree;
using WattleScript.Interpreter.Tree.Expressions;
using WattleScript.Interpreter.Tree.Fast_Interface;

namespace WattleScript.Interpreter
{
	/// <summary>
	/// This class implements a WattleScript scripting session. Multiple Script objects can coexist in the same program but cannot share
	/// data among themselves unless some mechanism is put in place.
	/// </summary>
	public class Script : IScriptPrivateResource
	{
		public enum ScriptParserMessageType
		{
			Error,
			Warning,
			Info
		}
	
		public class ScriptParserMessage 
		{
			public string Msg { get; set; }
			private Token Token { get; set; }
			public ScriptParserMessageType Type { get; set; }

			internal ScriptParserMessage(Token token)
			{
				Token = token;
				Msg = $"unexpected symbol near '{token}'";
			}
		
			internal ScriptParserMessage(Token token, string msg)
			{
				Token = token;
				Msg = msg;
			}
		}
		
		/// <summary>
		/// The version of the WattleScript engine
		/// </summary>
		public static readonly string VERSION;

		/// <summary>
		/// The version of the WattleScript engine as a double.
		/// </summary>
		public static readonly double VERSION_NUMBER;
		
		/// <summary>
		/// The Lua version being supported
		/// </summary>
		public const string LUA_VERSION = "5.2";

		private Processor m_MainProcessor;
		private List<SourceCode> m_Sources = new List<SourceCode>();
		private Table m_GlobalTable;
		private IDebugger m_Debugger;
		private Table[] m_TypeMetatables = new Table[(int)LuaTypeExtensions.MaxMetaTypes];
		private Table m_TablePrototype;
		internal List<ScriptParserMessage> i_ParserMessages { get; set; } = new List<ScriptParserMessage>();

		/// <summary>
		/// Initializes the <see cref="Script"/> class.
		/// </summary>
		static Script()
		{
			GlobalOptions = new ScriptGlobalOptions();

			DefaultOptions = new ScriptOptions()
			{
				DebugPrint = s => { GlobalOptions.Platform.DefaultPrint(s); },
				DebugInput = s => GlobalOptions.Platform.DefaultInput(s),
				CheckThreadAccess = true,
				ScriptLoader = PlatformAutoDetector.GetDefaultScriptLoader(),
				TailCallOptimizationThreshold = 65536
			};

			Version ver = typeof(Script).Assembly.GetName().Version;
			VERSION = ver.ToString();
			VERSION_NUMBER = ver.Major + ver.Minor / Math.Pow(10, Math.Floor(Math.Log10(ver.Minor) + 1));
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Script"/> clas.s
		/// </summary>
		public Script()
			: this(CoreModules.Preset_Default)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Script"/> class.
		/// </summary>
		/// <param name="coreModules">The core modules to be pre-registered in the default global table.</param>
		public Script(CoreModules coreModules)
		{
			Options = new ScriptOptions(DefaultOptions);
			PerformanceStats = new PerformanceStatistics();
			Registry = new Table(this);

			m_MainProcessor = new Processor(this, m_GlobalTable);
			m_GlobalTable = new Table(this).RegisterCoreModules(coreModules);
		}


		/// <summary>
		/// Gets or sets the script loader which will be used as the value of the
		/// ScriptLoader property for all newly created scripts.
		/// </summary>
		public static ScriptOptions DefaultOptions { get; private set; }

		/// <summary>
		/// Gets access to the script options. 
		/// </summary>
		public ScriptOptions Options { get; private set; }

		/// <summary>
		/// Gets the global options, that is options which cannot be customized per-script.
		/// </summary>
		public static ScriptGlobalOptions GlobalOptions { get; private set; }

		/// <summary>
		/// Gets access to performance statistics.
		/// </summary>
		public PerformanceStatistics PerformanceStats { get; private set; }

		/// <summary>
		/// Gets the default global table for this script. Unless a different table is intentionally passed (or setfenv has been used)
		/// execution uses this table.
		/// </summary>
		public Table Globals => m_GlobalTable;

		/// <summary>
		/// Loads a string containing a Lua/WattleScript function.
		/// </summary>
		/// <param name="code">The code.</param>
		/// <param name="globalTable">The global table to bind to this chunk.</param>
		/// <param name="funcFriendlyName">Name of the function used to report errors, etc.</param>
		/// <returns>
		/// A DynValue containing a function which will execute the loaded code.
		/// </returns>
		public DynValue LoadFunction(string code, Table globalTable = null, string funcFriendlyName = null)
		{
			this.CheckScriptOwnership(globalTable);

			string chunkName = string.Format("libfunc_{0}", funcFriendlyName ?? m_Sources.Count.ToString());

			SourceCode source = new SourceCode(chunkName, code, m_Sources.Count, this);

			m_Sources.Add(source);

			var func = Loader_Fast.LoadFunction(this, source, globalTable != null || m_GlobalTable != null);
			SignalSourceCodeChange(source);
			SignalByteCodeChange();

			return MakeClosure(func, globalTable ?? m_GlobalTable);
		}

		/// <summary>
		/// no-op
		/// </summary>
		private void SignalByteCodeChange()
		{
			if (m_Debugger != null)
			{
				//TODO: This is no-op for now
			}
		}

		private void SignalSourceCodeChange(SourceCode source)
		{
			if (m_Debugger != null)
			{
				m_Debugger.SetSourceCode(source);
			}
		}

		/// <summary>
		/// Loads a string containing a Lua/WattleScript script.
		/// </summary>
		/// <param name="code">The code.</param>
		/// <param name="globalTable">The global table to bind to this chunk.</param>
		/// <param name="codeFriendlyName">Name of the code - used to report errors, etc. Also used by debuggers to locate the original source file.</param>
		/// <returns>
		/// A DynValue containing a function which will execute the loaded code.
		/// </returns>
		public DynValue LoadString(string code, Table globalTable = null, string codeFriendlyName = null)
		{
			this.CheckScriptOwnership(globalTable);

			if (code.StartsWith(StringModule.BASE64_DUMP_HEADER))
			{
				code = code.Substring(StringModule.BASE64_DUMP_HEADER.Length);
				byte[] data = Convert.FromBase64String(code);
				using MemoryStream ms = new MemoryStream(data);
				return LoadStream(ms, globalTable, codeFriendlyName);
			}

			string chunkName = string.Format("{0}", codeFriendlyName ?? "chunk_" + m_Sources.Count.ToString());

			SourceCode source = new SourceCode(codeFriendlyName ?? chunkName, code, m_Sources.Count, this);

			m_Sources.Add(source);

			FunctionProto func = Loader_Fast.LoadChunk(this, source);

			SignalSourceCodeChange(source);
			SignalByteCodeChange();

			return MakeClosure(func, globalTable ?? m_GlobalTable);
		}

		/// <summary>
		/// Loads a Lua/WattleScript script from a System.IO.Stream. NOTE: This will *NOT* close the stream!
		/// </summary>
		/// <param name="stream">The stream containing code.</param>
		/// <param name="globalTable">The global table to bind to this chunk.</param>
		/// <param name="codeFriendlyName">Name of the code - used to report errors, etc.</param>
		/// <returns>
		/// A DynValue containing a function which will execute the loaded code.
		/// </returns>
		public DynValue LoadStream(Stream stream, Table globalTable = null, string codeFriendlyName = null)
		{
			this.CheckScriptOwnership(globalTable);
			
			if (!Processor.IsDumpStream(stream))
			{
				using StreamReader sr = new StreamReader(stream, Encoding.UTF8, true, 4096, true);
				string scriptCode = sr.ReadToEnd();
				return LoadString(scriptCode, globalTable, codeFriendlyName);
			}
			
			string chunkName = string.Format("{0}", codeFriendlyName ?? "dump_" + m_Sources.Count);

			SourceCode source = new SourceCode(codeFriendlyName ?? chunkName,
				string.Format("-- This script was decoded from a binary dump - dump_{0}", m_Sources.Count),
				m_Sources.Count, this);

			m_Sources.Add(source);

			FunctionProto func = m_MainProcessor.Undump(stream, m_Sources.Count - 1);

			SignalSourceCodeChange(source);
			SignalByteCodeChange();

			return MakeClosure(func, globalTable ?? m_GlobalTable);
		}

		/// <summary>
		/// Dumps on the specified stream.
		/// </summary>
		/// <param name="function">The function.</param>
		/// <param name="stream">The stream.</param>
		/// <param name="writeSourceRefs">Write referenced line numbers</param>
		/// <exception cref="System.ArgumentException">
		/// function arg is not a function!
		/// or
		/// stream is readonly!
		/// or
		/// function arg has upvalues other than _ENV
		/// </exception>
		public void Dump(DynValue function, Stream stream, bool writeSourceRefs = true)
		{
			this.CheckScriptOwnership(function);

			if (function.Type != DataType.Function)
				throw new ArgumentException("function arg is not a function!");

			if (!stream.CanWrite)
				throw new ArgumentException("stream is readonly!");

			Closure.UpvaluesType upvaluesType = function.Function.GetUpvaluesType();

			if (upvaluesType == Closure.UpvaluesType.Closure)
				throw new ArgumentException("function arg has upvalues other than _ENV");

			m_MainProcessor.Dump(stream, function.Function.Function, writeSourceRefs);
		}

		/// <summary>
		/// Dumps bytecode to byte[]
		/// </summary>
		/// <param name="function">The function.</param>
		/// <param name="writeSourceRefs">Write referenced line numbers</param>
		/// <exception cref="System.ArgumentException">
		/// function arg is not a function!
		/// or
		/// stream is readonly!
		/// or
		/// function arg has upvalues other than _ENV
		/// </exception>
		public byte[] Dump(DynValue function, bool writeSourceRefs = true)
		{
			this.CheckScriptOwnership(function);

			if (function.Type != DataType.Function)
				throw new ArgumentException("function arg is not a function!");
			
			Closure.UpvaluesType upvaluesType = function.Function.GetUpvaluesType();

			if (upvaluesType == Closure.UpvaluesType.Closure)
				throw new ArgumentException("function arg has upvalues other than _ENV");
			
			using MemoryStream ms = new MemoryStream();
			m_MainProcessor.Dump(ms, function.Function.Function, writeSourceRefs);

			return ms.ToArray();
		}

		/// <summary>
		/// Dumps the bytecode for a function to a human-readable string
		/// </summary>
		/// <param name="function"></param>
		public string DumpString(DynValue function)
		{
			this.CheckScriptOwnership(function);

			if (function.Type != DataType.Function)
				throw new ArgumentException("function arg is not a function!");
			return m_MainProcessor.DumpString(function.Function.Function);
		}

		/// <summary>
		/// Loads a string containing a Lua/WattleScript script.
		/// </summary>
		/// <param name="filename">The code.</param>
		/// <param name="globalContext">The global table to bind to this chunk.</param>
		/// <param name="friendlyFilename">The filename to be used in error messages.</param>
		/// <returns>
		/// A DynValue containing a function which will execute the loaded code.
		/// </returns>
		public DynValue LoadFile(string filename, Table globalContext = null, string friendlyFilename = null)
		{
			this.CheckScriptOwnership(globalContext);

#pragma warning disable 618
			filename = Options.ScriptLoader.ResolveFileName(filename, globalContext ?? m_GlobalTable);
#pragma warning restore 618

			object code = Options.ScriptLoader.LoadFile(filename, globalContext ?? m_GlobalTable);

			switch (code)
			{
				case string s:
					return LoadString(s, globalContext, friendlyFilename ?? filename);
				case byte[] bytes:
				{
					using MemoryStream ms = new MemoryStream(bytes);
					return LoadStream(ms, globalContext, friendlyFilename ?? filename);
				}
				case Stream stream:
				{
					try
					{
						return LoadStream(stream, globalContext, friendlyFilename ?? filename);
					}
					finally
					{
						stream.Dispose();
					}
				}

				case null:
					throw new InvalidCastException("Unexpected null from IScriptLoader.LoadFile");
				default:
					throw new InvalidCastException(string.Format("Unsupported return type from IScriptLoader.LoadFile : {0}", code.GetType()));
			}
		}


		/// <summary>
		/// Loads and executes a string containing a Lua/WattleScript script.
		/// </summary>
		/// <param name="code">The code.</param>
		/// <param name="globalContext">The global context.</param>
		/// <param name="codeFriendlyName">Name of the code - used to report errors, etc. Also used by debuggers to locate the original source file.</param>
		/// <returns>
		/// A DynValue containing the result of the processing of the loaded chunk.
		/// </returns>
		public DynValue DoString(string code, Table globalContext = null, string codeFriendlyName = null)
		{
			DynValue func = LoadString(code, globalContext, codeFriendlyName);
			return Call(func);
		}

		/// <summary>
		/// Loads and asynchronously executes a string containing a Lua/WattleScript script. 
		/// </summary>
		/// <param name="code">The code.</param>
		/// <param name="globalContext">The global context.</param>
		/// <param name="codeFriendlyName">Name of the code - used to report errors, etc. Also used by debuggers to locate the original source file.</param>
		/// <returns>
		/// A DynValue containing the result of the processing of the loaded chunk.
		/// </returns>
		public Task<DynValue> DoStringAsync(string code, Table globalContext = null, string codeFriendlyName = null)
		{
			DynValue func = LoadString(code, globalContext, codeFriendlyName);
			return CallAsync(func);
		}

		/// <summary>
		/// Loads and executes a stream containing a Lua/WattleScript script.
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <param name="globalContext">The global context.</param>
		/// <param name="codeFriendlyName">Name of the code - used to report errors, etc. Also used by debuggers to locate the original source file.</param>
		/// <returns>
		/// A DynValue containing the result of the processing of the loaded chunk.
		/// </returns>
		public DynValue DoStream(Stream stream, Table globalContext = null, string codeFriendlyName = null)
		{
			DynValue func = LoadStream(stream, globalContext, codeFriendlyName);
			return Call(func);
		}
		
		/// <summary>
		/// Loads and asynchronously executes a stream containing a Lua/WattleScript script.
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <param name="globalContext">The global context.</param>
		/// <param name="codeFriendlyName">Name of the code - used to report errors, etc. Also used by debuggers to locate the original source file.</param>
		/// <returns>
		/// A DynValue containing the result of the processing of the loaded chunk.
		/// </returns>
		public Task<DynValue> DoStreamAsync(Stream stream, Table globalContext = null, string codeFriendlyName = null)
		{
			DynValue func = LoadStream(stream, globalContext, codeFriendlyName);
			return CallAsync(func);
		}
		
		/// <summary>
		/// Loads and asynchronously executes a bytecode array containing a Lua/WattleScript script.
		/// </summary>
		/// <param name="bytecode">The bytecode.</param>
		/// <param name="globalContext">The global context.</param>
		/// <param name="codeFriendlyName">Name of the code - used to report errors, etc. Also used by debuggers to locate the original source file.</param>
		/// <returns>
		/// A DynValue containing the result of the processing of the loaded chunk.
		/// </returns>
		public Task<DynValue> DoBytecodeAsync(byte[] bytecode, Table globalContext = null, string codeFriendlyName = null)
		{
			using MemoryStream ms = new MemoryStream(bytecode);
			DynValue func = LoadStream(ms, globalContext, codeFriendlyName);
			return CallAsync(func);
		}
		
		/// <summary>
		/// Loads and executes a bytecode array containing a Lua/WattleScript script.
		/// </summary>
		/// <param name="bytecode">The bytecode.</param>
		/// <param name="globalContext">The global context.</param>
		/// <param name="codeFriendlyName">Name of the code - used to report errors, etc. Also used by debuggers to locate the original source file.</param>
		/// <returns>
		/// A DynValue containing the result of the processing of the loaded chunk.
		/// </returns>
		public DynValue DoBytecode(byte[] bytecode, Table globalContext = null, string codeFriendlyName = null)
		{
			using MemoryStream ms = new MemoryStream(bytecode);
			DynValue func = LoadStream(ms, globalContext, codeFriendlyName);
			return Call(func);
		}

		/// <summary>
		/// Loads and executes a file containing a Lua/WattleScript script.
		/// </summary>
		/// <param name="filename">The filename.</param>
		/// <param name="globalContext">The global context.</param>
		/// <param name="codeFriendlyName">Name of the code - used to report errors, etc. Also used by debuggers to locate the original source file.</param>
		/// <returns>
		/// A DynValue containing the result of the processing of the loaded chunk.
		/// </returns>
		public DynValue DoFile(string filename, Table globalContext = null, string codeFriendlyName = null)
		{
			DynValue func = LoadFile(filename, globalContext, codeFriendlyName);
			return Call(func);
		}
		
		/// <summary>
		/// Runs the specified file with all possible defaults for quick experimenting.
		/// </summary>
		/// <param name="filename">The filename.</param>
		/// A DynValue containing the result of the processing of the executed script.
		public static DynValue RunFile(string filename)
		{
			Script S = new Script();
			return S.DoFile(filename);
		}

		/// <summary>
		/// Runs the specified code with all possible defaults for quick experimenting.
		/// </summary>
		/// <param name="code">The Lua/WattleScript code.</param>
		/// A DynValue containing the result of the processing of the executed script.
		public static DynValue RunString(string code)
		{
			return new Script().DoString(code);
		}

		/// <summary>
		/// Creates a closure from a bytecode address.
		/// </summary>
		/// <param name="proto">The function prototype.</param>
		/// <param name="envTable">The env table to create a 0-upvalue</param>
		/// <returns></returns>
		private DynValue MakeClosure(FunctionProto proto, Table envTable = null)
		{
			this.CheckScriptOwnership(envTable);
			Closure c;
			
			if (envTable == null)
			{
				if ((proto.flags & FunctionFlags.IsChunk) == FunctionFlags.IsChunk)
				{
					c = new Closure(this, proto,
						new SymbolRef[] { SymbolRef.Upvalue(WellKnownSymbols.ENV, 0) },
						new Upvalue[1]);
				}
				else
				{
					c = new Closure(this, proto, Array.Empty<SymbolRef>(), Array.Empty<Upvalue>());
				}
			}
			else
			{
				var syms = new SymbolRef[] {
					new SymbolRef() { i_Env = null, i_Index= 0, i_Name = WellKnownSymbols.ENV, i_Type =  SymbolRefType.DefaultEnv },
				};

				var vals = new Upvalue[] {
					Upvalue.Create(DynValue.NewTable(envTable))
				};
				c = new Closure(this, proto, syms, vals);
			}

			return DynValue.NewClosure(c);
		}

		/// <summary>
		/// Calls the specified function.
		/// </summary>
		/// <param name="function">The Lua/WattleScript function to be called</param>
		/// <returns>
		/// The return value(s) of the function call.
		/// </returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public DynValue Call(DynValue function)
		{
			return Call(function, Array.Empty<DynValue>());
		}

		public Task<DynValue> CallAsync(DynValue function)
		{
			return CallAsync(function, Array.Empty<DynValue>());
		}
		
		/// <summary>
		/// Calls the specified function, marking the first parameter as this/self.
		/// </summary>
		/// <param name="function">The Lua/WattleScript function to be called</param>
		/// <param name="args">The arguments to pass to the function.</param>
		/// <returns>
		/// The return value(s) of the function call.
		/// </returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		/// <exception cref="System.ArgumentException">Thrown if args.Length is less than 1</exception>
		public DynValue ThisCall(DynValue function, params DynValue[] args)
		{
			this.CheckScriptOwnership(function);
			this.CheckScriptOwnership(args);

			if (args == null || args.Length < 1)
				throw new ArgumentException("args");
			
			if (function.Type != DataType.Function && function.Type != DataType.ClrFunction)
			{
				DynValue metafunction = m_MainProcessor.GetMetamethod(function, "__call");

				if (metafunction.IsNotNil())
				{
					DynValue[] metaargs = new DynValue[args.Length + 1];
					metaargs[0] = function;
					for (int i = 0; i < args.Length; i++)
						metaargs[i + 1] = args[i];

					function = metafunction;
					args = metaargs;
				}
				else
				{
					throw new ArgumentException("function is not a function and has no __call metamethod.");
				}
			}
			else if (function.Type == DataType.ClrFunction)
			{
				return function.Callback.ClrCallback(CreateDynamicExecutionContext(function.Callback), new CallbackArguments(args, DynValue.Nil, true));
			}

			return m_MainProcessor.ThisCall(function, args);
		}
		
		/// <summary>
		/// Calls the specified function.
		/// </summary>
		/// <param name="function">The Lua/WattleScript function to be called</param>
		/// <param name="args">The arguments to pass to the function.</param>
		/// <returns>
		/// The return value(s) of the function call.
		/// </returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public Task<DynValue> ThisCallAsync(DynValue function, params DynValue[] args)
		{
			this.CheckScriptOwnership(function);
			this.CheckScriptOwnership(args);

			if (args == null || args.Length < 1)
				throw new ArgumentException("args");
			
			if (function.Type != DataType.Function && function.Type != DataType.ClrFunction)
			{
				DynValue metafunction = m_MainProcessor.GetMetamethod(function, "__call");

				if (metafunction.IsNotNil())
				{
					DynValue[] metaargs = new DynValue[args.Length + 1];
					metaargs[0] = function;
					for (int i = 0; i < args.Length; i++)
						metaargs[i + 1] = args[i];

					function = metafunction;
					args = metaargs;
				}
				else
				{
					throw new ArgumentException("function is not a function and has no __call metamethod.");
				}
			}
			else if (function.Type == DataType.ClrFunction)
			{
				return Task.FromResult(function.Callback.ClrCallback(
					CreateDynamicExecutionContext(function.Callback), new CallbackArguments(args, DynValue.Nil, true)));
			}

			return m_MainProcessor.ThisCallAsync(function, args);
		}
		
		/// <summary>
		/// Calls the specified function.
		/// </summary>
		/// <param name="function">The Lua/WattleScript function to be called</param>
		/// <param name="args">The arguments to pass to the function.</param>
		/// <returns>
		/// The return value(s) of the function call.
		/// </returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public DynValue Call(DynValue function, params DynValue[] args)
		{
			this.CheckScriptOwnership(function);
			this.CheckScriptOwnership(args);

			if (function.Type != DataType.Function && function.Type != DataType.ClrFunction)
			{
				DynValue metafunction = m_MainProcessor.GetMetamethod(function, "__call");

				if (metafunction.IsNotNil())
				{
					DynValue[] metaargs = new DynValue[args.Length + 1];
					metaargs[0] = function;
					for (int i = 0; i < args.Length; i++)
						metaargs[i + 1] = args[i];

					function = metafunction;
					args = metaargs;
				}
				else
				{
					throw new ArgumentException("function is not a function and has no __call metamethod.");
				}
			}
			else if (function.Type == DataType.ClrFunction)
			{
				return function.Callback.ClrCallback(CreateDynamicExecutionContext(function.Callback), new CallbackArguments(args, DynValue.Nil, false));
			}

			return m_MainProcessor.Call(function, args);
		}

		/// <summary>
		/// Calls the specified function.
		/// </summary>
		/// <param name="function">The Lua/WattleScript function to be called</param>
		/// <param name="args">The arguments to pass to the function.</param>
		/// <returns>
		/// The return value(s) of the function call.
		/// </returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public Task<DynValue> CallAsync(DynValue function, params DynValue[] args)
		{
			this.CheckScriptOwnership(function);
			this.CheckScriptOwnership(args);

			if (function.Type != DataType.Function && function.Type != DataType.ClrFunction)
			{
				DynValue metafunction = m_MainProcessor.GetMetamethod(function, "__call");

				if (metafunction.IsNotNil())
				{
					DynValue[] metaargs = new DynValue[args.Length + 1];
					metaargs[0] = function;
					for (int i = 0; i < args.Length; i++)
						metaargs[i + 1] = args[i];

					function = metafunction;
					args = metaargs;
				}
				else
				{
					throw new ArgumentException("function is not a function and has no __call metamethod.");
				}
			}
			else if (function.Type == DataType.ClrFunction)
			{
				return Task.FromResult(function.Callback.ClrCallback(
					CreateDynamicExecutionContext(function.Callback), new CallbackArguments(args, DynValue.Nil, false)));
			}

			return m_MainProcessor.CallAsync(function, args);
		}

		/// <summary>
		/// Calls the specified function.
		/// </summary>
		/// <param name="function">The Lua/WattleScript function to be called</param>
		/// <param name="args">The arguments to pass to the function.</param>
		/// <returns>
		/// The return value(s) of the function call.
		/// </returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public DynValue Call(DynValue function, params object[] args)
		{
			DynValue[] dargs = new DynValue[args.Length];

			for (int i = 0; i < dargs.Length; i++)
				dargs[i] = DynValue.FromObject(this, args[i]);

			return Call(function, dargs);
		}

		/// <summary>
		/// Calls the specified function.
		/// </summary>
		/// <param name="function">The Lua/WattleScript function to be called</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public DynValue Call(object function)
		{
			return Call(DynValue.FromObject(this, function));
		}

		/// <summary>
		/// Calls the specified function.
		/// </summary>
		/// <param name="function">The Lua/WattleScript function to be called </param>
		/// <param name="args">The arguments to pass to the function.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public DynValue Call(object function, params object[] args)
		{
			return Call(DynValue.FromObject(this, function), args);
		}

		/// <summary>
		/// Calls the specified function.
		/// </summary>
		/// <param name="function">The Lua/WattleScript function to be called </param>
		/// <param name="args">The arguments to pass to the function.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public DynValue Call(object function, params DynValue[] args)
		{
			return Call(DynValue.FromObject(this, function), args);
		}
		
		/// <summary>
		/// Calls the specified function, passing the first parameter as this
		/// </summary>
		/// <param name="function">The Lua/WattleScript function to be called </param>
		/// <param name="args">The arguments to pass to the function.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public DynValue ThisCall(object function, params DynValue[] args)
		{
			return ThisCall(DynValue.FromObject(this, function), args);
		}
		
		/// <summary>
		/// Calls the specified function, passing the first parameter as this
		/// </summary>
		/// <param name="function">The Lua/WattleScript function to be called </param>
		/// <param name="args">The arguments to pass to the function. Converted to DynValue[] via DynValue.FromObject()</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public DynValue ThisCall(object function, params object[] args)
		{
			return ThisCall(DynValue.FromObject(this, function), args);
		}
		
		/// <summary>
		/// Calls the specified function, passing the first parameter as this
		/// </summary>
		/// <param name="function">The Lua/WattleScript function to be called </param>
		/// <param name="args">The arguments to pass to the function. Converted to DynValue[] via DynValue.FromObject()</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public DynValue ThisCall(DynValue function, params object[] args)
		{
			DynValue[] dargs = new DynValue[args.Length];

			for (int i = 0; i < dargs.Length; i++)
				dargs[i] = DynValue.FromObject(this, args[i]);

			return ThisCall(function, dargs);
		}
		
		/// <summary>
		/// Calls the specified function, passing the first parameter as this
		/// </summary>
		/// <param name="function">The Lua/WattleScript function to be called </param>
		/// <param name="args">The arguments to pass to the function.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public Task<DynValue> ThisCallAsync(object function, params DynValue[] args)
		{
			return ThisCallAsync(DynValue.FromObject(this, function), args);
		}
		
		/// <summary>
		/// Calls the specified function, passing the first parameter as this
		/// </summary>
		/// <param name="function">The Lua/WattleScript function to be called </param>
		/// <param name="args">The arguments to pass to the function. Converted to DynValue[] via DynValue.FromObject()</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public Task<DynValue> ThisCallAsync(object function, params object[] args)
		{
			return ThisCallAsync(DynValue.FromObject(this, function), args);
		}
		
		/// <summary>
		/// Calls the specified function, passing the first parameter as this
		/// </summary>
		/// <param name="function">The Lua/WattleScript function to be called </param>
		/// <param name="args">The arguments to pass to the function. Converted to DynValue[] via DynValue.FromObject()</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public Task<DynValue> ThisCallAsync(DynValue function, params object[] args)
		{
			DynValue[] dargs = new DynValue[args.Length];

			for (int i = 0; i < dargs.Length; i++)
				dargs[i] = DynValue.FromObject(this, args[i]);

			return ThisCallAsync(function, dargs);
		}

		/// <summary>
		/// Calls the specified function, passing the first parameter as this
		/// </summary>
		/// <param name="function">The Lua/WattleScript function to be called </param>
		/// <param name="args">The arguments to pass to the function. Converted to DynValue[] via DynValue.FromObject()</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public Task<DynValue> CallAsync(DynValue function, params object[] args)
		{
			DynValue[] dargs = new DynValue[args.Length];

			for (int i = 0; i < dargs.Length; i++)
				dargs[i] = DynValue.FromObject(this, args[i]);

			return CallAsync(function, dargs);
		}

		/// <summary>
		/// Calls the specified function, passing the first parameter as this
		/// </summary>
		/// <param name="function">The Lua/WattleScript function to be called.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public  Task<DynValue> CallAsync(object function)
		{
			return CallAsync(DynValue.FromObject(this, function));
		}

		/// <summary>
		/// Calls the specified function, passing the first parameter as this
		/// </summary>
		/// <param name="function">The Lua/WattleScript function to be called </param>
		/// <param name="args">The arguments to pass to the function. Converted to DynValue[] via DynValue.FromObject()</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function</exception>
		public Task<DynValue> CallAsync(object function, params object[] args)
		{
			return CallAsync(DynValue.FromObject(this, function), args);
		}

		/// <summary>
		/// Creates a coroutine pointing at the specified function.
		/// </summary>
		/// <param name="function">The function.</param>
		/// <returns>
		/// The coroutine handle.
		/// </returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function or DataType.ClrFunction</exception>
		public DynValue CreateCoroutine(DynValue function)
		{
			this.CheckScriptOwnership(function);

			return function.Type switch
			{
				DataType.Function => m_MainProcessor.Coroutine_Create(function.Function),
				DataType.ClrFunction => DynValue.NewCoroutine(new Coroutine(function.Callback)),
				_ => throw new ArgumentException("function is not of DataType.Function or DataType.ClrFunction")
			};
		}

		/// <summary>
		/// Creates a coroutine pointing at the specified function.
		/// </summary>
		/// <param name="function">The function.</param>
		/// <returns>
		/// The coroutine handle.
		/// </returns>
		/// <exception cref="System.ArgumentException">Thrown if function is not of DataType.Function or DataType.ClrFunction</exception>
		public DynValue CreateCoroutine(object function)
		{
			return CreateCoroutine(DynValue.FromObject(this, function));
		}

		/// <summary>
		/// Gets or sets a value indicating whether the debugger is enabled.
		/// Note that unless a debugger attached, this property returns a 
		/// value which might not reflect the real status of the debugger.
		/// Use this property if you want to disable the debugger for some 
		/// executions.
		/// </summary>
		public bool DebuggerEnabled
		{
			get => m_MainProcessor.DebuggerEnabled;
			set => m_MainProcessor.DebuggerEnabled = value;
		}


		/// <summary>
		/// Attaches a debugger. This usually should be called by the debugger itself and not by user code.
		/// </summary>
		/// <param name="debugger">The debugger object.</param>
		public void AttachDebugger(IDebugger debugger)
		{
			DebuggerEnabled = true;
			m_Debugger = debugger;
			m_MainProcessor.AttachDebugger(debugger);

			foreach (SourceCode src in m_Sources)
				SignalSourceCodeChange(src);

			SignalByteCodeChange();
		}

		/// <summary>
		/// Gets the source code.
		/// </summary>
		/// <param name="sourceCodeID">The source code identifier.</param>
		/// <returns></returns>
		public SourceCode GetSourceCode(int sourceCodeID)
		{
			return m_Sources[sourceCodeID];
		}

		/// <summary>
		/// Gets the source code count.
		/// </summary>
		/// <value>
		/// The source code count.
		/// </value>
		public int SourceCodeCount => m_Sources.Count;

		/// <summary>
		/// Loads a module as per the "require" Lua function. http://www.lua.org/pil/8.1.html
		/// </summary>
		/// <param name="modname">The module name</param>
		/// <param name="globalContext">The global context.</param>
		/// <returns></returns>
		/// <exception cref="ScriptRuntimeException">Raised if module is not found</exception>
		public DynValue RequireModule(string modname, Table globalContext = null)
		{
			this.CheckScriptOwnership(globalContext);

			Table globals = globalContext ?? m_GlobalTable;
			string filename = Options.ScriptLoader.ResolveModuleName(modname, globals);

			if (filename == null)
				throw new ScriptRuntimeException("module '{0}' not found", modname);

			DynValue func = LoadFile(filename, globalContext, filename);
			return func;
		}

		/// <summary>
		/// Gets a type metatable.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <returns></returns>
		public Table GetTypeMetatable(DataType type)
		{
			int t = (int)type;

			if (t >= 0 && t < m_TypeMetatables.Length)
				return m_TypeMetatables[t];

			return null;
		}

		/// <summary>
		/// Sets a type metatable.
		/// </summary>
		/// <param name="type">The type. Must be Nil, Boolean, Number, String, Range or Function</param>
		/// <param name="metatable">The metatable.</param>
		/// <exception cref="System.ArgumentException">Specified type not supported :  + type.ToString()</exception>
		public void SetTypeMetatable(DataType type, Table metatable)
		{
			this.CheckScriptOwnership(metatable);

			int t = (int)type;

			if (t >= 0 && t < m_TypeMetatables.Length)
				m_TypeMetatables[t] = metatable;
			else
				throw new ArgumentException("Specified type not supported : " + type.ToString());
		}

		/// <summary>
		/// Sets the prototype for tables
		/// </summary>
		internal void SetTablePrototype(Table prototype)
		{
			this.CheckScriptOwnership(prototype);
			m_TablePrototype = prototype;
		}

		internal Table GetTablePrototype() => m_TablePrototype;
		
		/// <summary>
		/// Warms up the parser/lexer structures so that WattleScript operations start faster.
		/// </summary>
		public static void WarmUp()
		{
			Script s = new Script(CoreModules.Basic);
			s.LoadString("return 1;");
		}
		
		/// <summary>
		/// Creates a new dynamic expression.
		/// </summary>
		/// <param name="code">The code of the expression.</param>
		/// <returns></returns>
		public DynamicExpression CreateDynamicExpression(string code)
		{
			DynamicExprExpression dee = Loader_Fast.LoadDynamicExpr(this, new SourceCode("__dynamic", code, -1, this));
			return new DynamicExpression(this, code, dee);
		}

		/// <summary>
		/// Creates a new dynamic expression which is actually quite static, returning always the same constant value.
		/// </summary>
		/// <param name="code">The code of the not-so-dynamic expression.</param>
		/// <param name="constant">The constant to return.</param>
		/// <returns></returns>
		public DynamicExpression CreateConstantDynamicExpression(string code, DynValue constant)
		{
			this.CheckScriptOwnership(constant);

			return new DynamicExpression(this, code, constant);
		}

		/// <summary>
		/// Gets an execution context exposing only partial functionality, which should be used for
		/// those cases where the execution engine is not really running - for example for dynamic expression
		/// or calls from CLR to CLR callbacks
		/// </summary>
		internal ScriptExecutionContext CreateDynamicExecutionContext(CallbackFunction func = null)
		{
			return new ScriptExecutionContext(m_MainProcessor, func, null, isDynamic: true);
		}

		/// <summary>
		/// WattleScript (like Lua itself) provides a registry, a predefined table that can be used by any CLR code to 
		/// store whatever Lua values it needs to store. 
		/// Any CLR code can store data into this table, but it should take care to choose keys 
		/// that are different from those used by other libraries, to avoid collisions. 
		/// Typically, you should use as key a string GUID, a string containing your library name, or a 
		/// userdata with the address of a CLR object in your code.
		/// </summary>
		public Table Registry
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets a banner string with copyright info, link to website, version, etc.
		/// </summary>
		public static string GetBanner(string subproduct = null)
		{
			subproduct = (subproduct != null) ? (subproduct + " ") : "";

			StringBuilder sb = new StringBuilder();
			sb.AppendLine(string.Format("WattleScript {0}{1} [{2}]", subproduct, VERSION, GlobalOptions.Platform.GetPlatformName()));
			sb.AppendLine("Copyright (C) 2014-2022 WattleScript Contributors");
			sb.AppendLine("https://wattlescript.github.io");
			return sb.ToString();
		}

		Script IScriptPrivateResource.OwnerScript => this;
		public List<ScriptParserMessage> ParserMessages => i_ParserMessages;
	}
}
