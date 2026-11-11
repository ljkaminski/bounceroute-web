namespace BounceRoute.Api.Domain;

public enum UserRole
{
	// Everything, including managing accounts.
	Owner,

	// Bookings, scheduling, crew, and pay.
	Office,

	// Their own jobs and their own units.
	Crew
}

// Identity stores roles by name; these keep the names in one place.
public static class Roles
{
	public const string Owner = nameof(UserRole.Owner);
	public const string Office = nameof(UserRole.Office);
	public const string Crew = nameof(UserRole.Crew);

	public static readonly string[] All = [Owner, Office, Crew];
}
