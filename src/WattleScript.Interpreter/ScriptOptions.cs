using System;
using System.Collections.Generic;
using System.IO;
using WattleScript.Interpreter.Loaders;

namespace WattleScript.Interpreter
{
	/// <summary>
	/// This class contains options to customize behaviour of Script objects.
	/// </summary>
	public class ScriptOptions
	{
		internal ScriptOptions()
		{
		}

		internal ScriptOptions(ScriptOptions defaults)
		{
			this.DebugInput = defaults.DebugInput;
			this.DebugPrint = defaults.DebugPrint;

			this.UseLuaErrorLocations = defaults.UseLuaErrorLocations;
			this.Stdin = defaults.Stdin;
			this.Stdout = defaults.Stdout;
			this.Stderr = defaults.Stderr;
			this.TailCallOptimizationThreshold = defaults.TailCallOptimizationThreshold;

			this.ScriptLoader = defaults.ScriptLoader;

			this.CheckThreadAccess = defaults.CheckThreadAccess;
		}
		
		public enum ParserErrorModes
		{
			Throw,
			Report
		}

		/// <summary>
		/// Gets or sets the current script-loader.
		/// </summary>
		public IScriptLoader ScriptLoader { get; set; }

		/// <summary>
		/// Gets or sets the debug print handler
		/// </summary>
		public Action<string> DebugPrint { get; set; }

		/// <summary>
		/// Gets or sets the debug input handler (takes a prompt as an input, for interactive interpreters, like debug.debug).
		/// </summary>
		public Func<string, string> DebugInput { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether error messages will use Lua error locations instead of WattleScript 
		/// improved ones. Use this for compatibility with legacy Lua code which parses error messages.
		/// </summary>
		public bool UseLuaErrorLocations { get; set; }

		/// <summary>
		/// Gets or sets a value which dictates the behaviour of the colon (':') operator in callbacks to CLR code.
		/// </summary>
		public ColonOperatorBehaviour ColonOperatorClrCallbackBehaviour { get; set; }

		/// <summary>
		/// Gets or sets the stream used as stdin. If null, a default stream is used.
		/// </summary>
		public Stream Stdin { get; set; }

		/// <summary>
		/// Gets or sets the stream used as stdout. If null, a default stream is used.
		/// </summary>
		public Stream Stdout { get; set; }

		/// <summary>
		/// Gets or sets the stream used as stderr. If null, a default stream is used.
		/// </summary>
		public Stream Stderr { get; set; }

		/// <summary>
		/// Gets or sets the stack depth threshold at which WattleScript starts doing
		/// tail call optimizations.
		/// TCOs can provide the little benefit of avoiding stack overflows in corner case
		/// scenarios, at the expense of losing debug information and error stack traces 
		/// in all other, more common scenarios. WattleScript choice is to start performing
		/// TCOs only after a certain threshold of stack usage is reached - by default
		/// half the current stack depth (128K entries), thus 64K entries, on either
		/// the internal stacks.
		/// Set this to int.MaxValue to disable TCOs entirely, or to 0 to always have
		/// TCOs enabled.
		/// </summary>
		public int TailCallOptimizationThreshold { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the thread check is enabled.
		/// A "lazy" thread check is performed everytime execution is entered to ensure that no two threads
		/// calls WattleScript execution concurrently. However 1) the check is performed best effort (thus, it might
		/// not detect all issues) and 2) it might trigger in very odd legal situations (like, switching threads 
		/// inside a CLR-callback without actually having concurrency.
		/// 
		/// Disable this option if the thread check is giving problems in your scenario, but please check that
		/// you are not calling WattleScript execution concurrently as it is not supported.
		/// </summary>
		public bool CheckThreadAccess { get; set; }
		
		/// <summary>
		/// Gets or sets a value indicating whether or not tasks are automatically awaited.
		/// When set to true, each call to a CLR function returning Task will automatically await and cast the value
		/// When set to false, the call returns a task object that can have await() called on it.
		/// </summary>
		public bool AutoAwait { get; set; }
		
		/// <summary>
		/// Gets or sets a value indicating the syntax used by the compiler.
		/// </summary>
		public ScriptSyntax Syntax { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether tables are indexed from zero or one (default)
		/// When set to 1, tables will be indexed from one
		/// When set to 0, tables will be indexed from zero
		/// Other values are not supported
		/// </summary>
		public int IndexTablesFrom { get; set; } = 1;

		/// <summary>
		/// Gets or sets the annotation policy for the script compiler (C-Like mode only)
		/// <see cref="AnnotationValueParsingPolicy" />
		/// </summary>
		public IAnnotationPolicy AnnotationPolicy { get; set; } = AnnotationPolicies.Allow;

		/// <summary>
		/// List of keywords that will be interpreted as directives by the compiler (C-Like mode only).
		/// These directions will store the RHS as a string annotation on the chunk.
		/// </summary>
		public HashSet<string> Directives { get; set; } = new HashSet<string>();

		/// <summary>
		/// Specifies how parser reacts to errors while parsing.
		/// Options are: Throw (paring is aborted after first error), Report (errors are stashed and available in Script.ParserMessages)
		/// </summary>
		public ParserErrorModes ParserErrorMode { get; set; } = ParserErrorModes.Throw;

		/// <summary>
		/// Definitions to be passed to the preprocessor.
		/// Only used when <see cref="Syntax"/> is set to WattleScript
		/// </summary>
		public List<PreprocessorDefine> Defines { get; set; } = new List<PreprocessorDefine>();

		/// <summary>
		/// Set maximum number of instructions to be executed before forceful termination of the script.
		/// If set to 0, this property is ignored (no limit is applied).
		/// </summary>
		public ulong InstructionLimit { get; set; } = 0;
	}
}
