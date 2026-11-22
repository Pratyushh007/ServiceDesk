using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Auth;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Data;

public sealed class ServiceDeskDbContext(DbContextOptions<ServiceDeskDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<IdentityRole>().HasData(
            new IdentityRole { Id = "servicedesk-admin", Name = RoleNames.Admin, NormalizedName = "ADMIN", ConcurrencyStamp = "servicedesk-admin" },
            new IdentityRole { Id = "servicedesk-user", Name = RoleNames.User, NormalizedName = "USER", ConcurrencyStamp = "servicedesk-user" });

        var ticket = modelBuilder.Entity<Ticket>();
        ticket.Property(item => item.Title).HasMaxLength(Ticket.TitleMaxLength);
        ticket.Property(item => item.Description).HasMaxLength(Ticket.DescriptionMaxLength);
        ticket.Property(item => item.Status).HasMaxLength(32);
        ticket.HasOne<Category>()
            .WithMany()
            .HasForeignKey(item => item.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        ticket.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(item => item.OwnerId)
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
