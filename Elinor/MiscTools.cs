using System;
using System.Diagnostics;

namespace Elinor
{
    internal static class MiscTools
    {
        /// <summary>Opens a URL in the default browser (.NET Core+ needs UseShellExecute).</summary>
        internal static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Warn("Could not open " + url, ex);
            }
        }
    }
}
