namespace ServiceDesk.Api.Models;

public sealed class TicketAttachment
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const int MaxRequestBytes = MaxBytes + 64 * 1024;
    public const int FileNameMaxLength = 180;

    public int Id { get; private set; }
    public required int TicketId { get; init; }
    public required string UploadedById { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required byte[] Content { get; init; }
    public required int SizeBytes { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
