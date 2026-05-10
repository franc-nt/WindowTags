using Windows.Win32;
using Windows.Win32.Foundation;

namespace WindowCards.Core.Tracking;

public sealed class WindowTitlePrefixer : IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly WindowTracker _tracker;
    private string _prefix;
    private bool _suppress;
    private bool _disposed;

    public WindowTitlePrefixer(IntPtr hwnd, WindowTracker tracker, string prefix)
    {
        _hwnd = hwnd;
        _tracker = tracker;
        _prefix = NormalizePrefix(prefix);

        _tracker.TitleChanged += OnTitleChanged;
        Apply();
    }

    public void UpdatePrefix(string prefix)
    {
        if (_disposed) return;

        var newPrefix = NormalizePrefix(prefix);
        if (newPrefix == _prefix) return;

        var oldFormatted = FormatPrefix(_prefix);
        _prefix = newPrefix;

        var current = ReadTitle();
        var stripped = current.StartsWith(oldFormatted, StringComparison.Ordinal)
            ? current[oldFormatted.Length..]
            : current;

        WriteTitle(string.IsNullOrEmpty(_prefix) ? stripped : FormatPrefix(_prefix) + stripped);
    }

    private void OnTitleChanged()
    {
        if (_disposed || _suppress) return;
        if (string.IsNullOrEmpty(_prefix)) return;

        var current = ReadTitle();
        var formatted = FormatPrefix(_prefix);
        if (current.StartsWith(formatted, StringComparison.Ordinal)) return;

        WriteTitle(formatted + current);
    }

    private void Apply()
    {
        if (string.IsNullOrEmpty(_prefix)) return;

        var current = ReadTitle();
        var formatted = FormatPrefix(_prefix);
        if (current.StartsWith(formatted, StringComparison.Ordinal)) return;

        WriteTitle(formatted + current);
    }

    private void WriteTitle(string title)
    {
        if (!WindowGeometryIsAlive()) return;
        try
        {
            _suppress = true;
            PInvoke.SetWindowText((HWND)_hwnd, title);
        }
        catch
        {
            // window may have closed between events; nothing actionable
        }
        finally
        {
            _suppress = false;
        }
    }

    private unsafe string ReadTitle()
    {
        var h = (HWND)_hwnd;
        int len = PInvoke.GetWindowTextLength(h);
        if (len <= 0) return string.Empty;
        Span<char> buffer = stackalloc char[len + 1];
        fixed (char* p = buffer)
        {
            int copied = PInvoke.GetWindowText(h, (PWSTR)p, buffer.Length);
            return copied > 0 ? new string(buffer[..copied]) : string.Empty;
        }
    }

    private bool WindowGeometryIsAlive() =>
        _hwnd != IntPtr.Zero && PInvoke.IsWindow((HWND)_hwnd);

    private static string NormalizePrefix(string prefix) =>
        string.IsNullOrWhiteSpace(prefix) ? string.Empty : prefix.Trim();

    private static string FormatPrefix(string prefix) =>
        string.IsNullOrEmpty(prefix) ? string.Empty : $"❗{prefix}❗ ";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _tracker.TitleChanged -= OnTitleChanged;

        if (string.IsNullOrEmpty(_prefix)) return;
        if (!WindowGeometryIsAlive()) return;

        var current = ReadTitle();
        var formatted = FormatPrefix(_prefix);
        if (current.StartsWith(formatted, StringComparison.Ordinal))
            WriteTitle(current[formatted.Length..]);
    }
}
