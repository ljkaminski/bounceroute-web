using System.ComponentModel.DataAnnotations;

namespace BounceRoute.Api.Contracts;

public sealed record TruckDto(int Id, string Name, int Capacity, bool IsActive, IReadOnlyList<int> CrewMemberIds);

public sealed record SaveTruckRequest(
	[property: Required, MaxLength(30)] string Name,
	[property: Range(1, 50)] int Capacity,
	bool IsActive,
	IReadOnlyList<int>? CrewMemberIds);
