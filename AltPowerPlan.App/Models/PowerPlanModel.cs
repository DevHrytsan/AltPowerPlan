using CommunityToolkit.Mvvm.ComponentModel;
using System;
using Wpf.Ui.Controls;

namespace AltPowerPlan.Models
{
    public partial class PowerPlanModel : ObservableObject, IEquatable<PowerPlanModel>
    {
        public Guid Id { get; init; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isActive;

        public PowerPlanType Type { get; set; }

        public SymbolRegular Icon => Type switch
        {
            PowerPlanType.Balanced => SymbolRegular.LeafTwo24,
            PowerPlanType.HighPerformance => SymbolRegular.Flash24,
            PowerPlanType.PowerSaver => SymbolRegular.BatterySaver24,
            PowerPlanType.UltimatePerformance => SymbolRegular.Rocket24,
            _ => SymbolRegular.Wrench24
        };

        public bool Equals(PowerPlanModel? other)
        {
            if (other is null) return false;
            return Id == other.Id;
        }

        public override bool Equals(object? obj) => Equals(obj as PowerPlanModel);

        public override int GetHashCode() => Id.GetHashCode();

        public static bool operator ==(PowerPlanModel? left, PowerPlanModel? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        public static bool operator !=(PowerPlanModel? left, PowerPlanModel? right) => !(left == right);
    }
}
