using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AltPowerPlan.Pipeline.Settings;
using Microsoft.Extensions.Logging;
using ModularPipelines.Context;
using ModularPipelines.Modules;

namespace AltPowerPlan.Pipeline.Modules
{
    public class CleanModule : Module<string>
    {
        private readonly PipelineSettings _settings;

        public CleanModule(PipelineSettings settings)
        {
            _settings = settings;
        }

        protected override Task<string?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
        {
            if (_settings.Clean && Directory.Exists(_settings.BuildDir))
            {
                context.Logger.LogInformation("Cleaning existing Build directory: {BuildDir}", _settings.BuildDir);
                Directory.Delete(_settings.BuildDir, recursive: true);
            }

            if (!Directory.Exists(_settings.BuildDir))
            {
                Directory.CreateDirectory(_settings.BuildDir);
            }

            return Task.FromResult<string?>(_settings.BuildDir);
        }
    }
}
