using BounceRoute.Api.Domain;

namespace BounceRoute.Api.Contracts;

// One stop on a truck's day: when the crew should get there, start, and finish.
public sealed record PlannedStopDto(
	int JobId,
	JobType Type,
	string CustomerName,
	string Street,
	DateTimeOffset ScheduledAt,
	int? DriveMinutesBefore,
	DateTimeOffset ArriveAt,
	DateTimeOffset StartAt,
	DateTimeOffset FinishAt,
	int WorkMinutes,
	int MinutesLate,
	DateTimeOffset? PartyStartsAt);

public sealed record TruckPlanDto(
	string Truck,
	DateTimeOffset? LeaveShopAt,
	DateTimeOffset DoneAt,
	DateTimeOffset? BackAtShopAt,
	int WorkMinutes,
	int DriveMinutes,
	bool DriveTimesComplete,
	IReadOnlyList<PlannedStopDto> Stops);

public sealed record RoutePlanDto(DateOnly Date, bool MapsConfigured, bool ShopAddressSet, IReadOnlyList<TruckPlanDto> Trucks);
