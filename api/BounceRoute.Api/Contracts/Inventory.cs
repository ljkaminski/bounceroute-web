using System.ComponentModel.DataAnnotations;
using BounceRoute.Api.Domain;

namespace BounceRoute.Api.Contracts;

public sealed record InventoryItemDto(
	int Id,
	string Name,
	InventoryCategory Category,
	bool IsUnit,
	int Quantity,
	WaterUse WaterUse,
	bool NeedsPower,
	int? LengthFt,
	int? WidthFt,
	int? HeightFt,
	decimal? DailyPrice,
	string Notes,
	bool IsActive,
	string? PhotoUrl,
	int? SetupMinutes,
	int TruckSpace);

public sealed record SaveInventoryItemRequest(
	[property: Required, MaxLength(80)] string Name,
	InventoryCategory Category,
	bool IsUnit,
	[property: Range(1, 100)] int Quantity,
	WaterUse WaterUse,
	bool NeedsPower,
	[property: Range(1, 500)] int? LengthFt,
	[property: Range(1, 500)] int? WidthFt,
	[property: Range(1, 100)] int? HeightFt,
	[property: Range(0, 100000)] decimal? DailyPrice,
	[property: MaxLength(500)] string? Notes,
	bool IsActive = true,
	[property: Range(0, 600)] int? SetupMinutes = null,
	[property: Range(0, 10)] int? TruckSpace = null);

public sealed record InventoryImportResult(int Created, int Updated);

public sealed record InventoryAvailabilityDto(int InventoryItemId, int Quantity, int Booked, int Available);
