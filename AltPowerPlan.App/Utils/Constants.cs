using System;
using System.IO;

namespace AltPowerPlan.Utils
{
    internal static class Constants
    {
        public const string AppName = "AltPowerPlan";

        public static readonly string ProgramDirectory = AppContext.BaseDirectory;

        public static readonly string UserDataDirectory = ResolveUserDataDirectory();

        /// <summary>
        /// Backward-compatible alias pointing to the writable user data directory.
        /// </summary>
        public static readonly string DirectoryName = UserDataDirectory;

        public static readonly string LanguageResourceName = ProgramDirectory;

        private static string ResolveUserDataDirectory()
        {
            // 1. Portable mode: if a writable config.json or portable.dat exists next to the .exe
            try
            {
                string localConfig = Path.Combine(ProgramDirectory, "config.json");
                string portableMarker = Path.Combine(ProgramDirectory, "portable.dat");
                if ((File.Exists(localConfig) || File.Exists(portableMarker)) && IsDirectoryWritable(ProgramDirectory))
                {
                    return ProgramDirectory;
                }
            }
            catch
            {
                // Fall through to standard directories
            }

            // 2. Standard Windows %LocalAppData%\AltPowerPlan directory
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrWhiteSpace(localAppData))
                {
                    return Path.Combine(localAppData, AppName);
                }
            }
            catch
            {
                // Fall through
            }

            // 3. Fallback to Roaming %AppData%\AltPowerPlan directory
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (!string.IsNullOrWhiteSpace(appData))
                {
                    return Path.Combine(appData, AppName);
                }
            }
            catch
            {
                // Fall through
            }

            // 4. Last-resort fallback to user temporary directory
            return Path.Combine(Path.GetTempPath(), AppName);
        }

        private static bool IsDirectoryWritable(string path)
        {
            try
            {
                string testFile = Path.Combine(path, $".test_write_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(testFile, string.Empty);
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
