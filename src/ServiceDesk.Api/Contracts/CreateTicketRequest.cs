using System.ComponentModel.DataAnnotations;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Contracts;

public sealed class CreateTicketRequest
{
    [Required]
    [StringLength(Ticket.TitleMaxLength)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [StringLength(Ticket.DescriptionMaxLength)]
    public string Description { get; init; } = string.Empty;
}
