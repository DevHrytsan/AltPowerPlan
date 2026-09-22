using AltPowerPlan.Models;
using AltPowerPlan.Services.Power;
using AltPowerPlan.Services.Settings;
using FluentAssertions;
using NSubstitute;
using System.Linq;
using Xunit;

namespace AltPowerPlan.Tests.Services
{
    public class PowerPlanServiceTests
    {
        private readonly IAppSettingsProvider _settingsProvider = Substitute.For<IAppSettingsProvider>();
        private readonly AppSettings _settings = new();

        public PowerPlanServiceTests()
        {
            _settingsProvider.Settings.Returns(_settings);
        }

        [Fact]
        public void GetPowerPlans_DoesNotContainDuplicates()
        {
            using var service = new PowerPlanService(_settingsProvider);

            var plans = service.GetPowerPlans(forceRefresh: true);

            plans.Should().NotBeNull();
            plans.Select(p => p.Id).Should().OnlyHaveUniqueItems();
        }

        [Fact]
        public void GetPowerPlans_DoesNotContainOverlays()
        {
            using var service = new PowerPlanService(_settingsProvider);

            var plans = service.GetPowerPlans(forceRefresh: true);

            foreach (var plan in plans)
            {
                plan.Name.Should().NotContainEquivalentOf("Overlay");
            }
        }

        [Fact]
        public void GetActivePowerPlan_WhenAvailable_MatchesActiveFlagInPlans()
        {
            using var service = new PowerPlanService(_settingsProvider);

            var active = service.GetActivePowerPlan();
            var plans = service.GetPowerPlans();

            if (active != null)
            {
                plans.Should().ContainSingle(p => p.Id == active.Id && p.IsActive);
            }
        }

        [Fact]
        public void CreatePowerPlan_WithPowerSaverGuid_CreatesOrFallsBackSuccessfully()
        {
            using var service = new PowerPlanService(_settingsProvider);
            var guid = service.CreatePowerPlan(PowerPlanService.PowerSaverGuid, "Test Power Saver Unit");
            guid.Should().NotBeNull();
            if (guid.HasValue)
            {
                try
                {
                    var plans = service.GetPowerPlans(forceRefresh: true);
                    var created = plans.FirstOrDefault(p => p.Id == guid.Value);
                    created.Should().NotBeNull();
                    created!.Name.Should().Be("Test Power Saver Unit");
                    created.Type.Should().Be(PowerPlanType.PowerSaver);
                }
                finally
                {
                    service.DeletePowerPlan(guid.Value);
                }
            }
        }

        [Fact]
        public void CreatePowerPlan_WithNonExistentGuid_FallsBackSuccessfully()
        {
            using var service = new PowerPlanService(_settingsProvider);
            var guid = service.CreatePowerPlan(System.Guid.NewGuid(), "Test Fallback Unit");
            if (guid.HasValue)
            {
                service.DeletePowerPlan(guid.Value);
            }
            guid.Should().NotBeNull();
        }
    }
}
