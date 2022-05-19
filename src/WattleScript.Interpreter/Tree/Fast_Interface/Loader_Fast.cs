using System.Collections.Generic;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;
using WattleScript.Interpreter.Tree.Expressions;
using WattleScript.Interpreter.Tree.Statements;

namespace WattleScript.Interpreter.Tree.Fast_Interface
{
	internal static class Loader_Fast
	{
		internal static DynamicExprExpression LoadDynamicExpr(Script script, SourceCode source)
		{
			ScriptLoadingContext lcontext = CreateLoadingContext(script, source);

			try
			{
				lcontext.IsDynamicExpression = true;
				lcontext.Anonymous = true;

				Expression exp;
				using (script.PerformanceStats.StartStopwatch(Diagnostics.PerformanceCounter.AstCreation))
				{
					exp = Expression.Expr(lcontext);
					lcontext.Scope = new BuildTimeScope();
					exp.ResolveScope(lcontext);
				}

				return new DynamicExprExpression(exp, lcontext);
			}
			catch (SyntaxErrorException ex)
			{
				ex.DecorateMessage(script);
				ex.Rethrow();
				throw;
			}
		}

		private static ScriptLoadingContext CreateLoadingContext(Script script, SourceCode source, 
			string preprocessedCode = null,
			Dictionary<string, DefineNode> defines = null)
		{
			return new ScriptLoadingContext(script)
			{
				Source = source,
				Lexer = new Lexer(source.SourceID, preprocessedCode ?? source.Code, true, script.Options.Syntax, script.Options.Directives, defines),
				Syntax = script.Options.Syntax
			};
		}

		internal static FunctionProto LoadChunk(Script script, SourceCode source)
		{
			
	#if !DEBUG_PARSER
			try
			{
	#endif
				ScriptLoadingContext lcontext;
				ChunkStatement stat;

				using (script.PerformanceStats.StartStopwatch(Diagnostics.PerformanceCounter.AstCreation))
				{
					if (script.Options.Syntax == ScriptSyntax.Wattle)
					{
						var preprocess = new Preprocessor(script, source.SourceID, source.Code);
						preprocess.Process();
						lcontext = CreateLoadingContext(script, source, preprocess.ProcessedSource,
							preprocess.Defines);
					}
					else
					{
						lcontext = CreateLoadingContext(script, source);
					}
					stat = new ChunkStatement(lcontext);
					lcontext.Scope = new BuildTimeScope();
					stat.ResolveScope(lcontext);
				}
				
				using (script.PerformanceStats.StartStopwatch(Diagnostics.PerformanceCounter.Compilation))
					return stat.CompileFunction(script);
#if !DEBUG_PARSER

			}
			catch (SyntaxErrorException ex)
			{
				ex.DecorateMessage(script);
				ex.Rethrow();
				throw;
			}
#endif
		}

		internal static FunctionProto LoadFunction(Script script, SourceCode source, bool usesGlobalEnv)
		{
			ScriptLoadingContext lcontext = CreateLoadingContext(script, source);

			try
			{
				FunctionDefinitionExpression fnx;

				using (script.PerformanceStats.StartStopwatch(Diagnostics.PerformanceCounter.AstCreation))
				{
					fnx = new FunctionDefinitionExpression(lcontext, usesGlobalEnv);
					lcontext.Scope = new BuildTimeScope();
					fnx.ResolveScope(lcontext);
				}
				

				using (script.PerformanceStats.StartStopwatch(Diagnostics.PerformanceCounter.Compilation))
					return fnx.CompileBody(null, script, source.Name);

			}
			catch (SyntaxErrorException ex)
			{
				ex.DecorateMessage(script);
				ex.Rethrow();
				throw;
			}

		}

	}
}
