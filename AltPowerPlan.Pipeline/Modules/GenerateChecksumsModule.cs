using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AltPowerPlan.Pipeline.Settings;
using Microsoft.Extensions.Logging;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Modules;

namespace AltPowerPlan.Pipeline.Modules
{
    [DependsOn<InnoSetupModule>]
    [DependsOn<ZipArchivesModule>]
    public class GenerateChecksumsModule : Module<string>
    {
        private readonly PipelineSettings _settings;

        public GenerateChecksumsModule(PipelineSettings settings)
        {
            _settings = settings;
        }

        protected override async Task<string?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
        {
            if (!Directory.Exists(_settings.BuildDir)) return null;

            var checksumsFile = Path.Combine(_settings.BuildDir, "SHA256SUMS.txt");
            var sb = new StringBuilder();

            var extensions = new[] { ".zip", ".exe" };
            var files = Directory.GetFiles(_settings.BuildDir, "*.*", SearchOption.TopDirectoryOnly);


            int count = 0;
            foreach (var file in files)
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (Array.IndexOf(extensions, ext) >= 0)
                {
                    await using var stream = File.OpenRead(file);
                    var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
                    var hashHex = Convert.ToHexString(hashBytes);
                    var name = Path.GetFileName(file);

                    sb.AppendLine($"{hashHex}  {name}");
                    count++;
                }
            }

            if (count > 0)
            {
                await File.WriteAllTextAsync(checksumsFile, sb.ToString(), Encoding.UTF8, cancellationToken);
                context.Logger.LogInformation("Generated SHA256SUMS.txt for {Count} release artifacts", count);
            }

            return checksumsFile;
        }
    }
}
