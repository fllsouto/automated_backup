// FileSystemService.cs - Handles all file system operations
// This service scans directories and provides information about files and folders

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AutomatedBackup.Services;

/// <summary>
/// Represents information about a folder including its size and contents
/// A "record" is a special C# type that's ideal for data containers - 
/// it automatically implements equality, ToString(), etc.
/// </summary>
public record FolderInfo(
    string Path,              // Full path to the folder (e.g., "C:\Users\John\Documents")
    string Name,              // Just the folder name (e.g., "Documents")
    long SizeInBytes,         // Total size of all files in this folder and subfolders
    int FileCount,            // Number of files in this folder and subfolders
    int FolderCount           // Number of subfolders
);

/// <summary>
/// Service class that provides file system scanning capabilities
/// </summary>
public class FileSystemService
{
    /// <summary>
    /// Scans a directory and returns information about it
    /// </summary>
    /// <param name="path">The directory path to scan</param>
    /// <returns>A FolderInfo object with size and count information</returns>
    public FolderInfo GetFolderInfo(string path)
    {
        // Check if the directory exists before trying to scan it
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Directory not found: {path}");
        }

        // Create a DirectoryInfo object - this is .NET's way of representing a folder
        var dirInfo = new DirectoryInfo(path);
        
        // Calculate the total size and counts
        // We use a tuple to return multiple values from the recursive calculation
        var (totalSize, fileCount, folderCount) = CalculateDirectorySize(dirInfo);

        // Return a new FolderInfo record with all the gathered information
        return new FolderInfo(
            Path: path,
            Name: dirInfo.Name,
            SizeInBytes: totalSize,
            FileCount: fileCount,
            FolderCount: folderCount
        );
    }

    /// <summary>
    /// Gets a list of all subdirectories in a given path
    /// </summary>
    /// <param name="path">The directory to list</param>
    /// <returns>List of FolderInfo for each immediate subdirectory</returns>
    public List<FolderInfo> GetSubdirectories(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Directory not found: {path}");
        }

        var result = new List<FolderInfo>();
        var dirInfo = new DirectoryInfo(path);

        // Iterate through each subdirectory
        // We use try-catch because some system folders may deny access
        foreach (var subDir in dirInfo.GetDirectories())
        {
            try
            {
                var (totalSize, fileCount, folderCount) = CalculateDirectorySize(subDir);
                result.Add(new FolderInfo(
                    Path: subDir.FullName,
                    Name: subDir.Name,
                    SizeInBytes: totalSize,
                    FileCount: fileCount,
                    FolderCount: folderCount
                ));
            }
            catch (UnauthorizedAccessException)
            {
                // Skip folders we don't have permission to access
                // This is common for system folders like "System Volume Information"
                result.Add(new FolderInfo(
                    Path: subDir.FullName,
                    Name: subDir.Name + " (Access Denied)",
                    SizeInBytes: 0,
                    FileCount: 0,
                    FolderCount: 0
                ));
            }
        }

        // Sort by size descending - largest folders first
        // This is useful for identifying what's taking up the most space
        return result.OrderByDescending(f => f.SizeInBytes).ToList();
    }

    /// <summary>
    /// Gets all available drives on the system (C:, D:, external drives, etc.)
    /// </summary>
    /// <returns>Array of DriveInfo objects representing each drive</returns>
    public DriveInfo[] GetAvailableDrives()
    {
        // GetDrives() returns all drives, but we filter to only "Ready" drives
        // A drive might not be ready if it's a CD drive with no disc, for example
        return DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .ToArray();
    }

    /// <summary>
    /// Recursively calculates the total size of a directory
    /// "Recursive" means this method calls itself to process subdirectories
    /// </summary>
    private (long size, int fileCount, int folderCount) CalculateDirectorySize(DirectoryInfo directory)
    {
        long totalSize = 0;
        int totalFiles = 0;
        int totalFolders = 0;

        try
        {
            // Sum up the size of all files in this directory
            // GetFiles() returns an array of FileInfo objects
            foreach (var file in directory.GetFiles())
            {
                totalSize += file.Length;  // Length is the file size in bytes
                totalFiles++;
            }

            // Recursively process each subdirectory
            foreach (var subDir in directory.GetDirectories())
            {
                totalFolders++;
                
                // Recursive call - this method calls itself for each subdirectory
                var (subSize, subFiles, subFolders) = CalculateDirectorySize(subDir);
                totalSize += subSize;
                totalFiles += subFiles;
                totalFolders += subFolders;
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Silently skip directories we can't access
        }
        catch (PathTooLongException)
        {
            // Windows has a path length limit (~260 chars by default)
            // Some deeply nested folders might exceed this
        }

        return (totalSize, totalFiles, totalFolders);
    }

    /// <summary>
    /// Utility method to convert bytes to a human-readable format
    /// For example: 1073741824 bytes -> "1.00 GB"
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        // Array of size suffixes
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double size = bytes;

        // Keep dividing by 1024 until we get a reasonable number
        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        // Format with 2 decimal places
        return $"{size:F2} {suffixes[suffixIndex]}";
    }
}
