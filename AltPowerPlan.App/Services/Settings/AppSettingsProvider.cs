using System;
using System.Collections.Generic;
using System.Text;

namespace AltPowerPlan.Services.Settings
{
    internal class AppSettingsProvider : IAppSettingsProvider
    {
        public AppSettings Settings { get; private set; } = new();
        public AppSettingsProvider() { Load(); }

        public void Load()
        {
            Settings = JsonConfigHandler<AppSettings>.Load();
        }

        public void Save()
        {
           JsonConfigHandler<AppSettings>.Save(Settings);
        }
    }
}

