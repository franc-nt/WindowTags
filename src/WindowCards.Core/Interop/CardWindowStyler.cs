using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace WindowCards.Core.Interop;

public static class CardWindowStyler
{
    public static void ApplyOverlayStyles(IntPtr cardHwnd)
    {
        var h = (HWND)cardHwnd;

        var ex = (uint)PInvoke.GetWindowLong(h, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        ex |= (uint)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW;
        ex |= (uint)WINDOW_EX_STYLE.WS_EX_NOACTIVATE;
        ex |= (uint)WINDOW_EX_STYLE.WS_EX_LAYERED;

        PInvoke.SetWindowLong(h, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)ex);
    }

    public static void SetOwner(IntPtr cardHwnd, IntPtr ownerHwnd)
    {
        if (IntPtr.Size == 8)
            SetWindowLongPtr64(cardHwnd, GWLP_HWNDPARENT, ownerHwnd);
        else
            SetWindowLong32(cardHwnd, GWLP_HWNDPARENT, ownerHwnd.ToInt32());
    }

    public static void MoveTo(IntPtr cardHwnd, int x, int y, int width, int height, bool show)
    {
        var flags = SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER;
        if (show) flags |= SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW;
        PInvoke.SetWindowPos((HWND)cardHwnd, default, x, y, width, height, flags);
    }

    public static void Move(IntPtr cardHwnd, int x, int y)
    {
        PInvoke.SetWindowPos((HWND)cardHwnd, default, x, y, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
            SET_WINDOW_POS_FLAGS.SWP_NOSIZE);
    }

    public static void Hide(IntPtr cardHwnd)
    {
        PInvoke.SetWindowPos((HWND)cardHwnd, default, 0, 0, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
            SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
            SET_WINDOW_POS_FLAGS.SWP_HIDEWINDOW);
    }

    private const int GWLP_HWNDPARENT = -8;

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", CharSet = CharSet.Unicode)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", CharSet = CharSet.Unicode)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);
}
