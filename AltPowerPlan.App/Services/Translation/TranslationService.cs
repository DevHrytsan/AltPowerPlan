using AltPowerPlan.Services.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using WPFLocalizeExtension.Engine;
using WPFLocalizeExtension.Extensions;

namespace AltPowerPlan.Services.Translation
{
    internal partial class TranslationService : ITranslationService
    {
        public event EventHandler? LanguageChanged;

        private readonly string _assemblyName;
        private readonly string _dictionaryName = "Strings"; // Your .resx file name

        private readonly IAppSettingsProvider _appSettingsProvider;
        public TranslationService(IAppSettingsProvider appSettingsProvider)
        {
            _appSettingsProvider = appSettingsProvider;

            _assemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? "AltPowerPlan";
            LocalizeDictionary.Instance.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(LocalizeDictionary.Culture))
                {
                    LanguageChanged?.Invoke(this, EventArgs.Empty);
                }
            };

        }
        public void ApplySavedLanguageOnStartup()
        {
            var config = _appSettingsProvider.Settings;

            SetLanguage(config.AppLanguage);
        }

        public string this[string key] => GetString(key);

        public void SetLanguage(string languageCode)
        {
            if (!string.Equals(_appSettingsProvider.Settings.AppLanguage, languageCode, StringComparison.OrdinalIgnoreCase))
            {
                _appSettingsProvider.Settings.AppLanguage = languageCode;
                _appSettingsProvider.Save();
            }

            CultureInfo targetCulture;

            if (string.IsNullOrWhiteSpace(languageCode) || languageCode.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                targetCulture = MatchSupportedCulture(GetDeviceLanguage());
            }
            else
            {
                try
                {
                    targetCulture = new CultureInfo(languageCode);
                }
                catch
                {
                    targetCulture = MatchSupportedCulture(GetDeviceLanguage());
                }
            }

            CultureInfo.CurrentUICulture = targetCulture;
            CultureInfo.DefaultThreadCurrentUICulture = targetCulture;

            if (Equals(LocalizeDictionary.Instance.Culture, targetCulture))
            {
                LanguageChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                LocalizeDictionary.Instance.Culture = targetCulture;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern ushort GetUserDefaultUILanguage();

        public static CultureInfo GetDeviceLanguage()
        {
            try
            {
                ushort lcid = GetUserDefaultUILanguage();
                if (lcid != 0)
                {
                    return CultureInfo.GetCultureInfo(lcid);
                }
            }
            catch
            {
                // Fallback to thread UI culture
            }

            return CultureInfo.CurrentUICulture;
        }

        private static CultureInfo MatchSupportedCulture(CultureInfo culture)
        {
            if (culture.TwoLetterISOLanguageName.Equals("uk", StringComparison.OrdinalIgnoreCase))
            {
                return new CultureInfo("uk");
            }

            return new CultureInfo("en");
        }

        public string GetSelectedLanguageCode()
        {
            var lang = _appSettingsProvider.Settings.AppLanguage;
            if (string.IsNullOrWhiteSpace(lang))
                return "system";

            if (lang.Equals("system", StringComparison.OrdinalIgnoreCase))
                return "system";

            var available = GetAvailableLanguages();
            if (available.Any(c => c.Name.Equals(lang, StringComparison.OrdinalIgnoreCase)))
                return lang;

            var parent = lang.Split('-')[0];
            if (available.Any(c => c.Name.Equals(parent, StringComparison.OrdinalIgnoreCase)))
                return parent;

            return "system";
        }

        public IEnumerable<CultureInfo> GetAvailableLanguages()
        {
            var availableCultures = LocalizeDictionary.Instance.MergedAvailableCultures;

            return availableCultures
                .Select(culture => string.IsNullOrEmpty(culture.Name) ? new CultureInfo("en") : culture)
                .DistinctBy(culture => culture.Name)
                .OrderBy(culture => culture.NativeName);
        }

        public string GetString(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;

            string formattedKey = $"{_assemblyName}:{_dictionaryName}:{key}";
            string translated = LocExtension.GetLocalizedValue<string>(formattedKey);

            return string.IsNullOrEmpty(translated) ? $"[{key}]" : translated;
        }

     
    }

}

