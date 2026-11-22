using System.ComponentModel.DataAnnotations;

namespace ServiceDesk.Api.Contracts;

public sealed class CredentialsRequest
{
    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Password { get; init; } = string.Empty;
}
