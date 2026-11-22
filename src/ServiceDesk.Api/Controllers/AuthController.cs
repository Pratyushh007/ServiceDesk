using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Api.Auth;
using ServiceDesk.Api.Contracts;

namespace ServiceDesk.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AccountRegistration registration,
    UserManager<IdentityUser> users,
    SignInManager<IdentityUser> signIn,
    JwtTokens tokens) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(CredentialsRequest request, CancellationToken cancellationToken)
    {
        var result = await registration.CreateAsync(
            request.Email.Trim(), request.Password, RoleNames.User, cancellationToken);
        if (result.Succeeded)
        {
            return StatusCode(StatusCodes.Status201Created);
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(error.Code, error.Description);
        }

        return ValidationProblem(ModelState);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<ActionResult<AccessTokenResponse>> Login(CredentialsRequest request) => Authenticate(request);

    [AllowAnonymous]
    [HttpPost("admin/login")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public Task<ActionResult<AccessTokenResponse>> AdminLogin(CredentialsRequest request) =>
        Authenticate(request, RoleNames.Admin);

    private async Task<ActionResult<AccessTokenResponse>> Authenticate(CredentialsRequest request, string? requiredRole = null)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !(await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)).Succeeded)
        {
            return Unauthorized();
        }

        var roles = await users.GetRolesAsync(user);
        if (requiredRole is not null && !roles.Contains(requiredRole))
        {
            return Unauthorized();
        }

        return tokens.Issue(user.Id, roles);
    }
}
