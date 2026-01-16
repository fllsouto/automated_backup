using AutomatedBackup.Models;

namespace AutomatedBackup.Services.Analyzers;

/// <summary>
/// Interface for insight analyzers that detect cleanup/backup opportunities
/// </summary>
public interface IInsightAnalyzer
{
    /// <summary>
    /// Display name for this analyzer
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether this analyzer is available on the current system
    /// (e.g., Docker analyzer requires Docker to be installed)
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Analyze the system and return insights
    /// </summary>
    Task<IEnumerable<Insight>> AnalyzeAsync(CancellationToken cancellationToken = default);
}
