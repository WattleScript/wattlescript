using System;
using System.Text;
using MoonSharp.Interpreter.Diagnostics.PerformanceCounters;

namespace MoonSharp.Interpreter.Diagnostics
{
	/// <summary>
	/// A single object of this type exists for every script and gives access to performance statistics.
	/// </summary>
	public class PerformanceStatistics
	{
		IPerformanceStopwatch[] m_Stopwatches = new IPerformanceStopwatch[(int)PerformanceCounter.LastValue];
		static IPerformanceStopwatch[] m_GlobalStopwatches = new IPerformanceStopwatch[(int)PerformanceCounter.LastValue];
		private bool m_Enabled;


		/// <summary>
		/// Gets or sets a value indicating whether this collection of performance stats is enabled.
		/// </summary>
		/// <value>
		///   <c>true</c> if enabled; otherwise, <c>false</c>.
		/// </value>
		public bool Enabled
		{
			get => m_Enabled;
			set
			{
				switch (value)
				{
					case true when !m_Enabled:
					{
						m_GlobalStopwatches[(int) PerformanceCounter.AdaptersCompilation] ??= new GlobalPerformanceStopwatch(PerformanceCounter.AdaptersCompilation);

						for (int i = 0; i < (int)PerformanceCounter.LastValue; i++)
							m_Stopwatches[i] = m_GlobalStopwatches[i] ?? new PerformanceStopwatch((PerformanceCounter)i);
						break;
					}
					case false when m_Enabled:
						m_Stopwatches = new IPerformanceStopwatch[(int)PerformanceCounter.LastValue];
						m_GlobalStopwatches = new IPerformanceStopwatch[(int)PerformanceCounter.LastValue];
						break;
				}

				m_Enabled = value;
			}
		}
		
		/// <summary>
		/// Gets the result of the specified performance counter .
		/// </summary>
		/// <param name="pc">The PerformanceCounter.</param>
		/// <returns></returns>
		public PerformanceResult GetPerformanceCounterResult(PerformanceCounter pc)
		{
			var pco = m_Stopwatches[(int)pc];
			return pco?.GetResult();
		}

		/// <summary>
		/// Starts a stopwatch.
		/// </summary>
		/// <returns></returns>
		internal IDisposable StartStopwatch(PerformanceCounter pc)
		{
			var pco = m_Stopwatches[(int)pc];
			return pco?.Start();
		}

		/// <summary>
		/// Starts a stopwatch.
		/// </summary>
		/// <returns></returns>
		internal static IDisposable StartGlobalStopwatch(PerformanceCounter pc)
		{
			var pco = m_GlobalStopwatches[(int)pc];
			return pco?.Start();
		}

		/// <summary>
		/// Gets a string with a complete performance log.
		/// </summary>
		/// <returns></returns>
		public string GetPerformanceLog()
		{
			StringBuilder sb = new StringBuilder();

			for (int i = 0; i < (int)PerformanceCounter.LastValue; i++)
			{
				var res = GetPerformanceCounterResult((PerformanceCounter)i);
				if (res != null)
					sb.AppendLine(res.ToString());
			}

			return sb.ToString();
		}
	}
}
