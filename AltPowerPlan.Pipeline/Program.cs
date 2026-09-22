using System;
using System.Threading.Tasks;
using AltPowerPlan.Pipeline.Modules;
using AltPowerPlan.Pipeline.Settings;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.Enums;
using ModularPipelines.Extensions;

namespace AltPowerPlan.Pipeline
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var settings = PipelineSettings.FromArgs(args);

            var builder = ModularPipelines.Pipeline.CreateBuilder(args);

            builder.Services.AddSingleton(settings);

            // Configure pipeline execution options for clean, non-interactive CI/CD runs
            builder.Options.ShowProgressInConsole = false;
            builder.Options.PrintLogo = false;
            builder.Options.ThrowOnPipelineFailure = false;

            // Register build and packaging modules
            builder.AddModule<CleanModule>()
                   .AddModule<RestoreDependenciesModule>()
                   .AddModule<RunTestsModule>()
                   .AddModule<PublishWinX64Module>()
                   .AddModule<PublishWinArm64Module>()
                   .AddModule<InnoSetupModule>()
                   .AddModule<ZipArchivesModule>()
                   .AddModule<GenerateChecksumsModule>();


            var pipeline = await builder.BuildAsync();
            var summary = await pipeline.RunAsync();

            return summary.Status == Status.Successful ? 0 : 1;
        }
    }
}
