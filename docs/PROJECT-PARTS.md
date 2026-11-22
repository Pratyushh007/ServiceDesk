# ServiceDesk project parts

This is the development sequence for the **IT Service Desk and Ticket Management System**. Use the part numbers when revisiting the implementation or preparing the practical files.

| Part | Feature | Code to trace |
| --- | --- | --- |
| 1 | Ticket REST API | `Program.cs` maps controllers. `TicketsController` defines ticket routes and HTTP responses. |
| 2 | SQL persistence | `ServiceDeskDbContext` connects the API to SQL Server. EF Core migrations create and evolve the schema. |
| 3 | Ticket CRUD | `TicketsController` creates, reads, updates, and deletes tickets. Request contracts validate incoming data. |
| 4 | Categories | `Category` and `CategoriesController` provide the categories. The ticket stores an optional category foreign key. |
| 5 | Queue search, sorting, and filters | `TicketListQuery` binds query parameters. `TicketsController.GetAll` applies them with LINQ before executing the SQL query. |
| 6 | Login and permissions | `AuthController`, ASP.NET Core Identity, and `JwtTokens` handle accounts and authentication. Ticket queries limit employee access to their own records. |
| 7 | Angular workspace | `app.ts` manages form and screen state. `service-desk-api.ts` sends HTTP requests through the local Angular proxy. |
| 8 | Admin ticket editing | The edit form sends a `PUT` request for title, description, category, and status. |
| 9 | Ticket activity | `TicketComment` and `TicketStatusChange` store messages and progress. The Activity panel requests both for the selected ticket. |
| 10 | Interactive dashboard and VPN form | `DashboardController` groups ticket counts. `DashboardPanel` links status cards to queue filters. The intake form displays an optional network detail for VPN tickets. |
| 11 | Attachments | `TicketAttachment` stores file metadata and bytes. The API enforces ticket access and validates PNG, JPEG, and PDF uploads up to 5 MB. |
| 12 | Excel reporting | `ReportsController` queries tickets by UTC creation date and creates a workbook with summary and ticket sheets. Only admins can export it. |
| 13 | Admin deletion and Swagger testing | The editor asks before sending `DELETE`. SQL Server deletes the ticket's dependent rows. Swagger in Development exercises authenticated endpoints with employee and admin JWTs. |
| 14 | Evaluation materials | The capstone project report documents the architecture, relationships, data flow, and results. The presentation follows the employee and admin workflows. |

## Local demonstration

1. Start the API and frontend using the [README](../README.md). Sign in as an employee, select **Network & VPN**, add a connection type, and submit a ticket.
2. Open **Activity** and add a comment and a small screenshot. Note the ticket number.
3. Sign in as an admin. Search for the ticket, open its attachment, and change the status to **Resolved**.
4. Open the dashboard. Select the **Resolved** card to filter the queue, then return to the dashboard to export the Excel report.
5. Call `GET /api/dashboard/summary` in Swagger using an admin token. A standard employee token receives `403 Forbidden` for this endpoint.
6. Use a disposable demo ticket for deletion. Open **Edit**, select **Delete ticket** and confirm. The ticket and its activity are permanently removed.

## Practical file references

The project provides reusable examples for Git (7), ASP.NET Core REST APIs (12), SQL Server integration with EF Core (16), JWT and roles (17), Angular consuming an API (21), and reactive forms with custom validation (25). Keep each practical's named subject and submission requirements when adapting a project example.
