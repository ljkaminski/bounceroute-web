namespace BounceRoute.Api.Maps;

public enum StopKind
{
	// Units go out on the truck from the shop and come off here.
	Setup,
	// Units come onto the truck here and ride back to the shop.
	Pickup
}

public sealed record OptimizerStop(
	int Id,
	GeoPoint Location,
	StopKind Kind,
	int ServiceMinutes,
	int Pieces,
	DateTimeOffset EarliestStart,
	DateTimeOffset LatestStart);

public sealed record OptimizerTruck(int Id, int Capacity, GeoPoint Shop, DateTimeOffset DayStart, DateTimeOffset DayEnd);

public sealed record OptimizedStep(int StopId, DateTimeOffset StartAt);

public sealed record OptimizedRoute(int TruckId, IReadOnlyList<OptimizedStep> Steps);

public sealed record OptimizerResult(IReadOnlyList<OptimizedRoute> Routes, IReadOnlyList<int> Unassigned);

// Works out which truck takes which stops, in what order, given deadlines and how much each truck carries.
// Null means it couldn't run (not set up, or the service failed).
public interface IRouteOptimizer
{
	Task<OptimizerResult?> OptimizeAsync(IReadOnlyList<OptimizerStop> stops, IReadOnlyList<OptimizerTruck> trucks, CancellationToken cancellationToken = default);
}

public sealed class NoRouteOptimizer : IRouteOptimizer
{
	public Task<OptimizerResult?> OptimizeAsync(IReadOnlyList<OptimizerStop> stops, IReadOnlyList<OptimizerTruck> trucks, CancellationToken cancellationToken = default) =>
		Task.FromResult<OptimizerResult?>(null);
}
