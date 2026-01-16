using AutomatedBackup.Models;

namespace AutomatedBackup.Services.Analyzers;

/// <summary>
/// Analyzes IDE caches and temporary files (Visual Studio, Rider, VS Code)
/// </summary>
public class IDECacheAnalyzer : IInsightAnalyzer
{
    public string Name => "IDE Caches";

    public bool IsAvailable => true;

    public async Task<IEnumerable<Insight>> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        var insights = new List<Insight>();

        await Task.Run(() =>
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // Visual Studio caches
            AnalyzeVisualStudio(localAppData, insights, cancellationToken);

            // JetBrains (Rider, IntelliJ, etc.)
            AnalyzeJetBrains(localAppData, appData, insights, cancellationToken);

            // VS Code
            AnalyzeVSCode(localAppData, appData, userProfile, insights, cancellationToken);

        }, cancellationToken);

        return insights;
    }

    private void AnalyzeVisualStudio(string localAppData, List<Insight> insights, CancellationToken ct)
    {
        // VS Component Model Cache
        var vsVersions = new[] { "17.0", "16.0", "15.0" }; // VS 2022, 2019, 2017
        foreach (var version in vsVersions)
        {
            var componentCache = Path.Combine(localAppData, "Microsoft", "VisualStudio", $"{version}_*");
            try
            {
                var matchingDirs = Directory.GetDirectories(Path.Combine(localAppData, "Microsoft", "VisualStudio"), $"{version}_*");
                foreach (var dir in matchingDirs)
                {
                    ct.ThrowIfCancellationRequested();

                    // ComponentModelCache
                    var cmCache = Path.Combine(dir, "ComponentModelCache");
                    if (Directory.Exists(cmCache))
                    {
                        var size = CalculateDirectorySize(cmCache, ct);
                        if (size > 50 * 1024 * 1024)
                        {
                            insights.Add(new Insight(
                                InsightType.IDECache,
                                $"VS {version.Split('.')[0]} Component Cache: {FormatSize(size)}",
                                cmCache,
                                size,
                                RecommendedAction.Clean
                            ));
                        }
                    }

                    // Designer cache
                    var designerCache = Path.Combine(dir, "Designer", "ShadowCache");
                    if (Directory.Exists(designerCache))
                    {
                        var size = CalculateDirectorySize(designerCache, ct);
                        if (size > 20 * 1024 * 1024)
                        {
                            insights.Add(new Insight(
                                InsightType.IDECache,
                                $"VS {version.Split('.')[0]} Designer Cache: {FormatSize(size)}",
                                designerCache,
                                size,
                                RecommendedAction.Clean
                            ));
                        }
                    }
                }
            }
            catch { }
        }

        // VS Code Analysis cache
        var codeAnalysis = Path.Combine(localAppData, "Microsoft", "CodeAnalysis");
        if (Directory.Exists(codeAnalysis))
        {
            var size = CalculateDirectorySize(codeAnalysis, ct);
            if (size > 100 * 1024 * 1024)
            {
                insights.Add(new Insight(
                    InsightType.IDECache,
                    $"VS Code Analysis Cache: {FormatSize(size)}",
                    codeAnalysis,
                    size,
                    RecommendedAction.Clean
                ));
            }
        }

        // VS temp files
        var vsTemp = Path.Combine(localAppData, "Temp", "VisualStudio");
        if (Directory.Exists(vsTemp))
        {
            var size = CalculateDirectorySize(vsTemp, ct);
            if (size > 50 * 1024 * 1024)
            {
                insights.Add(new Insight(
                    InsightType.IDECache,
                    $"Visual Studio Temp: {FormatSize(size)}",
                    vsTemp,
                    size,
                    RecommendedAction.Clean
                ));
            }
        }
    }

    private void AnalyzeJetBrains(string localAppData, string appData, List<Insight> insights, CancellationToken ct)
    {
        // JetBrains products store caches in different locations
        var jetBrainsLocal = Path.Combine(localAppData, "JetBrains");
        var jetBrainsRoaming = Path.Combine(appData, "JetBrains");

        foreach (var basePath in new[] { jetBrainsLocal, jetBrainsRoaming })
        {
            if (!Directory.Exists(basePath))
                continue;

            try
            {
                foreach (var productDir in Directory.GetDirectories(basePath))
                {
                    ct.ThrowIfCancellationRequested();

                    var productName = Path.GetFileName(productDir);

                    // Check caches subdirectory
                    var cachesDir = Path.Combine(productDir, "caches");
                    if (Directory.Exists(cachesDir))
                    {
                        var size = CalculateDirectorySize(cachesDir, ct);
                        if (size > 100 * 1024 * 1024)
                        {
                            insights.Add(new Insight(
                                InsightType.IDECache,
                                $"{productName} caches: {FormatSize(size)}",
                                cachesDir,
                                size,
                                RecommendedAction.Clean
                            ));
                        }
                    }

                    // Check index directory
                    var indexDir = Path.Combine(productDir, "index");
                    if (Directory.Exists(indexDir))
                    {
                        var size = CalculateDirectorySize(indexDir, ct);
                        if (size > 100 * 1024 * 1024)
                        {
                            insights.Add(new Insight(
                                InsightType.IDECache,
                                $"{productName} index: {FormatSize(size)}",
                                indexDir,
                                size,
                                RecommendedAction.Review // Indexes are rebuilt but takes time
                            ));
                        }
                    }

                    // Check log directory
                    var logDir = Path.Combine(productDir, "log");
                    if (Directory.Exists(logDir))
                    {
                        var size = CalculateDirectorySize(logDir, ct);
                        if (size > 50 * 1024 * 1024)
                        {
                            insights.Add(new Insight(
                                InsightType.IDECache,
                                $"{productName} logs: {FormatSize(size)}",
                                logDir,
                                size,
                                RecommendedAction.Clean
                            ));
                        }
                    }
                }
            }
            catch { }
        }
    }

    private void AnalyzeVSCode(string localAppData, string appData, string userProfile, List<Insight> insights, CancellationToken ct)
    {
        // VS Code cache
        var vscodeCache = Path.Combine(appData, "Code", "Cache");
        if (Directory.Exists(vscodeCache))
        {
            var size = CalculateDirectorySize(vscodeCache, ct);
            if (size > 100 * 1024 * 1024)
            {
                insights.Add(new Insight(
                    InsightType.IDECache,
                    $"VS Code Cache: {FormatSize(size)}",
                    vscodeCache,
                    size,
                    RecommendedAction.Clean
                ));
            }
        }

        // VS Code CachedData
        var vscodeCachedData = Path.Combine(appData, "Code", "CachedData");
        if (Directory.Exists(vscodeCachedData))
        {
            var size = CalculateDirectorySize(vscodeCachedData, ct);
            if (size > 50 * 1024 * 1024)
            {
                insights.Add(new Insight(
                    InsightType.IDECache,
                    $"VS Code Cached Data: {FormatSize(size)}",
                    vscodeCachedData,
                    size,
                    RecommendedAction.Clean
                ));
            }
        }

        // VS Code CachedExtensions
        var vscodeCachedExt = Path.Combine(appData, "Code", "CachedExtensions");
        if (Directory.Exists(vscodeCachedExt))
        {
            var size = CalculateDirectorySize(vscodeCachedExt, ct);
            if (size > 50 * 1024 * 1024)
            {
                insights.Add(new Insight(
                    InsightType.IDECache,
                    $"VS Code Cached Extensions: {FormatSize(size)}",
                    vscodeCachedExt,
                    size,
                    RecommendedAction.Clean
                ));
            }
        }

        // VS Code extensions themselves
        var vscodeExtensions = Path.Combine(userProfile, ".vscode", "extensions");
        if (Directory.Exists(vscodeExtensions))
        {
            var size = CalculateDirectorySize(vscodeExtensions, ct);
            if (size > 500 * 1024 * 1024) // Only if > 500MB
            {
                insights.Add(new Insight(
                    InsightType.IDECache,
                    $"VS Code Extensions: {FormatSize(size)}",
                    vscodeExtensions,
                    size,
                    RecommendedAction.Review // Don't want to delete needed extensions
                ));
            }
        }
    }

    private long CalculateDirectorySize(string path, CancellationToken ct)
    {
        long size = 0;
        try
        {
            foreach (var file in Directory.GetFiles(path))
            {
                ct.ThrowIfCancellationRequested();
                try { size += new FileInfo(file).Length; } catch { }
            }

            foreach (var dir in Directory.GetDirectories(path))
            {
                ct.ThrowIfCancellationRequested();
                size += CalculateDirectorySize(dir, ct);
            }
        }
        catch { }
        return size;
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
