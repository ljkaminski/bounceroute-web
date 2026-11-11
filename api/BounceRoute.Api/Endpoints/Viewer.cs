using System.Globalization;
using System.Security.Claims;
using BounceRoute.Api.Data;
using BounceRoute.Api.Domain;

namespace BounceRoute.Api.Endpoints;

// Who is asking, reduced to what decides which jobs they can see.
public sealed record Viewer(bool IsOffice, int? CrewMemberId)
{
	public static Viewer From(ClaimsPrincipal principal)
	{
		var crewClaim = principal.FindFirstValue(AppClaimsFactory.CrewMemberIdClaim);
		return new Viewer(
			principal.IsInRole(Roles.Owner) || principal.IsInRole(Roles.Office),
			crewClaim is null ? null : int.Parse(crewClaim, CultureInfo.InvariantCulture));
	}

	public bool CanSee(Job job) => IsOffice || job.Assignments.Any(assignment => assignment.CrewMemberId == CrewMemberId);
}

public static class ViewerQueries
{
	// Office sees every job; crew see only jobs they are assigned to.
	public static IQueryable<Job> VisibleTo(this IQueryable<Job> jobs, Viewer viewer) => viewer.IsOffice
		? jobs
		: jobs.Where(job => job.Assignments.Any(assignment => assignment.CrewMemberId == viewer.CrewMemberId));
}
