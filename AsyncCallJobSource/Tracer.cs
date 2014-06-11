using System.ComponentModel;
using System.Diagnostics;

namespace Nivot.PowerShell.Async
{
    [Localizable(false)]
    internal static class Tracer
    {
        private static readonly ITracer Impl = new TracerImpl("PSAsyncCall");

        /// <summary>
        /// 
        /// </summary>
        /// <param name="format"></param>
        /// <param name="parameters"></param>
        [Conditional("DEBUG")]
        [JetBrains.Annotations.StringFormatMethod("format")]
        internal static void LogVerbose(string format, params object[] parameters)
        {
            Impl.LogMessage(TraceLevel.Verbose, format, parameters);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="format"></param>
        /// <param name="parameters"></param>
        [JetBrains.Annotations.StringFormatMethod("format")]
        internal static void LogError(string format, params object[] parameters)
        {
            Impl.LogMessage(TraceLevel.Error, "* " + format, parameters);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="format"></param>
        /// <param name="parameters"></param>
        [JetBrains.Annotations.StringFormatMethod("format")]
        internal static void LogWarning(string format, params object[] parameters)
        {
            Impl.LogMessage(TraceLevel.Warning, "! " + format, parameters);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="format"></param>
        /// <param name="parameters"></param>
        [Conditional("DEBUG")]
        [JetBrains.Annotations.StringFormatMethod("format")]
        internal static void LogInfo(string format, params object[] parameters)
        {
            Impl.LogMessage(TraceLevel.Info, format, parameters);
        }
    }
}