namespace BounceRoute.Api.Domain;

// One stop on the route: either the setup or the pickup for a booking.
public sealed class Job
{
	public int Id { get; set; }

	public int BookingId { get; set; }

	public Booking Booking { get; set; } = null!;

	public JobType Type { get; set; }

	// Stored in UTC; the route for a day is worked out in the business time zone.
	public DateTimeOffset ScheduledAt { get; set; }

	public string Truck { get; set; } = string.Empty;

	public JobStatus Status { get; set; }

	public DateTimeOffset? CompletedAt { get; set; }

	public int? CompletedByCrewMemberId { get; set; }

	public CrewMember? CompletedBy { get; set; }

	public List<JobAssignment> Assignments { get; set; } = [];
}
