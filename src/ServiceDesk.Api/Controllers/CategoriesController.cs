using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;
using ServiceDesk.Api.Models;

namespace ServiceDesk.Api.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(ServiceDeskDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Category[]>> GetAll(CancellationToken cancellationToken)
    {
        return await database.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .ToArrayAsync(cancellationToken);
    }
}
