using AltPowerPlan.Services.Themes;
using AltPowerPlan.Services.Translation;
using AltPowerPlan.Utils;
using System.Collections.ObjectModel;
using System.Reflection.Metadata;
using Wpf.Ui.Controls;

namespace AltPowerPlan.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _applicationTitle = Constants.AppName;

        [ObservableProperty]
        private ObservableCollection<object> _menuItems = new();

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems = new();

        [ObservableProperty]
        private ObservableCollection<MenuItem> _trayMenuItems = new();

        private readonly ITranslationService _translationService;

        public MainWindowViewModel(ITranslationService translationService)
        {
            _translationService = translationService;
            InitializeMenus();

            _translationService.LanguageChanged += OnLanguageChanged;
        }

        private void InitializeMenus()
        {
            MenuItems.Add(new NavigationViewItem()
            {
                Content = _translationService.GetString("MenuHome"),
                Icon = new SymbolIcon { Symbol = SymbolRegular.Home24 },
                TargetPageType = typeof(Views.Pages.DashboardPage),
                Tag = "MenuHome"
            });

            FooterMenuItems.Add(new NavigationViewItem()
            {
                Content = _translationService.GetString("MenuAbout"),
                Icon = new SymbolIcon { Symbol = SymbolRegular.Info24 },
                TargetPageType = typeof(Views.Pages.AboutPage),
                Tag = "MenuAbout"
            });

            FooterMenuItems.Add(new NavigationViewItem()
            {
                Content = _translationService.GetString("MenuSettings"),
                Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                TargetPageType = typeof(Views.Pages.SettingsPage),
                Tag = "MenuSettings"
            });

            TrayMenuItems.Add(new MenuItem
            {
                Header = _translationService.GetString("MenuHome"),
                Tag = "MenuHome"
            });

        }
        public event Action? MenuItemsUpdated;

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            foreach (var item in MenuItems.OfType<NavigationViewItem>())
            {
                if (item.Tag is string key)
                {
                    item.Content = _translationService.GetString(key);
                }
            }

            foreach (var item in FooterMenuItems.OfType<NavigationViewItem>())
            {
                if (item.Tag is string key)
                {
                    item.Content = _translationService.GetString(key);
                }
            }

            foreach (var item in TrayMenuItems)
            {
                if (item.Tag is string key)
                {
                    item.Header = _translationService.GetString(key);
                }
            }

            MenuItemsUpdated?.Invoke();
        }
    }
}

