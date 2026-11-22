using System.ComponentModel.DataAnnotations;

namespace ServiceDesk.Api.Contracts;

public sealed class SetTicketCategoryRequest
{
    [Range(1, int.MaxValue)]
    public required int? CategoryId { get; init; }
}
