using AutomatedBackup.Models;
using AutomatedBackup.Services.Analyzers;

namespace AutomatedBackup.Services;

/// <summary>
/// Aggregates insights from all available analyzers
/// </summary>
public class InsightAggregator
{
    private readonly List<IInsightAnalyzer> _analyzers;

    public InsightAggregator()
    {
        _analyzers = new List<IInsightAnalyzer>
        {
            new DockerAnalyzer(),
            new WSL2Analyzer(),
            new NodeModulesAnalyzer(),
            new PackageCacheAnalyzer(),
            new IDECacheAnalyzer(),
            new StaticFilesAnalyzer()
        };
    }

    /// <summary>
    /// Get all available analyzers
    /// </summary>
    public IReadOnlyList<IInsightAnalyzer> Analyzers => _analyzers.AsReadOnly();

    /// <summary>
    /// Run all analyzers and collect insights
    /// </summary>
    public async Task<AnalysisResult> AnalyzeAllAsync(
        IProgress<AnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var allInsights = new List<Insight>();
        var analyzerResults = new Dictionary<string, IEnumerable<Insight>>();
        var errors = new List<string>();

        var availableAnalyzers = _analyzers.Where(a => a.IsAvailable).ToList();
        var totalAnalyzers = availableAnalyzers.Count;
        var completed = 0;

        foreach (var analyzer in availableAnalyzers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new AnalysisProgress(
                analyzer.Name,
                completed,
                totalAnalyzers
            ));

            try
            {
                var insights = await analyzer.AnalyzeAsync(cancellationToken);
                var insightList = insights.ToList();

                analyzerResults[analyzer.Name] = insightList;
                allInsights.AddRange(insightList);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add($"{analyzer.Name}: {ex.Message}");
            }

            completed++;
        }

        progress?.Report(new AnalysisProgress("Complete", totalAnalyzers, totalAnalyzers));

        return new AnalysisResult(allInsights, analyzerResults, errors);
    }

    /// <summary>
    /// Group insights by their file path location (drive/folder)
    /// </summary>
    public static Dictionary<string, List<Insight>> GroupByLocation(IEnumerable<Insight> insights)
    {
        var grouped = new Dictionary<string, List<Insight>>();

        foreach (var insight in insights)
        {
            // Extract the drive or root folder
            var location = GetLocationKey(insight.Path);

            if (!grouped.ContainsKey(location))
            {
                grouped[location] = new List<Insight>();
            }

            grouped[location].Add(insight);
        }

        // Sort each group by size descending
        foreach (var key in grouped.Keys)
        {
            grouped[key] = grouped[key].OrderByDescending(i => i.SizeInBytes).ToList();
        }

        return grouped;
    }

    private static string GetLocationKey(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "Unknown";

        // Check if it's a file path
        if (path.Length >= 2 && path[1] == ':')
        {
            // Windows path - extract drive letter
            var drive = path.Substring(0, 2);

            // Try to get a meaningful subfolder
            var parts = path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return $"{drive}\\{parts[1]}";
            }

            return drive;
        }

        // For non-file paths (like "docker images"), return as-is
        return path;
    }
}

/// <summary>
/// Progress information during analysis
/// </summary>
public record AnalysisProgress(string CurrentAnalyzer, int CompletedCount, int TotalCount)
{
    public int PercentComplete => TotalCount > 0 ? (CompletedCount * 100) / TotalCount : 0;
}

/// <summary>
/// Results from running all analyzers
/// </summary>
public record AnalysisResult(
    IReadOnlyList<Insight> AllInsights,
    IReadOnlyDictionary<string, IEnumerable<Insight>> ByAnalyzer,
    IReadOnlyList<string> Errors
)
{
    public long TotalReclaimableBytes => AllInsights.Sum(i => i.SizeInBytes);

    public int TotalInsightCount => AllInsights.Count;
}
