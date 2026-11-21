using System.ComponentModel.DataAnnotations;

namespace ServiceDesk.Api.Contracts;

public sealed class CreateTicketRequest
{
    [Required]
    [StringLength(150)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Description { get; init; } = string.Empty;
}
