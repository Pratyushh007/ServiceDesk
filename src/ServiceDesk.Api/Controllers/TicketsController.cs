using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Api.Contracts;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Controllers;

[ApiController]
[Route("api/tickets")]
public sealed class TicketsController(InMemoryTicketStore store) : ControllerBase
{
    [HttpGet]
    public ActionResult<Ticket[]> GetAll()
    {
        return store.GetAll();
    }

    [HttpGet("{id:int}")]
    public ActionResult<Ticket> GetById(int id)
    {
        var ticket = store.GetById(id);
        if (ticket is null)
        {
            return NotFound();
        }

        return ticket;
    }

    [HttpPost]
    public ActionResult<Ticket> Create(CreateTicketRequest request)
    {
        var ticket = store.Create(request.Title.Trim(), request.Description.Trim());
        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, ticket);
    }
}
