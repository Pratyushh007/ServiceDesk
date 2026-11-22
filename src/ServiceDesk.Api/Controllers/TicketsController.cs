using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Auth;
using ServiceDesk.Api.Contracts;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tickets")]
public sealed class TicketsController(ServiceDeskDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Ticket[]>> GetAll(
        [FromQuery] TicketListQuery filters, CancellationToken cancellationToken)
    {
        var tickets = VisibleTickets().AsNoTracking();

        var search = filters.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            tickets = tickets.Where(ticket =>
                ticket.Title.Contains(search) || ticket.Description.Contains(search));
        }

        if (filters.Status is { } status)
        {
            tickets = tickets.Where(ticket => ticket.Status == status);
        }

        if (filters.CategoryId is { } categoryId)
        {
            tickets = tickets.Where(ticket => ticket.CategoryId == categoryId);
        }

        var sortedTickets = filters.Sort switch
        {
            "newest" => tickets.OrderByDescending(ticket => ticket.CreatedAtUtc).ThenByDescending(ticket => ticket.Id),
            "oldest" => tickets.OrderBy(ticket => ticket.CreatedAtUtc).ThenBy(ticket => ticket.Id),
            "title" => tickets.OrderBy(ticket => ticket.Title).ThenBy(ticket => ticket.Id),
            _ => tickets.OrderBy(ticket => ticket.Id)
        };

        return await sortedTickets.ToArrayAsync(cancellationToken);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Ticket>> GetById(int id, CancellationToken cancellationToken)
    {
        var ticket = await VisibleTickets()
            .AsNoTracking()
            .SingleOrDefaultAsync(ticket => ticket.Id == id, cancellationToken);
        if (ticket is null)
        {
            return NotFound();
        }

        return ticket;
    }

    [HttpPost]
    public async Task<ActionResult<Ticket>> Create(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        if (!await ValidateCategoryAsync(request.CategoryId, cancellationToken))
        {
            return ValidationProblem(ModelState);
        }

        var ticket = new Ticket
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            CategoryId = request.CategoryId,
            OwnerId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        };

        database.Tickets.Add(ticket);
        await database.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateTicketRequest request, CancellationToken cancellationToken)
    {
        var title = request.Title.Trim();
        var description = request.Description.Trim();

        var affectedRows = await database.Tickets
            .Where(ticket => ticket.Id == id)
            .ExecuteUpdateAsync(update => update
                .SetProperty(ticket => ticket.Title, title)
                .SetProperty(ticket => ticket.Description, description)
                .SetProperty(ticket => ticket.Status, request.Status), cancellationToken);

        return affectedRows == 0 ? NotFound() : NoContent();
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPut("{id:int}/category")]
    public async Task<IActionResult> SetCategory(
        int id, SetTicketCategoryRequest request, CancellationToken cancellationToken)
    {
        if (!await ValidateCategoryAsync(request.CategoryId, cancellationToken))
        {
            return ValidationProblem(ModelState);
        }

        var affectedRows = await database.Tickets
            .Where(ticket => ticket.Id == id)
            .ExecuteUpdateAsync(update => update
                .SetProperty(ticket => ticket.CategoryId, request.CategoryId), cancellationToken);

        return affectedRows == 0 ? NotFound() : NoContent();
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var affectedRows = await database.Tickets
            .Where(ticket => ticket.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        return affectedRows == 0 ? NotFound() : NoContent();
    }

    private IQueryable<Ticket> VisibleTickets()
    {
        if (User.IsInRole(RoleNames.Admin))
        {
            return database.Tickets;
        }

        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return database.Tickets.Where(ticket => ticket.OwnerId == userId);
    }

    private async Task<bool> ValidateCategoryAsync(int? categoryId, CancellationToken cancellationToken)
    {
        if (categoryId is null ||
            await database.Categories.AnyAsync(category => category.Id == categoryId, cancellationToken))
        {
            return true;
        }

        ModelState.AddModelError(nameof(Ticket.CategoryId), "That category does not exist.");
        return false;
    }
}
