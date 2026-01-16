using Terminal.Gui;

namespace AutomatedBackup.UI.Animations;

/// <summary>
/// Demo dialog to showcase all animation styles
/// </summary>
public class AnimationDemo : Dialog
{
    private readonly List<IAnimatable> _animations = new();
    private readonly AnimatedProgressBar _demoProgress;
    private object? _progressTimer;

    public AnimationDemo() : base("Animation Demo", 70, 22)
    {
        var y = 1;

        // Spinner styles demo
        Add(new Label("Spinner Styles:") { X = 1, Y = y++ });

        var spinnerStyles = new (string name, string[] style)[]
        {
            ("Dots", SpinnerView.Styles.Dots),
            ("Line", SpinnerView.Styles.Line),
            ("Blocks", SpinnerView.Styles.Blocks),
            ("Circle", SpinnerView.Styles.Circle),
            ("Arrow", SpinnerView.Styles.Arrow),
            ("Bar", SpinnerView.Styles.GrowingBar),
        };

        var x = 1;
        foreach (var (name, style) in spinnerStyles)
        {
            var label = new Label(name + ":") { X = x, Y = y };
            var spinner = new SpinnerView(style) { X = x + name.Length + 2, Y = y, Width = 3 };
            Add(label, spinner);
            _animations.Add(spinner);
            x += name.Length + 6;

            if (x > 55)
            {
                x = 1;
                y++;
            }
        }

        y += 2;

        // Progress bar styles demo
        Add(new Label("Progress Bar Styles:") { X = 1, Y = y++ });

        var progressStyles = new[]
        {
            AnimatedProgressBar.ProgressStyle.Smooth,
            AnimatedProgressBar.ProgressStyle.Blocks,
            AnimatedProgressBar.ProgressStyle.Ascii,
            AnimatedProgressBar.ProgressStyle.Line,
            AnimatedProgressBar.ProgressStyle.Dots,
        };

        foreach (var style in progressStyles)
        {
            var label = new Label($"{style,-8}:") { X = 1, Y = y };
            var progress = new AnimatedProgressBar { X = 11, Y = y, Width = 45, Style = style };
            progress.Progress = 0.65f;
            Add(label, progress);
            _animations.Add(progress);
            y++;
        }

        y++;

        // Interactive demo
        Add(new Label("Interactive Progress (use slider):") { X = 1, Y = y++ });

        _demoProgress = new AnimatedProgressBar
        {
            X = 1,
            Y = y++,
            Width = 50,
            Style = AnimatedProgressBar.ProgressStyle.Smooth,
            EnableShimmer = true
        };
        Add(_demoProgress);
        _animations.Add(_demoProgress);

        // Slider to control progress
        var slider = new TextField("0")
        {
            X = 52,
            Y = y - 1,
            Width = 5
        };
        slider.TextChanged += (_) =>
        {
            if (int.TryParse(slider.Text.ToString(), out var val))
            {
                _demoProgress.ProgressPercent = Math.Clamp(val, 0, 100);
            }
        };
        Add(slider);
        Add(new Label("%") { X = 58, Y = y - 1 });

        // Auto-progress button
        var autoBtn = new Button("Auto 0→100")
        {
            X = 1,
            Y = y + 1
        };
        autoBtn.Clicked += StartAutoProgress;
        Add(autoBtn);

        // Close button
        var closeBtn = new Button("Close")
        {
            X = Pos.Center(),
            Y = Pos.Bottom(this) - 3
        };
        closeBtn.Clicked += () =>
        {
            StopAll();
            Application.RequestStop();
        };
        Add(closeBtn);

        // Start all animations
        StartAll();
    }

    private void StartAll()
    {
        foreach (var anim in _animations)
        {
            anim.Start();
        }
    }

    private void StopAll()
    {
        foreach (var anim in _animations)
        {
            anim.Stop();
        }

        if (_progressTimer != null)
        {
            Application.MainLoop?.RemoveTimeout(_progressTimer);
            _progressTimer = null;
        }
    }

    private void StartAutoProgress()
    {
        _demoProgress.Progress = 0;

        _progressTimer = Application.MainLoop?.AddTimeout(
            TimeSpan.FromMilliseconds(50),
            _ =>
            {
                _demoProgress.Progress += 0.01f;
                if (_demoProgress.Progress >= 1f)
                {
                    _progressTimer = null;
                    return false;
                }
                return true;
            }
        );
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopAll();
            foreach (var anim in _animations)
            {
                if (anim is IDisposable disposable)
                    disposable.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}
