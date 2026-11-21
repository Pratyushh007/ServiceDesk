using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Data;

public sealed class ServiceDeskDbContext(DbContextOptions<ServiceDeskDbContext> options) : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var ticket = modelBuilder.Entity<Ticket>();
        ticket.Property(item => item.Title).HasMaxLength(Ticket.TitleMaxLength);
        ticket.Property(item => item.Description).HasMaxLength(Ticket.DescriptionMaxLength);
        ticket.Property(item => item.Status).HasMaxLength(32);
    }
}
