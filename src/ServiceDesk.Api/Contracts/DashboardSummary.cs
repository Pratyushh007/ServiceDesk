namespace ServiceDesk.Api.Contracts;

public sealed record DashboardCategoryCount(string Name, int Count);

public sealed record DashboardSummary(
    int Total, int Open, int InProgress, int Resolved, DashboardCategoryCount[] Categories);
