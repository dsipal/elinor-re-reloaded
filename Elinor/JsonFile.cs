using System.IO;
using System.Text.Json;

namespace Elinor
{
    internal static class JsonFile
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { WriteIndented = true };

        internal static T? Read<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            using FileStream stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<T>(stream, Options);
        }

        /// <summary>
        /// Writes to a temp file and then swaps it in, so a crash or power loss mid-save
        /// can't leave a truncated file behind.
        /// </summary>
        internal static void WriteAtomic<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            string tmp = path + ".tmp";

            using (FileStream stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, value, Options);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tmp, path, overwrite: true);
        }
    }
}
