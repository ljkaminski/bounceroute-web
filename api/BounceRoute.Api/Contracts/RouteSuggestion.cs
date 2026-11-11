using BounceRoute.Api.Domain;

namespace BounceRoute.Api.Contracts;

public enum SuggestionStatus
{
	// A plan that beats the current one; show it with a "Use this plan" button.
	Better,
	// The current plan is already as good as the planner can do; show nothing.
	AlreadyBest,
	// Planning didn't run; Reason says why in plain words.
	NotAvailable
}

public sealed record SuggestedChangeDto(
	int JobId,
	JobType Type,
	string CustomerName,
	string FromTruck,
	string ToTruck,
	DateTimeOffset FromTime,
	DateTimeOffset ToTime);

public sealed record PlanSummaryDto(int LateStops, DateTimeOffset? AllDoneAt, int TrucksUsed);

public sealed record RouteSuggestionDto(
	DateOnly Date,
	SuggestionStatus Status,
	string? Reason,
	PlanSummaryDto? Current,
	PlanSummaryDto? Suggested,
	IReadOnlyList<SuggestedChangeDto> Changes,
	IReadOnlyList<string> CantFit,
	string? Fingerprint);

public sealed record ApplySuggestionRequest(string Fingerprint);
