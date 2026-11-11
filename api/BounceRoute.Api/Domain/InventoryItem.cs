namespace BounceRoute.Api.Domain;

// Something the company owns and rents out. Bookings pick from this list, so names stay
// consistent, pay counting is decided once per item, and the app can tell when it's all booked.
public sealed class InventoryItem
{
	public int Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public InventoryCategory Category { get; set; }

	// Units count toward crew pay; extras (generators, games) do not.
	public bool IsUnit { get; set; }

	// How many identical copies the company owns, e.g. three of the same combo.
	public int Quantity { get; set; } = 1;

	public WaterUse WaterUse { get; set; }

	public bool NeedsPower { get; set; }

	// Footprint and height in feet, so the office can check the unit fits the site.
	public int? LengthFt { get; set; }

	public int? WidthFt { get; set; }

	public int? HeightFt { get; set; }

	public decimal? DailyPrice { get; set; }

	public string Notes { get; set; } = string.Empty;

	// Bumped on every photo upload; null when there's no photo. Goes in the photo URL so browsers
	// cache a photo forever but fetch the new one as soon as it changes.
	public int? PhotoVersion { get; set; }

	// Retired items stay so old bookings still point at something.
	public bool IsActive { get; set; } = true;
}
