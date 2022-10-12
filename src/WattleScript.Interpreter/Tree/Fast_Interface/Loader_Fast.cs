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

		internal static ScriptLoadingContext CreateLoadingContext(Script script, SourceCode source, string preprocessedCode = null, Dictionary<string, DefineNode> defines = null, bool lexerAutoSkipComments = true, bool lexerKeepInsignificantChars = false, Linker staticImport = null)
		{
			return new ScriptLoadingContext(script)
			{
				Source = source,
				Lexer = new Lexer(source.SourceID, preprocessedCode ?? source.Code, lexerAutoSkipComments, script.Options.Syntax, script.Options.Directives, defines, lexerKeepInsignificantChars),
				Syntax = script.Options.Syntax,
				Linker = staticImport
			};
		}

		internal static FunctionProto LoadChunk(Script script, SourceCode source, Linker staticImport = null)
		{
			
	#if !DEBUG_PARSER
			try
			{
	#endif
				ChunkStatement stat;

				using (script.PerformanceStats.StartStopwatch(Diagnostics.PerformanceCounter.AstCreation))
				{
					ScriptLoadingContext lcontext;
					bool staticImportIsNull = staticImport == null;
					
					if (script.Options.Syntax == ScriptSyntax.Wattle)
					{
						Preprocessor preprocess = new Preprocessor(script, source.SourceID, source.Code);
						preprocess.Process();

						staticImport ??= new Linker(script, source.SourceID, preprocess.ProcessedSource, preprocess.Defines);
						staticImport.Process();
						
						lcontext = CreateLoadingContext(script, source, preprocess.ProcessedSource, preprocess.Defines, staticImport: staticImport);
					}
					else
					{
						lcontext = CreateLoadingContext(script, source);
					}
					
					stat = new ChunkStatement(lcontext);

					if (script.Options.Syntax == ScriptSyntax.Wattle && staticImportIsNull)
					{
						stat.Block.InsertStatements(staticImport?.Export());	
					}
					
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
