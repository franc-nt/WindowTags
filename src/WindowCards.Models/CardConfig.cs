namespace WindowCards.Models;

public sealed class CardConfig
{
    public string Text { get; set; } = "Card";
    public string BackgroundHex { get; set; } = "#D32F2F";
    public string ForegroundHex { get; set; } = "#FFFFFF";

    public double OffsetX { get; set; } = 8;
    public double OffsetY { get; set; } = 4;
    public double Width { get; set; } = 220;
    public double Height { get; set; } = 36;
}
