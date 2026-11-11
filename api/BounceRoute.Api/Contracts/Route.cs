using BounceRoute.Api.Domain;

namespace BounceRoute.Api.Contracts;

public sealed record CrewSummaryDto(int Id, string Name, string Initials);

// One stop on the route with everything a crew needs for it.
public sealed record JobCardDto(
	int JobId,
	int BookingId,
	int JobNumber,
	JobType Type,
	DateTimeOffset ScheduledAt,
	string Truck,
	JobStatus Status,
	DateTimeOffset? CompletedAt,
	string? CompletedBy,
	bool IsNextUp,
	string CustomerName,
	string Phone,
	string Street,
	string CityStateZip,
	Surface Surface,
	string SiteNotes,
	IReadOnlyList<BookingItemDto> Units,
	IReadOnlyList<BookingItemDto> Extras,
	IReadOnlyList<CrewSummaryDto> Crew);

public sealed record RouteDayDto(DateOnly Date, IReadOnlyList<JobCardDto> Jobs);

// Office staff say who did the job; crew members complete jobs as themselves and can leave it out.
public sealed record CompleteJobRequest(int? CrewMemberId);

public sealed record SetItemLoadedRequest(bool IsLoaded);
