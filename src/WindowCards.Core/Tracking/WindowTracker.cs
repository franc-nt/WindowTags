using WindowCards.Core.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace WindowCards.Core.Tracking;

public sealed class WindowTracker : IDisposable
{
    private readonly IntPtr _targetHwnd;
    private WinEventHook? _hook;
    private bool _disposed;

    public IntPtr TargetHwnd => _targetHwnd;

    public event Action<WindowBounds>? BoundsChanged;
    public event Action? Minimized;
    public event Action? Restored;
    public event Action? Destroyed;

    public WindowTracker(IntPtr targetHwnd)
    {
        if (targetHwnd == IntPtr.Zero) throw new ArgumentException("targetHwnd is null", nameof(targetHwnd));
        _targetHwnd = targetHwnd;

        _hook = new WinEventHook(PInvoke.EVENT_OBJECT_DESTROY, PInvoke.EVENT_OBJECT_LOCATIONCHANGE);
        _hook.Event += OnWinEvent;
    }

    public WindowBounds CurrentBounds() => WindowGeometry.GetExtendedFrameBounds(_targetHwnd);
    public bool IsCurrentlyMinimized() => WindowGeometry.IsMinimized(_targetHwnd);

    private void OnWinEvent(uint eventType, IntPtr hwnd, int idObject, int idChild)
    {
        if (hwnd != _targetHwnd) return;
        if (idObject != 0) return;

        switch (eventType)
        {
            case PInvoke.EVENT_OBJECT_LOCATIONCHANGE:
                if (WindowGeometry.IsMinimized(hwnd))
                    Minimized?.Invoke();
                else
                    BoundsChanged?.Invoke(WindowGeometry.GetExtendedFrameBounds(hwnd));
                break;

            case PInvoke.EVENT_SYSTEM_MINIMIZESTART:
                Minimized?.Invoke();
                break;

            case PInvoke.EVENT_SYSTEM_MINIMIZEEND:
                Restored?.Invoke();
                BoundsChanged?.Invoke(WindowGeometry.GetExtendedFrameBounds(hwnd));
                break;

            case PInvoke.EVENT_OBJECT_DESTROY:
                Destroyed?.Invoke();
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _hook?.Dispose();
        _hook = null;
    }
}
