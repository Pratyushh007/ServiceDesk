using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Api.Auth;
using ServiceDesk.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("ServiceDesk")
    ?? throw new InvalidOperationException("Configure the ServiceDesk connection string before running the API.");
builder.Services.AddDbContext<ServiceDeskDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddIdentityCore<IdentityUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ServiceDeskDbContext>()
    .AddSignInManager();
builder.Services.AddScoped<AccountRegistration>();

var tokens = new JwtTokens(builder.Configuration["Jwt:SigningKey"]);
builder.Services.AddSingleton(tokens);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = tokens.ValidationParameters();
    });
builder.Services.AddAuthorization(options =>
{
    var signedIn = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim(JwtRegisteredClaimNames.Sub)
        .Build();
    options.DefaultPolicy = signedIn;
    options.FallbackPolicy = signedIn;
});

var app = builder.Build();

if (app.Configuration.GetValue<bool>("seed-admin"))
{
    await AdminProvisioning.RunAsync(app);
    return;
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
