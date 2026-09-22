using System;
using System.Threading.Tasks;
using AltPowerPlan.Services.Translation;
using AltPowerPlan.ViewModels.Pages;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AltPowerPlan.Tests.ViewModels
{
    public class AboutViewModelTests
    {
        private readonly ITranslationService _translationService;

        public AboutViewModelTests()
        {
            _translationService = Substitute.For<ITranslationService>();
            _translationService.GetString(Arg.Any<string>()).Returns(call => call.Arg<string>());
        }

        [Fact]
        public void Constructor_InitializesAppVersionAndAuthor()
        {
            var vm = new AboutViewModel(_translationService);

            vm.AppVersion.Should().NotBeNullOrWhiteSpace();
            vm.Author.Should().NotBeNull();
            vm.Author.Login.Should().Be("DevHrytsan");
            vm.IsUpdateAvailable.Should().BeFalse();
            vm.IsCheckingUpdate.Should().BeFalse();
        }

        [Fact]
        public async Task CheckForUpdatesAsync_WhenExecuted_DoesNotThrow()
        {
            var vm = new AboutViewModel(_translationService);

            Func<Task> act = async () => await vm.CheckForUpdatesCommand.ExecuteAsync(null);

            await act.Should().NotThrowAsync();
            vm.IsCheckingUpdate.Should().BeFalse();
        }

        [Fact]
        public void ResolveAppVersion_ReturnsVersionStartingWithV()
        {
            var version = AboutViewModel.ResolveAppVersion();

            version.Should().NotBeNullOrWhiteSpace();
            version.Should().StartWith("v");
        }

        [Fact]
        public void ResolveAppVersion_WhenProvidedAssembly_ReturnsCorrectVersion()
        {
            var asm = typeof(AboutViewModel).Assembly;
            var version = AboutViewModel.ResolveAppVersion(asm);

            version.Should().NotBeNullOrWhiteSpace();
            version.Should().StartWith("v");
        }
    }
}
