namespace AutomatedBackup.Models;

/// <summary>
/// Types of insights the analyzers can generate
/// </summary>
public enum InsightType
{
    DockerImages,
    DockerContainers,
    DockerVolumes,
    WSL2Distribution,
    NodeModules,
    PackageCache,
    IDECache,
    TempFiles,
    OldFiles,
    LargeFiles
}

/// <summary>
/// Recommended action for an insight
/// </summary>
public enum RecommendedAction
{
    Clean,      // Safe to delete, can be regenerated
    Archive,    // Move to external storage
    Review      // User should review before action
}

/// <summary>
/// Represents an actionable insight about disk usage
/// </summary>
public record Insight(
    InsightType Type,
    string Description,
    string Path,
    long SizeInBytes,
    RecommendedAction Action,
    string? CleanupCommand = null  // Optional command to execute for cleanup
);
