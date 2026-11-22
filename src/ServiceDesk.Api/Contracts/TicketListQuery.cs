using System.ComponentModel.DataAnnotations;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Contracts;

public sealed class TicketListQuery
{
    [StringLength(150)]
    public string? Search { get; init; }

    [AllowedValues(null, Ticket.OpenStatus, Ticket.InProgressStatus, Ticket.ResolvedStatus,
        ErrorMessage = "Status must be Open, In Progress, or Resolved.")]
    public string? Status { get; init; }

    [Range(1, int.MaxValue)]
    public int? CategoryId { get; init; }

    [AllowedValues("id", "newest", "oldest", "title",
        ErrorMessage = "Sort must be id, newest, oldest, or title.")]
    public string Sort { get; init; } = "id";
}
