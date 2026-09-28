using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Elinor
{
    /// <summary>
    /// Profiles stored as one JSON file each. The "Default" profile is never saved.
    /// </summary>
    internal sealed class ProfileStore
    {
        private readonly string _dir;

        internal ProfileStore(string dir)
        {
            _dir = dir;
        }

        internal string PathFor(string profileName) => Path.Combine(_dir, profileName + ".json");

        internal bool Exists(string profileName) => File.Exists(PathFor(profileName));

        internal List<Profile> LoadAll()
        {
            var profiles = new List<Profile>();
            if (!Directory.Exists(_dir)) return profiles;

            foreach (string file in Directory.EnumerateFiles(_dir, "*.json"))
            {
                try
                {
                    Profile? profile = JsonFile.Read<Profile>(file);
                    if (profile == null) continue;

                    // The file name is the source of truth for the name.
                    profile.profileName = Path.GetFileNameWithoutExtension(file);
                    if (profile.profileName != Profile.DefaultName) profiles.Add(profile);
                }
                catch (Exception ex)
                {
                    Log.Warn("Skipping unreadable profile " + file, ex);
                }
            }

            return profiles.OrderBy(p => p.profileName, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        internal bool Save(Profile profile)
        {
            if (profile.profileName == Profile.DefaultName) return true;

            try
            {
                JsonFile.WriteAtomic(PathFor(profile.profileName), profile);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Could not save profile " + profile.profileName, ex);
                return false;
            }
        }

        internal void Delete(string profileName)
        {
            try
            {
                File.Delete(PathFor(profileName));
            }
            catch (Exception ex)
            {
                Log.Error("Could not delete profile " + profileName, ex);
            }
        }
    }
}
