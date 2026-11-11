using System.ComponentModel.DataAnnotations;
using BounceRoute.Api.Domain;

namespace BounceRoute.Api.Contracts;

public sealed record BookingItemDto(int Id, string Name, bool IsUnit, bool IsLoaded, int? InventoryItemId, bool? IsWet, string? PhotoUrl);

public sealed record JobScheduleDto(
	int Id,
	DateTimeOffset ScheduledAt,
	string Truck,
	JobStatus Status,
	IReadOnlyList<int> CrewMemberIds);

public sealed record BookingDto(
	int Id,
	int JobNumber,
	string CustomerName,
	string Phone,
	string Street,
	string CityStateZip,
	Surface Surface,
	string SiteNotes,
	JobScheduleDto Setup,
	JobScheduleDto Pickup,
	IReadOnlyList<BookingItemDto> Items);

public sealed record BookingSummaryDto(
	int Id,
	int JobNumber,
	string CustomerName,
	string Street,
	DateTimeOffset SetupAt,
	DateTimeOffset PickupAt,
	int UnitCount,
	bool IsComplete);

public sealed record SaveJobScheduleRequest(
	DateTimeOffset ScheduledAt,
	[property: MaxLength(30)] string? Truck,
	IReadOnlyList<int>? CrewMemberIds);

// Either an inventory item (name and pay counting come from the catalog) or a one-off with a name.
// IsWet is required for items that can go either way; ignored for the rest.
public sealed record SaveBookingItemRequest(
	[property: MaxLength(80)] string? Name,
	bool IsUnit,
	int? InventoryItemId = null,
	bool? IsWet = null);

public sealed record SaveBookingRequest(
	[property: Required, MaxLength(100)] string CustomerName,
	[property: MaxLength(20)] string? Phone,
	[property: Required, MaxLength(150)] string Street,
	[property: MaxLength(100)] string? CityStateZip,
	Surface Surface,
	[property: MaxLength(500)] string? SiteNotes,
	[property: Required] SaveJobScheduleRequest Setup,
	[property: Required] SaveJobScheduleRequest Pickup,
	[property: Required] IReadOnlyList<SaveBookingItemRequest> Items);
