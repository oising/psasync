using System;
using System.Diagnostics;
using System.Threading;

using JetBrains.Annotations;

namespace Nivot.PowerShell.Async
{
    /// <summary>
    /// Represents the contract for a tracer in the HQMA application.
    /// </summary>
    public interface ITracer
    {
        /// <summary>
        /// Report a diagnostic or informational message.
        /// </summary>
        /// <param name="level">The category of message to report.</param>
        /// <param name="format">A composite format string.</param>
        /// <param name="parameters">An array of parameters to use for the format string.</param>
        [StringFormatMethod("format")]
        void LogMessage(TraceLevel level, string format, params object[] parameters);
    }

    /// <summary>
    /// Represents the shared tracer implementation for all subsystems in the HQMA application.
    /// </summary>
    public sealed class TracerImpl : ITracer
    {
        private readonly string _tracerName;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The name of this tracer instance.</param>
        public TracerImpl([NotNull] string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException("name");
            }
            this._tracerName = name;
        }

        /// <summary>
        /// Report a diagnostic or informational message. This implementation sends all information to .NET Trace output.
        /// </summary>
        /// <param name="level">The category of message to report.</param>
        /// <param name="format">A composite format string.</param>
        /// <param name="parameters">An array of parameters to use for the format string.</param>
        [StringFormatMethod("format")]
        void ITracer.LogMessage(TraceLevel level, string format, params object[] parameters)
        {
            if (format == null)
            {
                throw new ArgumentNullException("format");
            }

            string formatWithThreadId = String.Format(
                "[{0:X4}][{1}] {2}",
                Thread.CurrentThread.ManagedThreadId,
                level,
                format);

            // 0: off
            // 1: error
            // 2: warning
            // 3: info
            // 4: verbose

            Trace.WriteLine(String.Format(formatWithThreadId, parameters), this._tracerName);
        }
    }
}
