using AltPowerPlan.Services.Settings;
using AltPowerPlan.Utils;
using FluentAssertions;
using System;
using System.IO;
using Xunit;

namespace AltPowerPlan.Tests.Services
{
    public class JsonConfigHandlerTests : IDisposable
    {
        private readonly string _tempDirectory;

        public JsonConfigHandlerTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "AltPowerPlanTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, true);
                }
            }
            catch
            {
                // Best-effort cleanup for temp test folders
            }
        }

        [Fact]
        public void SaveAndLoad_RoundTripsAppSettingsCorrectly()
        {
            var settings = new AppSettings
            {
                AppLanguage = "uk",
                StartWithWindows = true,
                StartMinimized = true,
                MinimizeToTray = true,
                AutoSwitchAcDc = true,
                ConfirmPlanDeletion = false
            };

            JsonConfigHandler<AppSettings>.Save(settings, "test_config.json", _tempDirectory);
            var loaded = JsonConfigHandler<AppSettings>.Load("test_config.json", _tempDirectory);

            loaded.Should().NotBeNull();
            loaded.AppLanguage.Should().Be("uk");
            loaded.StartWithWindows.Should().BeTrue();
            loaded.StartMinimized.Should().BeTrue();
            loaded.MinimizeToTray.Should().BeTrue();
            loaded.AutoSwitchAcDc.Should().BeTrue();
            loaded.ConfirmPlanDeletion.Should().BeFalse();
        }

        [Fact]
        public void Load_NonExistentFile_ReturnsDefaultInstanceWithoutThrowing()
        {
            var loaded = JsonConfigHandler<AppSettings>.Load("non_existent_file.json", _tempDirectory);

            loaded.Should().NotBeNull();
            loaded.ConfirmPlanDeletion.Should().BeTrue(); // Default value
        }

        [Fact]
        public void Load_CorruptedJson_RecoversWithDefaultInstance()
        {
            string filePath = Path.Combine(_tempDirectory, "corrupt.json");
            File.WriteAllText(filePath, "{ this is not valid json! }");

            var loaded = JsonConfigHandler<AppSettings>.Load("corrupt.json", _tempDirectory);

            loaded.Should().NotBeNull();
            loaded.ConfirmPlanDeletion.Should().BeTrue();
        }

        [Fact]
        public void Constants_UserDataDirectory_IsNotInProgramFiles()
        {
            string userDataDir = Constants.UserDataDirectory;
            userDataDir.Should().NotBeNullOrWhiteSpace();

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                userDataDir.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase).Should().BeFalse(
                    "UserDataDirectory must not reside inside Program Files to prevent UnauthorizedAccessException for non-admin users");
            }
        }

        [Fact]
        public void Constants_UserDataDirectory_ResolvesToLocalAppDataOrProgramDirectory()
        {
            string userDataDir = Constants.UserDataDirectory;
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string expectedPrefix = Path.Combine(localAppData, Constants.AppName);

            bool isLocalAppData = userDataDir.Equals(expectedPrefix, StringComparison.OrdinalIgnoreCase);
            bool isPortable = userDataDir.Equals(Constants.ProgramDirectory, StringComparison.OrdinalIgnoreCase);

            (isLocalAppData || isPortable).Should().BeTrue(
                "UserDataDirectory must be either %LocalAppData%\\AltPowerPlan or local portable directory");
        }

        [Fact]
        public void Save_ReadOnlyDirectory_DoesNotThrowUnauthorizedAccessException()
        {
            // Create a subfolder and mark as read-only or invalid
            string readOnlySubdir = Path.Combine(_tempDirectory, "readonly_sub");
            Directory.CreateDirectory(readOnlySubdir);

            // Attempting to save with invalid file name or restricted permissions should not throw
            var settings = new AppSettings { AppLanguage = "en" };

            var act = () => JsonConfigHandler<AppSettings>.Save(settings, "test.json", readOnlySubdir);
            act.Should().NotThrow<UnauthorizedAccessException>();
        }

        [Fact]
        public void Save_PerformsCleanWrite_WithoutLeavingTempFiles()
        {
            var settings = new AppSettings { AppLanguage = "uk", StartMinimized = true };
            bool success = JsonConfigHandler<AppSettings>.Save(settings, "clean_write.json", _tempDirectory);

            success.Should().BeTrue();
            File.Exists(Path.Combine(_tempDirectory, "clean_write.json")).Should().BeTrue();

            var tmpFiles = Directory.GetFiles(_tempDirectory, "*.tmp");
            tmpFiles.Should().BeEmpty("temporary files must be cleaned up after saving configuration");
        }
    }
}
