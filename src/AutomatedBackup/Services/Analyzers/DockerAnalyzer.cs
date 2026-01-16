using System.Diagnostics;
using System.Text.RegularExpressions;
using AutomatedBackup.Models;

namespace AutomatedBackup.Services.Analyzers;

/// <summary>
/// Analyzes Docker disk usage using the Docker CLI
/// </summary>
public class DockerAnalyzer : IInsightAnalyzer
{
    public string Name => "Docker";

    public bool IsAvailable => CheckDockerAvailable();

    private bool CheckDockerAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo("docker", "version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IEnumerable<Insight>> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        var insights = new List<Insight>();

        if (!IsAvailable)
            return insights;

        // Get Docker system disk usage
        var dfOutput = await RunDockerCommandAsync("system df -v", cancellationToken);
        if (string.IsNullOrEmpty(dfOutput))
            return insights;

        // Parse images
        insights.AddRange(ParseImages(dfOutput));

        // Parse containers
        insights.AddRange(ParseContainers(dfOutput));

        // Parse volumes
        insights.AddRange(ParseVolumes(dfOutput));

        // Check for dangling images
        var danglingOutput = await RunDockerCommandAsync("images -f dangling=true -q", cancellationToken);
        if (!string.IsNullOrWhiteSpace(danglingOutput))
        {
            var danglingCount = danglingOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            if (danglingCount > 0)
            {
                insights.Add(new Insight(
                    InsightType.DockerImages,
                    $"{danglingCount} dangling image(s) can be removed",
                    "docker images",
                    0, // Size calculated separately
                    RecommendedAction.Clean,
                    "docker image prune -f"
                ));
            }
        }

        return insights;
    }

    private async Task<string> RunDockerCommandAsync(string arguments, CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo("docker", arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return string.Empty;

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            return process.ExitCode == 0 ? output : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private IEnumerable<Insight> ParseImages(string dfOutput)
    {
        var insights = new List<Insight>();

        // Look for the Images section and parse reclaimable space
        var match = Regex.Match(dfOutput, @"Images\s+\d+\s+\d+\s+([\d.]+[KMGT]?B)\s+([\d.]+[KMGT]?B)\s+\((\d+)%\)");
        if (match.Success)
        {
            var reclaimable = ParseSize(match.Groups[2].Value);
            var percentage = int.Parse(match.Groups[3].Value);

            if (reclaimable > 0 && percentage > 0)
            {
                insights.Add(new Insight(
                    InsightType.DockerImages,
                    $"Docker images: {match.Groups[2].Value} reclaimable ({percentage}%)",
                    "docker images",
                    reclaimable,
                    RecommendedAction.Review,
                    "docker image prune -a"
                ));
            }
        }

        return insights;
    }

    private IEnumerable<Insight> ParseContainers(string dfOutput)
    {
        var insights = new List<Insight>();

        var match = Regex.Match(dfOutput, @"Containers\s+\d+\s+\d+\s+([\d.]+[KMGT]?B)\s+([\d.]+[KMGT]?B)");
        if (match.Success)
        {
            var reclaimable = ParseSize(match.Groups[2].Value);
            if (reclaimable > 0)
            {
                insights.Add(new Insight(
                    InsightType.DockerContainers,
                    $"Stopped containers: {match.Groups[2].Value} reclaimable",
                    "docker ps -a",
                    reclaimable,
                    RecommendedAction.Clean,
                    "docker container prune -f"
                ));
            }
        }

        return insights;
    }

    private IEnumerable<Insight> ParseVolumes(string dfOutput)
    {
        var insights = new List<Insight>();

        var match = Regex.Match(dfOutput, @"Local Volumes\s+\d+\s+\d+\s+([\d.]+[KMGT]?B)\s+([\d.]+[KMGT]?B)");
        if (match.Success)
        {
            var reclaimable = ParseSize(match.Groups[2].Value);
            if (reclaimable > 0)
            {
                insights.Add(new Insight(
                    InsightType.DockerVolumes,
                    $"Unused volumes: {match.Groups[2].Value} reclaimable",
                    "docker volume ls",
                    reclaimable,
                    RecommendedAction.Review,
                    "docker volume prune -f"
                ));
            }
        }

        return insights;
    }

    private long ParseSize(string sizeStr)
    {
        var match = Regex.Match(sizeStr, @"([\d.]+)\s*([KMGT]?)B?", RegexOptions.IgnoreCase);
        if (!match.Success) return 0;

        var value = double.Parse(match.Groups[1].Value);
        var unit = match.Groups[2].Value.ToUpperInvariant();

        return unit switch
        {
            "K" => (long)(value * 1024),
            "M" => (long)(value * 1024 * 1024),
            "G" => (long)(value * 1024 * 1024 * 1024),
            "T" => (long)(value * 1024 * 1024 * 1024 * 1024),
            _ => (long)value
        };
    }
}
