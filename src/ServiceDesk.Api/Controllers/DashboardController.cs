using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Auth;
using ServiceDesk.Api.Contracts;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/dashboard")]
public sealed class DashboardController(ServiceDeskDbContext database) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummary>> GetSummary(CancellationToken cancellationToken)
    {
        var groups = await (
            from ticket in database.Tickets
            join category in database.Categories on ticket.CategoryId equals (int?)category.Id into assigned
            from category in assigned.DefaultIfEmpty()
            group ticket by new
            {
                ticket.Status,
                CategoryName = category == null ? "Uncategorized" : category.Name
            }
            into bucket
            select new { bucket.Key.Status, bucket.Key.CategoryName, Count = bucket.Count() }
        ).ToArrayAsync(cancellationToken);

        var categories = groups
            .GroupBy(item => item.CategoryName)
            .Select(bucket => new DashboardCategoryCount(bucket.Key, bucket.Sum(item => item.Count)))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new DashboardSummary(
            groups.Sum(item => item.Count),
            groups.Where(item => item.Status == Ticket.OpenStatus).Sum(item => item.Count),
            groups.Where(item => item.Status == Ticket.InProgressStatus).Sum(item => item.Count),
            groups.Where(item => item.Status == Ticket.ResolvedStatus).Sum(item => item.Count),
            categories);
    }
}
