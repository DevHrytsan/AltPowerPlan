using System;
using System.Collections.Generic;
using System.Text;

namespace AltPowerPlan.Services.Settings
{
    public interface IAppSettingsProvider
    {
        AppSettings Settings { get; }
        void Save();
        void Load();

    }
}
