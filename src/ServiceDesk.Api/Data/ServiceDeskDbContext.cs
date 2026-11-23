using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Auth;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Data;

public sealed class ServiceDeskDbContext(DbContextOptions<ServiceDeskDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public const string Schema = "servicedesk";

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketStatusChange> TicketStatusChanges => Set<TicketStatusChange>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<IdentityRole>().HasData(
            new IdentityRole { Id = "servicedesk-admin", Name = RoleNames.Admin, NormalizedName = "ADMIN", ConcurrencyStamp = "servicedesk-admin" },
            new IdentityRole { Id = "servicedesk-user", Name = RoleNames.User, NormalizedName = "USER", ConcurrencyStamp = "servicedesk-user" });

        var ticket = modelBuilder.Entity<Ticket>();
        ticket.Property(item => item.Title).HasMaxLength(Ticket.TitleMaxLength);
        ticket.Property(item => item.Description).HasMaxLength(Ticket.DescriptionMaxLength);
        ticket.Property(item => item.Status).HasMaxLength(32);
        ticket.Property(item => item.RowVersion).IsRowVersion();
        ticket.HasOne<Category>()
            .WithMany()
            .HasForeignKey(item => item.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        ticket.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(item => item.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        var comment = modelBuilder.Entity<TicketComment>();
        comment.Property(item => item.Body).HasMaxLength(TicketComment.BodyMaxLength);
        comment.HasOne<Ticket>().WithMany().HasForeignKey(item => item.TicketId).OnDelete(DeleteBehavior.Cascade);
        comment.HasOne<IdentityUser>().WithMany().HasForeignKey(item => item.AuthorId).OnDelete(DeleteBehavior.Restrict);

        var change = modelBuilder.Entity<TicketStatusChange>();
        change.Property(item => item.PreviousStatus).HasMaxLength(32);
        change.Property(item => item.Status).HasMaxLength(32);
        change.HasOne(item => item.Ticket).WithMany().HasForeignKey(item => item.TicketId).OnDelete(DeleteBehavior.Cascade);
        change.HasOne<IdentityUser>().WithMany().HasForeignKey(item => item.ChangedById).OnDelete(DeleteBehavior.Restrict);

        var attachment = modelBuilder.Entity<TicketAttachment>();
        attachment.Property(item => item.FileName).HasMaxLength(TicketAttachment.FileNameMaxLength);
        attachment.Property(item => item.ContentType).HasMaxLength(32);
        attachment.HasOne<Ticket>().WithMany().HasForeignKey(item => item.TicketId).OnDelete(DeleteBehavior.Cascade);
        attachment.HasOne<IdentityUser>().WithMany().HasForeignKey(item => item.UploadedById).OnDelete(DeleteBehavior.Restrict);

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
