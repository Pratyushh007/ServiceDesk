namespace ServiceDesk.Api.Models;

public sealed class Category
{
    public int Id { get; private set; }
    public required string Name { get; init; }
}
