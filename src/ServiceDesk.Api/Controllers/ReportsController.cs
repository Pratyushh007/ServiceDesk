using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Auth;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Admin)]
[Route("api/reports")]
public sealed class ReportsController(ServiceDeskDbContext database) : ControllerBase
{
    private const int MaxTickets = 10_000;
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet("tickets.xlsx")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, ExcelContentType)]
    public async Task<IActionResult> ExportTickets(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        if (from > to)
        {
            ModelState.AddModelError(nameof(to), "The end date must be on or after the start date.");
            return ValidationProblem(ModelState);
        }

        var tickets = database.Tickets.AsNoTracking();
        if (from is { } firstDay)
        {
            var start = AtUtcMidnight(firstDay);
            tickets = tickets.Where(ticket => ticket.CreatedAtUtc >= start);
        }

        if (to is { } lastDay && lastDay < DateOnly.MaxValue)
        {
            var end = AtUtcMidnight(lastDay.AddDays(1));
            tickets = tickets.Where(ticket => ticket.CreatedAtUtc < end);
        }

        var rows = await (
            from ticket in tickets
            join category in database.Categories on ticket.CategoryId equals (int?)category.Id into assigned
            from category in assigned.DefaultIfEmpty()
            orderby ticket.CreatedAtUtc descending, ticket.Id descending
            select new TicketReportRow(ticket.Id, ticket.Title, ticket.Status,
                category == null ? "Uncategorized" : category.Name, ticket.CreatedAtUtc)
        ).Take(MaxTickets + 1).ToArrayAsync(cancellationToken);

        if (rows.Length > MaxTickets)
        {
            ModelState.AddModelError(nameof(from), "This report has more than 10,000 tickets. Choose a smaller date range.");
            return ValidationProblem(ModelState);
        }

        var now = DateTimeOffset.UtcNow;
        Response.Headers.CacheControl = "private, no-store";
        return File(BuildWorkbook(rows, from, to, now), ExcelContentType, $"service-desk-tickets-{now:yyyy-MM-dd}.xlsx");
    }

    private static byte[] BuildWorkbook(TicketReportRow[] rows, DateOnly? from, DateOnly? to, DateTimeOffset now)
    {
        using var workbook = new XLWorkbook();
        var summary = workbook.Worksheets.Add("Summary");
        summary.Cell("A1").Value = "ServiceDesk ticket report";
        summary.Cell("A1").Style.Font.Bold = true;
        summary.Cell("A2").Value = "Created on or after (UTC)";
        summary.Cell("B2").Value = from?.ToString("yyyy-MM-dd") ?? "Any date";
        summary.Cell("A3").Value = "Created on or before (UTC)";
        summary.Cell("B3").Value = to?.ToString("yyyy-MM-dd") ?? "Any date";
        summary.Cell("A4").Value = "Report generated (UTC)";
        summary.Cell("B4").Value = now.UtcDateTime;
        summary.Cell("B4").Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
        summary.Cell("A6").Value = "Status";
        summary.Cell("B6").Value = "Tickets";
        StyleHeader(summary.Range("A6:B6"));

        string[] statuses = [Ticket.OpenStatus, Ticket.InProgressStatus, Ticket.ResolvedStatus];
        for (var i = 0; i < statuses.Length; i++)
        {
            summary.Cell(i + 7, 1).Value = statuses[i];
            summary.Cell(i + 7, 2).Value = rows.Count(row => row.Status == statuses[i]);
        }
        summary.Cell("A10").Value = "Total";
        summary.Cell("B10").Value = rows.Length;
        summary.Range("A10:B10").Style.Font.Bold = true;
        summary.Column(1).Width = 33;
        summary.Column(2).Width = 24;

        var details = workbook.Worksheets.Add("Tickets");
        string[] headers = ["Ticket ID", "Title", "Status", "Category", "Created (UTC)"];
        for (var i = 0; i < headers.Length; i++)
        {
            details.Cell(1, i + 1).Value = headers[i];
        }
        StyleHeader(details.Range("A1:E1"));
        details.Columns("A:E").Width = 23;
        details.Column(2).Width = 55;
        details.Column(5).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
        details.SheetView.FreezeRows(1);

        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            var sheetRow = i + 2;
            details.Cell(sheetRow, 1).Value = row.Id;
            details.Cell(sheetRow, 2).Value = row.Title;
            details.Cell(sheetRow, 3).Value = row.Status;
            details.Cell(sheetRow, 4).Value = row.Category;
            details.Cell(sheetRow, 5).Value = row.CreatedAtUtc.UtcDateTime;
        }

        details.Range(1, 1, rows.Length + 1, headers.Length).SetAutoFilter();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#175C54");
    }

    private static DateTimeOffset AtUtcMidnight(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private sealed record TicketReportRow(int Id, string Title, string Status, string Category, DateTimeOffset CreatedAtUtc);
}
