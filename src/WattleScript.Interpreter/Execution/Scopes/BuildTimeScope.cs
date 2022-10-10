using System.Collections.Generic;
using System.Linq;
using WattleScript.Interpreter.Execution.Scopes;
using WattleScript.Interpreter.Tree;
using WattleScript.Interpreter.Tree.Statements;

namespace WattleScript.Interpreter.Execution
{
	internal class BuildTimeScope
	{
		List<BuildTimeScopeFrame> m_Frames = new List<BuildTimeScopeFrame>();
		List<IClosureBuilder> m_ClosureBuilders = new List<IClosureBuilder>();

		public void PushFunction(IClosureBuilder closureBuilder, bool isConstructor = false)
		{
			m_ClosureBuilders.Add(closureBuilder);
			m_Frames.Add(new BuildTimeScopeFrame(isConstructor));
		}

		public bool InConstructor => m_Frames.Last().IsConstructor;

		public void SetHasVarArgs()
		{
			m_Frames.Last().HasVarArgs = true;
		}

		public void PushBlock()
		{
			m_Frames.Last().PushBlock();
		}

		public RuntimeScopeBlock PopBlock()
		{
			return m_Frames.Last().PopBlock();
		}

		public RuntimeScopeFrame PopFunction()
		{
			var last = m_Frames.Last();
			last.ResolveLRefs();
			m_Frames.RemoveAt(m_Frames.Count - 1);

			m_ClosureBuilders.RemoveAt(m_ClosureBuilders.Count - 1);

			return last.GetRuntimeFrameData();
		}


		public SymbolRef Find(string name)
		{
			SymbolRef local = m_Frames.Last().Find(name);

			if (local != null)
				return local;

			for (int i = m_Frames.Count - 2; i >= 0; i--)
			{
				SymbolRef symb = m_Frames[i].Find(name);

				if (symb != null)
				{
					symb = CreateUpValue(this, symb, i, m_Frames.Count - 2);
						
					if (symb != null)
						return symb;
				}
			}

			return CreateGlobalReference(name);
		}

		public SymbolRef CreateGlobalReference(string name)
		{
			if (name == WellKnownSymbols.ENV)
				throw new InternalErrorException("_ENV passed in CreateGlobalReference");

			SymbolRef env = Find(WellKnownSymbols.ENV);
			return SymbolRef.Global(name, env);
		}


		public void ForceEnvUpValue()
		{
			Find(WellKnownSymbols.ENV);
		}

		private SymbolRef CreateUpValue(BuildTimeScope buildTimeScope, SymbolRef symb, int closuredFrame, int currentFrame)
		{
			// it's a 0-level upvalue. Just create it and we're done.
			if (closuredFrame == currentFrame) {
				var uv = m_ClosureBuilders[currentFrame + 1].CreateUpvalue(this, symb);
				uv.IsBaseClass = symb.IsBaseClass;
				uv.IsThisArgument = symb.IsThisArgument;
				uv.Placeholder = symb.Placeholder;
				return uv;
			}
			else
			{

				SymbolRef upvalue = CreateUpValue(buildTimeScope, symb, closuredFrame, currentFrame - 1);
				var uv = m_ClosureBuilders[currentFrame + 1].CreateUpvalue(this, upvalue);
				uv.IsBaseClass = symb.IsBaseClass;
				uv.IsThisArgument = symb.IsThisArgument;
				uv.Placeholder = symb.Placeholder;
				return uv;
			}
		}

		public SymbolRef DefineLocal(string name)
		{
			return m_Frames.Last().DefineLocal(name);
		}

		public SymbolRef DefineBaseRef()
		{
			var retVal = DefineLocal("base");
			retVal.IsBaseClass = true;
			return retVal;
		}

		public SymbolRef DefineThisArg(string name)
		{
			var retVal = DefineLocal(name);
			retVal.IsThisArgument = true;
			return retVal;
		}
		
		//Defines a placeholder symbol for base that will error if used
		public SymbolRef DefineBaseEmpty()
		{
			var retVal = DefineLocal("base");
			retVal.IsBaseClass = true;
			retVal.Placeholder = true;
			return retVal;
		}
		
		

		public SymbolRef TryDefineLocal(string name, out SymbolRef oldLocal)
		{
			return m_Frames.Last().TryDefineLocal(name, out oldLocal);
		}

		public void TemporaryScope(Dictionary<string, SymbolRef> locals)
		{
			m_Frames.Last().TemporaryScope(locals);
		}
		
		public void ResetTemporaryScope() => m_Frames.Last().ResetTemporaryScope();


		public bool CurrentFunctionHasVarArgs()
		{
			return m_Frames.Last().HasVarArgs;
		}

		internal void DefineLabel(LabelStatement label)
		{
			m_Frames.Last().DefineLabel(label);
		}

		internal void RegisterGoto(GotoStatement gotostat)
		{
			m_Frames.Last().RegisterGoto(gotostat);
		}

	}
}
