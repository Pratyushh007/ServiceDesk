using ServiceDesk.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<InMemoryTicketStore>();

var app = builder.Build();

app.MapControllers();
app.Run();
