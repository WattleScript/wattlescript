using System;
using System.Collections.Generic;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;
using WattleScript.Interpreter.Tree.Expressions;

namespace WattleScript.Interpreter.Tree
{
    class GeneratedClosure : IClosureBuilder
    {
        private Dictionary<string, SymbolRefExpression> referenced = new Dictionary<string, SymbolRefExpression>();
        private RuntimeScopeFrame frame;
        private string name;
        private SourceRef sr;
        private FunctionFlags flags;
        private bool varargs;

        private List<string> symbols = new List<string>();
        private List<string> locals = new List<string>();
        private List<Expression> toResolve = new List<Expression>();
        private List<SymbolRef> upvalues = new List<SymbolRef>();

        public GeneratedClosure(string name, SourceRef sr, FunctionFlags flags, bool varargs)
        {
            this.name = name;
            this.sr = sr;
            this.flags = flags;
            this.varargs = varargs;
        }
        
        public void AddSymbol(string symbol)
        {
            symbols.Add(symbol);
        }

        public void AddExpression(Expression expr)
        {
            toResolve.Add(expr);
        }

        public void DefineLocal(string symbol)
        {
            locals.Add(symbol);
        }
        
        public void ResolveScope(ScriptLoadingContext lcontext, Action<ScriptLoadingContext> extra = null)
        {
            lcontext.Scope.PushFunction(this);
            lcontext.Scope.ForceEnvUpValue();
            if(varargs) lcontext.Scope.SetHasVarArgs();
            foreach (var s in symbols)
            {
                referenced[s] = new SymbolRefExpression(lcontext, lcontext.Scope.Find(s));
            }
            foreach (var s in locals)
            {
                if (s == "this")
                    referenced[s] = new SymbolRefExpression(lcontext, lcontext.Scope.DefineThisArg("this"));
                else
                    referenced[s] = new SymbolRefExpression(lcontext, lcontext.Scope.DefineLocal(s));
            }
            extra?.Invoke(lcontext);
            foreach(var e in toResolve) e.ResolveScope(lcontext);
            frame = lcontext.Scope.PopFunction();
        }

        public void Compile(FunctionBuilder parent, Action<FunctionBuilder, IReadOnlyDictionary<string, SymbolRefExpression>> body)
        {
            var bc = new FunctionBuilder(parent.Script);
            bc.PushSourceRef(sr);
            bc.LoopTracker.Loops.Push(new LoopBoundary());
            body(bc, referenced);
            bc.LoopTracker.Loops.Pop();
            bc.PopSourceRef();
            var proto = bc.GetProto(name, frame);
            proto.annotations = Array.Empty<Annotation>();
            proto.flags = flags;
            proto.upvalues = upvalues.ToArray();
            parent.Protos.Add(proto);
            var idx = parent.Protos.Count - 1;
            parent.Emit_Closure(idx);
        }

        SymbolRef IClosureBuilder.CreateUpvalue(BuildTimeScope scope, SymbolRef symbol)
        {
            for (int i = 0; i < upvalues.Count; i++)
            {
                if (upvalues[i].i_Name == symbol.i_Name)
                {
                    return SymbolRef.Upvalue(symbol.i_Name, i);
                }
            }
            upvalues.Add(symbol);
            return SymbolRef.Upvalue(symbol.i_Name, upvalues.Count - 1);
        }
    }
}