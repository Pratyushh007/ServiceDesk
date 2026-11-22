using System.ComponentModel.DataAnnotations;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Contracts;

public sealed class UpdateTicketRequest
{
    [Required]
    [StringLength(Ticket.TitleMaxLength)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [StringLength(Ticket.DescriptionMaxLength)]
    public string Description { get; init; } = string.Empty;

    [Required]
    [AllowedValues(Ticket.OpenStatus, "In Progress", "Resolved",
        ErrorMessage = "Status must be Open, In Progress, or Resolved.")]
    public string Status { get; init; } = string.Empty;
}
