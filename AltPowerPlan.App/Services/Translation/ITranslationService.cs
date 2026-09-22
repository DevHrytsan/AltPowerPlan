using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AltPowerPlan.Services.Translation
{
    public interface ITranslationService
    {
        void ApplySavedLanguageOnStartup();
        string this[string key] { get; }
        string GetString(string key);
        void SetLanguage(string languageCode);
        string GetSelectedLanguageCode();
        IEnumerable<CultureInfo> GetAvailableLanguages();

        event EventHandler? LanguageChanged;
    }
}
