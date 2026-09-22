using AltPowerPlan.Utils;
using System;
using System.IO;
using System.Text.Json;

namespace AltPowerPlan.Services.Settings
{
    internal class JsonConfigHandler<T> where T : new()
    {
        private const string DEFAULT_SAVE_NAME = "config.json";

        private static readonly string _saveDirectory = Constants.UserDataDirectory;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true
        };

        public void Save(string fileName = DEFAULT_SAVE_NAME)
        {
            if (this is T typedSettings)
            {
                Save(typedSettings, fileName);
            }
            else
            {
                SaveStatic(this, fileName, _saveDirectory);
            }
        }

        public static bool Save(T settings, string fileName = DEFAULT_SAVE_NAME, string? directoryPath = null)
        {
            return SaveStatic(settings, fileName, directoryPath ?? _saveDirectory);
        }

        private static bool SaveStatic(object? settings, string fileName, string targetDir)
        {
            settings ??= new T();
            try
            {
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }
            }
            catch
            {
                // If requested directory cannot be created (e.g. read-only installed directory),
                // fallback to user data directory or temp
                try
                {
                    targetDir = Constants.UserDataDirectory;
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }
                }
                catch
                {
                    targetDir = Path.Combine(Path.GetTempPath(), Constants.AppName);
                    try
                    {
                        Directory.CreateDirectory(targetDir);
                    }
                    catch
                    {
                        return false;
                    }
                }
            }

            string filePath = Path.Combine(targetDir, fileName);
            string tempFilePath = Path.Combine(targetDir, $"{fileName}.{Guid.NewGuid():N}.tmp");

            try
            {
                string json = JsonSerializer.Serialize(settings, _jsonOptions);
                File.WriteAllText(tempFilePath, json);

                File.Move(tempFilePath, filePath, overwrite: true);
                return true;
            }
            catch
            {
                // Fallback attempt: write directly
                try
                {
                    string json = JsonSerializer.Serialize(settings, _jsonOptions);
                    File.WriteAllText(filePath, json);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            finally
            {
                // Clean up transient temp file if left behind
                try
                {
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                }
                catch { }
            }
        }

        public static T Load(string fileName = DEFAULT_SAVE_NAME, string? directoryPath = null)
        {
            string targetDir = directoryPath ?? _saveDirectory;
            string filePath = Path.Combine(targetDir, fileName);

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<T>(json, _jsonOptions) ?? new T();
                }
                catch
                {
                    return new T();
                }
            }

            // Fallback: Check if config exists in legacy directories (e.g. Roaming AppData, or previous install / portable run)
            if (directoryPath == null)
            {
                try
                {
                    string roamingDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Constants.AppName);
                    string roamingFile = Path.Combine(roamingDir, fileName);
                    if (File.Exists(roamingFile))
                    {
                        string json = File.ReadAllText(roamingFile);
                        var result = JsonSerializer.Deserialize<T>(json, _jsonOptions);
                        if (result != null)
                        {
                            Save(result, fileName, targetDir);
                            return result;
                        }
                    }

                    string legacyAppDir = Constants.ProgramDirectory;
                    string legacyFilePath = Path.Combine(legacyAppDir, fileName);
                    if (File.Exists(legacyFilePath))
                    {
                        string json = File.ReadAllText(legacyFilePath);
                        var result = JsonSerializer.Deserialize<T>(json, _jsonOptions);
                        if (result != null)
                        {
                            // Automatically migrate settings to the writable user data directory
                            Save(result, fileName, targetDir);
                            return result;
                        }
                    }
                }
                catch { }
            }

            return new T();
        }
    }
}
