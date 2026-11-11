namespace BounceRoute.Api.Domain;

// Links a crew member to a job. A job has 2-3 crew; a crew member works many jobs.
public sealed class JobAssignment
{
	public int JobId { get; set; }

	public int CrewMemberId { get; set; }

	public CrewMember CrewMember { get; set; } = null!;
}
