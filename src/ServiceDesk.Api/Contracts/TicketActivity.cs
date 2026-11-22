using System.ComponentModel.DataAnnotations;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Contracts;

public sealed class CreateTicketCommentRequest
{
    [Required]
    [StringLength(TicketComment.BodyMaxLength)]
    public string Body { get; init; } = string.Empty;
}

public sealed record TicketCommentResponse(int Id, string Body, bool IsStaff, DateTimeOffset CreatedAtUtc);

public sealed record TicketStatusChangeResponse(
    int Id, string? PreviousStatus, string Status, DateTimeOffset ChangedAtUtc);

public sealed record TicketAttachmentResponse(int Id, string FileName, int SizeBytes, DateTimeOffset CreatedAtUtc);

public sealed record TicketActivityResponse(
    TicketCommentResponse[] Comments, TicketStatusChangeResponse[] StatusChanges, TicketAttachmentResponse[] Attachments);
