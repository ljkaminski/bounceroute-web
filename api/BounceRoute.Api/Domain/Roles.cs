namespace BounceRoute.Api.Domain;

public static class Roles
{
	// Everything, including managing accounts.
	public const string Owner = "Owner";

	// Bookings, scheduling, crew, and pay.
	public const string Office = "Office";

	// Their own jobs and their own units.
	public const string Crew = "Crew";

	public static readonly string[] All = [Owner, Office, Crew];
}
