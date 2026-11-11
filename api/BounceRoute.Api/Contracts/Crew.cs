using System.ComponentModel.DataAnnotations;

namespace BounceRoute.Api.Contracts;

public sealed record CrewMemberDto(int Id, string Name, string Initials, string Role, bool IsActive);

public sealed record SaveCrewMemberRequest(
	[property: Required, MaxLength(80)] string Name,
	[property: MaxLength(3)] string? Initials,
	[property: MaxLength(50)] string? Role,
	bool IsActive = true);

// Units on completed jobs a crew member was on, between two dates (inclusive).
public sealed record CrewUnitTotalsDto(
	int CrewMemberId,
	string Name,
	string Initials,
	int Jobs,
	int SetupUnits,
	int PickupUnits,
	int TotalUnits);

public sealed record UnitTotalsReport(DateOnly From, DateOnly To, IReadOnlyList<CrewUnitTotalsDto> Crew);
