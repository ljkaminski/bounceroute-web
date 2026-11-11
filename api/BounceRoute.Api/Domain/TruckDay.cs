namespace BounceRoute.Api.Domain;

// A truck going out on a given day. A day with no rows uses every truck that's in service.
public sealed class TruckDay
{
	public DateOnly Date { get; set; }

	public int TruckId { get; set; }
}
