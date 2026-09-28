using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Elinor
{
    /// <summary>
    /// Application settings stored in %APPDATA%\Elinor\settings.json.
    /// Replaces Properties.Settings (user.config), which reset on every version bump.
    /// </summary>
    internal sealed class AppSettings
    {
        public string LogPath { get; set; } = "";
        public bool CheckForUpdates { get; set; }
        /// <summary>0 = off, 1 = sell price, -1 = buy price.</summary>
        public int AutoCopy { get; set; }
        public bool Pin { get; set; }
        public string SelectedProfile { get; set; } = "";
        public bool LegacyImportDone { get; set; }

        /// <summary>"System", "Light" or "Dark".</summary>
        public string Theme { get; set; } = ThemeManager.System;

        public double OverlayOpacity { get; set; } = 0.95;
        public double? OverlayLeft { get; set; }
        public double? OverlayTop { get; set; }
        /// <summary>Reopen in compact overlay mode if that's how the app was closed.</summary>
        public bool OverlayOpen { get; set; }

        internal static AppSettings Load()
        {
            try
            {
                return JsonFile.Read<AppSettings>(AppPaths.SettingsFile) ?? ImportLegacy() ?? new AppSettings();
            }
            catch (Exception ex)
            {
                Log.Warn("Could not read settings, using defaults", ex);
                return new AppSettings();
            }
        }

        /// <summary>
        /// First run after upgrading: pick up the newest user.config written by Elinor 1.12 and earlier
        /// (%LOCALAPPDATA%\&lt;company&gt;\Elinor.exe_Url_*\&lt;version&gt;\user.config).
        /// </summary>
        private static AppSettings? ImportLegacy()
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                FileInfo? newest = Directory.EnumerateDirectories(localAppData)
                    .SelectMany(company => SafeEnumerate(() => Directory.EnumerateDirectories(company, "Elinor.exe_*")))
                    .SelectMany(app => SafeEnumerate(() => Directory.EnumerateFiles(app, "user.config", SearchOption.AllDirectories)))
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();

                if (newest == null) return null;

                AppSettings settings = FromLegacyUserConfig(File.ReadAllText(newest.FullName));
                Log.Info("Imported settings from " + newest.FullName);
                return settings;
            }
            catch (Exception ex)
            {
                Log.Warn("Could not import legacy settings", ex);
                return null;
            }
        }

        // Some LocalAppData subfolders deny listing; skip them rather than abort the scan.
        private static string[] SafeEnumerate(Func<System.Collections.Generic.IEnumerable<string>> enumerate)
        {
            try
            {
                return enumerate().ToArray();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }

        internal static AppSettings FromLegacyUserConfig(string xml)
        {
            var settings = new AppSettings();

            foreach (XElement setting in XDocument.Parse(xml).Descendants("setting"))
            {
                string value = ((string?)setting.Element("value") ?? "").Trim();

                switch ((string?)setting.Attribute("name"))
                {
                    case "logpath": settings.LogPath = value; break;
                    case "selectedprofile": settings.SelectedProfile = value; break;
                    case "checkforupdates": settings.CheckForUpdates = bool.TryParse(value, out bool c) && c; break;
                    case "pin": settings.Pin = bool.TryParse(value, out bool p) && p; break;
                    case "autocopy":
                        settings.AutoCopy = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int a)
                            ? Math.Sign(a)
                            : 0;
                        break;
                }
            }

            return settings;
        }

        internal void Save()
        {
            try
            {
                JsonFile.WriteAtomic(AppPaths.SettingsFile, this);
            }
            catch (Exception ex)
            {
                Log.Error("Could not save settings", ex);
            }
        }
    }
}
