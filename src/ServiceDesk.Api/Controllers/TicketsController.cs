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
}
