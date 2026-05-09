namespace WindowCards.Models;

public sealed class Rule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProcessName { get; set; } = "";
    public string TitleContains { get; set; } = "";
    public CardConfig Card { get; set; } = new();
}
