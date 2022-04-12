using System.IO;

namespace WattleScript.Interpreter.CoreLib.IO
{
	/// <summary>
	/// Abstract class implementing an unclosable file Lua userdata. Methods are meant to be called by Lua code.
	/// </summary>
	internal class StandardIOFileUserDataBase : StreamFileUserDataBase
	{
		protected override string Close()
		{
			return ("cannot close standard file");
		}

		public static StandardIOFileUserDataBase CreateInputStream(Stream stream)
		{
			var f = new StandardIOFileUserDataBase();
			f.Initialize(stream, new StreamReader(stream), null);
			return f;
		}

		public static StandardIOFileUserDataBase CreateOutputStream(Stream stream)
		{
			var f = new StandardIOFileUserDataBase();
			f.Initialize(stream, null, new StreamWriter(stream) { AutoFlush = true });
			return f;
		}

	}

}
