using AltPowerPlan.Models;
using AltPowerPlan.Services.Translation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Abstractions.Controls;

namespace AltPowerPlan.ViewModels.Pages
{
    public partial class AboutViewModel : ObservableObject, INavigationAware
    {
        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(6)
        };

        private readonly ITranslationService _translationService;

        private const string GITHUB_REPO_OWNER = "DevHrytsan";
        private const string GITHUB_REPO_NAME = "AltPowerPlan";
        private const string GITHUB_AUTHOR_LOGIN = "DevHrytsan";

        [ObservableProperty]
        private string _appVersion = string.Empty;

        [ObservableProperty]
        private bool _isCheckingUpdate;

        [ObservableProperty]
        private bool _isUpdateAvailable;

        [ObservableProperty]
        private string _latestVersion = string.Empty;

        [ObservableProperty]
        private string _updateStatusText = string.Empty;

        [ObservableProperty]
        private string _releaseUrl = "https://github.com/DevHrytsan/AltPowerPlan/releases";

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _hasCollaborators;

        [ObservableProperty]
        private CollaboratorModel _author = new()
        {
            Login = GITHUB_AUTHOR_LOGIN,
            Name = GITHUB_AUTHOR_LOGIN,
            AvatarUrl = "https://avatars.githubusercontent.com/u/55915163?v=4",
            HtmlUrl = "https://github.com/DevHrytsan",
            IsAuthor = true
        };

        public ObservableCollection<CollaboratorModel> Collaborators { get; } = new();

        private bool _isLoaded;

        public AboutViewModel(ITranslationService translationService)
        {
            _translationService = translationService;

            AppVersion = ResolveAppVersion();

            UpdateRoleTitles();
            _translationService.LanguageChanged += (s, e) => UpdateRoleTitles();
        }

