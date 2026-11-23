using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Contracts;

public sealed class UpdateTicketRequest
{
    private int? _categoryId;

    [StringLength(Ticket.TitleMaxLength)]
    public string? Title { get; init; }

    [StringLength(Ticket.DescriptionMaxLength)]
    public string? Description { get; init; }

    [Required]
    [AllowedValues(Ticket.OpenStatus, Ticket.InProgressStatus, Ticket.ResolvedStatus,
        ErrorMessage = "Status must be Open, In Progress, or Resolved.")]
    public string Status { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int? CategoryId
    {
        get => _categoryId;
        init
        {
            _categoryId = value;
            IncludesCategory = true;
        }
    }

    [JsonIgnore]
    public bool IncludesCategory { get; private set; }
}
