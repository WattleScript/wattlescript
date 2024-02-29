using System;
using System.Collections.Generic;
using System.Text;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Tree.Expressions;
using WattleScript.Interpreter.Tree.Statements;

namespace WattleScript.Interpreter.Tree
{
	interface IStaticallyImportableStatement
	{
		public Token NameToken { get; }
		public string DefinitionType { get; }
		public string Namespace { get; }
	}
}
