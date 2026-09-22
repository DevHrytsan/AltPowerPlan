using System.Threading;
using System.Threading.Tasks;
using AltPowerPlan.Pipeline.Settings;
using Microsoft.Extensions.Logging;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Models;
using ModularPipelines.Modules;

namespace AltPowerPlan.Pipeline.Modules
{
    [DependsOn<RestoreDependenciesModule>]
    public class RunTestsModule : Module<CommandResult>
    {
        private readonly PipelineSettings _settings;

        public RunTestsModule(PipelineSettings settings)
        {
            _settings = settings;
        }

        protected override async Task<CommandResult?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
        {
            if (_settings.SkipTests)
            {
                context.Logger.LogInformation("Skipping unit tests (-SkipTests specified).");
                return null;
            }

            context.Logger.LogInformation("Executing test suite ({Config}) for {Project}", _settings.Configuration, _settings.TestsProjectPath);

            var options = new DotNetTestOptions
            {
                Configuration = _settings.Configuration,
                NoRestore = true,
                Arguments = new[] { _settings.TestsProjectPath }
            };


            return await context.DotNet().Test(options, cancellationToken: cancellationToken);
        }
    }
}

