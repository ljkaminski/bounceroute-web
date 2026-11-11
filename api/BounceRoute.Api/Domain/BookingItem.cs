namespace BounceRoute.Api.Domain;

// Something that goes out on a booking. Only units (bounce houses, slides,
// slip and slides) count toward pay; extras like generators and games do not.
public sealed class BookingItem
{
	public int Id { get; set; }

	public int BookingId { get; set; }

	public string Name { get; set; } = string.Empty;

	public bool IsUnit { get; set; }

	public bool IsLoaded { get; set; }

	// The catalog item this is, or null for a one-off typed in by hand.
	// Name and IsUnit are copied from it so old bookings don't change if the catalog does.
	public int? InventoryItemId { get; set; }

	public InventoryItem? InventoryItem { get; set; }

	// Set up with water or without. Null when the item isn't an inflatable.
	public bool? IsWet { get; set; }
}
