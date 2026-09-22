using AltPowerPlan.Services;
using AltPowerPlan.Services.Hotkey;
using AltPowerPlan.Services.Power;
using AltPowerPlan.Services.Settings;
using AltPowerPlan.Services.Startup;
using AltPowerPlan.Services.Themes;
using AltPowerPlan.Services.Translation;
using AltPowerPlan.Services.Tray;
using AltPowerPlan.Utils;
using AltPowerPlan.ViewModels.Pages;
using AltPowerPlan.ViewModels.Windows;
using AltPowerPlan.Views.Pages;
using AltPowerPlan.Views.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Automation.Provider;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;

namespace AltPowerPlan
{

    public partial class App
    {

        private static readonly IHost _host = Host
            .CreateDefaultBuilder()
            .ConfigureAppConfiguration(c => { c.SetBasePath(Constants.ProgramDirectory); })
            .ConfigureServices((context, services) =>
            {
                services.AddNavigationViewPageProvider();

                services.AddHostedService<ApplicationHostService>();

                services.AddSingleton<IAppSettingsProvider, AppSettingsProvider>();
                services.AddSingleton<IStartupService, StartupService>();

                services.AddSingleton<ITranslationService, TranslationService>();

                // Power Plan Service
                services.AddSingleton<IPowerPlanService, PowerPlanService>();

                // Themes
                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<IAppThemeService, AppThemeService>();

                // TaskBar
                services.AddSingleton<ITaskBarService, TaskBarService>();

                // Service containing navigation, same as INavigationWindow... but without window
                services.AddSingleton<INavigationService, NavigationService>();

                // Snackbar notifications
                services.AddSingleton<ISnackbarService, SnackbarService>();

                // Hotkey Service
                services.AddSingleton<IHotkeyService, HotkeyService>();

                // System Tray
                services.AddSingleton<ITrayService, TrayService>();
                services.AddSingleton<PowerFlyoutWindow>();

                // Main window with navigation
                services.AddSingleton<INavigationWindow, MainWindow>();
                services.AddSingleton<MainWindowViewModel>();

                services.AddSingleton<DashboardPage>();
                services.AddSingleton<DashboardViewModel>();

                services.AddSingleton<AboutPage>();
                services.AddSingleton<AboutViewModel>();

                services.AddSingleton<SettingsPage>();
                services.AddSingleton<SettingsViewModel>();
            }).Build();

        /// <summary>
        /// Gets services.
        /// </summary>
        public static IServiceProvider Services
        {
            get { return _host.Services; }
        }

        private static Mutex? _singleInstanceMutex;

        /// <summary>
        /// Occurs when the application is loading.
        /// </summary>
        private async void OnStartup(object sender, StartupEventArgs e)
        {
            const string mutexName = @"Local\AltPowerPlan_SingleInstance_B95B30D1";
            _singleInstanceMutex = new Mutex(true, mutexName, out bool isNewInstance);
            if (!isNewInstance)
            {
                Shutdown();
                return;
            }

            await _host.StartAsync();
        }

        /// <summary>
        /// Occurs when the application is closing.
        /// </summary>
        private async void OnExit(object sender, ExitEventArgs e)
        {
            try
            {
                if (_singleInstanceMutex != null)
                {
                    _singleInstanceMutex.ReleaseMutex();
                    _singleInstanceMutex.Dispose();
                    _singleInstanceMutex = null;
                }
            }
            catch { }

            await _host.StopAsync();
            _host.Dispose();
        }

        /// <summary>
        /// Occurs when an exception is thrown by an application but not handled.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (e.Exception is UnauthorizedAccessException)
            {
                e.Handled = true;
            }
        }
    }
}
