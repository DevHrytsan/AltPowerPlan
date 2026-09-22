using System;
using System.IO;

namespace AltPowerPlan.Pipeline.Utils
{
    public static class ToolLocator
    {
        public static string? FindExecutable(string exeName)
        {
            // 1. Check in PATH environment variable
            var pathVar = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathVar))
            {
                foreach (var folder in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                {
                    try
                    {
                        var candidate = Path.Combine(folder.Trim('\"'), exeName);
                        if (File.Exists(candidate)) return candidate;
                    }
                    catch { }
                }
            }
            return null;
        }

        public static string? FindInnoSetupCompiler()
        {
            var onPath = FindExecutable("iscc.exe");
            if (onPath != null) return onPath;

            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            var candidates = new[]
            {
                Path.Combine(programFilesX86, "Inno Setup 6", "ISCC.exe"),
                Path.Combine(programFiles, "Inno Setup 6", "ISCC.exe"),
                @"C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
                @"C:\Program Files\Inno Setup 6\ISCC.exe"
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }

            return null;
        }
    }
}

