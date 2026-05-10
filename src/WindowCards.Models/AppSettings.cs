namespace WindowCards.Models;

public sealed class AppSettings
{
    public HotkeyBinding CreateOrEdit { get; set; } = new()
    {
        Ctrl = true,
        Alt = true,
        VirtualKey = 0x4C // VK_L
    };

    public HotkeyBinding Remove { get; set; } = new()
    {
        Ctrl = true,
        Alt = true,
        VirtualKey = 0x52 // VK_R
    };

    public bool StartWithWindows { get; set; } = true;
}
