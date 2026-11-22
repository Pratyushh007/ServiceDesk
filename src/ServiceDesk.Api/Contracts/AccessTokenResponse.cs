namespace ServiceDesk.Api.Contracts;

public sealed record AccessTokenResponse(
    string AccessToken, DateTime ExpiresAtUtc, IList<string> Roles, string TokenType = "Bearer");
