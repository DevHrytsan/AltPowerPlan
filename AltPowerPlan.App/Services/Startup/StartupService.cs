using Microsoft.Win32;
using System;

namespace AltPowerPlan.Services.Startup
{
    public class StartupService : IStartupService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "AltPowerPlan";

        public bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                return key?.GetValue(AppName) is string;
            }
            catch
            {
                return false;
            }
        }

        public bool SetStartup(bool enable, bool startMinimized = false)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                if (key == null) return false;

                if (enable)
                {
                    string? exePath = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(exePath)) return false;

                    string command = startMinimized
                        ? $"\"{exePath}\" --minimized"
                        : $"\"{exePath}\"";

                    key.SetValue(AppName, command, RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
