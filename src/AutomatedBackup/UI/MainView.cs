using Terminal.Gui;
using AutomatedBackup.Services;
using AutomatedBackup.Models;
using AutomatedBackup.UI.Animations;

namespace AutomatedBackup.UI;

/// <summary>
/// Main application window
/// </summary>
public class MainView : Window
{
    private readonly InsightAggregator _aggregator;
    private readonly FrameView _leftFrame;
    private readonly FrameView _rightFrame;
    private readonly ListView _locationListView;
    private readonly ListView _insightsListView;
    private readonly Label _statusLabel;
    private readonly Label _summaryLabel;
    private readonly Button _scanButton;
    private readonly Button _demoButton;

    // Animation components
    private readonly FrameView _scanProgressFrame;
    private readonly SpinnerView _spinner;
    private readonly AnimatedProgressBar _progressBar;
    private readonly Label _scanningLabel;

    private Dictionary<string, List<Insight>> _groupedInsights = new();
    private List<string> _locationKeys = new();
    private string? _selectedLocation;
    private bool _isScanning;

    public MainView() : base("Automated Backup - Filesystem Analysis (Ctrl+Q to quit)")
    {
        _aggregator = new InsightAggregator();

        // === Bottom status bar (fixed at bottom) ===
        _statusLabel = new Label("Ready")
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill() - 30
        };

        _scanButton = new Button("_Scan (F5)")
        {
            X = Pos.AnchorEnd(28),
            Y = Pos.AnchorEnd(1)
        };
        _scanButton.Clicked += OnScanClicked;

        _demoButton = new Button("_Demo")
        {
            X = Pos.AnchorEnd(12),
            Y = Pos.AnchorEnd(1)
        };
        _demoButton.Clicked += OnDemoClicked;

        // === Summary line (above status bar) ===
        _summaryLabel = new Label("Press 'Scan' to analyze your filesystem (F5) | 'Demo' for animation demo")
        {
            X = 0,
            Y = Pos.AnchorEnd(2),
            Width = Dim.Fill()
        };

        // === Scan progress frame (above summary, hidden by default) ===
        _scanProgressFrame = new FrameView("Scanning")
        {
            X = 0,
            Y = Pos.AnchorEnd(5),
            Width = Dim.Fill(),
            Height = 3,
            Visible = false
        };

        _spinner = new SpinnerView(SpinnerView.Styles.Dots)
        {
            X = 1,
            Y = 0,
            Width = 3
        };

        _scanningLabel = new Label("Initializing...")
        {
            X = 5,
            Y = 0,
            Width = 25
        };

        _progressBar = new AnimatedProgressBar
        {
            X = 32,
            Y = 0,
            Width = Dim.Fill() - 1,
            Style = AnimatedProgressBar.ProgressStyle.Smooth,
            EnableShimmer = true
        };

        _scanProgressFrame.Add(_spinner, _scanningLabel, _progressBar);

        // === Main content panels ===
        _leftFrame = new FrameView("Locations")
        {
            X = 0,
            Y = 0,
            Width = Dim.Percent(35),
            Height = Dim.Fill(3) // Leave 3 rows for status area
        };

        _locationListView = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        _locationListView.SelectedItemChanged += OnLocationSelected;
        _leftFrame.Add(_locationListView);

        _rightFrame = new FrameView("Insights")
        {
            X = Pos.Right(_leftFrame),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(3)
        };

        _insightsListView = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        _insightsListView.OpenSelectedItem += OnInsightSelected;
        _rightFrame.Add(_insightsListView);

        // === Add all components (order matters for z-index) ===
        Add(_leftFrame, _rightFrame, _scanProgressFrame, _summaryLabel, _statusLabel, _scanButton, _demoButton);

