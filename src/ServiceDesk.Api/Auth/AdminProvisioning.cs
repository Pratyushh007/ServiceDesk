using Microsoft.AspNetCore.Identity;

namespace ServiceDesk.Api.Auth;

internal static class AdminProvisioning
{
    public static async Task RunAsync(WebApplication app)
    {
        var email = app.Configuration["Admin:Email"]?.Trim();
        var password = app.Configuration["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Set Admin__Email and Admin__Password before provisioning an admin.");
        }

        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (!await users.IsInRoleAsync(existing, RoleNames.Admin))
            {
                throw new InvalidOperationException("This email belongs to an existing non-admin account.");
            }

            Console.WriteLine("The administrator account already exists.");
            return;
        }

        var registration = scope.ServiceProvider.GetRequiredService<AccountRegistration>();
        var result = await registration.CreateAsync(email, password, RoleNames.Admin, CancellationToken.None);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        Console.WriteLine("Administrator account created.");
    }
}
