using System;
using System.IO;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;

namespace AltPowerPlan.Pipeline.Settings
{
    public class PipelineSettings
    {
        public string RepoRoot { get; }
        public string BuildDir { get; }
        public string SolutionPath { get; }
        public string AppProjectPath { get; }
        public string TestsProjectPath { get; }
        public string Configuration { get; set; } = "Release";
        public string Version { get; set; } = "1.0.0";
        public bool SkipTests { get; set; }
        public bool SkipInno { get; set; }
        public bool SkipZip { get; set; }
        public bool Clean { get; set; }

        public PipelineSettings(IConfiguration? configuration = null)
        {
            // Discover repo root by searching for AltPowerPlan.slnx
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AltPowerPlan.slnx")))
            {
                dir = dir.Parent;
            }

            RepoRoot = dir?.FullName ?? Directory.GetCurrentDirectory();
            BuildDir = Path.Combine(RepoRoot, "Build");
            SolutionPath = Path.Combine(RepoRoot, "AltPowerPlan.slnx");
            AppProjectPath = Path.Combine(RepoRoot, "AltPowerPlan.App", "AltPowerPlan.App.csproj");
            TestsProjectPath = Path.Combine(RepoRoot, "AltPowerPlan.Tests", "AltPowerPlan.Tests.csproj");

            // Extract version from Directory.Build.props
            var propsPath = Path.Combine(RepoRoot, "Directory.Build.props");
            if (File.Exists(propsPath))
            {
                try
                {
                    var doc = XDocument.Load(propsPath);
                    var prefix = doc.Descendants("VersionPrefix").FirstOrDefault()?.Value?.Trim();
                    var suffix = doc.Descendants("VersionSuffix").FirstOrDefault()?.Value?.Trim();

                    if (!string.IsNullOrEmpty(prefix))
                    {
                        Version = !string.IsNullOrEmpty(suffix) ? $"{prefix}-{suffix}" : prefix;
                    }
                }
                catch
                {
                    // Fallback to default
                }
            }

            // Command-line or environment configuration overrides
            if (configuration != null)
            {
                var cfg = configuration["configuration"] ?? configuration["Configuration"];
                if (!string.IsNullOrEmpty(cfg)) Configuration = cfg;

                var ver = configuration["version"] ?? configuration["Version"];
                if (!string.IsNullOrEmpty(ver)) Version = ver.TrimStart('v');

                if (bool.TryParse(configuration["skip-tests"] ?? configuration["SkipTests"], out var skipTests))
                    SkipTests = skipTests;

                if (bool.TryParse(configuration["skip-inno"] ?? configuration["SkipInno"], out var skipInno))
                    SkipInno = skipInno;

                if (bool.TryParse(configuration["skip-zip"] ?? configuration["SkipZip"], out var skipZip))
                    SkipZip = skipZip;

                if (bool.TryParse(configuration["clean"] ?? configuration["Clean"], out var clean))
                    Clean = clean;
            }
        }

        public static PipelineSettings FromArgs(string[] args, IConfiguration? configuration = null)
        {
            var settings = new PipelineSettings(configuration);
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.Equals("--skip-tests", StringComparison.OrdinalIgnoreCase) || arg.Equals("-SkipTests", StringComparison.OrdinalIgnoreCase))
                {
                    settings.SkipTests = true;
                }
                else if (arg.Equals("--skip-inno", StringComparison.OrdinalIgnoreCase) || arg.Equals("-SkipInno", StringComparison.OrdinalIgnoreCase))
                {
                    settings.SkipInno = true;
                }
                else if (arg.Equals("--skip-zip", StringComparison.OrdinalIgnoreCase) || arg.Equals("-SkipZip", StringComparison.OrdinalIgnoreCase))
                {
                    settings.SkipZip = true;
                }
                else if (arg.Equals("--clean", StringComparison.OrdinalIgnoreCase) || arg.Equals("-Clean", StringComparison.OrdinalIgnoreCase))
                {
                    settings.Clean = true;
                }
                else if ((arg.Equals("--configuration", StringComparison.OrdinalIgnoreCase) || arg.Equals("-c", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    settings.Configuration = args[++i];
                }
                else if ((arg.Equals("--version", StringComparison.OrdinalIgnoreCase) || arg.Equals("-v", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    settings.Version = args[++i].TrimStart('v');
                }
            }
            return settings;
        }
    }
}
