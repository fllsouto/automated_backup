using AutomatedBackup.Models;

namespace AutomatedBackup.Services.Analyzers;

/// <summary>
/// Analyzes package manager caches (NuGet, npm, pip, cargo)
/// </summary>
public class PackageCacheAnalyzer : IInsightAnalyzer
{
    public string Name => "Package Caches";

    public bool IsAvailable => true;

    public async Task<IEnumerable<Insight>> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        var insights = new List<Insight>();

        await Task.Run(() =>
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // NuGet cache
            var nugetCache = Path.Combine(userProfile, ".nuget", "packages");
            if (Directory.Exists(nugetCache))
            {
                var size = CalculateDirectorySize(nugetCache, cancellationToken);
                if (size > 100 * 1024 * 1024) // > 100MB
                {
                    insights.Add(new Insight(
                        InsightType.PackageCache,
                        $"NuGet package cache: {FormatSize(size)}",
                        nugetCache,
                        size,
                        RecommendedAction.Review,
                        "dotnet nuget locals all --clear"
                    ));
                }
            }

            // npm cache
            var npmCache = Path.Combine(localAppData, "npm-cache");
            if (Directory.Exists(npmCache))
            {
                var size = CalculateDirectorySize(npmCache, cancellationToken);
                if (size > 100 * 1024 * 1024)
                {
                    insights.Add(new Insight(
                        InsightType.PackageCache,
                        $"npm cache: {FormatSize(size)}",
                        npmCache,
                        size,
                        RecommendedAction.Clean,
                        "npm cache clean --force"
                    ));
                }
            }

            // Alternative npm cache location
            var npmCacheAlt = Path.Combine(userProfile, ".npm");
            if (Directory.Exists(npmCacheAlt))
            {
                var size = CalculateDirectorySize(npmCacheAlt, cancellationToken);
                if (size > 100 * 1024 * 1024)
                {
                    insights.Add(new Insight(
                        InsightType.PackageCache,
                        $"npm cache (~/.npm): {FormatSize(size)}",
                        npmCacheAlt,
                        size,
                        RecommendedAction.Clean,
                        "npm cache clean --force"
                    ));
                }
            }

            // yarn cache
            var yarnCache = Path.Combine(localAppData, "Yarn", "Cache");
            if (Directory.Exists(yarnCache))
            {
                var size = CalculateDirectorySize(yarnCache, cancellationToken);
                if (size > 100 * 1024 * 1024)
                {
                    insights.Add(new Insight(
                        InsightType.PackageCache,
                        $"Yarn cache: {FormatSize(size)}",
                        yarnCache,
                        size,
                        RecommendedAction.Clean,
                        "yarn cache clean"
                    ));
                }
            }

            // pnpm store
            var pnpmStore = Path.Combine(localAppData, "pnpm-store");
            if (Directory.Exists(pnpmStore))
            {
                var size = CalculateDirectorySize(pnpmStore, cancellationToken);
                if (size > 100 * 1024 * 1024)
                {
                    insights.Add(new Insight(
                        InsightType.PackageCache,
                        $"pnpm store: {FormatSize(size)}",
                        pnpmStore,
                        size,
                        RecommendedAction.Review,
                        "pnpm store prune"
                    ));
                }
            }

            // pip cache
            var pipCache = Path.Combine(localAppData, "pip", "cache");
            if (Directory.Exists(pipCache))
            {
                var size = CalculateDirectorySize(pipCache, cancellationToken);
                if (size > 50 * 1024 * 1024)
                {
                    insights.Add(new Insight(
                        InsightType.PackageCache,
                        $"pip cache: {FormatSize(size)}",
                        pipCache,
                        size,
                        RecommendedAction.Clean,
                        "pip cache purge"
                    ));
                }
            }

            // Cargo cache (Rust)
            var cargoCache = Path.Combine(userProfile, ".cargo", "registry");
            if (Directory.Exists(cargoCache))
            {
                var size = CalculateDirectorySize(cargoCache, cancellationToken);
                if (size > 100 * 1024 * 1024)
                {
                    insights.Add(new Insight(
                        InsightType.PackageCache,
                        $"Cargo registry cache: {FormatSize(size)}",
                        cargoCache,
                        size,
                        RecommendedAction.Review,
                        "cargo cache --autoclean"
                    ));
                }
            }

            // Go modules cache
            var goPath = Environment.GetEnvironmentVariable("GOPATH") ?? Path.Combine(userProfile, "go");
            var goModCache = Path.Combine(goPath, "pkg", "mod", "cache");
            if (Directory.Exists(goModCache))
            {
                var size = CalculateDirectorySize(goModCache, cancellationToken);
                if (size > 100 * 1024 * 1024)
                {
                    insights.Add(new Insight(
                        InsightType.PackageCache,
                        $"Go modules cache: {FormatSize(size)}",
                        goModCache,
                        size,
                        RecommendedAction.Clean,
                        "go clean -modcache"
                    ));
                }
            }

            // Maven cache
            var mavenCache = Path.Combine(userProfile, ".m2", "repository");
            if (Directory.Exists(mavenCache))
            {
                var size = CalculateDirectorySize(mavenCache, cancellationToken);
                if (size > 100 * 1024 * 1024)
                {
                    insights.Add(new Insight(
                        InsightType.PackageCache,
                        $"Maven repository cache: {FormatSize(size)}",
                        mavenCache,
                        size,
                        RecommendedAction.Review
                    ));
                }
            }

            // Gradle cache
            var gradleCache = Path.Combine(userProfile, ".gradle", "caches");
            if (Directory.Exists(gradleCache))
            {
                var size = CalculateDirectorySize(gradleCache, cancellationToken);
                if (size > 100 * 1024 * 1024)
                {
                    insights.Add(new Insight(
                        InsightType.PackageCache,
                        $"Gradle caches: {FormatSize(size)}",
                        gradleCache,
                        size,
                        RecommendedAction.Review
                    ));
                }
            }

        }, cancellationToken);

        return insights;
    }

    private long CalculateDirectorySize(string path, CancellationToken cancellationToken)
    {
        long size = 0;
        try
        {
            foreach (var file in Directory.GetFiles(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try { size += new FileInfo(file).Length; } catch { }
            }

            foreach (var dir in Directory.GetDirectories(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                size += CalculateDirectorySize(dir, cancellationToken);
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
