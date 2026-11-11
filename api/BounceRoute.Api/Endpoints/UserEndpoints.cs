using System.Security.Claims;
using BounceRoute.Api.Contracts;
using BounceRoute.Api.Data;
using BounceRoute.Api.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BounceRoute.Api.Endpoints;

// Account management for the owner. There is no public sign-up: every account is created here.
public static class UserEndpoints
{
	public static void MapUserEndpoints(this IEndpointRouteBuilder app)
	{
		var users = app.MapGroup("/api/users").WithTags("Users").RequireAuthorization(Policies.Owner);

		users.MapGet("/", ListUsers);
		users.MapPost("/", CreateUser);
		users.MapPut("/{id}", UpdateUser)
			.WithSummary("Changes name, role, crew link, or disables the account. You cannot demote or disable yourself.");
		users.MapPost("/{id}/password", ResetPassword)
			.WithSummary("Sets a new password for someone who forgot theirs.");
	}

	private static async Task<List<UserDto>> ListUsers(BounceRouteDbContext db, UserManager<AppUser> users)
	{
		var accounts = await db.Users.Include(user => user.CrewMember).OrderBy(user => user.DisplayName).ToListAsync();

		var result = new List<UserDto>(accounts.Count);
		foreach (var account in accounts)
			result.Add(await ToDtoAsync(account, users));
		return result;
	}

	private static async Task<Results<Created<UserDto>, ValidationProblem>> CreateUser(
		CreateUserRequest request, UserManager<AppUser> users, BounceRouteDbContext db)
	{
		if (await ValidateCrewLinkAsync(db, request.Role, request.CrewMemberId, userId: null) is { } problem)
			return problem;

		var user = new AppUser
		{
			UserName = request.Email.Trim(),
			Email = request.Email.Trim(),
			DisplayName = request.DisplayName.Trim(),
			CrewMemberId = request.CrewMemberId,
			EmailConfirmed = true
		};
		var created = await users.CreateAsync(user, request.Password);
		if (!created.Succeeded)
			return IdentityProblem(created);

		await users.AddToRoleAsync(user, request.Role.ToString());
		await db.Entry(user).Reference(u => u.CrewMember).LoadAsync();
		return TypedResults.Created($"/api/users/{user.Id}", await ToDtoAsync(user, users));
	}

	private static async Task<Results<Ok<UserDto>, NotFound, ValidationProblem>> UpdateUser(
		string id, UpdateUserRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, BounceRouteDbContext db)
	{
		var user = await users.FindByIdAsync(id);
		if (user is null)
			return TypedResults.NotFound();

		// Keeps the owner from locking everyone out of account management by accident.
		if (user.Id == users.GetUserId(principal) && (request.Role != UserRole.Owner || request.IsDisabled))
			return Problem("Role", "You can't remove your own owner access or disable yourself.");

		if (await ValidateCrewLinkAsync(db, request.Role, request.CrewMemberId, user.Id) is { } problem)
			return problem;

		user.DisplayName = request.DisplayName.Trim();
		user.CrewMemberId = request.CrewMemberId;
		await users.UpdateAsync(user);

		var currentRoles = await users.GetRolesAsync(user);
		if (!currentRoles.SequenceEqual([request.Role.ToString()]))
		{
			await users.RemoveFromRolesAsync(user, currentRoles);
			await users.AddToRoleAsync(user, request.Role.ToString());
		}

		var wasDisabled = user.IsDisabled;
		if (request.IsDisabled != wasDisabled)
			await users.SetLockoutEndDateAsync(user, request.IsDisabled ? DateTimeOffset.MaxValue : null);

		// Role, crew link, and disabled state live in the session cookie; this signs the person out
		// everywhere so their next request picks up the change.
		await users.UpdateSecurityStampAsync(user);

		await db.Entry(user).Reference(u => u.CrewMember).LoadAsync();
		return TypedResults.Ok(await ToDtoAsync(user, users));
	}

	private static async Task<Results<NoContent, NotFound, ValidationProblem>> ResetPassword(
		string id, ResetPasswordRequest request, UserManager<AppUser> users)
	{
		var user = await users.FindByIdAsync(id);
		if (user is null)
			return TypedResults.NotFound();

		var token = await users.GeneratePasswordResetTokenAsync(user);
		var reset = await users.ResetPasswordAsync(user, token, request.NewPassword);
		if (!reset.Succeeded)
			return IdentityProblem(reset);

		// A reset after a run of bad guesses should let them straight back in.
		await users.ResetAccessFailedCountAsync(user);
		if (!user.IsDisabled)
			await users.SetLockoutEndDateAsync(user, null);

		return TypedResults.NoContent();
	}

	private static async Task<ValidationProblem?> ValidateCrewLinkAsync(
		BounceRouteDbContext db, UserRole role, int? crewMemberId, string? userId)
	{
		if (role == UserRole.Crew && crewMemberId is null)
			return Problem("CrewMemberId", "Crew accounts must be linked to a crew member.");

		if (crewMemberId is null)
			return null;

		if (!await db.CrewMembers.AnyAsync(crew => crew.Id == crewMemberId))
			return Problem("CrewMemberId", "No crew member with that id.");

		if (await db.Users.AnyAsync(user => user.CrewMemberId == crewMemberId && user.Id != userId))
			return Problem("CrewMemberId", "That crew member already has an account.");

		return null;
	}

	private static async Task<UserDto> ToDtoAsync(AppUser user, UserManager<AppUser> users)
	{
		var current = await AuthEndpoints.ToDtoAsync(user, users);
		return new UserDto(user.Id, current.Email, user.DisplayName, current.Role, user.CrewMemberId, user.CrewMember?.Name, user.IsDisabled);
	}

	private static ValidationProblem Problem(string field, string message) =>
		TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });

	internal static ValidationProblem IdentityProblem(IdentityResult result) => TypedResults.ValidationProblem(result.Errors
		.GroupBy(error => error.Code.StartsWith("Password", StringComparison.Ordinal) ? "Password"
			: error.Code.Contains("Email", StringComparison.Ordinal) || error.Code.Contains("UserName", StringComparison.Ordinal) ? "Email"
			: "General")
		.ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray()));
}
