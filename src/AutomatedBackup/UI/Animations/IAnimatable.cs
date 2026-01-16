using Terminal.Gui;

namespace AutomatedBackup.UI.Animations;

/// <summary>
/// Interface for animated UI components
/// </summary>
public interface IAnimatable
{
    /// <summary>
    /// Start the animation loop
    /// </summary>
    void Start();

    /// <summary>
    /// Stop the animation loop
    /// </summary>
    void Stop();

    /// <summary>
    /// Whether the animation is currently running
    /// </summary>
    bool IsRunning { get; }
}

/// <summary>
/// Base class for animated views with timer management
/// </summary>
public abstract class AnimatedView : View, IAnimatable, IDisposable
{
    private object? _timerToken;
    private readonly int _intervalMs;
    private bool _disposed;

    public bool IsRunning => _timerToken != null;

    protected AnimatedView(int intervalMs = 100)
    {
        _intervalMs = intervalMs;
    }

    public void Start()
    {
        if (_timerToken != null) return;

        _timerToken = Application.MainLoop?.AddTimeout(
            TimeSpan.FromMilliseconds(_intervalMs),
            _ =>
            {
                if (_disposed) return false;

                OnTick();
                SetNeedsDisplay();
                return true; // Keep running
            }
        );
    }

    public void Stop()
    {
        if (_timerToken != null)
        {
            Application.MainLoop?.RemoveTimeout(_timerToken);
            _timerToken = null;
        }
    }

    /// <summary>
    /// Called on each animation frame - override to update state
    /// </summary>
    protected abstract void OnTick();

    public new void Dispose()
    {
        _disposed = true;
        Stop();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
