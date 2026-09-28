using System;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace Elinor
{
    internal static class Updates
    {
        // Bump <version> in Elinor/currentVersion.xml on master when publishing a release.
        private const string VersionUrl =
            "https://raw.githubusercontent.com/dsipal/elinor-re-reloaded/master/Elinor/currentVersion.xml";

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        /// <summary>
        /// Safe in single-file publishes, unlike FileVersionInfo on Assembly.Location
        /// (Location is empty there).
        /// </summary>
        internal static Version CurrentVersion =>
            Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);

        /// <summary>
        /// Checks for a newer version. Never throws. When <paramref name="manual"/> is false
        /// (startup check) only a newer version is reported; failures are just logged.
        /// </summary>
        internal static async Task CheckForUpdatesAsync(Window owner, bool manual = false)
        {
            try
            {
                string xml = await Http.GetStringAsync(VersionUrl);
                XElement root = XDocument.Parse(xml).Root ?? throw new FormatException("Empty version file");

                var newVersion = new Version((string?)root.Element("version") ?? "0.0");
                string? url = (string?)root.Element("url");

                if (newVersion > CurrentVersion && !string.IsNullOrEmpty(url) &&
                    MessageBox.Show(owner,
                        "There's a new version of Elinor available, do you want to download it?",
                        "New version available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    MiscTools.OpenUrl(url);
                }
                else if (manual && newVersion <= CurrentVersion)
                {
                    MessageBox.Show(owner, "You're running the latest version (" + CurrentVersion.ToString(3) + ").",
                        "Elinor", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Log.Info("Update check failed: " + ex.Message);
                if (manual)
                    MessageBox.Show(owner, "Could not check for updates:\n" + ex.Message,
                        "Elinor", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
