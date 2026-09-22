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
    [DependsOn<CleanModule>]
    public class RestoreDependenciesModule : Module<CommandResult>
    {
        private readonly PipelineSettings _settings;

        public RestoreDependenciesModule(PipelineSettings settings)
        {
            _settings = settings;
        }

        protected override async Task<CommandResult?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
        {
            context.Logger.LogInformation("Restoring NuGet dependencies for {Solution}", _settings.SolutionPath);

            var options = new DotNetRestoreOptions
            {
                ProjectSolution = _settings.SolutionPath
            };

            return await context.DotNet().Restore(options, cancellationToken: cancellationToken);
        }
    }
}

