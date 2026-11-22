using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Contracts;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Controllers;

[ApiController]
[Route("api/tickets")]
public sealed class TicketsController(ServiceDeskDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Ticket[]>> GetAll(CancellationToken cancellationToken)
    {
        return await database.Tickets
            .AsNoTracking()
            .OrderBy(ticket => ticket.Id)
            .ToArrayAsync(cancellationToken);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Ticket>> GetById(int id, CancellationToken cancellationToken)
    {
        var ticket = await database.Tickets
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
        var ticket = new Ticket
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim()
        };

        database.Tickets.Add(ticket);
        await database.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
    }

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

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var affectedRows = await database.Tickets
            .Where(ticket => ticket.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        return affectedRows == 0 ? NotFound() : NoContent();
    }
}
