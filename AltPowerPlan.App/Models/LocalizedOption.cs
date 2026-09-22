using AltPowerPlan.Services.Translation;
using System;
using System.Collections.Generic;
using System.Text;

namespace AltPowerPlan.Models
{
    public partial class LocalizedOption<T> : ObservableObject
    {
        private readonly ITranslationService _translator;
        private readonly string _nativeFallback;

        public T Value { get; }
        public string TranslationKey { get; }
        public string DisplayName => string.IsNullOrEmpty(TranslationKey)
            ? _nativeFallback
            : _translator.GetString(TranslationKey);

        public LocalizedOption(T value, string translationKey, ITranslationService translator, string nativeFallback = "")
        {
            Value = value;
            TranslationKey = translationKey;
            _translator = translator;
            _nativeFallback = nativeFallback;

            _translator.LanguageChanged += (s, e) =>
            {
                if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
                {
                    dispatcher.BeginInvoke(() => OnPropertyChanged(nameof(DisplayName)));
                }
                else
                {
                    OnPropertyChanged(nameof(DisplayName));
                }
            };
        }

        public override string ToString() => DisplayName;
    }
}
