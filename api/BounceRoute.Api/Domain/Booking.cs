namespace BounceRoute.Api.Domain;

// One customer rental. A booking owns its setup job, its pickup job, and its items.
public sealed class Booking
{
	public int Id { get; set; }

	// Number the office and crews say out loud ("job 1042"). Comes from a database sequence.
	public int JobNumber { get; set; }

	public string CustomerName { get; set; } = string.Empty;

	public string Phone { get; set; } = string.Empty;

	public string Street { get; set; } = string.Empty;

	public string CityStateZip { get; set; } = string.Empty;

	public Surface Surface { get; set; }

	public string SiteNotes { get; set; } = string.Empty;

	// When the customer's party runs. Setup has to be finished before it starts, and pickup can't
	// happen until it ends. The route planner works out the setup and pickup times from these.
	public DateTimeOffset? PartyStartsAt { get; set; }

	public DateTimeOffset? PartyEndsAt { get; set; }

	// Same-day pickups should happen within this long after the party ends, so units aren't left sitting.
	public static readonly TimeSpan PickupWithin = TimeSpan.FromHours(2);

	// Map position, looked up from the address the first time a route plan needs it.
	// GeocodedAddress is the address it was looked up for, so an edited address gets looked up again.
	public double? Latitude { get; set; }

	public double? Longitude { get; set; }

	public string? GeocodedAddress { get; set; }

	public string FullAddress => string.IsNullOrWhiteSpace(CityStateZip) ? Street : $"{Street}, {CityStateZip}";

	public DateTimeOffset CreatedAt { get; set; }

	public DateTimeOffset UpdatedAt { get; set; }

	public List<Job> Jobs { get; set; } = [];

	public List<BookingItem> Items { get; set; } = [];
}
