using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AltPowerPlan.Pipeline.Settings;
using AltPowerPlan.Pipeline.Utils;
using Microsoft.Extensions.Logging;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Modules;

namespace AltPowerPlan.Pipeline.Modules
{
    [DependsOn<PublishWinX64Module>]
    [DependsOn<PublishWinArm64Module>]
    public class InnoSetupModule : Module<bool>
    {
        private readonly PipelineSettings _settings;

        public InnoSetupModule(PipelineSettings settings)
        {
            _settings = settings;
        }

        protected override async Task<bool> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
        {
            if (_settings.SkipInno)
            {
                context.Logger.LogInformation("Skipping Inno Setup packaging (-SkipInno specified).");
                return false;
            }

            var isccPath = ToolLocator.FindInnoSetupCompiler();
            var issScript = Path.Combine(_settings.RepoRoot, "installer", "setup.iss");

            if (string.IsNullOrEmpty(isccPath) || !File.Exists(issScript))
            {
                context.Logger.LogInformation("Inno Setup Compiler (ISCC.exe) not found. Skipping .exe installer generation.");
                return false;
            }

            var x64Result = await context.GetModule<PublishWinX64Module>();
            var arm64Result = await context.GetModule<PublishWinArm64Module>();

            context.Logger.LogInformation("Compiling Windows Inno Setup installers using: {Iscc}", isccPath);

            // 1. Standard x64 Installer (Framework-Dependent)
            if (x64Result?.ValueOrDefault != null)
            {
                await RunIsccAsync(isccPath, new[]
                {
                    $"/DAppVersion={_settings.Version}",
                    $"/DSourceDir={x64Result.ValueOrDefault.PortableDir}",
                    $"/O{_settings.BuildDir}",
                    issScript
                }, context.Logger, cancellationToken);
                context.Logger.LogInformation("Standard Inno Setup installer compiled.");

                // 2. Standalone x64 Installer
                await RunIsccAsync(isccPath, new[]
                {
                    $"/DAppVersion={_settings.Version}",
                    $"/DSourceDir={x64Result.ValueOrDefault.StandaloneDir}",
                    "/DStandalone=1",
                    $"/DOutputBaseFilename=AltPowerPlan-v{_settings.Version}-Setup-Standalone",
                    $"/O{_settings.BuildDir}",
                    issScript
                }, context.Logger, cancellationToken);
                context.Logger.LogInformation("Standalone x64 Inno Setup installer compiled.");
            }

            // 3. Standalone ARM64 Installer
            if (arm64Result?.ValueOrDefault != null)
            {
                await RunIsccAsync(isccPath, new[]
                {
                    $"/DAppVersion={_settings.Version}",
                    $"/DSourceDir={arm64Result.ValueOrDefault.StandaloneDir}",
                    "/DStandalone=1",
                    $"/DOutputBaseFilename=AltPowerPlan-v{_settings.Version}-win-arm64-Setup-Standalone",
                    $"/O{_settings.BuildDir}",
                    issScript
                }, context.Logger, cancellationToken);
                context.Logger.LogInformation("Standalone ARM64 Inno Setup installer compiled.");
            }


            return true;
        }

        private static async Task RunIsccAsync(string isccPath, string[] args, ILogger logger, CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo(isccPath)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = new Process { StartInfo = psi };

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    logger.LogInformation("[ISCC] {Line}", e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    logger.LogWarning("[ISCC Error] {Line}", e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                logger.LogError("ISCC compiler failed with exit code {Code}", process.ExitCode);
                throw new System.InvalidOperationException($"ISCC compiler failed with exit code {process.ExitCode}");
            }
        }
    }
}
