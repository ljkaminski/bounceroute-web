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
}
