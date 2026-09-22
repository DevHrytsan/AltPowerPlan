using System;
using Wpf.Ui.Controls;

namespace AltPowerPlan.Models
{
    public class PowerPlanPresetModel
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public Guid BaseGuid { get; init; }
        public SymbolRegular Icon { get; init; }
        public bool IsCustom { get; init; }
    }
}
