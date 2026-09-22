using AltPowerPlan.Models;
using FluentAssertions;
using System;
using Wpf.Ui.Controls;
using Xunit;

namespace AltPowerPlan.Tests.Models
{
    public class PowerPlanModelTests
    {
        [Theory]
        [InlineData(PowerPlanType.Balanced, SymbolRegular.LeafTwo24)]
        [InlineData(PowerPlanType.HighPerformance, SymbolRegular.Flash24)]
        [InlineData(PowerPlanType.PowerSaver, SymbolRegular.BatterySaver24)]
        [InlineData(PowerPlanType.UltimatePerformance, SymbolRegular.Rocket24)]
        [InlineData(PowerPlanType.Custom, SymbolRegular.Wrench24)]
        public void Icon_MatchesType(PowerPlanType type, SymbolRegular expectedSymbol)
        {
            var plan = new PowerPlanModel
            {
                Id = Guid.NewGuid(),
                Name = "Test Plan",
                Type = type
            };

            plan.Icon.Should().Be(expectedSymbol);
        }

        [Fact]
        public void IsActive_PropertyChanged_RaisesNotification()
        {
            var plan = new PowerPlanModel
            {
                Id = Guid.NewGuid(),
                Name = "Test Plan",
                IsActive = false
            };

            string? changedProp = null;
            plan.PropertyChanged += (s, e) => changedProp = e.PropertyName;

            plan.IsActive = true;

            changedProp.Should().Be(nameof(PowerPlanModel.IsActive));
        }
    }
}
