using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using AutomatedBackup.Models;

namespace AutomatedBackup.Services;

/// <summary>
/// Service to gather hardware specifications using WMI (Windows only)
/// </summary>
[SupportedOSPlatform("windows")]
public class HardwareService
{
    /// <summary>
    /// Get complete hardware specification
    /// </summary>
    public async Task<HardwareSpec> GetHardwareSpecAsync()
    {
        return await Task.Run(() =>
        {
            var cpu = GetCpuInfo();
            var gpus = GetGpuInfo();
            var memory = GetMemoryInfo();
            var storage = GetStorageInfo();
            var motherboard = GetMotherboardInfo();
            var bios = GetBiosInfo();
            var os = GetOsInfo();

            return new HardwareSpec(cpu, gpus, memory, storage, motherboard, bios, os);
        });
    }

    /// <summary>
    /// Get CPU information
    /// </summary>
    public CpuInfo GetCpuInfo()
    {
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
        using var results = searcher.Get();

        foreach (ManagementObject obj in results)
        {
            return new CpuInfo(
                Name: GetString(obj, "Name"),
                Manufacturer: GetString(obj, "Manufacturer"),
                Architecture: GetArchitecture((ushort)(obj["Architecture"] ?? 0)),
                Cores: (int)(uint)(obj["NumberOfCores"] ?? 0),
                LogicalProcessors: (int)(uint)(obj["NumberOfLogicalProcessors"] ?? 0),
                MaxClockSpeedMhz: (int)(uint)(obj["MaxClockSpeed"] ?? 0),
                SocketDesignation: GetString(obj, "SocketDesignation"),
                L2CacheSizeKB: (int)(uint)(obj["L2CacheSize"] ?? 0),
                L3CacheSizeKB: (int)(uint)(obj["L3CacheSize"] ?? 0),
                ProcessorId: GetString(obj, "ProcessorId"),
                VirtualizationEnabled: (bool)(obj["VirtualizationFirmwareEnabled"] ?? false),
                Description: GetString(obj, "Description")
            );
        }

        return new CpuInfo("Unknown", "Unknown", "Unknown", 0, 0, 0, "", 0, 0, "", false, "");
    }

    /// <summary>
    /// Get GPU information for all video controllers
    /// </summary>
    public GpuInfo[] GetGpuInfo()
    {
        var gpus = new List<GpuInfo>();

        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
        using var results = searcher.Get();

        foreach (ManagementObject obj in results)
        {
            gpus.Add(new GpuInfo(
                Name: GetString(obj, "Name"),
                Manufacturer: GetString(obj, "AdapterCompatibility"),
                DriverVersion: GetString(obj, "DriverVersion"),
                DriverDate: ParseDriverDate(GetString(obj, "DriverDate")),
                VideoMemoryBytes: (long)(uint)(obj["AdapterRAM"] ?? 0),
                HorizontalResolution: (int)(uint)(obj["CurrentHorizontalResolution"] ?? 0),
                VerticalResolution: (int)(uint)(obj["CurrentVerticalResolution"] ?? 0),
                RefreshRate: (int)(uint)(obj["CurrentRefreshRate"] ?? 0),
                VideoProcessor: GetString(obj, "VideoProcessor"),
                AdapterCompatibility: GetString(obj, "AdapterCompatibility")
            ));
        }

        return gpus.ToArray();
    }

    /// <summary>
    /// Get memory information including all modules
    /// </summary>
    public MemoryInfo GetMemoryInfo()
    {
        var modules = new List<MemoryModuleInfo>();

        // Get physical memory modules
        using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory"))
        using (var results = searcher.Get())
        {
            foreach (ManagementObject obj in results)
            {
                modules.Add(new MemoryModuleInfo(
                    BankLabel: GetString(obj, "BankLabel"),
                    DeviceLocator: GetString(obj, "DeviceLocator"),
                    CapacityBytes: (long)(ulong)(obj["Capacity"] ?? 0UL),
                    SpeedMhz: (int)(uint)(obj["ConfiguredClockSpeed"] ?? obj["Speed"] ?? 0),
                    Manufacturer: GetString(obj, "Manufacturer")?.Trim() ?? "Unknown",
                    PartNumber: GetString(obj, "PartNumber")?.Trim() ?? "",
                    MemoryType: GetMemoryType((ushort)(obj["SMBIOSMemoryType"] ?? 0)),
                    DataWidth: (int)(ushort)(obj["DataWidth"] ?? 0)
                ));
            }
        }

        // Get total memory from computer system
        long totalPhysical = 0;
        long available = 0;

        using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
        using (var results = searcher.Get())
        {
            foreach (ManagementObject obj in results)
            {
                totalPhysical = (long)(ulong)(obj["TotalPhysicalMemory"] ?? 0UL);
            }
        }

        using (var searcher = new ManagementObjectSearcher("SELECT FreePhysicalMemory FROM Win32_OperatingSystem"))
        using (var results = searcher.Get())
        {
            foreach (ManagementObject obj in results)
            {
                available = (long)(ulong)(obj["FreePhysicalMemory"] ?? 0UL) * 1024; // Convert KB to bytes
            }
        }

        return new MemoryInfo(totalPhysical, available, modules.ToArray());
    }

