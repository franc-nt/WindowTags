namespace WindowCards.Models;

public sealed class HotkeyBinding
{
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool Win { get; set; }
    public uint VirtualKey { get; set; }

    public HotkeyBinding Clone() => new()
    {
        Ctrl = Ctrl,
        Alt = Alt,
        Shift = Shift,
        Win = Win,
        VirtualKey = VirtualKey
    };

    public bool HasModifier => Ctrl || Alt || Shift || Win;
}
