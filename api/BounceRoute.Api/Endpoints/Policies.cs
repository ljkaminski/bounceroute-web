using BounceRoute.Api.Domain;
using Microsoft.AspNetCore.Authorization;

namespace BounceRoute.Api.Endpoints;

public static class Policies
{
	// Owner and office staff: bookings, scheduling, crew, pay.
	public const string Office = nameof(Office);

	// Account management.
	public const string Owner = nameof(Owner);

	public static void Add(AuthorizationOptions options)
	{
		options.AddPolicy(Office, policy => policy.RequireRole(Roles.Owner, Roles.Office));
		options.AddPolicy(Owner, policy => policy.RequireRole(Roles.Owner));
	}
}
