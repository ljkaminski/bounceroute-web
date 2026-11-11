using System.Globalization;
using System.Security.Claims;
using BounceRoute.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BounceRoute.Api.Data;

// Puts the linked crew member id into the session cookie so every request knows whose jobs
// a crew user may see without another database lookup.
public sealed class AppClaimsFactory(UserManager<AppUser> users, RoleManager<IdentityRole> roles, IOptions<IdentityOptions> options)
	: UserClaimsPrincipalFactory<AppUser, IdentityRole>(users, roles, options)
{
	public const string CrewMemberIdClaim = "crew_member_id";

	protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
	{
		var identity = await base.GenerateClaimsAsync(user);
		if (user.CrewMemberId is int crewMemberId)
			identity.AddClaim(new Claim(CrewMemberIdClaim, crewMemberId.ToString(CultureInfo.InvariantCulture)));
		return identity;
	}
}
