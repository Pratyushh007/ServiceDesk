using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Data;

public sealed class InMemoryTicketStore
{
    private readonly Lock _gate = new();
    private readonly List<Ticket> _tickets = [];
    private int _nextId = 1;

    public Ticket Create(string title, string description)
    {
        lock (_gate)
        {
            var ticket = new Ticket(
                _nextId++,
                title,
                description,
                "Open",
                DateTimeOffset.UtcNow);

            _tickets.Add(ticket);
            return ticket;
        }
    }

    public Ticket[] GetAll()
    {
        lock (_gate)
        {
            return _tickets.ToArray();
        }
    }

    public Ticket? GetById(int id)
    {
        lock (_gate)
        {
            return _tickets.Find(ticket => ticket.Id == id);
        }
    }
}
