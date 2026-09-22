using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using AltPowerPlan.Pipeline.Settings;
using Microsoft.Extensions.Logging;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Modules;

namespace AltPowerPlan.Pipeline.Modules
{
    [DependsOn<PublishWinX64Module>]
    [DependsOn<PublishWinArm64Module>]
    public class ZipArchivesModule : Module<IReadOnlyList<string>>
    {
        private readonly PipelineSettings _settings;

        public ZipArchivesModule(PipelineSettings settings)
        {
            _settings = settings;
        }

        protected override async Task<IReadOnlyList<string>?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
        {
            if (_settings.SkipZip)
            {
                context.Logger.LogInformation("Skipping ZIP creation (-SkipZip specified).");
                return System.Array.Empty<string>();
            }

            var x64Result = await context.GetModule<PublishWinX64Module>();
            var arm64Result = await context.GetModule<PublishWinArm64Module>();

            var createdZips = new List<string>();

            void CompressFolder(string sourceFolder, string destinationZip)
            {
                if (File.Exists(destinationZip)) File.Delete(destinationZip);
                context.Logger.LogInformation("Creating ZIP: {File}...", Path.GetFileName(destinationZip));
                ZipFile.CreateFromDirectory(sourceFolder, destinationZip, CompressionLevel.Optimal, includeBaseDirectory: false);
                createdZips.Add(destinationZip);
            }

            if (x64Result?.ValueOrDefault != null)
            {
                var standaloneZip = Path.Combine(_settings.BuildDir, $"AltPowerPlan-v{_settings.Version}-win-x64-standalone.zip");
                var portableZip = Path.Combine(_settings.BuildDir, $"AltPowerPlan-v{_settings.Version}-win-x64-portable.zip");

                CompressFolder(x64Result.ValueOrDefault.StandaloneDir, standaloneZip);
                CompressFolder(x64Result.ValueOrDefault.PortableDir, portableZip);
            }

            if (arm64Result?.ValueOrDefault != null)
            {
                var standaloneZip = Path.Combine(_settings.BuildDir, $"AltPowerPlan-v{_settings.Version}-win-arm64-standalone.zip");
                var portableZip = Path.Combine(_settings.BuildDir, $"AltPowerPlan-v{_settings.Version}-win-arm64-portable.zip");

                CompressFolder(arm64Result.ValueOrDefault.StandaloneDir, standaloneZip);
                CompressFolder(arm64Result.ValueOrDefault.PortableDir, portableZip);
            }


            return createdZips;
        }
    }
}
