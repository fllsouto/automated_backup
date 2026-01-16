using AutomatedBackup.Models;

namespace AutomatedBackup.Services.Analyzers;

/// <summary>
/// Finds node_modules folders that can be deleted and regenerated
/// </summary>
public class NodeModulesAnalyzer : IInsightAnalyzer
{
    private readonly string[] _searchPaths;

    public string Name => "Node Modules";

    public bool IsAvailable => true;

    public NodeModulesAnalyzer() : this(GetDefaultSearchPaths())
    {
    }

    public NodeModulesAnalyzer(string[] searchPaths)
    {
        _searchPaths = searchPaths;
    }

    private static string[] GetDefaultSearchPaths()
    {
        var paths = new List<string>();

        // Common development directories
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        paths.Add(Path.Combine(userProfile, "source"));
        paths.Add(Path.Combine(userProfile, "repos"));
        paths.Add(Path.Combine(userProfile, "projects"));
        paths.Add(Path.Combine(userProfile, "dev"));
        paths.Add(Path.Combine(userProfile, "code"));
        paths.Add(Path.Combine(userProfile, "workspace"));
        paths.Add(Path.Combine(userProfile, "Workspace"));
        paths.Add(Path.Combine(userProfile, "Documents", "GitHub"));

        return paths.Where(Directory.Exists).ToArray();
    }

    public async Task<IEnumerable<Insight>> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        var insights = new List<Insight>();

        await Task.Run(() =>
        {
            foreach (var searchPath in _searchPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FindNodeModules(searchPath, insights, cancellationToken, maxDepth: 5);
            }
        }, cancellationToken);

        return insights;
    }

    private void FindNodeModules(string path, List<Insight> insights, CancellationToken cancellationToken, int maxDepth, int currentDepth = 0)
    {
        if (currentDepth > maxDepth)
            return;

        try
        {
            foreach (var dir in Directory.GetDirectories(path))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var dirName = Path.GetFileName(dir);

                // Skip hidden directories and other node_modules
                if (dirName.StartsWith(".") || dirName == "node_modules")
                    continue;

                var nodeModulesPath = Path.Combine(dir, "node_modules");
                if (Directory.Exists(nodeModulesPath))
                {
                    // Check if there's a package.json (confirms it's a Node project)
                    var packageJsonPath = Path.Combine(dir, "package.json");
                    if (File.Exists(packageJsonPath))
                    {
                        var size = CalculateDirectorySize(nodeModulesPath, cancellationToken);
                        if (size > 10 * 1024 * 1024) // Only report if > 10MB
                        {
                            insights.Add(new Insight(
                                InsightType.NodeModules,
                                $"node_modules in {Path.GetFileName(dir)}: {FormatSize(size)}",
                                nodeModulesPath,
                                size,
                                RecommendedAction.Clean,
                                $"rmdir /s /q \"{nodeModulesPath}\""
                            ));
                        }
                    }
                }
                else
                {
                    // Recurse into subdirectories
                    FindNodeModules(dir, insights, cancellationToken, maxDepth, currentDepth + 1);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip inaccessible directories
        }
        catch (PathTooLongException)
        {
            // Skip paths that are too long
        }
    }

    private long CalculateDirectorySize(string path, CancellationToken cancellationToken)
    {
        long size = 0;
        try
        {
            foreach (var file in Directory.GetFiles(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                size += new FileInfo(file).Length;
            }

            foreach (var dir in Directory.GetDirectories(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                size += CalculateDirectorySize(dir, cancellationToken);
            }
        }
        catch
        {
            // Ignore errors
        }
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
