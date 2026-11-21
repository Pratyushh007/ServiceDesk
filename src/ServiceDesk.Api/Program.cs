using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("ServiceDesk")
    ?? throw new InvalidOperationException("Configure the ServiceDesk connection string before running the API.");
builder.Services.AddDbContext<ServiceDeskDbContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();

app.MapControllers();
app.Run();
