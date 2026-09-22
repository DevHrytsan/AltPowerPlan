using AltPowerPlan.Models;
using AltPowerPlan.Resources.Translation;
using AltPowerPlan.Services.Translation;
using AltPowerPlan.ViewModels.Pages;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Abstractions.Controls;

namespace AltPowerPlan.Views.Pages
{
    public partial class DashboardPage : INavigableView<DashboardViewModel>
    {
        private readonly ITranslationService? _translationService;

        public DashboardViewModel ViewModel { get; }

        public DashboardPage(DashboardViewModel viewModel, ITranslationService? translationService = null)
        {
            ViewModel = viewModel;
            _translationService = translationService;
            DataContext = this;

            InitializeComponent();

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape && ViewModel.IsDialogOpen)
                {
                    ViewModel.CloseDialog();
                    e.Handled = true;
                }
            };
        }

        private void OnDialogBackdropMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel.IsDialogOpen)
            {
                ViewModel.CloseDialog();
            }
        }

        private void OnPlanCardDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is PowerPlanModel plan)
            {
                ViewModel.OpenEditDialog(plan);
                e.Handled = true;
            }
        }

        private static PowerPlanModel? GetPlanFromMenuSender(object sender)
        {
            if (sender is MenuItem menuItem)
            {
                if (menuItem.DataContext is PowerPlanModel plan)
                {
                    return plan;
                }

                if (menuItem.Parent is ContextMenu contextMenu &&
                    contextMenu.PlacementTarget is FrameworkElement element &&
                    element.DataContext is PowerPlanModel targetPlan)
                {
                    return targetPlan;
                }
            }

            return null;
        }

        private void OnContextActivateClicked(object sender, RoutedEventArgs e)
        {
            var plan = GetPlanFromMenuSender(sender);
            if (plan != null)
            {
                ViewModel.ActivatePlan(plan);
            }
        }

        private void OnContextEditClicked(object sender, RoutedEventArgs e)
        {
            var plan = GetPlanFromMenuSender(sender);
            if (plan != null)
            {
                ViewModel.OpenEditDialog(plan);
            }
            else if (ViewModel.ActivePlan != null)
            {
                ViewModel.OpenEditDialog(ViewModel.ActivePlan);
            }
        }

        private void OnContextDeleteClicked(object sender, RoutedEventArgs e)
        {
            var plan = GetPlanFromMenuSender(sender);
            if (plan != null)
            {
                ViewModel.OpenDeleteDialog(plan);
            }
            else if (ViewModel.ActivePlan != null)
            {
                ViewModel.OpenDeleteDialog(ViewModel.ActivePlan);
            }
        }

        private void OnContextDuplicateClicked(object sender, RoutedEventArgs e)
        {
            var plan = GetPlanFromMenuSender(sender);
            if (plan != null)
            {
                ViewModel.DuplicatePlan(plan);
            }
            else if (ViewModel.ActivePlan != null)
            {
                ViewModel.DuplicatePlan(ViewModel.ActivePlan);
            }
        }

        private async void OnContextExportClicked(object sender, RoutedEventArgs e)
        {
            var plan = GetPlanFromMenuSender(sender);
            if (plan != null)
            {
                await ViewModel.ExportPlanAsync(plan);
            }
            else if (ViewModel.ActivePlan != null)
            {
                await ViewModel.ExportPlanAsync(ViewModel.ActivePlan);
            }
        }

        private void OnContextMenuOpened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu menu)
            {
                foreach (var item in menu.Items)
                {
                    if (item is MenuItem menuItem && menuItem.Tag is string locKey)
                    {
                        menuItem.Header = _translationService?.GetString(locKey)
                            ?? Strings.ResourceManager.GetString(locKey, Strings.Culture)
                            ?? menuItem.Header;
                    }
                }
            }
        }
    }
}
