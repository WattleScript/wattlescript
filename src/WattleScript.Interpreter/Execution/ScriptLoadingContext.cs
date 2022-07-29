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

		private ScriptLoadingContext() {}
		
		public ScriptLoadingContext(Script s)
		{
			Script = s;

			KnownTypes.Add("object", new TypeDefinition("object", TypeDefinition.TypeTypes.Root, false));
			KnownTypes.Add("number", new TypeDefinition("number", TypeDefinition.TypeTypes.Atomic, false));
			KnownTypes.Add("string", new TypeDefinition("string", TypeDefinition.TypeTypes.Atomic, false));
		}

		internal class TypeDefinition
		{
			public enum TypeTypes
			{
				Root,
				Atomic,
				Complex
			}
			
			public Dictionary<TypeDefinition, bool> Childs = new Dictionary<TypeDefinition, bool>();
			public TypeTypes TypeType { get; set; }
			public string Name { get; set; }
			public bool IsGeneric { get; set; }

			public TypeDefinition(string name, TypeTypes typeType, bool isGeneric)
			{
				Name = name;
				TypeType = typeType;
				IsGeneric = isGeneric;
			}
		}

		internal Dictionary<string, TypeDefinition> KnownTypes = new Dictionary<string, TypeDefinition>();
	}
}
