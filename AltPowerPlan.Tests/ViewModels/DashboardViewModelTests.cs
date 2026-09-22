using AltPowerPlan.Models;
using AltPowerPlan.Services.Power;
using AltPowerPlan.Services.Settings;
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
    public class DashboardViewModelTests
    {
        private readonly IPowerPlanService _powerPlanService = Substitute.For<IPowerPlanService>();
        private readonly ISnackbarService _snackbarService = Substitute.For<ISnackbarService>();
        private readonly ITranslationService _translationService = Substitute.For<ITranslationService>();
        private readonly IAppSettingsProvider _settingsProvider = Substitute.For<IAppSettingsProvider>();

        private readonly AppSettings _appSettings = new();

        public DashboardViewModelTests()
        {
            _settingsProvider.Settings.Returns(_appSettings);
            _translationService.GetString(Arg.Any<string>()).Returns(callInfo => callInfo.Arg<string>());
        }

        private DashboardViewModel CreateViewModel()
        {
            return new DashboardViewModel(_powerPlanService, _snackbarService, _translationService, _settingsProvider);
        }

        [UIFact]
        public void ActivatePlan_WhenPlanIsInactive_CallsPowerPlanService()
        {
            var planId = Guid.NewGuid();
            var plan = new PowerPlanModel { Id = planId, Name = "High Performance", IsActive = false };

            _powerPlanService.SetActivePowerPlan(planId).Returns(true);
            var vm = CreateViewModel();

            vm.ActivatePlanCommand.Execute(plan);

            _powerPlanService.Received(1).SetActivePowerPlan(planId);
        }

        [UIFact]
        public void OpenDeleteDialog_WhenTargetIsActivePlan_ShowsWarningAndDoesNotOpenDialog()
        {
            var planId = Guid.NewGuid();
            var activePlan = new PowerPlanModel { Id = planId, Name = "Active Balanced", IsActive = true };

            var vm = CreateViewModel();

            vm.OpenDeleteDialog(activePlan);

            vm.IsDialogOpen.Should().BeFalse();
            vm.DialogMode.Should().Be(DashboardDialogMode.None);
            _powerPlanService.DidNotReceive().DeletePowerPlan(Arg.Any<Guid>());
        }

        [UIFact]
        public void OpenDeleteDialog_WhenConfirmDeletionIsTrue_OpensDeleteConfirmationDialog()
        {
            _appSettings.ConfirmPlanDeletion = true;
            var planId = Guid.NewGuid();
            var inactivePlan = new PowerPlanModel { Id = planId, Name = "Old Plan", IsActive = false };

            var vm = CreateViewModel();

            vm.OpenDeleteDialog(inactivePlan);

            vm.IsDialogOpen.Should().BeTrue();
            vm.DialogMode.Should().Be(DashboardDialogMode.Delete);
            vm.PlanToDelete.Should().Be(inactivePlan);
            _powerPlanService.DidNotReceive().DeletePowerPlan(Arg.Any<Guid>());
        }

        [UIFact]
        public void OpenDeleteDialog_WhenConfirmDeletionIsFalse_DirectlyDeletesPlan()
        {
            _appSettings.ConfirmPlanDeletion = false;
            var planId = Guid.NewGuid();
            var inactivePlan = new PowerPlanModel { Id = planId, Name = "Quick Delete Plan", IsActive = false };

            _powerPlanService.DeletePowerPlan(planId).Returns(true);
            var vm = CreateViewModel();

            vm.OpenDeleteDialog(inactivePlan);

            vm.IsDialogOpen.Should().BeFalse();
            _powerPlanService.Received(1).DeletePowerPlan(planId);
        }

        [UIFact]
        public void ConfirmDeletePlan_WhenDoNotAskAgainChecked_UpdatesSettingsAndDeletes()
        {
            _appSettings.ConfirmPlanDeletion = true;
            var planId = Guid.NewGuid();
            var inactivePlan = new PowerPlanModel { Id = planId, Name = "To Delete", IsActive = false };

            _powerPlanService.DeletePowerPlan(planId).Returns(true);
            var vm = CreateViewModel();

            vm.OpenDeleteDialog(inactivePlan);
            vm.DoNotAskAgainDelete = true;

            vm.ConfirmDeletePlanCommand.Execute(null);

            _appSettings.ConfirmPlanDeletion.Should().BeFalse();
            _settingsProvider.Received(1).Save();
            _powerPlanService.Received(1).DeletePowerPlan(planId);
            vm.IsDialogOpen.Should().BeFalse();
        }

        [UIFact]
        public void OpenEditDialog_WhenTargetIsInactivePlan_DoesNotActivatePlan()
        {
            var activeId = Guid.NewGuid();
            var inactiveId = Guid.NewGuid();

            var activePlan = new PowerPlanModel { Id = activeId, Name = "Active Balanced", IsActive = true };
            var inactivePlan = new PowerPlanModel { Id = inactiveId, Name = "Inactive Power Saver", IsActive = false };

            _powerPlanService.GetActivePowerPlan().Returns(activePlan);
            _powerPlanService.GetPowerPlans(Arg.Any<bool>()).Returns(new List<PowerPlanModel> { activePlan, inactivePlan });

            var vm = CreateViewModel();
            vm.LoadPlans();

            vm.OpenEditDialog(inactivePlan);

            vm.IsDialogOpen.Should().BeTrue();
            vm.DialogMode.Should().Be(DashboardDialogMode.Edit);
            vm.EditingPlan.Should().Be(inactivePlan);
            vm.ActivePlan.Should().Be(activePlan);
            inactivePlan.IsActive.Should().BeFalse();

            _powerPlanService.DidNotReceive().SetActivePowerPlan(Arg.Any<Guid>());
        }

        [UIFact]
        public void SubmitEditPlan_WhenTargetIsInactivePlan_PreservesActivePlanAndDoesNotActivate()
        {
            var activeId = Guid.NewGuid();
            var inactiveId = Guid.NewGuid();

            var activePlan = new PowerPlanModel { Id = activeId, Name = "Active Balanced", IsActive = true };
            var inactivePlan = new PowerPlanModel { Id = inactiveId, Name = "Inactive Power Saver", IsActive = false };

            _powerPlanService.GetActivePowerPlan().Returns(activePlan);
            _powerPlanService.GetPowerPlans(Arg.Any<bool>()).Returns(new List<PowerPlanModel> { activePlan, inactivePlan });
            _powerPlanService.UpdatePowerPlan(inactiveId, "Renamed Saver", Arg.Any<string?>()).Returns(true);

            var vm = CreateViewModel();
            vm.LoadPlans();

            vm.OpenEditDialog(inactivePlan);
            vm.EditPlanName = "Renamed Saver";

            vm.SubmitEditPlanCommand.Execute(null);

            vm.IsDialogOpen.Should().BeFalse();
            vm.ActivePlan.Should().Be(activePlan);
            _powerPlanService.Received(1).UpdatePowerPlan(inactiveId, "Renamed Saver", Arg.Any<string?>());
            _powerPlanService.DidNotReceive().SetActivePowerPlan(Arg.Any<Guid>());
        }

        [UIFact]
        public void OpenWindowsEditPlan_WhenTargetIsInactivePlan_DoesNotCallSetActivePowerPlan()
        {
            var activeId = Guid.NewGuid();
            var inactiveId = Guid.NewGuid();

            var activePlan = new PowerPlanModel { Id = activeId, Name = "Active Balanced", IsActive = true };
            var inactivePlan = new PowerPlanModel { Id = inactiveId, Name = "Inactive Power Saver", IsActive = false };

            _powerPlanService.GetActivePowerPlan().Returns(activePlan);
            _powerPlanService.GetPowerPlans(Arg.Any<bool>()).Returns(new List<PowerPlanModel> { activePlan, inactivePlan });

            var vm = CreateViewModel();
            vm.LoadPlans();

            vm.OpenWindowsEditPlanCommand.Execute(inactivePlan);

            _powerPlanService.Received(1).OpenWindowsEditPlanSettings(inactiveId);
            _powerPlanService.DidNotReceive().SetActivePowerPlan(Arg.Any<Guid>());
        }

        [UIFact]
        public void LoadPlans_WhenPlanIdsUnchanged_UpdatesPropertiesInPlaceWithoutClearingCollection()
        {
            var planId1 = Guid.NewGuid();
            var planId2 = Guid.NewGuid();

            var initialPlan1 = new PowerPlanModel { Id = planId1, Name = "Plan 1", IsActive = true };
            var initialPlan2 = new PowerPlanModel { Id = planId2, Name = "Plan 2", IsActive = false };

            _powerPlanService.GetPowerPlans(Arg.Any<bool>()).Returns(new List<PowerPlanModel> { initialPlan1, initialPlan2 });
            _powerPlanService.GetActivePowerPlan().Returns(initialPlan1);

            var vm = CreateViewModel();
            vm.LoadPlans();

            var item1Reference = vm.PowerPlans[0];
            var item2Reference = vm.PowerPlans[1];

            var updatedPlan1 = new PowerPlanModel { Id = planId1, Name = "Plan 1 Updated", IsActive = false };
            var updatedPlan2 = new PowerPlanModel { Id = planId2, Name = "Plan 2 Updated", IsActive = true };

            _powerPlanService.GetPowerPlans(Arg.Any<bool>()).Returns(new List<PowerPlanModel> { updatedPlan1, updatedPlan2 });
            _powerPlanService.GetActivePowerPlan().Returns(updatedPlan2);

            vm.LoadPlans();

            vm.PowerPlans[0].Should().BeSameAs(item1Reference);
            vm.PowerPlans[1].Should().BeSameAs(item2Reference);
            vm.PowerPlans[0].Name.Should().Be("Plan 1 Updated");
            vm.PowerPlans[0].IsActive.Should().BeFalse();
            vm.PowerPlans[1].Name.Should().Be("Plan 2 Updated");
            vm.PowerPlans[1].IsActive.Should().BeTrue();
        }
    }
}
