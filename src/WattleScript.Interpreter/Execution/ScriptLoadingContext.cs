using System.Collections.Generic;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Tree;

namespace WattleScript.Interpreter.Execution
{
	class ScriptLoadingContext
	{
		public Script Script { get; private set; }
		public BuildTimeScope Scope { get; set; }
		public SourceCode Source { get; set; }
		public bool Anonymous { get; set; }
		public bool IsDynamicExpression { get; set; }
		public Lexer Lexer { get; set; }
		
		public ScriptSyntax Syntax { get; set; }

		//Compiler state
		internal List<Annotation> ChunkAnnotations { get; set; } = new List<Annotation>();
		internal List<Annotation> FunctionAnnotations { get; set; } = new List<Annotation>();

		
		public ScriptLoadingContext(Script s)
		{
			Script = s;
		}

	}
}
