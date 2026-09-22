using AltPowerPlan.Models;
using AltPowerPlan.Services.Hotkey;
using AltPowerPlan.Services.Power;
using AltPowerPlan.Services.Settings;
using AltPowerPlan.Services.Startup;
using AltPowerPlan.Services.Themes;
using AltPowerPlan.Services.Translation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace AltPowerPlan.ViewModels.Pages
{
    public partial class SettingsViewModel : ObservableObject, INavigationAware
    {
        private readonly IAppThemeService _appThemeService;
        private readonly ITranslationService _translationService;
        private readonly IAppSettingsProvider _settingsProvider;
        private readonly IHotkeyService _hotkeyService;
        private readonly IStartupService _startupService;
        private readonly IPowerPlanService _powerPlanService;
        private readonly ISnackbarService _snackbarService;

        [ObservableProperty]
        private List<LocalizedOption<ThemeChoice>> _themeOptions = [];

        [ObservableProperty]
        private ThemeChoice _selectedThemeChoice = ThemeChoice.System;

        [ObservableProperty]
        private List<LocalizedOption<string>> _languageOptions = [];

        [ObservableProperty]
        private string _selectedLanguageCode = "system";

        [ObservableProperty]
        private List<LocalizedOption<ScreenSide>> _screenSideOptions = [];

        [ObservableProperty]
        private ScreenSide _selectedScreenSide = ScreenSide.BottomRight;

        [ObservableProperty]
        private List<LocalizedOption<HotkeyChoice>> _hotkeyPresets = [];

        [ObservableProperty]
        private HotkeyChoice _selectedPreset = HotkeyChoice.CtrlAltP;

        [ObservableProperty]
        private HotkeyModel _selectedHotkey = HotkeyModel.Default;

        [ObservableProperty]
        private string _hotkeyDisplayText = "Ctrl + Alt + P";

        [ObservableProperty]
        private bool _isRecordingHotkey;

        [ObservableProperty]
        private SymbolRegular _hotkeyButtonIcon = SymbolRegular.Keyboard24;

        [ObservableProperty]
        private string _hotkeyErrorMessage = string.Empty;

        public Visibility HotkeyErrorVisibility =>
            string.IsNullOrEmpty(HotkeyErrorMessage) ? Visibility.Collapsed : Visibility.Visible;

        private bool _isInitialized = false;
        private bool _isUpdatingPreset = false;

        public SettingsViewModel(
            IAppThemeService themeService,
            ITranslationService translationService,
            IAppSettingsProvider settingsProvider,
            IHotkeyService hotkeyService,
            IStartupService startupService,
            IPowerPlanService powerPlanService,
            ISnackbarService snackbarService)
        {
            _appThemeService = themeService;
            _translationService = translationService;
            _settingsProvider = settingsProvider;
            _hotkeyService = hotkeyService;
            _startupService = startupService;
            _powerPlanService = powerPlanService;
            _snackbarService = snackbarService;
            _translationService.LanguageChanged += OnLanguageChanged;
        }

        [ObservableProperty]
        private bool _confirmPlanDeletion = true;

        partial void OnConfirmPlanDeletionChanged(bool value)
        {
            if (!_isInitialized)
                return;

            _settingsProvider.Settings.ConfirmPlanDeletion = value;
            _settingsProvider.Save();
        }

        [ObservableProperty]
        private bool _startWithWindows;

        partial void OnStartWithWindowsChanged(bool value)
        {
            if (!_isInitialized)
                return;

            _settingsProvider.Settings.StartWithWindows = value;
            _settingsProvider.Save();
            _startupService.SetStartup(value, _settingsProvider.Settings.StartMinimized);
        }

        [ObservableProperty]
        private bool _startMinimized;

        partial void OnStartMinimizedChanged(bool value)
        {
            if (!_isInitialized)
                return;

            _settingsProvider.Settings.StartMinimized = value;
            _settingsProvider.Save();
            if (_settingsProvider.Settings.StartWithWindows)
            {
                _startupService.SetStartup(true, value);
            }
        }

        [ObservableProperty]
        private bool _minimizeToTray;

        partial void OnMinimizeToTrayChanged(bool value)
        {
            if (!_isInitialized)
                return;

            _settingsProvider.Settings.MinimizeToTray = value;
            _settingsProvider.Save();
        }

        [ObservableProperty]
        private bool _autoSwitchAcDc;

        partial void OnAutoSwitchAcDcChanged(bool value)
        {
            if (!_isInitialized)
                return;

            _settingsProvider.Settings.AutoSwitchAcDc = value;
            _settingsProvider.Save();
        }

        public ObservableCollection<PowerPlanModel> AvailablePlans { get; } = new();

        [ObservableProperty]
        private Guid? _selectedAcPlanGuid;

        partial void OnSelectedAcPlanGuidChanged(Guid? value)
        {
            if (!_isInitialized || !value.HasValue || value.Value == Guid.Empty)
                return;

            _settingsProvider.Settings.PreferredAcPlanGuid = value.Value;
            _settingsProvider.Save();
        }

        [ObservableProperty]
        private Guid? _selectedDcPlanGuid;

        partial void OnSelectedDcPlanGuidChanged(Guid? value)
        {
            if (!_isInitialized || !value.HasValue || value.Value == Guid.Empty)
                return;

            _settingsProvider.Settings.PreferredDcPlanGuid = value.Value;
            _settingsProvider.Save();
        }

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
            {
                InitializeViewModel();
            }
            else
            {
                ConfirmPlanDeletion = _settingsProvider.Settings.ConfirmPlanDeletion;
                StartWithWindows = _settingsProvider.Settings.StartWithWindows;
                StartMinimized = _settingsProvider.Settings.StartMinimized;
                MinimizeToTray = _settingsProvider.Settings.MinimizeToTray;
                AutoSwitchAcDc = _settingsProvider.Settings.AutoSwitchAcDc;
                LoadAvailablePlans();
            }

            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync()
        {
            if (IsRecordingHotkey)
            {
                CancelRecordingHotkey();
            }
            return Task.CompletedTask;
        }

        private void InitializeViewModel()
        {
            if (_isInitialized)
                return;

            ThemeOptions = new List<LocalizedOption<ThemeChoice>>
            {
                new(ThemeChoice.System, "SettingsThemeSystemOption", _translationService),
                new(ThemeChoice.Light, "SettingsThemeLightOption", _translationService),
                new(ThemeChoice.Dark, "SettingsThemeDarkOption", _translationService)
            };

            ScreenSideOptions = new List<LocalizedOption<ScreenSide>>
            {
                new(ScreenSide.BottomRight, "SettingsScreenSideBottomRight", _translationService),
                new(ScreenSide.BottomLeft, "SettingsScreenSideBottomLeft", _translationService),
                new(ScreenSide.TopRight, "SettingsScreenSideTopRight", _translationService),
                new(ScreenSide.TopLeft, "SettingsScreenSideTopLeft", _translationService),
                new(ScreenSide.BottomCenter, "SettingsScreenSideBottomCenter", _translationService),
                new(ScreenSide.TopCenter, "SettingsScreenSideTopCenter", _translationService),
            };

            HotkeyPresets = new List<LocalizedOption<HotkeyChoice>>
            {
                new(HotkeyChoice.CtrlAltP, "SettingsHotkeyCtrlAltP", _translationService),
                new(HotkeyChoice.AltShiftP, "SettingsHotkeyAltShiftP", _translationService),
                new(HotkeyChoice.WinAltP, "SettingsHotkeyWinAltP", _translationService),
                new(HotkeyChoice.CtrlShiftP, "SettingsHotkeyCtrlShiftP", _translationService),
                new(HotkeyChoice.Custom, "SettingsHotkeyCustom", _translationService),
                new(HotkeyChoice.None, "SettingsHotkeyNone", _translationService),
            };

            LanguageOptions = BuildLanguageList();

            SelectedThemeChoice = _appThemeService.GetTheme();
            SelectedLanguageCode = _translationService.GetSelectedLanguageCode();
            SelectedScreenSide = _settingsProvider.Settings.QuickMenuSide;
            SelectedHotkey = _settingsProvider.Settings.QuickMenuHotkey ?? HotkeyModel.Default;

            UpdateHotkeyDisplay();
            UpdatePresetSelection();
            ConfirmPlanDeletion = _settingsProvider.Settings.ConfirmPlanDeletion;
            StartWithWindows = _settingsProvider.Settings.StartWithWindows;
            StartMinimized = _settingsProvider.Settings.StartMinimized;
            MinimizeToTray = _settingsProvider.Settings.MinimizeToTray;
            AutoSwitchAcDc = _settingsProvider.Settings.AutoSwitchAcDc;

            LoadAvailablePlans();

            _isInitialized = true;
        }

        private void LoadAvailablePlans()
        {
            var plans = _powerPlanService.GetPowerPlans();

            bool needsReload = plans.Count != AvailablePlans.Count;
            if (!needsReload)
            {
                for (int i = 0; i < plans.Count; i++)
                {
                    if (AvailablePlans[i].Id != plans[i].Id || AvailablePlans[i].Name != plans[i].Name)
                    {
                        needsReload = true;
                        break;
                    }
                }
            }

            if (needsReload)
            {
                AvailablePlans.Clear();
                for (int i = 0; i < plans.Count; i++)
                {
                    AvailablePlans.Add(plans[i]);
                }
            }

            var prefAc = _settingsProvider.Settings.PreferredAcPlanGuid;
            Guid targetAc;
            if (prefAc.HasValue && prefAc.Value != Guid.Empty && plans.Any(p => p.Id == prefAc.Value))
            {
                targetAc = prefAc.Value;
            }
            else
            {
                var fallback = plans.FirstOrDefault(p => p.Id == PowerPlanService.BalancedGuid)
                               ?? plans.FirstOrDefault(p => p.Id == PowerPlanService.HighPerformanceGuid)
                               ?? plans.FirstOrDefault(p => p.IsActive)
                               ?? plans.FirstOrDefault();
                targetAc = fallback?.Id ?? Guid.Empty;
                if (targetAc != Guid.Empty)
                {
                    _settingsProvider.Settings.PreferredAcPlanGuid = targetAc;
                    _settingsProvider.Save();
                }
            }

            var prefDc = _settingsProvider.Settings.PreferredDcPlanGuid;
            Guid targetDc;
            if (prefDc.HasValue && prefDc.Value != Guid.Empty && plans.Any(p => p.Id == prefDc.Value))
            {
                targetDc = prefDc.Value;
            }
            else
            {
                var fallback = plans.FirstOrDefault(p => p.Id == PowerPlanService.PowerSaverGuid)
                               ?? plans.FirstOrDefault(p => p.Id == PowerPlanService.BalancedGuid)
                               ?? plans.FirstOrDefault();
                targetDc = fallback?.Id ?? Guid.Empty;
                if (targetDc != Guid.Empty)
                {
                    _settingsProvider.Settings.PreferredDcPlanGuid = targetDc;
                    _settingsProvider.Save();
                }
            }

            if (SelectedAcPlanGuid != targetAc)
            {
                SelectedAcPlanGuid = targetAc;
            }
            else
            {
                OnPropertyChanged(nameof(SelectedAcPlanGuid));
            }

            if (SelectedDcPlanGuid != targetDc)
            {
                SelectedDcPlanGuid = targetDc;
            }
            else
            {
                OnPropertyChanged(nameof(SelectedDcPlanGuid));
            }
        }

        [RelayCommand]
        public async Task RestoreDefaultSchemesAsync()
        {
            bool success = await _powerPlanService.RestoreDefaultSchemesAsync();
            if (success)
            {
                LoadAvailablePlans();
                _snackbarService.Show(
                    _translationService.GetString("DashboardDefaultsRestoredTitle"),
                    _translationService.GetString("DashboardDefaultsRestoredMessage"),
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.ArrowClockwise24),
                    TimeSpan.FromSeconds(3)
                );
            }
        }

        private List<LocalizedOption<string>> BuildLanguageList()
        {
            var options = new List<LocalizedOption<string>>
            {
                new("system", "SettingsThemeSystemOption", _translationService)
            };

            foreach (var culture in _translationService.GetAvailableLanguages())
            {
                options.Add(new LocalizedOption<string>(
                    value: culture.Name,
                    translationKey: "",
                    translator: _translationService,
                    nativeFallback: culture.NativeName
                ));
            }
            return options;
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            if (!_isInitialized)
                return;

            UpdateHotkeyDisplay();
        }

        partial void OnSelectedThemeChoiceChanged(ThemeChoice value)
        {
            if (!_isInitialized)
                return;

            _appThemeService.ApplyTheme(value);
        }

        partial void OnSelectedLanguageCodeChanged(string value)
        {
            if (!_isInitialized)
                return;

            _translationService.SetLanguage(value);
        }

        partial void OnSelectedScreenSideChanged(ScreenSide value)
        {
            if (!_isInitialized)
                return;

            _settingsProvider.Settings.QuickMenuSide = value;
            _settingsProvider.Save();
        }

        partial void OnSelectedPresetChanged(HotkeyChoice value)
        {
            if (!_isInitialized || _isUpdatingPreset || value == HotkeyChoice.Custom)
                return;

            ApplyHotkey(HotkeyModel.FromChoice(value));
        }

        partial void OnHotkeyErrorMessageChanged(string value)
        {
            OnPropertyChanged(nameof(HotkeyErrorVisibility));
        }

        [RelayCommand]
        public void ToggleRecordingHotkey()
        {
            if (IsRecordingHotkey)
            {
                CancelRecordingHotkey();
            }
            else
            {
                StartRecordingHotkey();
            }
        }

        public void StartRecordingHotkey()
        {
            IsRecordingHotkey = true;
            HotkeyButtonIcon = SymbolRegular.Record24;
            HotkeyDisplayText = _translationService.GetString("SettingsHotkeyPressKeys");
            HotkeyErrorMessage = string.Empty;
        }

        public void CancelRecordingHotkey()
        {
            IsRecordingHotkey = false;
            HotkeyButtonIcon = SymbolRegular.Keyboard24;
            HotkeyErrorMessage = string.Empty;
            UpdateHotkeyDisplay();
        }

        public void UpdateRecordingModifiers(ModifierKeys modifiers)
        {
            var parts = new List<string>();
            if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            parts.Add("...");
            HotkeyDisplayText = string.Join(" + ", parts);
        }

        public void ApplyRecordedHotkey(ModifierKeys modifiers, Key key)
        {
            bool isFunctionKey = key >= Key.F1 && key <= Key.F24;
            bool hasModifiers = modifiers != ModifierKeys.None;

            if (!hasModifiers && !isFunctionKey)
            {
                HotkeyErrorMessage = _translationService.GetString("SettingsHotkeyNeedModifier");
                return;
            }

            IsRecordingHotkey = false;
            HotkeyButtonIcon = SymbolRegular.Keyboard24;

            var newHotkey = new HotkeyModel(modifiers, key);
            ApplyHotkey(newHotkey);
        }

        [RelayCommand]
        public void ClearHotkey()
        {
            if (IsRecordingHotkey)
            {
                IsRecordingHotkey = false;
                HotkeyButtonIcon = SymbolRegular.Keyboard24;
            }
            ApplyHotkey(HotkeyModel.None);
        }

        [RelayCommand]
        public void ResetHotkey()
        {
            if (IsRecordingHotkey)
            {
                IsRecordingHotkey = false;
                HotkeyButtonIcon = SymbolRegular.Keyboard24;
            }
            ApplyHotkey(HotkeyModel.Default);
        }

        public void ApplyHotkey(HotkeyModel hotkey)
        {
            SelectedHotkey = hotkey;
            _settingsProvider.Settings.QuickMenuHotkey = hotkey;
            _settingsProvider.Save();

            bool success = _hotkeyService.UpdateHotkey(hotkey);
            if (!success)
            {
                if (_hotkeyService.LastError == "HotkeyInUse")
                {
                    HotkeyErrorMessage = _translationService.GetString("SettingsHotkeyInUse");
                }
                else
                {
                    HotkeyErrorMessage = _hotkeyService.LastError ?? "Failed to register hotkey";
                }
            }
            else
            {
                HotkeyErrorMessage = string.Empty;
            }

            UpdateHotkeyDisplay();
            UpdatePresetSelection();
        }

        private void UpdateHotkeyDisplay()
        {
            if (SelectedHotkey.IsNone)
            {
                HotkeyDisplayText = _translationService.GetString("SettingsHotkeyNone");
            }
            else
            {
                HotkeyDisplayText = SelectedHotkey.ToString();
            }
        }

        private void UpdatePresetSelection()
        {
            _isUpdatingPreset = true;
            SelectedPreset = SelectedHotkey.ToChoice();
            _isUpdatingPreset = false;
        }
    }
}
