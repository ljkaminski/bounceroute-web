namespace BounceRoute.Api.Maps;

public readonly record struct GeoPoint(double Latitude, double Longitude);

// Address lookup and driving times. Null means "unknown" (no map service set up, address not found,
// or the service is down); the route plan still works and says the drive time is missing.
public interface IMaps
{
	bool IsConfigured { get; }

	Task<GeoPoint?> GeocodeAsync(string address, CancellationToken cancellationToken = default);

	// Minutes to drive from each point to the next, so the result has one fewer entry than points.
	Task<IReadOnlyList<int?>> DriveMinutesAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken = default);
}

public sealed class NoMaps : IMaps
{
	public bool IsConfigured => false;

	public Task<GeoPoint?> GeocodeAsync(string address, CancellationToken cancellationToken = default) => Task.FromResult<GeoPoint?>(null);

	public Task<IReadOnlyList<int?>> DriveMinutesAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken = default) =>
		Task.FromResult<IReadOnlyList<int?>>([.. Enumerable.Repeat<int?>(null, Math.Max(0, points.Count - 1))]);
}