        public async Task OnNavigatedToAsync()
        {
            if (!_isLoaded)
            {
                await LoadCollaboratorsAsync();
            }
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private void UpdateRoleTitles()
        {
            foreach (var c in Collaborators)
            {
                c.Role = _translationService.GetString("AboutRoleContributor");
                c.ContributionsText = string.Format(
                    _translationService.GetString("AboutContributionsCount"),
                    c.Contributions
                );
            }
        }

        [RelayCommand]
        public async Task RefreshCollaboratorsAsync()
        {
            await LoadCollaboratorsAsync();
        }

        public async Task LoadCollaboratorsAsync()
        {
            if (IsLoading)
                return;

            IsLoading = true;

            try
            {
                // Ensure default headers
                if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
                {
                    _httpClient.DefaultRequestHeaders.UserAgent.Add(
                        new ProductInfoHeaderValue("AltPowerPlan", "1.0")
                    );
                }

                await FetchAuthorProfileAsync();
                await FetchRepoContributorsAsync();

                _isLoaded = true;
            }
            catch (Exception)
            {
                // Network failure or rate limit: fallback gracefully
            }
            finally
            {
                IsLoading = false;
                HasCollaborators = Collaborators.Count > 0;
            }
        }

        private async Task FetchAuthorProfileAsync()
        {
            try
            {
                string userUrl = $"https://api.github.com/users/{GITHUB_REPO_OWNER}";
                using var response = await _httpClient.GetAsync(userUrl);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var userDto = JsonSerializer.Deserialize<GitHubUserDto>(json);
                    if (userDto != null)
                    {
                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            if (!string.IsNullOrWhiteSpace(userDto.AvatarUrl))
                                Author.AvatarUrl = userDto.AvatarUrl;
                            if (!string.IsNullOrWhiteSpace(userDto.Name))
                                Author.Name = userDto.Name;
                            if (!string.IsNullOrWhiteSpace(userDto.HtmlUrl))
                                Author.HtmlUrl = userDto.HtmlUrl;
                        });
                    }
                }
            }
            catch
            {
                // Ignore API errors eh
            }
        }

        private async Task FetchRepoContributorsAsync()
        {
            try
            {
                string contributorsUrl = $"https://api.github.com/repos/{GITHUB_REPO_OWNER}/{GITHUB_REPO_NAME}/contributors";
                using var response = await _httpClient.GetAsync(contributorsUrl);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var contributors = JsonSerializer.Deserialize<List<GitHubContributorDto>>(json);

                    if (contributors != null)
                    {
                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            Collaborators.Clear();

                            foreach (var item in contributors)
                            {
                                if (string.IsNullOrWhiteSpace(item.Login))
                                    continue;

                                if (item.Login.Equals(GITHUB_AUTHOR_LOGIN, StringComparison.OrdinalIgnoreCase))
                                {
                                    Author.Contributions = item.Contributions;
                                    Author.ContributionsText = string.Format(
                                        _translationService.GetString("AboutContributionsCount"),
                                        item.Contributions
                                    );
                                    if (!string.IsNullOrWhiteSpace(item.AvatarUrl))
                                        Author.AvatarUrl = item.AvatarUrl;
                                    if (!string.IsNullOrWhiteSpace(item.HtmlUrl))
                                        Author.HtmlUrl = item.HtmlUrl;
                                }
                                else
                                {
                                    Collaborators.Add(new CollaboratorModel
                                    {
                                        Login = item.Login,
                                        Name = item.Login,
                                        AvatarUrl = item.AvatarUrl,
                                        HtmlUrl = item.HtmlUrl ?? $"https://github.com/{item.Login}",
                                        Role = _translationService.GetString("AboutRoleContributor"),
                                        Contributions = item.Contributions,
                                        ContributionsText = string.Format(
                                            _translationService.GetString("AboutContributionsCount"),
                                            item.Contributions
                                        ),
                                        IsAuthor = false
                                    });
                                }
                            }
                        });
                    }
                }
            }
            catch
            {
                // Ignore it too
            }
        }

        [RelayCommand]
        private void OnOpenUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception)
            {
                // Fallback / ignore error
            }
        }

        [RelayCommand]
        public async Task CheckForUpdatesAsync()
        {
            if (IsCheckingUpdate)
                return;

            IsCheckingUpdate = true;
            UpdateStatusText = string.Empty;

            try
            {
                if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
                {
                    _httpClient.DefaultRequestHeaders.UserAgent.Add(
                        new ProductInfoHeaderValue("AltPowerPlan", "1.0")
                    );
                }

                string releaseUrl = $"https://api.github.com/repos/{GITHUB_REPO_OWNER}/{GITHUB_REPO_NAME}/releases/latest";
                using var response = await _httpClient.GetAsync(releaseUrl);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var release = JsonSerializer.Deserialize<GitHubReleaseDto>(json);

                    if (release != null && !string.IsNullOrWhiteSpace(release.TagName))
                    {
                        var tag = release.TagName.TrimStart('v', 'V');
                        var tagBase = tag.Split('-')[0];
                        var currentBase = AppVersion.TrimStart('v', 'V').Split('-')[0];

                        if (Version.TryParse(tagBase, out var latestVer) &&
                            Version.TryParse(currentBase, out var currentVer))
                        {
                            var currentNormalized = new Version(Math.Max(0, currentVer.Major), Math.Max(0, currentVer.Minor), Math.Max(0, currentVer.Build));
                            var latestNormalized = new Version(Math.Max(0, latestVer.Major), Math.Max(0, latestVer.Minor), Math.Max(0, latestVer.Build));

                            if (latestNormalized > currentNormalized)
                            {
                                IsUpdateAvailable = true;
                                LatestVersion = release.TagName;
                                ReleaseUrl = release.HtmlUrl ?? ReleaseUrl;
                                UpdateStatusText = $"{_translationService.GetString("AboutUpdateAvailable")}: {release.TagName}";
                                return;
                            }
                        }
                    }
                }

                IsUpdateAvailable = false;
                UpdateStatusText = _translationService.GetString("AboutUpToDate");
            }
            catch
            {
                UpdateStatusText = string.Empty;
            }
            finally
            {
                IsCheckingUpdate = false;
            }
        }

        public static string ResolveAppVersion(Assembly? assembly = null)
        {
            var asm = assembly ?? Assembly.GetExecutingAssembly();

            var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(infoVer))
            {
                string semVer = infoVer.Split('+')[0].Trim();
                if (!string.IsNullOrEmpty(semVer))
                {
                    return semVer.StartsWith('v') || semVer.StartsWith('V') ? semVer : $"v{semVer}";
                }
            }

            var ver = asm.GetName().Version;
            if (ver != null)
            {
                int build = Math.Max(0, ver.Build);
                return $"v{ver.Major}.{ver.Minor}.{build}";
            }

            return "v1.0.0";
        }

        private class GitHubReleaseDto
        {
            [JsonPropertyName("tag_name")]
            public string? TagName { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("html_url")]
            public string? HtmlUrl { get; set; }
        }

        private class GitHubContributorDto
        {
            [JsonPropertyName("login")]
            public string? Login { get; set; }

            [JsonPropertyName("avatar_url")]
            public string? AvatarUrl { get; set; }

            [JsonPropertyName("html_url")]
            public string? HtmlUrl { get; set; }

            [JsonPropertyName("contributions")]
            public int Contributions { get; set; }
        }

        private class GitHubUserDto
        {
            [JsonPropertyName("login")]
            public string? Login { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("avatar_url")]
            public string? AvatarUrl { get; set; }

            [JsonPropertyName("html_url")]
            public string? HtmlUrl { get; set; }

            [JsonPropertyName("bio")]
            public string? Bio { get; set; }
        }
    }
}
