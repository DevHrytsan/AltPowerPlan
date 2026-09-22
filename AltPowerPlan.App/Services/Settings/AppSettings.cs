using AltPowerPlan.Models;
using System;
using System.Collections.Generic;
using System.Text;
using static AltPowerPlan.ViewModels.Pages.SettingsViewModel;

namespace AltPowerPlan.Services.Settings
{
    public class AppSettings
    {
        public ThemeChoice selectedTheme { get; set; } = ThemeChoice.System;
        public string AppLanguage { get; set; } = "system";
        public ScreenSide QuickMenuSide { get; set; } = ScreenSide.BottomRight;
        public HotkeyModel QuickMenuHotkey { get; set; } = HotkeyModel.Default;
        public bool ConfirmPlanDeletion { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        public bool StartMinimized { get; set; } = false;
        public bool MinimizeToTray { get; set; } = false;
        public bool AutoSwitchAcDc { get; set; } = false;
        public Guid? PreferredAcPlanGuid { get; set; }
        public Guid? PreferredDcPlanGuid { get; set; }
    }
}
