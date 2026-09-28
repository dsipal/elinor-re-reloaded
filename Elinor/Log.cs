using System;
using System.Globalization;
using System.IO;

namespace Elinor
{
    /// <summary>
    /// Minimal daily-rolling file log in %LOCALAPPDATA%\Elinor\logs. Never throws.
    /// </summary>
    internal static class Log
    {
        private static readonly object Sync = new object();

        /// <summary>Overridable so tests don't write into the real log.</summary>
        internal static string LogsDir { get; set; } = AppPaths.LogsDir;

        internal static string CurrentFile =>
            Path.Combine(LogsDir, string.Format(CultureInfo.InvariantCulture, "elinor-{0:yyyyMMdd}.log", DateTime.Now));

        internal static void Info(string message) => Write("INFO", message, null);

        internal static void Warn(string message, Exception? ex = null) => Write("WARN", message, ex);

        internal static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

        private static void Write(string level, string message, Exception? ex)
        {
            try
            {
                string line = string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] {2}{3}{4}",
                    DateTime.Now, level, message,
                    ex == null ? "" : Environment.NewLine, ex?.ToString() ?? "");

                lock (Sync)
                {
                    Directory.CreateDirectory(LogsDir);
                    File.AppendAllText(CurrentFile, line + Environment.NewLine);
                }
            }
            catch
            {
                // Logging must never be the reason the app goes down.
            }
        }
    }
}
