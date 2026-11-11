using Microsoft.AspNetCore.Identity;

namespace BounceRoute.Api.Domain;

// A sign-in account. Crew accounts point at their crew member so the app knows whose jobs to show.
public sealed class AppUser : IdentityUser
{
	public string DisplayName { get; set; } = string.Empty;

	public int? CrewMemberId { get; set; }

	public CrewMember? CrewMember { get; set; }
}
