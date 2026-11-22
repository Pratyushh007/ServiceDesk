namespace ServiceDesk.Api.Models;

public sealed class TicketStatusChange
{
    public int Id { get; private set; }
    public int TicketId { get; private set; }
    public required Ticket Ticket { get; init; }
    public required string ChangedById { get; init; }
    public string? PreviousStatus { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset ChangedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
