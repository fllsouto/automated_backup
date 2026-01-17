using Terminal.Gui;
using AutomatedBackup.Models;
using AutomatedBackup.Services;
using AutomatedBackup.UI.Animations;

namespace AutomatedBackup.UI;

/// <summary>
/// Dialog to display hardware specifications
/// </summary>
public class HardwareSpecsView : Dialog
{
    private readonly HardwareService _hardwareService;
    private readonly TabView _tabView;
    private readonly Label _loadingLabel;
    private readonly SpinnerView _spinner;
    private HardwareSpec? _specs;

    public HardwareSpecsView() : base("Hardware Specifications", 80, 28)
    {
        _hardwareService = new HardwareService();

        // Loading indicator
        _loadingLabel = new Label("Loading hardware information...")
        {
            X = Pos.Center(),
            Y = Pos.Center() - 1
        };

        _spinner = new SpinnerView(SpinnerView.Styles.Dots)
        {
            X = Pos.Center(),
            Y = Pos.Center()
        };

        // Tab view for different hardware categories
        _tabView = new TabView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 2,
            Visible = false
        };

        // Close button
        var closeBtn = new Button("Close")
        {
            X = Pos.Center(),
            Y = Pos.AnchorEnd(1)
        };
        closeBtn.Clicked += () => Application.RequestStop();

        // Refresh button
        var refreshBtn = new Button("Refresh")
        {
            X = Pos.Center() - 15,
            Y = Pos.AnchorEnd(1)
        };
        refreshBtn.Clicked += async () => await LoadHardwareInfo();

        Add(_loadingLabel, _spinner, _tabView, refreshBtn, closeBtn);

