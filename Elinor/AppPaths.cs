using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Elinor
{
    /// <summary>
    /// Every location Elinor reads or writes. Nothing is relative to the working
    /// directory, so the app works when installed under Program Files.
    /// </summary>
    internal static class AppPaths
    {
        internal static string DataDir { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Elinor");

        internal static string ProfilesDir => Path.Combine(DataDir, "profiles");

        internal static string SettingsFile => Path.Combine(DataDir, "settings.json");

        internal static string LogsDir { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Elinor", "logs");

        /// <summary>
        /// Possible EVE market log folders, most likely first. MyDocuments follows
        /// OneDrive folder redirection, which is on by default on Windows 11.
        /// </summary>
        internal static IEnumerable<string> MarketLogDirCandidates()
        {
            var documents = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents"),
            };

            string? oneDrive = Environment.GetEnvironmentVariable("OneDrive");
            if (!string.IsNullOrEmpty(oneDrive))
                documents.Add(Path.Combine(oneDrive, "Documents"));

            return documents
                .Where(d => !string.IsNullOrEmpty(d))
                .Select(d => Path.Combine(d, "EVE", "logs", "Marketlogs"))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        internal static string DefaultMarketLogDir()
        {
            List<string> candidates = MarketLogDirCandidates().ToList();
            return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
        }

        /// <summary>Folders where Elinor 1.12 and earlier kept "profiles\*.dat".</summary>
        internal static IEnumerable<string> LegacyProfileDirs()
        {
            var roots = new List<string> { AppContext.BaseDirectory, Environment.CurrentDirectory };

            foreach (var programFiles in new[]
                     {
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     })
            {
                if (!string.IsNullOrEmpty(programFiles))
                    roots.Add(Path.Combine(programFiles, "Elinor reloaded"));
            }

            return roots
                .Select(r => Path.Combine(r, "profiles"))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }
    }
}
