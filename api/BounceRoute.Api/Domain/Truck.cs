namespace BounceRoute.Api.Domain;

// A truck (or truck and trailer) the company sends out. Jobs refer to it by name.
public sealed class Truck
{
	// Trailers carry about 6-7 pieces; 6 keeps plans from overloading.
	public const int DefaultCapacity = 6;

	public int Id { get; set; }

	public string Name { get; set; } = string.Empty;

	// How many pieces fit on it at once (see InventoryItem.TruckSpace). The route planner won't load more than this.
	public int Capacity { get; set; } = DefaultCapacity;

	// Trucks out of service stay listed for old jobs but aren't planned with.
	public bool IsActive { get; set; } = true;

	// Who usually rides this truck. When the planner moves a job to this truck, this crew goes with it.
	public List<TruckCrewMember> Crew { get; set; } = [];
}

public sealed class TruckCrewMember
{
	public int TruckId { get; set; }

	public int CrewMemberId { get; set; }
}
