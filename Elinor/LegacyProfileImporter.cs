using System;
using System.Collections.Generic;
using System.Formats.Nrbf;
using System.IO;

namespace Elinor
{
    /// <summary>
    /// Imports "profiles\*.dat" files written by Elinor 1.12 and earlier with BinaryFormatter.
    /// They are decoded with NrbfDecoder, which only reads the record data and never
    /// instantiates types, so this is safe on .NET 9+ where BinaryFormatter is gone.
    /// </summary>
    internal static class LegacyProfileImporter
    {
        internal static Profile Read(Stream stream)
        {
            ClassRecord record = NrbfDecoder.DecodeClassRecord(stream);
            var profile = new Profile();

            // Member names are the keys Profile.GetObjectData used in 1.12.
            if (record.HasMember("profilename")) profile.profileName = record.GetString("profilename") ?? Profile.DefaultName;
            if (record.HasMember("marginthreshold")) profile.marginThreshold = record.GetDouble("marginthreshold");
            if (record.HasMember("minimumthreshold")) profile.minimumThreshold = record.GetDouble("minimumthreshold");
            if (record.HasMember("accounting")) profile.accounting = record.GetInt32("accounting");
            if (record.HasMember("brokerrelations")) profile.brokerRelations = record.GetInt32("brokerrelations");
            if (record.HasMember("factionstanding")) profile.factionStanding = record.GetDouble("factionstanding");
            if (record.HasMember("corpstanding")) profile.corpStanding = record.GetDouble("corpstanding");
            if (record.HasMember("useBuyCustomBroker")) profile.useBuyCustomBroker = record.GetBoolean("useBuyCustomBroker");
            if (record.HasMember("buyCustomBroker")) profile.buyCustomBroker = record.GetDouble("buyCustomBroker");
            if (record.HasMember("useSellCustomBroker")) profile.useSellCustomBroker = record.GetBoolean("useSellCustomBroker");
            if (record.HasMember("sellCustomBroker")) profile.sellCustomBroker = record.GetDouble("sellCustomBroker");
            if (record.HasMember("buyRange")) profile.buyRange = record.GetInt32("buyRange");
            if (record.HasMember("sellRange")) profile.sellRange = record.GetInt32("sellRange");

            return profile;
        }

        /// <summary>Returns how many profiles were imported. Existing JSON profiles are never overwritten.</summary>
        internal static int ImportAll(IEnumerable<string> legacyDirs, ProfileStore store)
        {
            int imported = 0;

            foreach (string dir in legacyDirs)
            {
                if (!Directory.Exists(dir)) continue;

                foreach (string file in Directory.EnumerateFiles(dir, "*.dat"))
                {
                    try
                    {
                        Profile profile;
                        using (FileStream stream = File.OpenRead(file))
                            profile = Read(stream);

                        // Older versions named the file after the profile; trust the file name.
                        profile.profileName = Path.GetFileNameWithoutExtension(file);

                        if (profile.profileName == Profile.DefaultName || store.Exists(profile.profileName)) continue;

                        if (store.Save(profile))
                        {
                            imported++;
                            Log.Info("Imported legacy profile " + file);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("Could not import legacy profile " + file, ex);
                    }
                }
            }

            return imported;
        }
    }
}