        // Start loading
        _ = LoadHardwareInfo();
    }

    private async Task LoadHardwareInfo()
    {
        _loadingLabel.Visible = true;
        _spinner.Visible = true;
        _spinner.Start();
        _tabView.Visible = false;

        try
        {
            _specs = await _hardwareService.GetHardwareSpecAsync();

            Application.MainLoop.Invoke(() =>
            {
                _spinner.Stop();
                _loadingLabel.Visible = false;
                _spinner.Visible = false;

                PopulateTabs();
                _tabView.Visible = true;
            });
        }
        catch (Exception ex)
        {
            Application.MainLoop.Invoke(() =>
            {
                _spinner.Stop();
                _loadingLabel.Text = $"Error: {ex.Message}";
            });
        }
    }

    private void PopulateTabs()
    {
        if (_specs == null) return;

        // Clear existing tabs
        _tabView.RemoveAll();

        // CPU Tab
        _tabView.AddTab(new TabView.Tab("CPU", CreateCpuView(_specs.Cpu)), false);

        // GPU Tab
        _tabView.AddTab(new TabView.Tab("GPU", CreateGpuView(_specs.Gpus)), false);

        // Memory Tab
        _tabView.AddTab(new TabView.Tab("Memory", CreateMemoryView(_specs.Memory)), false);

        // Storage Tab
        _tabView.AddTab(new TabView.Tab("Storage", CreateStorageView(_specs.Storage)), false);

        // System Tab (Motherboard, BIOS, OS)
        _tabView.AddTab(new TabView.Tab("System", CreateSystemView(_specs)), false);

        _tabView.SelectedTab = _tabView.Tabs.First();
    }

    private View CreateCpuView(CpuInfo cpu)
    {
        var view = new View { Width = Dim.Fill(), Height = Dim.Fill() };

        var lines = new List<string>
        {
            $"Name:           {cpu.Name}",
            $"Manufacturer:   {cpu.Manufacturer}",
            $"Generation:     {cpu.Generation}",
            $"Architecture:   {cpu.Architecture}",
            "",
            $"Cores:          {cpu.Cores}",
            $"Threads:        {cpu.LogicalProcessors}",
            $"Max Clock:      {cpu.MaxClockSpeedGhz:F2} GHz",
            "",
            $"L2 Cache:       {cpu.L2CacheSizeKB} KB",
            $"L3 Cache:       {cpu.L3CacheSizeKB / 1024} MB",
            $"Socket:         {cpu.SocketDesignation}",
            "",
            $"Virtualization: {(cpu.VirtualizationEnabled ? "Enabled" : "Disabled")}",
            $"Processor ID:   {cpu.ProcessorId}"
        };

        var listView = new ListView(lines)
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill() - 1,
            Height = Dim.Fill()
        };

        view.Add(listView);
        return view;
    }

    private View CreateGpuView(GpuInfo[] gpus)
    {
        var view = new View { Width = Dim.Fill(), Height = Dim.Fill() };
        var lines = new List<string>();

        for (int i = 0; i < gpus.Length; i++)
        {
            var gpu = gpus[i];
            if (i > 0) lines.Add("");

            lines.Add($"=== GPU {i + 1}: {gpu.Name} ===");
            lines.Add($"Manufacturer:   {gpu.Manufacturer}");
            lines.Add($"Video Memory:   {gpu.VideoMemoryFormatted}");
            lines.Add($"Resolution:     {gpu.Resolution}");
            lines.Add($"Driver Version: {gpu.DriverVersion}");
            lines.Add($"Driver Date:    {gpu.DriverDate}");

            if (!string.IsNullOrEmpty(gpu.VideoProcessor) && gpu.VideoProcessor != gpu.Name)
                lines.Add($"Video Processor: {gpu.VideoProcessor}");
        }

        if (gpus.Length == 0)
        {
            lines.Add("No GPU information available");
        }

        var listView = new ListView(lines)
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill() - 1,
            Height = Dim.Fill()
        };

        view.Add(listView);
        return view;
    }

    private View CreateMemoryView(MemoryInfo memory)
    {
        var view = new View { Width = Dim.Fill(), Height = Dim.Fill() };
        var lines = new List<string>
        {
            $"=== Memory Summary ===",
            $"Total Installed: {memory.TotalFormatted}",
            $"Slots Used:      {memory.UsedSlots} of {memory.TotalSlots}",
            $"Channels Used:   {memory.ChannelsUsed}",
            ""
        };

        for (int i = 0; i < memory.Modules.Length; i++)
        {
            var module = memory.Modules[i];
            lines.Add($"=== Slot {i + 1}: {module.DeviceLocator} ===");

            if (module.CapacityBytes > 0)
            {
                lines.Add($"Capacity:     {module.CapacityFormatted}");
                lines.Add($"Speed:        {module.SpeedMhz} MHz");
                lines.Add($"Type:         {module.MemoryType}");
                lines.Add($"Manufacturer: {module.Manufacturer}");
                lines.Add($"Part Number:  {module.PartNumber}");
                lines.Add($"Bank:         {module.BankLabel}");
            }
            else
            {
                lines.Add("(Empty slot)");
            }
            lines.Add("");
        }

        var listView = new ListView(lines)
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill() - 1,
            Height = Dim.Fill()
        };

        view.Add(listView);
        return view;
    }

    private View CreateStorageView(StorageInfo[] storage)
    {
        var view = new View { Width = Dim.Fill(), Height = Dim.Fill() };
        var lines = new List<string>();

        var ssdCount = storage.Count(s => s.IsSsd);
        var hddCount = storage.Length - ssdCount;

        lines.Add($"=== Storage Summary ===");
        lines.Add($"Total Drives: {storage.Length} (SSD: {ssdCount}, HDD: {hddCount})");
        lines.Add("");

        for (int i = 0; i < storage.Length; i++)
        {
            var drive = storage[i];
            var driveType = drive.IsSsd ? "SSD" : "HDD";
            var systemMark = drive.IsSystemDrive ? " [SYSTEM]" : "";

            lines.Add($"=== Drive {i + 1}: {drive.Model}{systemMark} ===");
            lines.Add($"Type:       {driveType}");
            lines.Add($"Size:       {drive.SizeFormatted}");
            lines.Add($"Interface:  {drive.InterfaceType}");
            lines.Add($"Partitions: {drive.Partitions}");

            if (!string.IsNullOrEmpty(drive.FirmwareRevision))
                lines.Add($"Firmware:   {drive.FirmwareRevision}");

            lines.Add("");
        }

        if (storage.Length == 0)
        {
            lines.Add("No storage information available");
        }

        var listView = new ListView(lines)
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill() - 1,
            Height = Dim.Fill()
        };

        view.Add(listView);
        return view;
    }

    private View CreateSystemView(HardwareSpec specs)
    {
        var view = new View { Width = Dim.Fill(), Height = Dim.Fill() };
        var lines = new List<string>
        {
            $"=== Operating System ===",
            $"Name:         {specs.OperatingSystem.Name}",
            $"Version:      {specs.OperatingSystem.Version}",
            $"Build:        {specs.OperatingSystem.BuildNumber}",
            $"Architecture: {specs.OperatingSystem.Architecture}",
            $"Installed:    {specs.OperatingSystem.InstallDate:yyyy-MM-dd}",
            $"Last Boot:    {specs.OperatingSystem.LastBootTime:yyyy-MM-dd HH:mm}",
            "",
            $"=== Motherboard ===",
            $"Manufacturer: {specs.Motherboard.Manufacturer}",
            $"Product:      {specs.Motherboard.Product}",
            $"Version:      {specs.Motherboard.Version}",
            "",
            $"=== BIOS ===",
            $"Manufacturer: {specs.Bios.Manufacturer}",
            $"Version:      {specs.Bios.Version}",
            $"Date:         {specs.Bios.ReleaseDate}",
            $"UEFI:         {(specs.Bios.IsUefi ? "Yes" : "No/Legacy")}"
        };

        var listView = new ListView(lines)
        {
            X = 1,
            Y = 0,
            Width = Dim.Fill() - 1,
            Height = Dim.Fill()
        };

        view.Add(listView);
        return view;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _spinner.Stop();
            _spinner.Dispose();
        }
        base.Dispose(disposing);
    }
}
