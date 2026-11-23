using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ServiceDesk.Api.Data;

namespace ServiceDesk.Api.Auth;

public sealed class AccountRegistration(ServiceDeskDbContext database, UserManager<IdentityUser> users)
{
    public async Task<IdentityResult> CreateAsync(
        string email, string password, string role, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var user = new IdentityUser { Email = email, UserName = email };

        try
        {
            var created = await users.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                return created;
            }

            var assigned = await users.AddToRoleAsync(user, role);
            if (assigned.Succeeded)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return assigned;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "DuplicateAccount",
                Description = "An account already exists for this email address."
            });
        }
    }
}
