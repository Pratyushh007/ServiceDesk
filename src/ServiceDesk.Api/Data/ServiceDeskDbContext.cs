using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Data;

public sealed class ServiceDeskDbContext(DbContextOptions<ServiceDeskDbContext> options) : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var ticket = modelBuilder.Entity<Ticket>();
        ticket.Property(item => item.Title).HasMaxLength(Ticket.TitleMaxLength);
        ticket.Property(item => item.Description).HasMaxLength(Ticket.DescriptionMaxLength);
        ticket.Property(item => item.Status).HasMaxLength(32);
        ticket.HasOne<Category>()
            .WithMany()
            .HasForeignKey(item => item.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        var category = modelBuilder.Entity<Category>();
        category.Property(item => item.Name).HasMaxLength(80);
        category.HasIndex(item => item.Name).IsUnique();
        category.HasData(
            new { Id = 1, Name = "Hardware" },
            new { Id = 2, Name = "Network & VPN" },
            new { Id = 3, Name = "Software" },
            new { Id = 4, Name = "Access & Accounts" });
    }
}
