using AltPowerPlan.Models;
using AltPowerPlan.Services.Power;
using AltPowerPlan.Services.Settings;
using AltPowerPlan.Services.Translation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace AltPowerPlan.ViewModels.Pages
{
    public enum DashboardDialogMode
    {
        None,
        Create,
        Edit,
        Delete
    }

    public partial class DashboardViewModel : ObservableObject, INavigationAware
    {
        private readonly IPowerPlanService _powerPlanService;
        private readonly ISnackbarService _snackbarService;
        private readonly ITranslationService _translationService;
        private readonly IAppSettingsProvider _settingsProvider;

        public ObservableCollection<PowerPlanModel> PowerPlans { get; } = new();

        public ObservableCollection<PowerPlanModel> AvailableBasePlans { get; } = new();

        [ObservableProperty]
        private PowerPlanModel? _activePlan;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDialogOpen))]
        [NotifyPropertyChangedFor(nameof(IsCreateDialog))]
        [NotifyPropertyChangedFor(nameof(IsEditDialog))]
        [NotifyPropertyChangedFor(nameof(IsDeleteDialog))]
        private DashboardDialogMode _dialogMode = DashboardDialogMode.None;

        public bool IsDialogOpen => DialogMode != DashboardDialogMode.None;
        public bool IsCreateDialog => DialogMode == DashboardDialogMode.Create;
        public bool IsEditDialog => DialogMode == DashboardDialogMode.Edit;
        public bool IsDeleteDialog => DialogMode == DashboardDialogMode.Delete;

        [ObservableProperty]
        private string _dialogTitle = string.Empty;

        // Create Dialog fields
        [ObservableProperty]
        private ObservableCollection<PowerPlanPresetModel> _presets = new();

        [ObservableProperty]
        private PowerPlanPresetModel? _selectedPreset;

        [ObservableProperty]
        private bool _isCustomPresetSelected;

        [ObservableProperty]
        private string _newPlanName = string.Empty;

        [ObservableProperty]
        private string _newPlanDescription = string.Empty;

        [ObservableProperty]
        private PowerPlanModel? _selectedBasePlan;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasCreateError))]
        private string _createErrorMessage = string.Empty;

        public bool HasCreateError => !string.IsNullOrWhiteSpace(CreateErrorMessage);

        // Edit Dialog fields
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanDeleteEditingPlan))]
        private PowerPlanModel? _editingPlan;

        public bool CanDeleteEditingPlan => EditingPlan != null && !EditingPlan.IsActive;

        [ObservableProperty]
        private string _editPlanName = string.Empty;

        [ObservableProperty]
        private string _editPlanDescription = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasEditError))]
        private string _editErrorMessage = string.Empty;

        public bool HasEditError => !string.IsNullOrWhiteSpace(EditErrorMessage);

        // Delete Dialog fields
        [ObservableProperty]
        private PowerPlanModel? _planToDelete;

        [ObservableProperty]
        private string _deleteMessage = string.Empty;

        [ObservableProperty]
        private bool _doNotAskAgainDelete;

        public DashboardViewModel(
            IPowerPlanService powerPlanService,
            ISnackbarService snackbarService,
            ITranslationService translationService,
            IAppSettingsProvider settingsProvider)
        {
            _powerPlanService = powerPlanService;
            _snackbarService = snackbarService;
            _translationService = translationService;
            _settingsProvider = settingsProvider;

            _powerPlanService.ActivePlanChanged += OnActivePlanChanged;
            _translationService.LanguageChanged += OnLanguageChanged;
        }

        public Task OnNavigatedToAsync()
        {
            LoadPlans();
            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync()
        {
            CloseDialog();
            return Task.CompletedTask;
        }

        [RelayCommand]
        public void LoadPlans()
        {
            IsLoading = true;
            try
            {
                var plans = _powerPlanService.GetPowerPlans(forceRefresh: true);
                var active = _powerPlanService.GetActivePowerPlan();

                bool sameList = PowerPlans.Count == plans.Count;
                if (sameList)
                {
                    for (int i = 0; i < plans.Count; i++)
                    {
                        if (PowerPlans[i].Id != plans[i].Id)
                        {
                            sameList = false;
                            break;
                        }
                    }
                }

                if (sameList)
                {
                    for (int i = 0; i < plans.Count; i++)
                    {
                        var existing = PowerPlans[i];
                        var updated = plans[i];
                        if (existing.IsActive != updated.IsActive) existing.IsActive = updated.IsActive;
                        if (existing.Name != updated.Name) existing.Name = updated.Name;
                        if (existing.Description != updated.Description) existing.Description = updated.Description;
                        if (existing.Type != updated.Type) existing.Type = updated.Type;
                    }

                    if (AvailableBasePlans.Count != plans.Count)
                    {
                        AvailableBasePlans.Clear();
                        for (int i = 0; i < plans.Count; i++)
                        {
                            AvailableBasePlans.Add(plans[i]);
                        }
                    }
                }
                else
                {
                    PowerPlans.Clear();
                    AvailableBasePlans.Clear();

                    for (int i = 0; i < plans.Count; i++)
                    {
                        PowerPlans.Add(plans[i]);
                        AvailableBasePlans.Add(plans[i]);
                    }
                }

                ActivePlan = active;
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public void ActivatePlan(PowerPlanModel? plan)
        {
            if (plan == null || plan.IsActive)
                return;

            bool success = _powerPlanService.SetActivePowerPlan(plan.Id);
            if (success)
            {
                ActivePlan = plan;
                foreach (var p in PowerPlans)
                {
                    p.IsActive = (p.Id == plan.Id);
                }

                string title = _translationService.GetString("DashboardPlanActivated");
                _snackbarService.Show(
                    title,
                    plan.Name,
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.CheckmarkCircle24),
                    TimeSpan.FromSeconds(2.5)
                );
            }
        }

        [RelayCommand]
        public void OpenWindowsEditPlan(PowerPlanModel? plan)
        {
            var target = plan ?? ActivePlan;
            if (target != null)
            {
                _powerPlanService.OpenWindowsEditPlanSettings(target.Id);
            }
            else
            {
                _powerPlanService.OpenWindowsPowerOptions();
            }
        }

        [RelayCommand]
        public void OpenWindowsPowerOptions()
        {
            _powerPlanService.OpenWindowsPowerOptions();
        }

        [RelayCommand]
        public void DuplicatePlan(PowerPlanModel? plan)
        {
            var target = plan ?? ActivePlan;
            if (target == null) return;

            var newGuid = _powerPlanService.DuplicatePowerPlan(target.Id);
            if (newGuid.HasValue)
            {
                LoadPlans();
                _snackbarService.Show(
                    _translationService.GetString("DashboardPlanDuplicatedTitle"),
                    target.Name,
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.Copy24),
                    TimeSpan.FromSeconds(2.5)
                );
            }
        }

        [RelayCommand]
        public async Task ExportPlanAsync(PowerPlanModel? plan)
        {
            var target = plan ?? ActivePlan;
            if (target == null) return;

            var dialog = new SaveFileDialog
            {
                Filter = "Power Plan Files (*.pow)|*.pow|All Files (*.*)|*.*",
                FileName = $"{target.Name}.pow",
                DefaultExt = ".pow"
            };

            if (dialog.ShowDialog() == true)
            {
                bool success = await _powerPlanService.ExportPowerPlanAsync(target.Id, dialog.FileName);
                if (success)
                {
                    _snackbarService.Show(
                        _translationService.GetString("DashboardPlanExportedTitle"),
                        dialog.FileName,
                        ControlAppearance.Success,
                        new SymbolIcon(SymbolRegular.ArrowDownload24),
                        TimeSpan.FromSeconds(3)
                    );
                }
                else
                {
                    _snackbarService.Show(
                        _translationService.GetString("DashboardPlanExportFailedTitle"),
                        string.Empty,
                        ControlAppearance.Danger,
                        new SymbolIcon(SymbolRegular.ErrorCircle24),
                        TimeSpan.FromSeconds(3)
                    );
                }
            }
        }

        [RelayCommand]
        public async Task ImportPlanAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Power Plan Files (*.pow)|*.pow|All Files (*.*)|*.*",
                DefaultExt = ".pow"
            };

            if (dialog.ShowDialog() == true)
            {
                var newGuid = await _powerPlanService.ImportPowerPlanAsync(dialog.FileName);
                if (newGuid.HasValue)
                {
                    LoadPlans();
                    _snackbarService.Show(
                        _translationService.GetString("DashboardPlanImportedTitle"),
                        System.IO.Path.GetFileName(dialog.FileName),
                        ControlAppearance.Success,
                        new SymbolIcon(SymbolRegular.ArrowUpload24),
                        TimeSpan.FromSeconds(3)
                    );
                }
                else
                {
                    _snackbarService.Show(
                        _translationService.GetString("DashboardPlanImportFailedTitle"),
                        string.Empty,
                        ControlAppearance.Danger,
                        new SymbolIcon(SymbolRegular.ErrorCircle24),
                        TimeSpan.FromSeconds(3)
                    );
                }
            }
        }

        [RelayCommand]
        public async Task RestoreDefaultSchemesAsync()
        {
            bool success = await _powerPlanService.RestoreDefaultSchemesAsync();
            if (success)
            {
                LoadPlans();
                _snackbarService.Show(
                    _translationService.GetString("DashboardDefaultsRestoredTitle"),
                    _translationService.GetString("DashboardDefaultsRestoredMessage"),
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.ArrowClockwise24),
                    TimeSpan.FromSeconds(3)
                );
            }
        }

        partial void OnSelectedPresetChanged(PowerPlanPresetModel? value)
        {
            if (value == null) return;

            IsCustomPresetSelected = value.IsCustom;
            if (!value.IsCustom)
            {
                NewPlanName = value.Title;
                NewPlanDescription = value.Description;
            }
            else
            {
                NewPlanName = string.Empty;
                NewPlanDescription = string.Empty;
                SelectedBasePlan = ActivePlan ?? AvailableBasePlans.FirstOrDefault();
            }
            CreateErrorMessage = string.Empty;
        }

        [RelayCommand]
        public void OpenCreateDialog()
        {
            InitPresets();
            CreateErrorMessage = string.Empty;
            SelectedBasePlan = ActivePlan ?? AvailableBasePlans.FirstOrDefault();
            SelectedPreset = Presets.FirstOrDefault();
            DialogTitle = _translationService.GetString("DashboardCreatePlanTitle");
            DialogMode = DashboardDialogMode.Create;
        }

        private void InitPresets()
        {
            Presets.Clear();
            Presets.Add(new PowerPlanPresetModel
            {
                Id = "ultimate",
                Title = _translationService.GetString("DashboardPresetUltimate"),
                Description = _translationService.GetString("DashboardPresetUltimateDesc"),
                BaseGuid = PowerPlanService.UltimatePerformanceGuid,
                Icon = SymbolRegular.Rocket24,
                IsCustom = false
            });
            Presets.Add(new PowerPlanPresetModel
            {
                Id = "high",
                Title = _translationService.GetString("DashboardPresetHigh"),
                Description = _translationService.GetString("DashboardPresetHighDesc"),
                BaseGuid = PowerPlanService.HighPerformanceGuid,
                Icon = SymbolRegular.Flash24,
                IsCustom = false
            });
            Presets.Add(new PowerPlanPresetModel
            {
                Id = "balanced",
                Title = _translationService.GetString("DashboardPresetBalanced"),
                Description = _translationService.GetString("DashboardPresetBalancedDesc"),
                BaseGuid = PowerPlanService.BalancedGuid,
                Icon = SymbolRegular.LeafTwo24,
                IsCustom = false
            });
            Presets.Add(new PowerPlanPresetModel
            {
                Id = "saver",
                Title = _translationService.GetString("DashboardPresetSaver"),
                Description = _translationService.GetString("DashboardPresetSaverDesc"),
                BaseGuid = PowerPlanService.PowerSaverGuid,
                Icon = SymbolRegular.BatterySaver24,
                IsCustom = false
            });
            Presets.Add(new PowerPlanPresetModel
            {
                Id = "custom",
                Title = _translationService.GetString("DashboardPresetCustom"),
                Description = _translationService.GetString("DashboardPresetCustomDesc"),
                BaseGuid = Guid.Empty,
                Icon = SymbolRegular.Wrench24,
                IsCustom = true
            });
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            if (Presets.Count > 0)
            {
                var selectedId = SelectedPreset?.Id;
                InitPresets();
                if (selectedId != null)
                {
                    SelectedPreset = Presets.FirstOrDefault(p => p.Id == selectedId);
                }
            }

            if (DialogMode == DashboardDialogMode.Create)
            {
                DialogTitle = _translationService.GetString("DashboardCreatePlanTitle");
            }
            else if (DialogMode == DashboardDialogMode.Edit)
            {
                DialogTitle = _translationService.GetString("DashboardEditPlanTitle");
            }
            else if (DialogMode == DashboardDialogMode.Delete)
            {
                DialogTitle = _translationService.GetString("DashboardDeletePlanTitle");
                if (PlanToDelete != null)
                {
                    DeleteMessage = string.Format(_translationService.GetString("DashboardDeletePlanMessage"), PlanToDelete.Name);
                }
            }
        }

        [RelayCommand]
        public async Task SubmitCreatePlanAsync()
        {
            if (string.IsNullOrWhiteSpace(NewPlanName))
            {
                CreateErrorMessage = _translationService.GetString("DashboardPlanNameRequired");
                return;
            }

            Guid baseGuid;
            if (SelectedPreset != null && !SelectedPreset.IsCustom)
            {
                baseGuid = SelectedPreset.BaseGuid;
            }
            else
            {
                baseGuid = SelectedBasePlan?.Id ?? ActivePlan?.Id ?? PowerPlanService.BalancedGuid;
            }

            string name = NewPlanName.Trim();
            string? description = string.IsNullOrWhiteSpace(NewPlanDescription) ? null : NewPlanDescription.Trim();

            IsLoading = true;
            try
            {
                Guid? newGuid = await Task.Run(() => _powerPlanService.CreatePowerPlan(baseGuid, name, description)).ConfigureAwait(true);

                if (newGuid.HasValue)
                {
                    CloseDialog();
                    LoadPlans();

                    string title = _translationService.GetString("DashboardPlanCreated");
                    _snackbarService.Show(
                        title,
                        name,
                        ControlAppearance.Success,
                        new SymbolIcon(SymbolRegular.CheckmarkCircle24),
                        TimeSpan.FromSeconds(2.5)
                    );
                }
                else
                {
                    CreateErrorMessage = _translationService.GetString("DashboardPlanCreateFailed");
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public void OpenEditDialog(PowerPlanModel? plan)
        {
            var target = plan ?? ActivePlan;
            if (target == null)
                return;

            EditingPlan = target;
            EditPlanName = target.Name;
            EditPlanDescription = target.Description;
            EditErrorMessage = string.Empty;
            DialogTitle = _translationService.GetString("DashboardEditPlanTitle");
            DialogMode = DashboardDialogMode.Edit;
        }

        [RelayCommand]
        public void SubmitEditPlan()
        {
            if (EditingPlan == null)
                return;

            if (string.IsNullOrWhiteSpace(EditPlanName))
            {
                EditErrorMessage = _translationService.GetString("DashboardPlanNameRequired");
                return;
            }

            string name = EditPlanName.Trim();
            string? description = string.IsNullOrWhiteSpace(EditPlanDescription) ? null : EditPlanDescription.Trim();

            bool success = _powerPlanService.UpdatePowerPlan(EditingPlan.Id, name, description);

            if (success)
            {
                CloseDialog();
                LoadPlans();

                string title = _translationService.GetString("DashboardPlanUpdated");
                _snackbarService.Show(
                    title,
                    name,
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.CheckmarkCircle24),
                    TimeSpan.FromSeconds(2.5)
                );
            }
            else
            {
                EditErrorMessage = _translationService.GetString("DashboardPlanUpdateFailed");
            }
        }

        [RelayCommand]
        public void DeleteCurrentEditingPlan()
        {
            if (EditingPlan == null || EditingPlan.IsActive)
                return;

            OpenDeleteDialog(EditingPlan);
        }

        [RelayCommand]
        public void OpenDeleteDialog(PowerPlanModel? plan)
        {
            var target = plan ?? ActivePlan;
            if (target == null)
                return;

            if (target.IsActive)
            {
                _snackbarService.Show(
                    _translationService.GetString("DashboardDeletePlanTitle"),
                    _translationService.GetString("DashboardDeleteActivePlanError"),
                    ControlAppearance.Caution,
                    new SymbolIcon(SymbolRegular.Warning24),
                    TimeSpan.FromSeconds(3.5)
                );
                return;
            }

            if (!_settingsProvider.Settings.ConfirmPlanDeletion)
            {
                ExecuteDeletePlan(target);
                return;
            }

            PlanToDelete = target;
            DoNotAskAgainDelete = false;
            string format = _translationService.GetString("DashboardDeletePlanMessage");
            DeleteMessage = string.Format(format, target.Name);
            DialogTitle = _translationService.GetString("DashboardDeletePlanTitle");
            DialogMode = DashboardDialogMode.Delete;
        }

        [RelayCommand]
        public void ConfirmDeletePlan()
        {
            if (PlanToDelete == null || PlanToDelete.IsActive)
                return;

            if (DoNotAskAgainDelete)
            {
                _settingsProvider.Settings.ConfirmPlanDeletion = false;
                _settingsProvider.Save();
            }

            var plan = PlanToDelete;
            CloseDialog();
            ExecuteDeletePlan(plan);
        }

        private void ExecuteDeletePlan(PowerPlanModel plan)
        {
            string name = plan.Name;
            bool success = _powerPlanService.DeletePowerPlan(plan.Id);

            if (success)
            {
                LoadPlans();

                string title = _translationService.GetString("DashboardPlanDeleted");
                _snackbarService.Show(
                    title,
                    name,
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.Delete24),
                    TimeSpan.FromSeconds(2.5)
                );
            }
            else
            {
                string title = _translationService.GetString("DashboardPlanDeleteFailed");
                _snackbarService.Show(
                    title,
                    name,
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24),
                    TimeSpan.FromSeconds(3)
                );
            }
        }

        [RelayCommand]
        public void CloseDialog()
        {
            DialogMode = DashboardDialogMode.None;
            EditingPlan = null;
            PlanToDelete = null;
            DoNotAskAgainDelete = false;
            CreateErrorMessage = string.Empty;
            EditErrorMessage = string.Empty;
        }

        private void OnActivePlanChanged(object? sender, PowerPlanModel? newActivePlan)
        {
            if (newActivePlan?.Id == ActivePlan?.Id)
                return;

            Application.Current?.Dispatcher?.BeginInvoke(
                DispatcherPriority.Background,
                () =>
                {
                    ActivePlan = newActivePlan;
                    if (newActivePlan != null)
                    {
                        foreach (var plan in PowerPlans)
                        {
                            plan.IsActive = (plan.Id == newActivePlan.Id);
                        }
                    }
                });
        }
    }
}
