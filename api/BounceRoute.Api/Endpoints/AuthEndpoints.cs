using System.Security.Claims;
using BounceRoute.Api.Contracts;
using BounceRoute.Api.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace BounceRoute.Api.Endpoints;

public static class AuthEndpoints
{
	public const string LoginRateLimit = "login";

	public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
	{
		var auth = app.MapGroup("/api/auth").WithTags("Auth");

		auth.MapPost("/login", Login)
			.WithSummary("Signs in with email and password and sets the session cookie.")
			.RequireRateLimiting(LoginRateLimit);

		auth.MapPost("/logout", Logout).RequireAuthorization();

		auth.MapGet("/me", Me)
			.WithSummary("The signed-in user, or 401.")
			.RequireAuthorization();
	}

	private static async Task<Results<Ok<CurrentUserDto>, ProblemHttpResult>> Login(
		LoginRequest request, UserManager<AppUser> users, SignInManager<AppUser> signIn)
	{
		var user = await users.FindByEmailAsync(request.Email.Trim());
		if (user is null)
			return Unauthorized("Email or password is incorrect.");

		var result = await signIn.PasswordSignInAsync(user, request.Password, isPersistent: true, lockoutOnFailure: true);
		if (result.IsLockedOut)
			return Unauthorized(user.IsDisabled
				? "This account is turned off. Ask the owner to turn it back on."
				: "Too many failed attempts. Try again in a few minutes.");
		if (!result.Succeeded)
			return Unauthorized("Email or password is incorrect.");

		return TypedResults.Ok(await ToDtoAsync(user, users));
	}

	private static async Task<NoContent> Logout(SignInManager<AppUser> signIn)
	{
		await signIn.SignOutAsync();
		return TypedResults.NoContent();
	}

	private static async Task<Results<Ok<CurrentUserDto>, UnauthorizedHttpResult>> Me(ClaimsPrincipal principal, UserManager<AppUser> users)
	{
		var user = await users.GetUserAsync(principal);
		if (user is null)
			return TypedResults.Unauthorized();

		return TypedResults.Ok(await ToDtoAsync(user, users));
	}

	internal static async Task<CurrentUserDto> ToDtoAsync(AppUser user, UserManager<AppUser> users)
	{
		var role = (await users.GetRolesAsync(user)).FirstOrDefault() ?? Roles.Crew;
		return new CurrentUserDto(user.Id, user.Email!, user.DisplayName, Enum.Parse<UserRole>(role), user.CrewMemberId);
	}

	private static ProblemHttpResult Unauthorized(string detail) =>
		TypedResults.Problem(detail, statusCode: StatusCodes.Status401Unauthorized, title: "Sign-in failed");
}
