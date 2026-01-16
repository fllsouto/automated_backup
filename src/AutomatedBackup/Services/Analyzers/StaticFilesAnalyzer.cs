using AutomatedBackup.Models;

namespace AutomatedBackup.Services.Analyzers;

/// <summary>
/// Analyzes filesystem for old files, large files, and archival candidates
/// </summary>
public class StaticFilesAnalyzer : IInsightAnalyzer
{
    private readonly int _daysUntilOld;
    private readonly long _largeFileSizeThreshold;

    public string Name => "Static Files";

    public bool IsAvailable => true;

    public StaticFilesAnalyzer(int daysUntilOld = 180, long largeFileSizeMB = 500)
    {
        _daysUntilOld = daysUntilOld;
        _largeFileSizeThreshold = largeFileSizeMB * 1024 * 1024;
    }

    public async Task<IEnumerable<Insight>> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        var insights = new List<Insight>();

        await Task.Run(() =>
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // Check Downloads folder
            var downloads = Path.Combine(userProfile, "Downloads");
            if (Directory.Exists(downloads))
            {
                AnalyzeDownloads(downloads, insights, cancellationToken);
            }

            // Check for large media files in common locations
            var mediaLocations = new[]
            {
                Path.Combine(userProfile, "Videos"),
                Path.Combine(userProfile, "Documents"),
                Path.Combine(userProfile, "Desktop")
            };

            foreach (var location in mediaLocations)
            {
                if (Directory.Exists(location))
                {
                    FindLargeFiles(location, insights, cancellationToken, maxDepth: 3);
                }
            }

            // Check for ISO files anywhere in user profile (common backup candidates)
            FindFilesByExtension(userProfile, new[] { ".iso", ".img", ".vhd", ".vmdk" }, insights, cancellationToken, maxDepth: 4);

        }, cancellationToken);

        return insights;
    }

    private void AnalyzeDownloads(string path, List<Insight> insights, CancellationToken ct)
    {
        try
        {
            var cutoffDate = DateTime.Now.AddDays(-_daysUntilOld);
            var oldFiles = new List<FileInfo>();
            long totalOldSize = 0;

            foreach (var file in Directory.GetFiles(path))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var fi = new FileInfo(file);
                    if (fi.LastAccessTime < cutoffDate)
                    {
                        oldFiles.Add(fi);
                        totalOldSize += fi.Length;
                    }
                }
                catch { }
            }

            if (totalOldSize > 100 * 1024 * 1024) // > 100MB of old downloads
            {
                insights.Add(new Insight(
                    InsightType.OldFiles,
                    $"Downloads: {oldFiles.Count} old files ({FormatSize(totalOldSize)})",
                    path,
                    totalOldSize,
                    RecommendedAction.Archive
                ));
            }

            // Also check for installer files
            var installerExtensions = new[] { ".exe", ".msi", ".msix" };
            var installers = Directory.GetFiles(path)
                .Where(f => installerExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Select(f => new FileInfo(f))
                .Where(fi => fi.Length > 50 * 1024 * 1024) // > 50MB installers
                .ToList();

            if (installers.Any())
            {
                var totalInstallerSize = installers.Sum(f => f.Length);
                insights.Add(new Insight(
                    InsightType.LargeFiles,
                    $"Downloads: {installers.Count} large installers ({FormatSize(totalInstallerSize)})",
                    path,
                    totalInstallerSize,
                    RecommendedAction.Review
                ));
            }
        }
        catch { }
    }

    private void FindLargeFiles(string path, List<Insight> insights, CancellationToken ct, int maxDepth, int currentDepth = 0)
    {
        if (currentDepth > maxDepth)
            return;

        try
        {
            // Check files in current directory
            foreach (var file in Directory.GetFiles(path))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var fi = new FileInfo(file);
                    if (fi.Length > _largeFileSizeThreshold)
                    {
                        var extension = fi.Extension.ToLowerInvariant();
                        var isMediaFile = new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".zip", ".rar", ".7z" }.Contains(extension);

                        insights.Add(new Insight(
                            InsightType.LargeFiles,
                            $"Large file: {fi.Name} ({FormatSize(fi.Length)})",
                            fi.FullName,
                            fi.Length,
                            isMediaFile ? RecommendedAction.Archive : RecommendedAction.Review
                        ));
                    }
                }
                catch { }
            }

            // Recurse into subdirectories
            foreach (var dir in Directory.GetDirectories(path))
            {
                ct.ThrowIfCancellationRequested();
                var dirName = Path.GetFileName(dir);

                // Skip hidden and system directories
                if (dirName.StartsWith(".") || dirName.StartsWith("$"))
                    continue;

                FindLargeFiles(dir, insights, ct, maxDepth, currentDepth + 1);
            }
        }
        catch { }
    }

    private void FindFilesByExtension(string path, string[] extensions, List<Insight> insights, CancellationToken ct, int maxDepth, int currentDepth = 0)
    {
        if (currentDepth > maxDepth)
            return;

        try
        {
            foreach (var file in Directory.GetFiles(path))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var extension = Path.GetExtension(file).ToLowerInvariant();
                    if (extensions.Contains(extension))
                    {
                        var fi = new FileInfo(file);
                        insights.Add(new Insight(
                            InsightType.LargeFiles,
                            $"Disk image: {fi.Name} ({FormatSize(fi.Length)})",
                            fi.FullName,
                            fi.Length,
                            RecommendedAction.Archive
                        ));
                    }
                }
                catch { }
            }

            foreach (var dir in Directory.GetDirectories(path))
            {
                ct.ThrowIfCancellationRequested();
                var dirName = Path.GetFileName(dir);

                if (dirName.StartsWith(".") || dirName.StartsWith("$") || dirName == "AppData")
                    continue;

                FindFilesByExtension(dir, extensions, insights, ct, maxDepth, currentDepth + 1);
            }
        }
        catch { }
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
