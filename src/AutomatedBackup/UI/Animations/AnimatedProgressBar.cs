using Terminal.Gui;

namespace AutomatedBackup.UI.Animations;

/// <summary>
/// Animated progress bar with multiple visual styles
/// </summary>
public class AnimatedProgressBar : AnimatedView
{
    private float _targetProgress;
    private float _currentProgress;
    private int _shimmerOffset;
    private readonly float _smoothingFactor;

    /// <summary>
    /// Progress value from 0.0 to 1.0
    /// </summary>
    public float Progress
    {
        get => _targetProgress;
        set => _targetProgress = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Progress as percentage (0-100)
    /// </summary>
    public int ProgressPercent
    {
        get => (int)(_targetProgress * 100);
        set => Progress = value / 100f;
    }

    /// <summary>
    /// Whether to show percentage text
    /// </summary>
    public bool ShowPercentage { get; set; } = true;

    /// <summary>
    /// Whether to animate a shimmer effect
    /// </summary>
    public bool EnableShimmer { get; set; } = true;

    /// <summary>
    /// Visual style for the progress bar
    /// </summary>
    public ProgressStyle Style { get; set; } = ProgressStyle.Smooth;

    /// <summary>
    /// Color for the filled portion (null = default)
    /// </summary>
    public Terminal.Gui.Attribute? FillColor { get; set; }

    /// <summary>
    /// Color for the empty portion (null = default)
    /// </summary>
    public Terminal.Gui.Attribute? EmptyColor { get; set; }

    public enum ProgressStyle
    {
        /// <summary>Smooth gradient: ░▒▓█</summary>
        Smooth,
        /// <summary>Simple blocks: █░</summary>
        Blocks,
        /// <summary>ASCII: [====    ]</summary>
        Ascii,
        /// <summary>Thin line: ─━</summary>
        Line,
        /// <summary>Dots: ⣿⣀</summary>
        Dots
    }

    public AnimatedProgressBar(float smoothingFactor = 0.15f, int intervalMs = 50)
        : base(intervalMs)
    {
        _smoothingFactor = smoothingFactor;
        _currentProgress = 0;
        _targetProgress = 0;
        _shimmerOffset = 0;

        Height = 1;
        Width = Dim.Fill();
    }

    protected override void OnTick()
    {
        // Smooth interpolation towards target
        var diff = _targetProgress - _currentProgress;
        if (Math.Abs(diff) > 0.001f)
        {
            _currentProgress += diff * _smoothingFactor;
        }
        else
        {
            _currentProgress = _targetProgress;
        }

        // Shimmer animation
        if (EnableShimmer)
        {
            _shimmerOffset = (_shimmerOffset + 1) % 20;
        }
    }

    public override void Redraw(Rect bounds)
    {
        var width = bounds.Width;
        var percentText = ShowPercentage ? $" {(int)(_currentProgress * 100),3}%" : "";
        var barWidth = width - percentText.Length;

        if (barWidth < 3)
        {
            Move(0, 0);
            Driver.AddStr(percentText);
            return;
        }

        Move(0, 0);

        switch (Style)
        {
            case ProgressStyle.Smooth:
                DrawSmoothBar(barWidth);
                break;
            case ProgressStyle.Blocks:
                DrawBlockBar(barWidth);
                break;
            case ProgressStyle.Ascii:
                DrawAsciiBar(barWidth);
                break;
            case ProgressStyle.Line:
                DrawLineBar(barWidth);
                break;
            case ProgressStyle.Dots:
                DrawDotsBar(barWidth);
                break;
        }

        if (ShowPercentage)
        {
            Driver.AddStr(percentText);
        }
    }

    private void DrawSmoothBar(int width)
    {
        // Characters for smooth gradient
        char[] gradient = { ' ', '░', '▒', '▓', '█' };

        var filledWidth = _currentProgress * width;
        var fullBlocks = (int)filledWidth;
        var partialBlock = filledWidth - fullBlocks;

        for (int i = 0; i < width; i++)
        {
            char c;
            if (i < fullBlocks)
            {
                c = gradient[4]; // Full block

                // Shimmer effect
                if (EnableShimmer && (i + _shimmerOffset) % 8 == 0 && _currentProgress < 1f)
                {
                    c = gradient[3];
                }
            }
            else if (i == fullBlocks)
            {
                // Partial block
                var gradientIndex = (int)(partialBlock * (gradient.Length - 1));
                c = gradient[gradientIndex];
            }
            else
            {
                c = gradient[0]; // Empty
            }

            if (FillColor.HasValue && i <= fullBlocks)
                Driver.SetAttribute(FillColor.Value);
            else if (EmptyColor.HasValue)
                Driver.SetAttribute(EmptyColor.Value);

            Driver.AddRune(c);
        }
    }

    private void DrawBlockBar(int width)
    {
        var filledWidth = (int)(_currentProgress * width);

        for (int i = 0; i < width; i++)
        {
            char c = i < filledWidth ? '█' : '░';

            // Shimmer
            if (EnableShimmer && i == filledWidth - 1 && _currentProgress < 1f && _shimmerOffset % 4 < 2)
            {
                c = '▓';
            }

            Driver.AddRune(c);
        }
    }

    private void DrawAsciiBar(int width)
    {
        Driver.AddRune('[');
        var innerWidth = width - 2;
        var filledWidth = (int)(_currentProgress * innerWidth);

        for (int i = 0; i < innerWidth; i++)
        {
            char c;
            if (i < filledWidth)
            {
                c = '=';
            }
            else if (i == filledWidth && _currentProgress > 0 && _currentProgress < 1)
            {
                // Animated head
                c = (_shimmerOffset % 4) switch
                {
                    0 => '>',
                    1 => '=',
                    2 => '-',
                    _ => '>'
                };
            }
            else
            {
                c = ' ';
            }
            Driver.AddRune(c);
        }
        Driver.AddRune(']');
    }

    private void DrawLineBar(int width)
    {
        var filledWidth = (int)(_currentProgress * width);

        for (int i = 0; i < width; i++)
        {
            char c = i < filledWidth ? '━' : '─';
            Driver.AddRune(c);
        }
    }

    private void DrawDotsBar(int width)
    {
        // Braille-based bar
        var filledWidth = (int)(_currentProgress * width);

        for (int i = 0; i < width; i++)
        {
            char c = i < filledWidth ? '⣿' : '⣀';
            Driver.AddRune(c);
        }
    }
}
