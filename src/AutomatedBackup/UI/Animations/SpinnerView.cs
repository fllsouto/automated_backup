using Terminal.Gui;

namespace AutomatedBackup.UI.Animations;

/// <summary>
/// Animated spinner/loading indicator with multiple style options
/// </summary>
public class SpinnerView : AnimatedView
{
    private int _frame;
    private readonly string[] _frames;
    private readonly Terminal.Gui.Attribute? _color;

    /// <summary>
    /// Predefined spinner styles
    /// </summary>
    public static class Styles
    {
        // Classic spinning line
        public static readonly string[] Line = { "|", "/", "-", "\\" };

        // Dots rotating
        public static readonly string[] Dots = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };

        // Block rotating
        public static readonly string[] Blocks = { "▖", "▘", "▝", "▗" };

        // Circle filling
        public static readonly string[] Circle = { "◐", "◓", "◑", "◒" };

        // Arrow rotating
        public static readonly string[] Arrow = { "←", "↖", "↑", "↗", "→", "↘", "↓", "↙" };

        // Box bouncing
        public static readonly string[] Bounce = { "⠁", "⠂", "⠄", "⠂" };

        // Growing bar
        public static readonly string[] GrowingBar = { "▏", "▎", "▍", "▌", "▋", "▊", "▉", "█", "▉", "▊", "▋", "▌", "▍", "▎", "▏" };

        // Moon phases
        public static readonly string[] Moon = { "🌑", "🌒", "🌓", "🌔", "🌕", "🌖", "🌗", "🌘" };

        // Simple ASCII (safe for all terminals)
        public static readonly string[] SimpleAscii = { "[    ]", "[=   ]", "[==  ]", "[=== ]", "[====]", "[ ===]", "[  ==]", "[   =]" };
    }

    /// <summary>
    /// Optional text to display after the spinner
    /// </summary>
    public string SpinnerText { get; set; } = "";

    public SpinnerView(string[]? style = null, int intervalMs = 80, Terminal.Gui.Attribute? color = null)
        : base(intervalMs)
    {
        _frames = style ?? Styles.Dots;
        _color = color;
        _frame = 0;

        Height = 1;
        Width = Dim.Fill();
    }

    protected override void OnTick()
    {
        _frame = (_frame + 1) % _frames.Length;
    }

    public override void Redraw(Rect bounds)
    {
        Move(0, 0);

        if (_color.HasValue)
            Driver.SetAttribute(_color.Value);

        var spinChar = _frames[_frame];
        var display = string.IsNullOrEmpty(SpinnerText) ? spinChar : $"{spinChar} {SpinnerText}";

        Driver.AddStr(display.PadRight(bounds.Width));
    }
}
