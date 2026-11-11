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

	public DateTimeOffset CreatedAt { get; set; }

	public DateTimeOffset UpdatedAt { get; set; }

	public List<Job> Jobs { get; set; } = [];

	public List<BookingItem> Items { get; set; } = [];
}
