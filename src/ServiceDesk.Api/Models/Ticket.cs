namespace ServiceDesk.Api.Models;

public sealed record Ticket(
    int Id,
    string Title,
    string Description,
    string Status,
    DateTimeOffset CreatedAtUtc);
