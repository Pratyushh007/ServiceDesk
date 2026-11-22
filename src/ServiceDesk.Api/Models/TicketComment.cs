namespace ServiceDesk.Api.Models;

public sealed class TicketComment
{
    public const int BodyMaxLength = 1500;

    public int Id { get; private set; }
    public required int TicketId { get; init; }
    public required string AuthorId { get; init; }
    public required string Body { get; init; }
    public bool IsStaff { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
