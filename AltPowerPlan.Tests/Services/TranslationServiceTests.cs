using AltPowerPlan.Services.Settings;
using AltPowerPlan.Services.Translation;
using FluentAssertions;
using NSubstitute;
using System.Globalization;
using Xunit;

namespace AltPowerPlan.Tests.Services
{
    public class TranslationServiceTests
    {
        private readonly IAppSettingsProvider _settingsProvider = Substitute.For<IAppSettingsProvider>();
        private readonly AppSettings _settings = new();

        public TranslationServiceTests()
        {
            _settingsProvider.Settings.Returns(_settings);
        }

        [Fact]
        public void GetDeviceLanguage_ReturnsValidCulture()
        {
            var culture = TranslationService.GetDeviceLanguage();

            culture.Should().NotBeNull();
            culture.Name.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void SetLanguage_WhenSystem_SetsSupportedCulture()
        {
            var service = new TranslationService(_settingsProvider);

            service.SetLanguage("system");

            var currentCulture = CultureInfo.CurrentUICulture;
            currentCulture.TwoLetterISOLanguageName.Should().Match(lang => lang == "en" || lang == "uk");
        }

        [Theory]
        [InlineData("uk", "uk")]
        [InlineData("en", "en")]
        public void SetLanguage_WhenExplicitSupportedCode_SetsExactCulture(string input, string expected)
        {
            var service = new TranslationService(_settingsProvider);

            service.SetLanguage(input);

            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Should().Be(expected);
            _settings.AppLanguage.Should().Be(input);
        }

        [Fact]
        public void GetSelectedLanguageCode_WhenSettingsIsSystem_ReturnsSystem()
        {
            _settings.AppLanguage = "system";
            var service = new TranslationService(_settingsProvider);

            service.GetSelectedLanguageCode().Should().Be("system");
        }
    }
}
