using AltPowerPlan.Models;
using AltPowerPlan.Services.Settings;
using System;
using System.Collections.Generic;
using System.Text;
using Wpf.Ui.Appearance;

namespace AltPowerPlan.Services.Themes
{
    internal class AppThemeService : IAppThemeService
    {
        private readonly IAppSettingsProvider _settingsProvider;

        public AppThemeService(IAppSettingsProvider settingsProvider)
        {
            _settingsProvider = settingsProvider;
        }

        public void ApplySavedThemeOnStartup()
        {
            var config = _settingsProvider.Settings;
            ApplyTheme(config.selectedTheme);
        }

        public void ApplyTheme(ThemeChoice value)
        {
            ApplicationTheme targetTheme = value switch
            {
                ThemeChoice.System => GetAppThemeFromSystem(),
                ThemeChoice.Light => ApplicationTheme.Light,
                ThemeChoice.Dark => ApplicationTheme.Dark,
                _ => ApplicationTheme.Light
            };

            _settingsProvider.Settings.selectedTheme = value;
            _settingsProvider.Save();

            ApplicationThemeManager.Apply(targetTheme);
        }

        public ThemeChoice GetTheme()
        {
            return _settingsProvider.Settings.selectedTheme;
        }

        private ApplicationTheme GetAppThemeFromSystem()
        {
            var systemTheme = ApplicationThemeManager.GetSystemTheme();

            return systemTheme switch
            {
                SystemTheme.Dark => ApplicationTheme.Dark,
                _ => ApplicationTheme.Light
            };
        }
    }
}
