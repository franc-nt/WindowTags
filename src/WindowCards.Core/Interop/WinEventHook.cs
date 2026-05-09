using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.Accessibility;

namespace WindowCards.Core.Interop;

public sealed class WinEventHook : IDisposable
{
    private readonly WINEVENTPROC _callback;
    private readonly GCHandle _callbackHandle;
    private UnhookWinEventSafeHandle? _hook;
    private bool _disposed;

    public event Action<uint, IntPtr, int, int>? Event;

    public WinEventHook(uint eventMin, uint eventMax)
    {
        _callback = OnWinEvent;
        _callbackHandle = GCHandle.Alloc(_callback);

        const uint flags = PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS;

        _hook = PInvoke.SetWinEventHook(
            eventMin,
            eventMax,
            null,
            _callback,
            0,
            0,
            flags);

        if (_hook is null || _hook.IsInvalid)
            throw new InvalidOperationException("SetWinEventHook failed.");
    }

    private void OnWinEvent(
        HWINEVENTHOOK hWinEventHook,
        uint eventType,
        Windows.Win32.Foundation.HWND hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        Event?.Invoke(eventType, hwnd, idObject, idChild);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _hook?.Dispose();
        _hook = null;

        if (_callbackHandle.IsAllocated)
            _callbackHandle.Free();

        GC.SuppressFinalize(this);
    }
}
