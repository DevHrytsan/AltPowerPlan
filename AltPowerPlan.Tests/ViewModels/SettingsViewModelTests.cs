using AltPowerPlan.Models;
using AltPowerPlan.Services.Hotkey;
using AltPowerPlan.Services.Power;
using AltPowerPlan.Services.Settings;
using AltPowerPlan.Services.Startup;
using AltPowerPlan.Services.Themes;
using AltPowerPlan.Services.Translation;
using AltPowerPlan.ViewModels.Pages;
using FluentAssertions;
using NSubstitute;
using System;
using System.Collections.Generic;
using Wpf.Ui;
using Xunit;

namespace AltPowerPlan.Tests.ViewModels
{
    public class SettingsViewModelTests
    {
        private readonly IAppThemeService _themeService = Substitute.For<IAppThemeService>();
        private readonly ITranslationService _translationService = Substitute.For<ITranslationService>();
        private readonly IAppSettingsProvider _settingsProvider = Substitute.For<IAppSettingsProvider>();
        private readonly IHotkeyService _hotkeyService = Substitute.For<IHotkeyService>();
        private readonly IStartupService _startupService = Substitute.For<IStartupService>();
        private readonly IPowerPlanService _powerPlanService = Substitute.For<IPowerPlanService>();
        private readonly ISnackbarService _snackbarService = Substitute.For<ISnackbarService>();

        private readonly AppSettings _appSettings = new();
        private readonly List<PowerPlanModel> _mockPlans;

        public SettingsViewModelTests()
        {
            _settingsProvider.Settings.Returns(_appSettings);
            _translationService.GetString(Arg.Any<string>()).Returns(callInfo => callInfo.Arg<string>());
            _translationService.GetSelectedLanguageCode().Returns("en");
            _translationService.GetAvailableLanguages().Returns([]);

            _mockPlans = new List<PowerPlanModel>
            {
                new() { Id = PowerPlanService.BalancedGuid, Name = "Balanced", Type = PowerPlanType.Balanced, IsActive = true },
                new() { Id = PowerPlanService.HighPerformanceGuid, Name = "High Performance", Type = PowerPlanType.HighPerformance, IsActive = false },
                new() { Id = PowerPlanService.PowerSaverGuid, Name = "Power Saver", Type = PowerPlanType.PowerSaver, IsActive = false },
            };

            _powerPlanService.GetPowerPlans(Arg.Any<bool>()).Returns(_mockPlans);
        }

        private SettingsViewModel CreateViewModel()
        {
            return new SettingsViewModel(
                _themeService,
                _translationService,
                _settingsProvider,
                _hotkeyService,
                _startupService,
                _powerPlanService,
                _snackbarService);
        }

        [UIFact]
        public async Task LoadAvailablePlans_WhenCalledRepeatedly_PreservesSelectedAcAndDcPlans()
        {
            // Initial state: user has selected High Performance for AC and Power Saver for DC
            _appSettings.PreferredAcPlanGuid = PowerPlanService.HighPerformanceGuid;
            _appSettings.PreferredDcPlanGuid = PowerPlanService.PowerSaverGuid;

            var vm = CreateViewModel();
            await vm.OnNavigatedToAsync();

            vm.SelectedAcPlanGuid.Should().Be(PowerPlanService.HighPerformanceGuid);
            vm.SelectedDcPlanGuid.Should().Be(PowerPlanService.PowerSaverGuid);

            // User manually changes active plan to Power Saver on Dashboard
            _mockPlans[0].IsActive = false;
            _mockPlans[2].IsActive = true;

            // User navigates back to Settings
            await vm.OnNavigatedToAsync();

            // The preferred AC and DC plans MUST NOT get unselected or change to the newly active plan!
            vm.SelectedAcPlanGuid.Should().Be(PowerPlanService.HighPerformanceGuid);
            vm.SelectedDcPlanGuid.Should().Be(PowerPlanService.PowerSaverGuid);
            vm.AvailablePlans.Should().HaveCount(3);
        }

        [UIFact]
        public async Task LoadAvailablePlans_WhenInitialSettingsNull_PersistsSensibleDefaults()
        {
            // Initial state: unconfigured settings (null)
            _appSettings.PreferredAcPlanGuid = null;
            _appSettings.PreferredDcPlanGuid = null;

            var vm = CreateViewModel();
            await vm.OnNavigatedToAsync();

            // Must resolve and assign sensible defaults
            vm.SelectedAcPlanGuid.Should().NotBeNull();
            vm.SelectedAcPlanGuid.Should().NotBe(Guid.Empty);
            vm.SelectedDcPlanGuid.Should().NotBeNull();
            vm.SelectedDcPlanGuid.Should().NotBe(Guid.Empty);

            // Must immediately save to settings so future manual plan changes don't drift
            _appSettings.PreferredAcPlanGuid.Should().Be(vm.SelectedAcPlanGuid);
            _appSettings.PreferredDcPlanGuid.Should().Be(vm.SelectedDcPlanGuid);
            _settingsProvider.Received().Save();
        }

        [UIFact]
        public async Task OnSelectedAcPlanGuidChanged_WhenUserSelectsPlan_PersistsToSettings()
        {
            var vm = CreateViewModel();
            await vm.OnNavigatedToAsync();

            _settingsProvider.ClearReceivedCalls();

            // User picks High Performance
            vm.SelectedAcPlanGuid = PowerPlanService.HighPerformanceGuid;

            _appSettings.PreferredAcPlanGuid.Should().Be(PowerPlanService.HighPerformanceGuid);
            _settingsProvider.Received(1).Save();
        }

        [UIFact]
        public async Task OnSelectedDcPlanGuidChanged_WhenUserSelectsPlan_PersistsToSettings()
        {
            var vm = CreateViewModel();
            await vm.OnNavigatedToAsync();

            _settingsProvider.ClearReceivedCalls();

            // User picks Balanced for DC
            vm.SelectedDcPlanGuid = PowerPlanService.BalancedGuid;

            _appSettings.PreferredDcPlanGuid.Should().Be(PowerPlanService.BalancedGuid);
            _settingsProvider.Received(1).Save();
        }
    }
}
