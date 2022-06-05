﻿using System.Collections.Generic;
using System.Linq;
using WattleScript.Interpreter.Debugging;

namespace WattleScript.Interpreter
{
	public static class Extensions
	{
		public static string GetSourceCode(this SourceRef[] refs, string fullSourceCode)
		{
			if (refs == null)
			{
				return "";
			}
			
			SourceRef firstNotNull = refs.FirstOrDefault(x => x != null);
			SourceRef lastNotNull = refs.LastOrDefault(x => x != null);
			
			if (firstNotNull != null && lastNotNull == null)
			{
				return fullSourceCode.Substring(firstNotNull.FromCharIndex, firstNotNull.ToCharIndex - firstNotNull.FromCharIndex);
			}
			
			if (firstNotNull == null && lastNotNull != null)
			{
				return fullSourceCode.Substring(lastNotNull.FromCharIndex, lastNotNull.ToCharIndex - lastNotNull.FromCharIndex);
			}

			return fullSourceCode.Substring(firstNotNull?.FromCharIndex ?? 0, lastNotNull?.ToCharIndex - firstNotNull?.FromCharIndex ?? 0);
		}
	}
}
