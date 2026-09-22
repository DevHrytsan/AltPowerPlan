using AltPowerPlan.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AltPowerPlan.Services.Themes
{
    public interface IAppThemeService
    {
        void ApplySavedThemeOnStartup();
        ThemeChoice GetTheme();
        void ApplyTheme(ThemeChoice themeChoice);
    }
}