        // === Keyboard shortcuts ===
        KeyPress += (e) =>
        {
            if (e.KeyEvent.Key == Key.F5 && !_isScanning)
            {
                OnScanClicked();
                e.Handled = true;
            }
        };
    }

    private void OnDemoClicked()
    {
        using var demo = new AnimationDemo();
        Application.Run(demo);
    }

    private void ShowScanProgress(bool show)
    {
        _scanProgressFrame.Visible = show;
        _isScanning = show;

        // Adjust main frames height when progress is shown
        if (show)
        {
            _leftFrame.Height = Dim.Fill(6);
            _rightFrame.Height = Dim.Fill(6);
        }
        else
        {
            _leftFrame.Height = Dim.Fill(3);
            _rightFrame.Height = Dim.Fill(3);
        }

        SetNeedsDisplay();
    }

    private async void OnScanClicked()
    {
        _scanButton.Enabled = false;
        _demoButton.Enabled = false;
        _statusLabel.Text = "Scanning...";
        _locationListView.SetSource(new List<string> { "Please wait..." });
        _insightsListView.SetSource(new List<string>());

        // Show and start animations
        ShowScanProgress(true);
        _progressBar.Progress = 0;
        _spinner.Start();
        _progressBar.Start();

        Application.Refresh();

        try
        {
            var cts = new CancellationTokenSource();

            var progress = new Progress<AnalysisProgress>(p =>
            {
                Application.MainLoop.Invoke(() =>
                {
                    _scanningLabel.Text = p.CurrentAnalyzer;
                    _progressBar.Progress = p.PercentComplete / 100f;
                    _statusLabel.Text = $"Analyzing: {p.CurrentAnalyzer}";
                });
            });

            var result = await Task.Run(() => _aggregator.AnalyzeAllAsync(progress, cts.Token));

            // Complete the progress bar
            _progressBar.Progress = 1f;
            _scanningLabel.Text = "Complete!";

            // Small delay to show completion
            await Task.Delay(500);

            _groupedInsights = InsightAggregator.GroupByLocation(result.AllInsights);
            _locationKeys = _groupedInsights.Keys.OrderBy(k => k).ToList();

            UpdateLocationList();
            UpdateSummary(result);

            _statusLabel.Text = result.Errors.Any()
                ? $"Complete with {result.Errors.Count} error(s)"
                : $"Scan complete - found {result.TotalInsightCount} insights";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error: {ex.Message}";
        }
        finally
        {
            // Stop and hide animations
            _spinner.Stop();
            _progressBar.Stop();
            ShowScanProgress(false);

            _scanButton.Enabled = true;
            _demoButton.Enabled = true;
        }
    }

    private void UpdateLocationList()
    {
        var items = _locationKeys.Select(k =>
        {
            var insights = _groupedInsights[k];
            var totalSize = insights.Sum(i => i.SizeInBytes);
            return $"{k} ({FormatSize(totalSize)})";
        }).ToList();

        _locationListView.SetSource(items);
    }

    private void UpdateSummary(AnalysisResult result)
    {
        var cleanCount = result.AllInsights.Count(i => i.Action == RecommendedAction.Clean);
        var archiveCount = result.AllInsights.Count(i => i.Action == RecommendedAction.Archive);
        var reviewCount = result.AllInsights.Count(i => i.Action == RecommendedAction.Review);

        _summaryLabel.Text = $"Total: {FormatSize(result.TotalReclaimableBytes)} | " +
                            $"Clean: {cleanCount} | Archive: {archiveCount} | Review: {reviewCount}";
    }

    private void OnLocationSelected(ListViewItemEventArgs e)
    {
        if (e.Item >= 0 && e.Item < _locationKeys.Count)
        {
            _selectedLocation = _locationKeys[e.Item];
            UpdateInsightsList();
        }
    }

    private void UpdateInsightsList()
    {
        if (_selectedLocation == null || !_groupedInsights.ContainsKey(_selectedLocation))
        {
            _insightsListView.SetSource(new List<string>());
            return;
        }

        var insights = _groupedInsights[_selectedLocation];
        var items = insights.Select(i =>
        {
            var actionIcon = i.Action switch
            {
                RecommendedAction.Clean => "[C]",
                RecommendedAction.Archive => "[A]",
                RecommendedAction.Review => "[R]",
                _ => "[?]"
            };
            return $"{actionIcon} {i.Description}";
        }).ToList();

        _insightsListView.SetSource(items);
    }

    private void OnInsightSelected(ListViewItemEventArgs e)
    {
        if (_selectedLocation == null || !_groupedInsights.ContainsKey(_selectedLocation))
            return;

        var insights = _groupedInsights[_selectedLocation];
        if (e.Item >= 0 && e.Item < insights.Count)
        {
            var insight = insights[e.Item];
            ShowInsightDetails(insight);
        }
    }

    private void ShowInsightDetails(Insight insight)
    {
        var dialog = new Dialog("Insight Details", 60, 15);

        var typeLabel = new Label($"Type: {insight.Type}")
        {
            X = 1,
            Y = 1
        };

        var descLabel = new Label($"Description: {insight.Description}")
        {
            X = 1,
            Y = 2
        };

        var pathLabel = new Label($"Path: {insight.Path}")
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill() - 2
        };

        var sizeLabel = new Label($"Size: {FormatSize(insight.SizeInBytes)}")
        {
            X = 1,
            Y = 4
        };

        var actionLabel = new Label($"Recommended: {insight.Action}")
        {
            X = 1,
            Y = 5
        };

        dialog.Add(typeLabel, descLabel, pathLabel, sizeLabel, actionLabel);

        if (!string.IsNullOrEmpty(insight.CleanupCommand))
        {
            var cmdLabel = new Label($"Command: {insight.CleanupCommand}")
            {
                X = 1,
                Y = 7,
                Width = Dim.Fill() - 2
            };
            dialog.Add(cmdLabel);

            var copyBtn = new Button("Copy Command")
            {
                X = 1,
                Y = 9
            };
            copyBtn.Clicked += () =>
            {
                Clipboard.TrySetClipboardData(insight.CleanupCommand);
                MessageBox.Query("Copied", "Command copied to clipboard", "OK");
            };
            dialog.Add(copyBtn);
        }

        var closeBtn = new Button("Close")
        {
            X = Pos.Center(),
            Y = Pos.Bottom(dialog) - 3
        };
        closeBtn.Clicked += () => Application.RequestStop();
        dialog.Add(closeBtn);

        Application.Run(dialog);
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
