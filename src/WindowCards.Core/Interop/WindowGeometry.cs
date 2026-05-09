using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;

namespace WindowCards.Core.Interop;

public readonly record struct WindowBounds(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

public static class WindowGeometry
{
    public static WindowBounds GetExtendedFrameBounds(IntPtr hwnd)
    {
        RECT rect;
        unsafe
        {
            HRESULT hr = PInvoke.DwmGetWindowAttribute(
                (HWND)hwnd,
                DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS,
                &rect,
                (uint)Marshal.SizeOf<RECT>());

            if (hr.Failed)
            {
                if (!PInvoke.GetWindowRect((HWND)hwnd, out rect))
                    return default;
            }
        }
        return new WindowBounds(rect.left, rect.top, rect.right, rect.bottom);
    }

    public static WindowBounds GetWindowRectBounds(IntPtr hwnd)
    {
        if (!PInvoke.GetWindowRect((HWND)hwnd, out var rect)) return default;
        return new WindowBounds(rect.left, rect.top, rect.right, rect.bottom);
    }

    public static bool IsMinimized(IntPtr hwnd) => PInvoke.IsIconic((HWND)hwnd);

    public static bool IsValid(IntPtr hwnd) =>
        hwnd != IntPtr.Zero && PInvoke.IsWindow((HWND)hwnd);
}
