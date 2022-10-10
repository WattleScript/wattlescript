using System.Collections.Generic;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Tree.Expressions;

namespace WattleScript.Interpreter.Tree.Statements
{
    class EnumDefinitionStatement : Statement
    {
        private string enumName;
        private SourceRef assignment;
        private SourceRef buildCode;
        private SymbolRefExpression globalSymbol;
        private Annotation[] annotations;
        
        private Dictionary<string, DynValue> members = new Dictionary<string, DynValue>();

        public EnumDefinitionStatement(ScriptLoadingContext lcontext)
            : base(lcontext)
        {
            annotations = lcontext.FunctionAnnotations.ToArray();
            lcontext.FunctionAnnotations = new List<Annotation>();
            //lexer is at "enum"
            var start = lcontext.Lexer.Current;
            lcontext.Lexer.Next();
            var name = CheckTokenType(lcontext, TokenType.Name);
            enumName = name.Text;
            assignment = start.GetSourceRef(name);
            var buildStart = CheckTokenType(lcontext, TokenType.Brk_Open_Curly);
            HashSet<string> usedNames = new HashSet<string>();
            long nextVal = 0;
            while (lcontext.Lexer.Current.Type != TokenType.Brk_Close_Curly &&
                   lcontext.Lexer.Current.Type != TokenType.Eof)
            {
                var valName = CheckTokenType(lcontext, TokenType.Name);
                if (usedNames.Contains(valName.Text))
                    throw new SyntaxErrorException(valName, $"enum {enumName} already contains member {valName}");
                usedNames.Add(valName.Text);
                if (lcontext.Lexer.Current.Type == TokenType.Op_Assignment)
                {
                    lcontext.Lexer.Next();
                    var valStart = lcontext.Lexer.Current;
                    var value = Expression.Expr(lcontext);
                    if (!value.EvalLiteral(out var expVal, members))
                        throw new SyntaxErrorException(lcontext.Script,
                                valStart.GetSourceRef(lcontext.Lexer.Current), "enum expression cannot be non-constant");
                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    if(!expVal.TryCastToNumber(out var num) || (long)num != num)
                        throw new SyntaxErrorException(lcontext.Script,
                            valStart.GetSourceRef(lcontext.Lexer.Current), "enum value '{0}' not a literal integer", expVal.ToDebugPrintString());
                    nextVal = (long) num + 1;
                    members.Add(valName.Text, DynValue.NewNumber(num));
                }
                else
                {
                    members.Add(valName.Text, DynValue.NewNumber(nextVal++));
                }
                if (lcontext.Lexer.Current.Type == TokenType.Comma) {
                    lcontext.Lexer.Next(); //Move on to next value
                } else {
                    break; // Final value
                }
            }
            var buildEnd = CheckTokenType(lcontext, TokenType.Brk_Close_Curly);
            buildCode = buildStart.GetSourceRef(buildEnd, false);
        }

        public override void ResolveScope(ScriptLoadingContext lcontext)
        {
            globalSymbol = new SymbolRefExpression(lcontext, lcontext.Scope.CreateGlobalReference(enumName));
        }


        public override void Compile(Execution.VM.FunctionBuilder bc)
        {
            bc.PushSourceRef(buildCode);
            int j = 0;
            bool created = false;
            foreach (var m in members)
            {
                if (j >= 8)
                {
                    bc.Emit_TblInitN(j * 2, created ? 0 : 1);
                    created = true;
                    j = 0;
                }
                bc.Emit_Literal(DynValue.NewString(m.Key));
                bc.Emit_Literal(m.Value);
                j++;
            }
            if (j > 0) {
                bc.Emit_TblInitN(j * 2, created ? 0 : 1);
                created = true;
            }
            if (!created) bc.Emit_TblInitN(0, 1);
            
            bc.PopSourceRef();
            bc.PushSourceRef(assignment);
            bc.Emit_TabProps(TableKind.Enum, MemberModifierFlags.None, true);
            foreach(var annot in annotations)
                bc.Emit_Annot(annot);
            globalSymbol.CompileAssignment(bc, Operator.NotAnOperator, 0, 0);
            bc.Emit_Pop();
            bc.PopSourceRef();
        }
    }
}