using System.Windows.Input;
using WindowCards.Models;

namespace WindowCards.App.Hotkeys;

public static class HotkeyFormat
{
    public static string Display(HotkeyBinding b)
    {
        var parts = new List<string>(5);
        if (b.Ctrl) parts.Add("Ctrl");
        if (b.Alt) parts.Add("Alt");
        if (b.Shift) parts.Add("Shift");
        if (b.Win) parts.Add("Win");
        parts.Add(KeyName(b.VirtualKey));
        return string.Join(" + ", parts);
    }

    public static uint ToWin32Modifiers(HotkeyBinding b)
    {
        uint m = 0;
        if (b.Ctrl) m |= HotkeyHost.MOD_CONTROL;
        if (b.Alt) m |= HotkeyHost.MOD_ALT;
        if (b.Shift) m |= HotkeyHost.MOD_SHIFT;
        if (b.Win) m |= HotkeyHost.MOD_WIN;
        return m;
    }

    public static string KeyName(uint vk)
    {
        var key = KeyInterop.KeyFromVirtualKey((int)vk);
        return key switch
        {
            >= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => "Num " + (int)(key - Key.NumPad0),
            Key.OemPlus => "+",
            Key.OemMinus => "-",
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemQuestion => "/",
            Key.OemSemicolon => ";",
            Key.OemTilde => "`",
            Key.OemOpenBrackets => "[",
            Key.OemCloseBrackets => "]",
            Key.OemPipe => "\\",
            Key.OemQuotes => "'",
            Key.Space => "Space",
            Key.Return => "Enter",
            _ => key.ToString()
        };
    }
}
