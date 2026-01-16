using AutomatedBackup.Models;

namespace AutomatedBackup.Services.Analyzers;

/// <summary>
/// Analyzes WSL2 distributions and their VHDX disk usage
/// </summary>
public class WSL2Analyzer : IInsightAnalyzer
{
    public string Name => "WSL2";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public async Task<IEnumerable<Insight>> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        var insights = new List<Insight>();

        if (!IsAvailable)
            return insights;

        // WSL2 distributions are stored in %LOCALAPPDATA%\Packages
        var packagesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages"
        );

        if (!Directory.Exists(packagesPath))
            return insights;

        await Task.Run(() =>
        {
            // Look for WSL distribution folders
            var wslPatterns = new[]
            {
                "CanonicalGroupLimited.Ubuntu*",
                "TheDebianProject.DebianGNULinux*",
                "*WSL*",
                "*Linux*"
            };

            foreach (var pattern in wslPatterns)
            {
                try
                {
                    var matchingDirs = Directory.GetDirectories(packagesPath, pattern);
                    foreach (var dir in matchingDirs)
                    {
                        var localStatePath = Path.Combine(dir, "LocalState");
                        if (Directory.Exists(localStatePath))
                        {
                            var vhdxFiles = Directory.GetFiles(localStatePath, "*.vhdx");
                            foreach (var vhdx in vhdxFiles)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                var fileInfo = new FileInfo(vhdx);
                                var distroName = Path.GetFileName(dir);

                                // Extract a cleaner name
                                var cleanName = distroName;
                                if (cleanName.Contains("Ubuntu"))
                                    cleanName = "Ubuntu";
                                else if (cleanName.Contains("Debian"))
                                    cleanName = "Debian";

                                insights.Add(new Insight(
                                    InsightType.WSL2Distribution,
                                    $"WSL2 {cleanName}: {FormatSize(fileInfo.Length)}",
                                    vhdx,
                                    fileInfo.Length,
                                    RecommendedAction.Review,
                                    $"wsl --unregister {cleanName}" // Dangerous - mark as review
                                ));
                            }
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip inaccessible directories
                }
            }

            // Also check Docker Desktop WSL2 backend
            var dockerWslPath = Path.Combine(packagesPath, "Docker.Docker_*");
            try
            {
                var dockerDirs = Directory.GetDirectories(packagesPath, "Docker*");
                foreach (var dir in dockerDirs)
                {
                    var dataPath = Path.Combine(dir, "LocalState");
                    if (Directory.Exists(dataPath))
                    {
                        var vhdxFiles = Directory.GetFiles(dataPath, "*.vhdx", SearchOption.AllDirectories);
                        foreach (var vhdx in vhdxFiles)
                        {
                            var fileInfo = new FileInfo(vhdx);
                            insights.Add(new Insight(
                                InsightType.WSL2Distribution,
                                $"Docker Desktop WSL2 data: {FormatSize(fileInfo.Length)}",
                                vhdx,
                                fileInfo.Length,
                                RecommendedAction.Review
                            ));
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip
            }

        }, cancellationToken);

        return insights;
    }

    private static string FormatSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:F2} {suffixes[suffixIndex]}";
    }
}
