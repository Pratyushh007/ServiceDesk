using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
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
        database.TicketStatusChanges.Add(new TicketStatusChange
        {
            Ticket = ticket,
            ChangedById = CurrentUserId,
            Status = Ticket.OpenStatus,
            ChangedAtUtc = ticket.CreatedAtUtc
        });
        await database.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateTicketRequest request, CancellationToken cancellationToken)
    {
        if (request.IncludesCategory && !await ValidateCategoryAsync(request.CategoryId, cancellationToken))
        {
            return ValidationProblem(ModelState);
        }

        var ticket = await database.Tickets.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (ticket is null)
        {
            return NotFound();
        }

        ticket.Title = request.Title.Trim();
        ticket.Description = request.Description.Trim();
        if (request.IncludesCategory)
        {
            ticket.CategoryId = request.CategoryId;
        }

        if (ticket.Status != request.Status)
        {
            database.TicketStatusChanges.Add(new TicketStatusChange
            {
                Ticket = ticket,
                ChangedById = CurrentUserId,
                PreviousStatus = ticket.Status,
                Status = request.Status
            });
            ticket.Status = request.Status;
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "This ticket changed while saving. Refresh the queue and try again." });
        }

        return NoContent();
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

    [HttpGet("{id:int}/activity")]
    public async Task<ActionResult<TicketActivityResponse>> GetActivity(int id, CancellationToken cancellationToken)
    {
        if (!await VisibleTickets().AnyAsync(ticket => ticket.Id == id, cancellationToken))
        {
            return NotFound();
        }

        var comments = await database.TicketComments
            .Where(item => item.TicketId == id)
            .OrderBy(item => item.Id)
            .Select(item => new TicketCommentResponse(item.Id, item.Body, item.IsStaff, item.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
        var statusChanges = await database.TicketStatusChanges
            .Where(item => item.TicketId == id)
            .OrderBy(item => item.Id)
            .Select(item => new TicketStatusChangeResponse(item.Id, item.PreviousStatus, item.Status, item.ChangedAtUtc))
            .ToArrayAsync(cancellationToken);

        return new TicketActivityResponse(comments, statusChanges);
    }

    [HttpPost("{id:int}/comments")]
    public async Task<ActionResult<TicketCommentResponse>> AddComment(
        int id, CreateTicketCommentRequest request, CancellationToken cancellationToken)
    {
        if (!await VisibleTickets().AnyAsync(ticket => ticket.Id == id, cancellationToken))
        {
            return NotFound();
        }

        var comment = new TicketComment
        {
            TicketId = id,
            AuthorId = CurrentUserId,
            Body = request.Body.Trim(),
            IsStaff = User.IsInRole(RoleNames.Admin)
        };
        database.TicketComments.Add(comment);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 547 })
        {
            if (!await VisibleTickets().AnyAsync(ticket => ticket.Id == id, cancellationToken))
            {
                return NotFound();
            }
            throw;
        }

        return StatusCode(StatusCodes.Status201Created,
            new TicketCommentResponse(comment.Id, comment.Body, comment.IsStaff, comment.CreatedAtUtc));
    }

    private string CurrentUserId => User.FindFirstValue(JwtRegisteredClaimNames.Sub)!;

    private IQueryable<Ticket> VisibleTickets()
    {
        if (User.IsInRole(RoleNames.Admin))
        {
            return database.Tickets;
        }

        var userId = CurrentUserId;
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
