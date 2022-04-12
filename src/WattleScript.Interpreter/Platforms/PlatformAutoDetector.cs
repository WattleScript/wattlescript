using System;
using System.Linq;
using WattleScript.Interpreter.Interop;
using WattleScript.Interpreter.Loaders;

namespace WattleScript.Interpreter.Platforms
{
	/// <summary>
	/// A static class offering properties for autodetection of system/platform details
	/// </summary>
	public static class PlatformAutoDetector
	{
		private static bool? m_IsRunningOnAOT = null;

		private static bool m_AutoDetectionsDone = false;

		/// <summary>
		/// Gets a value indicating whether this instance is running on mono.
		/// </summary>
		public static bool IsRunningOnMono { get; private set; }

		/// <summary>
		/// Gets a value indicating whether this instance is running on a CLR4 compatible implementation
		/// </summary>
		public static bool IsRunningOnClr4 { get; private set; } = true;
		/// <summary>
		/// Gets a value indicating whether this instance is running on Unity-3D
		/// </summary>
		public static bool IsRunningOnUnity { get; private set; }

		/// <summary>
		/// Gets a value indicating whether this instance has been built as a Portable Class Library
		/// </summary>
		public static bool IsPortableFramework { get; private set; } = false;
		/// <summary>
		/// Gets a value indicating whether this instance has been compiled natively in Unity (as opposite to importing a DLL).
		/// </summary>
		public static bool IsUnityNative { get; private set; }


		/// <summary>
		/// Gets a value indicating whether this instance is running a system using Ahead-Of-Time compilation 
		/// and not supporting JIT.
		/// </summary>
		public static bool IsRunningOnAOT
		{
			// We do a lazy eval here, so we can wire out this code by not calling it, if necessary..
			get
			{

				if (!m_IsRunningOnAOT.HasValue)
				{
					try
					{
						System.Linq.Expressions.Expression e = System.Linq.Expressions.Expression.Constant(5, typeof(int));
						var lambda = System.Linq.Expressions.Expression.Lambda<Func<int>>(e);
						lambda.Compile();
						m_IsRunningOnAOT = false;
					}
					catch (Exception)
					{
						m_IsRunningOnAOT = true;
					}
				}

				return m_IsRunningOnAOT.Value;
			}
		}

		private static void AutoDetectPlatformFlags()
		{
			if (m_AutoDetectionsDone)
				return;

			IsRunningOnUnity = AppDomain.CurrentDomain
				.GetAssemblies()
				.SelectMany(a => a.SafeGetTypes())
				.Any(t => t.FullName.StartsWith("UnityEngine."));

			IsRunningOnMono = (Type.GetType("Mono.Runtime") != null);
			
			
			IsRunningOnClr4 = true;
			
			m_AutoDetectionsDone = true;
		}



		internal static IPlatformAccessor GetDefaultPlatform()
		{
			AutoDetectPlatformFlags();
			
			if (IsRunningOnUnity)
				return new LimitedPlatformAccessor();
			
			return new StandardPlatformAccessor();
		}

		internal static IScriptLoader GetDefaultScriptLoader()
		{
			AutoDetectPlatformFlags();

			if (IsRunningOnUnity)
				return new UnityAssetsScriptLoader();
			else
			{
				return new FileSystemScriptLoader();
			}
		}
	}
}
