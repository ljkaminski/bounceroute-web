using Microsoft.AspNetCore.Identity;

namespace BounceRoute.Api.Domain;

// A sign-in account. Crew accounts point at their crew member so the app knows whose jobs to show.
public sealed class AppUser : IdentityUser
{
	public string DisplayName { get; set; } = string.Empty;

	public int? CrewMemberId { get; set; }

	public CrewMember? CrewMember { get; set; }

	// The owner turns an account off by locking it out with no end date.
	// Failed-password lockouts end after a few minutes.
	public bool IsDisabled => LockoutEnd == DateTimeOffset.MaxValue;
}
