using System;
using System.IO;
using System.Linq;

namespace Elinor
{
    internal static class CacheTools
    {
        internal static void ClearMarketLogs(DirectoryInfo logdir)
        {
            logdir.Refresh();
            if (!logdir.Exists) return;

            foreach (FileInfo fi in logdir.EnumerateFiles("*.txt"))
            {
                try
                {
                    fi.Delete();
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    // In use (EVE may be writing it); leave it for next time.
                }
            }
        }

        /// <summary>Total size in bytes of the market logs, or 0 if the folder is missing or unreadable.</summary>
        internal static long MarketLogsSize(DirectoryInfo logdir)
        {
            try
            {
                logdir.Refresh();
                return logdir.Exists ? logdir.EnumerateFiles().Sum(fi => fi.Length) : 0;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return 0;
            }
        }
    }
}
