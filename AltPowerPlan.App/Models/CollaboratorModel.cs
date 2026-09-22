using CommunityToolkit.Mvvm.ComponentModel;

namespace AltPowerPlan.Models
{
    public partial class CollaboratorModel : ObservableObject
    {
        public string Login { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }

        public string HtmlUrl { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public int Contributions { get; set; }

        public string ContributionsText { get; set; } = string.Empty;

        public bool IsAuthor { get; set; }

        public string DisplayName => !string.IsNullOrWhiteSpace(Name) ? Name : Login;
    }
}
