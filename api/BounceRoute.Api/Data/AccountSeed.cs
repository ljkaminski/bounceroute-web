using BounceRoute.Api.Domain;
using Microsoft.AspNetCore.Identity;

namespace BounceRoute.Api.Data;

public static class AccountSeed
{
	public static async Task EnsureRolesAsync(RoleManager<IdentityRole> roles)
	{
		foreach (var role in Roles.All)
			if (!await roles.RoleExistsAsync(role))
				await roles.CreateAsync(new IdentityRole(role));
	}

	// A fresh install has no accounts and nobody to create them, so the first owner comes from
	// configuration (Auth:BootstrapOwner:Email and :Password). It is only used while there are no users.
	public static async Task EnsureBootstrapOwnerAsync(UserManager<AppUser> users, IConfiguration configuration, ILogger logger)
	{
		if (users.Users.Any())
			return;

		var email = configuration["Auth:BootstrapOwner:Email"];
		var password = configuration["Auth:BootstrapOwner:Password"];
		if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
		{
			logger.LogWarning("No user accounts exist. Set Auth:BootstrapOwner:Email and :Password to create the first owner.");
			return;
		}

		await CreateAsync(users, email, password, "Owner", Roles.Owner);
		logger.LogInformation("Created bootstrap owner account {Email}.", email);
	}

	public static async Task<AppUser> CreateAsync(
		UserManager<AppUser> users, string email, string password, string displayName, string role, int? crewMemberId = null)
	{
		var user = new AppUser { UserName = email, Email = email, DisplayName = displayName, CrewMemberId = crewMemberId };
		var created = await users.CreateAsync(user, password);
		if (!created.Succeeded)
			throw new InvalidOperationException(string.Join(" ", created.Errors.Select(error => error.Description)));

		await users.AddToRoleAsync(user, role);
		return user;
	}
}
