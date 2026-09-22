using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AltPowerPlan.Pipeline.Models;
using AltPowerPlan.Pipeline.Settings;
using Microsoft.Extensions.Logging;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Modules;

namespace AltPowerPlan.Pipeline.Modules
{
    [DependsOn<RestoreDependenciesModule>]
    public class PublishWinX64Module : Module<PublishResult>
    {
        private readonly PipelineSettings _settings;

        public PublishWinX64Module(PipelineSettings settings)
        {
            _settings = settings;
        }

        protected override async Task<PublishResult?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
        {
            const string rid = "win-x64";
            context.Logger.LogInformation("Publishing binaries for {Rid}...", rid);

            var standaloneDir = Path.Combine(_settings.BuildDir, $"AltPowerPlan-v{_settings.Version}-{rid}-standalone");
            var portableDir = Path.Combine(_settings.BuildDir, $"AltPowerPlan-v{_settings.Version}-{rid}-portable");

            // 1. Standalone (Self-Contained single file)
            var standaloneOptions = new DotNetPublishOptions
            {
                ProjectSolution = _settings.AppProjectPath,
                Configuration = _settings.Configuration,
                Runtime = rid,
                Output = standaloneDir,
                Arguments = new[]
                {
                    "--self-contained", "true",
                    "-p:PublishSingleFile=true",
                    "-p:EnableCompressionInSingleFile=true",
                    $"-p:Version={_settings.Version}"
                }
            };
            await context.DotNet().Publish(standaloneOptions, cancellationToken: cancellationToken);
            context.Logger.LogInformation("Standalone {Rid} generated at {Dir}", rid, standaloneDir);

            // 2. Portable (Framework-Dependent)
            var portableOptions = new DotNetPublishOptions
            {
                ProjectSolution = _settings.AppProjectPath,
                Configuration = _settings.Configuration,
                Runtime = rid,
                Output = portableDir,
                Arguments = new[]
                {
                    "--self-contained", "false",
                    $"-p:Version={_settings.Version}"
                }
            };
            await context.DotNet().Publish(portableOptions, cancellationToken: cancellationToken);


            // Inject portable.dat marker
            File.WriteAllText(Path.Combine(portableDir, "portable.dat"), string.Empty);
            context.Logger.LogInformation("Portable {Rid} generated at {Dir} (with portable.dat)", rid, portableDir);

            return new PublishResult(rid, standaloneDir, portableDir);
        }
    }
}
