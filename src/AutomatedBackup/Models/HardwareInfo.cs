namespace AutomatedBackup.Models;

/// <summary>
/// Complete hardware specification summary
/// </summary>
public record HardwareSpec(
    CpuInfo Cpu,
    GpuInfo[] Gpus,
    MemoryInfo Memory,
    StorageInfo[] Storage,
    MotherboardInfo Motherboard,
    BiosInfo Bios,
    OsInfo OperatingSystem
);

/// <summary>
/// CPU/Processor information
/// </summary>
public record CpuInfo(
    string Name,
    string Manufacturer,
    string Architecture,
    int Cores,
    int LogicalProcessors,
    int MaxClockSpeedMhz,
    string SocketDesignation,
    int L2CacheSizeKB,
    int L3CacheSizeKB,
    string ProcessorId,
    bool VirtualizationEnabled,
    string Description
)
{
    public string Generation => ExtractGeneration(Name);
    public double MaxClockSpeedGhz => MaxClockSpeedMhz / 1000.0;

    private static string ExtractGeneration(string cpuName)
    {
        // Intel: "Intel(R) Core(TM) i7-12700K" -> "12th Gen"
        // AMD: "AMD Ryzen 7 5800X" -> "Zen 3 (5000 series)"
        if (string.IsNullOrEmpty(cpuName))
            return "Unknown";

        if (cpuName.Contains("Intel", StringComparison.OrdinalIgnoreCase))
        {
            // Extract generation from Intel naming (i7-12700K -> 12)
            var match = System.Text.RegularExpressions.Regex.Match(cpuName, @"i[3579]-(\d{1,2})\d{3}");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var gen))
            {
                return gen switch
                {
                    >= 14 => $"{gen}th Gen (Raptor Lake Refresh)",
                    13 => "13th Gen (Raptor Lake)",
                    12 => "12th Gen (Alder Lake)",
                    11 => "11th Gen (Rocket Lake)",
                    10 => "10th Gen (Comet Lake)",
                    _ => $"{gen}th Gen"
                };
            }
        }
        else if (cpuName.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                 cpuName.Contains("Ryzen", StringComparison.OrdinalIgnoreCase))
        {
            // AMD Ryzen naming: Ryzen 7 5800X -> 5000 series
            var match = System.Text.RegularExpressions.Regex.Match(cpuName, @"Ryzen\s+\d+\s+(\d)\d{3}");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var series))
            {
                return series switch
                {
                    9 => "Zen 5 (9000 series)",
                    8 => "Zen 4 (8000 series)",
                    7 => "Zen 4 (7000 series)",
                    5 => "Zen 3 (5000 series)",
                    3 => "Zen 2 (3000 series)",
                    2 => "Zen+ (2000 series)",
                    1 => "Zen (1000 series)",
                    _ => $"{series}000 series"
                };
            }
        }

        return "Unknown";
    }
}

/// <summary>
/// GPU/Graphics card information
/// </summary>
public record GpuInfo(
    string Name,
    string Manufacturer,
    string DriverVersion,
    string DriverDate,
    long VideoMemoryBytes,
    int HorizontalResolution,
    int VerticalResolution,
    int RefreshRate,
    string VideoProcessor,
    string AdapterCompatibility
)
{
    public string VideoMemoryFormatted => FormatBytes(VideoMemoryBytes);
    public string Resolution => $"{HorizontalResolution}x{VerticalResolution} @ {RefreshRate}Hz";

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "Unknown";
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        double size = bytes;
        while (size >= 1024 && i < suffixes.Length - 1) { size /= 1024; i++; }
        return $"{size:F1} {suffixes[i]}";
    }
}

/// <summary>
/// Memory/RAM information
/// </summary>
public record MemoryInfo(
    long TotalPhysicalBytes,
    long AvailableBytes,
    MemoryModuleInfo[] Modules
)
{
    public string TotalFormatted => FormatBytes(TotalPhysicalBytes);
    public int TotalSlots => Modules.Length;
    public int UsedSlots => Modules.Count(m => m.CapacityBytes > 0);
    public int ChannelsUsed => Modules.Select(m => m.BankLabel).Distinct().Count();

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "Unknown";
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        double size = bytes;
        while (size >= 1024 && i < suffixes.Length - 1) { size /= 1024; i++; }
        return $"{size:F1} {suffixes[i]}";
    }
}

/// <summary>
/// Individual RAM module information
/// </summary>
public record MemoryModuleInfo(
    string BankLabel,
    string DeviceLocator,
    long CapacityBytes,
    int SpeedMhz,
    string Manufacturer,
    string PartNumber,
    string MemoryType,
    int DataWidth
)
{
    public string CapacityFormatted => CapacityBytes > 0 ? $"{CapacityBytes / (1024 * 1024 * 1024)} GB" : "Empty";
}

/// <summary>
/// Storage device information
/// </summary>
public record StorageInfo(
    string Model,
    string InterfaceType,
    string MediaType,
    long SizeBytes,
    string SerialNumber,
    string FirmwareRevision,
    int Partitions,
    bool IsSystemDrive
)
{
    public string SizeFormatted => FormatBytes(SizeBytes);
    public bool IsSsd => MediaType?.Contains("SSD", StringComparison.OrdinalIgnoreCase) == true ||
                         Model?.Contains("SSD", StringComparison.OrdinalIgnoreCase) == true ||
                         Model?.Contains("NVMe", StringComparison.OrdinalIgnoreCase) == true;

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "Unknown";
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        double size = bytes;
        while (size >= 1024 && i < suffixes.Length - 1) { size /= 1024; i++; }
        return $"{size:F1} {suffixes[i]}";
    }
}

/// <summary>
/// Motherboard information
/// </summary>
public record MotherboardInfo(
    string Manufacturer,
    string Product,
    string Version,
    string SerialNumber
);

/// <summary>
/// BIOS information
/// </summary>
public record BiosInfo(
    string Manufacturer,
    string Version,
    string ReleaseDate,
    bool IsUefi
);

/// <summary>
/// Operating System information
/// </summary>
public record OsInfo(
    string Name,
    string Version,
    string BuildNumber,
    string Architecture,
    DateTime InstallDate,
    DateTime LastBootTime
);
