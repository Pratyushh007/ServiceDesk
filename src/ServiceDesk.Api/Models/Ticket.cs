using System.Text.Json.Serialization;

namespace ServiceDesk.Api.Models;

public sealed class Ticket
{
    public const int TitleMaxLength = 150;
    public const int DescriptionMaxLength = 2000;
    public const string OpenStatus = "Open";
    public const string InProgressStatus = "In Progress";
    public const string ResolvedStatus = "Resolved";

    public int Id { get; private set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public string Status { get; set; } = OpenStatus;
    public int? CategoryId { get; set; }
    public string? OwnerId { get; init; }
    public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public byte[] RowVersion { get; private set; } = [];
}
