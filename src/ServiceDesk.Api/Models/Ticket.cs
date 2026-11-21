namespace ServiceDesk.Api.Models;

public sealed class Ticket
{
    public const int TitleMaxLength = 150;
    public const int DescriptionMaxLength = 2000;

    public int Id { get; private set; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public string Status { get; private set; } = "Open";
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
}