    /// <summary>
    /// Get storage device information
    /// </summary>
    public StorageInfo[] GetStorageInfo()
    {
        var drives = new List<StorageInfo>();
        var systemDriveLetter = Environment.GetFolderPath(Environment.SpecialFolder.System)[0];

        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
        using var results = searcher.Get();

        foreach (ManagementObject obj in results)
        {
            var deviceId = GetString(obj, "DeviceID");
            var isSystem = IsSystemDrive(deviceId, systemDriveLetter);

            drives.Add(new StorageInfo(
                Model: GetString(obj, "Model")?.Trim() ?? "Unknown",
                InterfaceType: GetString(obj, "InterfaceType"),
                MediaType: GetString(obj, "MediaType"),
                SizeBytes: (long)(ulong)(obj["Size"] ?? 0UL),
                SerialNumber: GetString(obj, "SerialNumber")?.Trim() ?? "",
                FirmwareRevision: GetString(obj, "FirmwareRevision")?.Trim() ?? "",
                Partitions: (int)(uint)(obj["Partitions"] ?? 0),
                IsSystemDrive: isSystem
            ));
        }

        return drives.ToArray();
    }

    /// <summary>
    /// Get motherboard information
    /// </summary>
    public MotherboardInfo GetMotherboardInfo()
    {
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
        using var results = searcher.Get();

        foreach (ManagementObject obj in results)
        {
            return new MotherboardInfo(
                Manufacturer: GetString(obj, "Manufacturer"),
                Product: GetString(obj, "Product"),
                Version: GetString(obj, "Version"),
                SerialNumber: GetString(obj, "SerialNumber")
            );
        }

        return new MotherboardInfo("Unknown", "Unknown", "", "");
    }

    /// <summary>
    /// Get BIOS information
    /// </summary>
    public BiosInfo GetBiosInfo()
    {
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
        using var results = searcher.Get();

        foreach (ManagementObject obj in results)
        {
            var releaseDate = GetString(obj, "ReleaseDate");
            return new BiosInfo(
                Manufacturer: GetString(obj, "Manufacturer"),
                Version: GetString(obj, "SMBIOSBIOSVersion"),
                ReleaseDate: ParseWmiDate(releaseDate),
                IsUefi: IsUefiBoot()
            );
        }

        return new BiosInfo("Unknown", "Unknown", "", false);
    }

    /// <summary>
    /// Get operating system information
    /// </summary>
    public OsInfo GetOsInfo()
    {
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
        using var results = searcher.Get();

        foreach (ManagementObject obj in results)
        {
            return new OsInfo(
                Name: GetString(obj, "Caption"),
                Version: GetString(obj, "Version"),
                BuildNumber: GetString(obj, "BuildNumber"),
                Architecture: GetString(obj, "OSArchitecture"),
                InstallDate: ParseWmiDateTime(GetString(obj, "InstallDate")),
                LastBootTime: ParseWmiDateTime(GetString(obj, "LastBootUpTime"))
            );
        }

        return new OsInfo("Unknown", "", "", RuntimeInformation.OSArchitecture.ToString(), DateTime.MinValue, DateTime.MinValue);
    }

    #region Helper Methods

    private static string GetString(ManagementObject obj, string propertyName)
    {
        try
        {
            return obj[propertyName]?.ToString() ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static string GetArchitecture(ushort arch)
    {
        return arch switch
        {
            0 => "x86",
            5 => "ARM",
            6 => "ia64",
            9 => "x64",
            12 => "ARM64",
            _ => "Unknown"
        };
    }

    private static string GetMemoryType(ushort type)
    {
        return type switch
        {
            20 => "DDR",
            21 => "DDR2",
            22 => "DDR2 FB-DIMM",
            24 => "DDR3",
            26 => "DDR4",
            34 => "DDR5",
            _ => $"Type {type}"
        };
    }

    private static string ParseDriverDate(string wmiDate)
    {
        if (string.IsNullOrEmpty(wmiDate) || wmiDate.Length < 8)
            return "Unknown";

        try
        {
            var year = wmiDate.Substring(0, 4);
            var month = wmiDate.Substring(4, 2);
            var day = wmiDate.Substring(6, 2);
            return $"{year}-{month}-{day}";
        }
        catch
        {
            return wmiDate;
        }
    }

    private static string ParseWmiDate(string wmiDate)
    {
        if (string.IsNullOrEmpty(wmiDate) || wmiDate.Length < 8)
            return "Unknown";

        try
        {
            var year = wmiDate.Substring(0, 4);
            var month = wmiDate.Substring(4, 2);
            var day = wmiDate.Substring(6, 2);
            return $"{year}-{month}-{day}";
        }
        catch
        {
            return wmiDate;
        }
    }

    private static DateTime ParseWmiDateTime(string wmiDate)
    {
        if (string.IsNullOrEmpty(wmiDate) || wmiDate.Length < 14)
            return DateTime.MinValue;

        try
        {
            var year = int.Parse(wmiDate.Substring(0, 4));
            var month = int.Parse(wmiDate.Substring(4, 2));
            var day = int.Parse(wmiDate.Substring(6, 2));
            var hour = int.Parse(wmiDate.Substring(8, 2));
            var minute = int.Parse(wmiDate.Substring(10, 2));
            var second = int.Parse(wmiDate.Substring(12, 2));
            return new DateTime(year, month, day, hour, minute, second);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static bool IsUefiBoot()
    {
        try
        {
            // Check for EFI system partition or firmware type
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskPartition WHERE Type LIKE '%EFI%'");
            using var results = searcher.Get();
            return results.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSystemDrive(string deviceId, char systemDriveLetter)
    {
        try
        {
            // Map physical drive to logical drives
            using var partitionSearcher = new ManagementObjectSearcher(
                $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{deviceId.Replace("\\", "\\\\")}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");

            foreach (ManagementObject partition in partitionSearcher.Get())
            {
                using var logicalSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");

                foreach (ManagementObject logical in logicalSearcher.Get())
                {
                    var driveLetter = logical["DeviceID"]?.ToString();
                    if (!string.IsNullOrEmpty(driveLetter) && driveLetter[0] == systemDriveLetter)
                        return true;
                }
            }
        }
        catch
        {
            // Ignore errors in drive mapping
        }

        return false;
    }

    #endregion
}
