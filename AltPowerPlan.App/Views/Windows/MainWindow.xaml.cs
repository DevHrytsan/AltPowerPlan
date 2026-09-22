using AltPowerPlan.Services.Hotkey;
using AltPowerPlan.Services.Power;
using AltPowerPlan.Services.Settings;
using AltPowerPlan.Services.Themes;
using AltPowerPlan.Services.Translation;
using AltPowerPlan.Services.Tray;
using AltPowerPlan.ViewModels.Windows;
using System.Collections;
using System.Configuration;
using System.Reflection;
using System.Windows.Automation.Provider;
using System.Windows.Interop;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace AltPowerPlan.Views.Windows
{
    public partial class MainWindow : INavigationWindow
    {
        private static readonly MethodInfo? UpdateBreadcrumbContentsMethod =
            typeof(NavigationView).GetMethod("UpdateBreadcrumbContents", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly IPowerPlanService _powerPlanService;
        private readonly ITrayService _trayService;
        private readonly IHotkeyService _hotkeyService;
        private readonly IAppSettingsProvider _settingsProvider;
        private readonly ITranslationService _translationService;
        private readonly PowerFlyoutWindow _powerFlyoutWindow;
        private bool _isExplicitExit;

        public MainWindowViewModel ViewModel { get; }

        public MainWindow(
            MainWindowViewModel viewModel,
            INavigationViewPageProvider navigationViewPageProvider,
            INavigationService navigationService,
            IAppThemeService appThemeService,
            ITranslationService translationService,
            IPowerPlanService powerPlanService,
            ISnackbarService snackbarService,
            ITrayService trayService,
            IHotkeyService hotkeyService,
            IAppSettingsProvider settingsProvider,
            PowerFlyoutWindow powerFlyoutWindow
        )
        {
            _powerPlanService = powerPlanService;
            _trayService = trayService;
            _hotkeyService = hotkeyService;
            _settingsProvider = settingsProvider;
            _translationService = translationService;
            _powerFlyoutWindow = powerFlyoutWindow;
            ViewModel = viewModel;
            DataContext = this;

            SystemThemeWatcher.Watch(this);

            translationService.ApplySavedLanguageOnStartup();
            appThemeService.ApplySavedThemeOnStartup();

            InitializeComponent();
            SetPageService(navigationViewPageProvider);

            navigationService.SetNavigationControl(RootNavigation);
            snackbarService.SetSnackbarPresenter(SnackbarPresenter);

            ViewModel.MenuItemsUpdated += UpdateNavigationBreadcrumbs;
            _translationService.LanguageChanged += OnLanguageChanged;

            if (Application.Current != null)
            {
                Application.Current.SessionEnding += OnSessionEnding;
            }

            Loaded += OnWindowLoaded;

            SourceInitialized += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                _powerPlanService.RegisterNotification(hwnd);
                _trayService.Initialize(this);
                _hotkeyService.Initialize(hwnd);
                _hotkeyService.HotkeyPressed += OnHotkeyPressed;
            };
        }

        private bool _isNavigated;
        private Type? _pendingNavigationPageType;

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (!_isNavigated)
            {
                var target = _pendingNavigationPageType ?? typeof(Views.Pages.DashboardPage);
                _pendingNavigationPageType = null;
                _isNavigated = true;
                RootNavigation.Navigate(target);
            }
        }

        private void OnHotkeyPressed()
        {
            Dispatcher.Invoke(() =>
            {
                _powerFlyoutWindow.ToggleAtScreenSide(_settingsProvider.Settings.QuickMenuSide, fromHotkey: true);
            });
        }

        #region INavigationWindow methods

        public INavigationView GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType)
        {
            if (!IsLoaded)
            {
                _pendingNavigationPageType = pageType;
                return true;
            }

            _isNavigated = true;
            return RootNavigation.Navigate(pageType);
        }

        public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) => RootNavigation.SetPageProviderService(navigationViewPageProvider);

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();

        #endregion INavigationWindow methods

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!_isExplicitExit)
            {
                e.Cancel = true;
                Hide();
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Minimized && _settingsProvider.Settings.MinimizeToTray)
            {
                Hide();
            }
        }

        /// <summary>
        /// Raises the closed event.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (Application.Current != null)
            {
                Application.Current.SessionEnding -= OnSessionEnding;
            }
            ViewModel.MenuItemsUpdated -= UpdateNavigationBreadcrumbs;
            _translationService.LanguageChanged -= OnLanguageChanged;
            _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
            _hotkeyService.Dispose();
            _powerPlanService.UnregisterNotification();
            _trayService.Remove();
        }

        private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            _isExplicitExit = true;
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(UpdateNavigationBreadcrumbs, DispatcherPriority.Background);
        }

        private void UpdateNavigationBreadcrumbs()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(UpdateNavigationBreadcrumbs, DispatcherPriority.Normal);
                return;
            }

            try
            {
                UpdateBreadcrumbContentsMethod?.Invoke(RootNavigation, null);
            }
            catch
            {
                // Fallback below
            }

            if (RootNavigation.BreadcrumbBar?.ItemsSource is IEnumerable items)
            {
                foreach (var item in items)
                {
                    if (item is null) continue;
                    try
                    {
                        item.GetType().GetMethod("UpdateFromSource", BindingFlags.Instance | BindingFlags.Public)?.Invoke(item, null);
                    }
                    catch
                    {
                    }
                }
            }

            try
            {
                RootNavigation.BreadcrumbBar?.Items.Refresh();
            }
            catch
            {
            }
        }

        public void ExitApplication()
        {
            _isExplicitExit = true;
            Close();
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            RootNavigation.SetServiceProvider(serviceProvider);
        }
    }
}
