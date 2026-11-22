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

        var attachments = await database.TicketAttachments
            .Where(item => item.TicketId == id)
            .OrderBy(item => item.Id)
            .Select(item => new TicketAttachmentResponse(item.Id, item.FileName, item.SizeBytes, item.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return new TicketActivityResponse(comments, statusChanges, attachments);
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

    [HttpPost("{id:int}/attachments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(TicketAttachment.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = TicketAttachment.MaxRequestBytes)]
    public async Task<ActionResult<TicketAttachmentResponse>> AddAttachment(
        int id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (!await VisibleTickets().AnyAsync(ticket => ticket.Id == id, cancellationToken))
        {
            return NotFound();
        }

        if (file is null || file.Length is <= 0 or > TicketAttachment.MaxBytes)
        {
            return InvalidAttachment("Choose a non-empty file up to 5 MB.");
        }

        var fileName = Path.GetFileName(file.FileName.Replace('\\', '/')).Trim();
        if (fileName.Length is 0 or > TicketAttachment.FileNameMaxLength || fileName.Any(char.IsControl))
        {
            return InvalidAttachment("Use a file name between 1 and 180 characters.");
        }

        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, cancellationToken);
        var content = buffer.ToArray();
        var contentType = AttachmentContentType(Path.GetExtension(fileName), content);
        if (contentType is null)
        {
            return InvalidAttachment("Choose a PNG, JPEG, or PDF file with a matching extension.");
        }

        var attachment = new TicketAttachment
        {
            TicketId = id,
            UploadedById = CurrentUserId,
            FileName = fileName,
            ContentType = contentType,
            Content = content,
            SizeBytes = content.Length
        };
        database.TicketAttachments.Add(attachment);
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

        return CreatedAtAction(nameof(DownloadAttachment), new { id, attachmentId = attachment.Id },
            new TicketAttachmentResponse(attachment.Id, attachment.FileName, attachment.SizeBytes, attachment.CreatedAtUtc));
    }

    [HttpGet("{id:int}/attachments/{attachmentId:int}")]
    public async Task<IActionResult> DownloadAttachment(int id, int attachmentId, CancellationToken cancellationToken)
    {
        var visibleTicket = VisibleTickets().Where(ticket => ticket.Id == id);
        var attachment = await database.TicketAttachments
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.TicketId == id && item.Id == attachmentId && visibleTicket.Any(),
                cancellationToken);
        if (attachment is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(attachment.Content, attachment.ContentType, attachment.FileName);
    }

    private ActionResult InvalidAttachment(string message)
    {
        ModelState.AddModelError("file", message);
        return ValidationProblem(ModelState);
    }

    private static string? AttachmentContentType(string extension, ReadOnlySpan<byte> content) =>
        extension.ToLowerInvariant() switch
        {
            ".png" when content.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) => "image/png",
            ".jpg" or ".jpeg" when content.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }) => "image/jpeg",
            ".pdf" when content.StartsWith("%PDF-"u8) => "application/pdf",
            _ => null
        };

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
