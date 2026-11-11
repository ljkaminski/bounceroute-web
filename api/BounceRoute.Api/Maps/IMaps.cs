namespace BounceRoute.Api.Maps;

public readonly record struct GeoPoint(double Latitude, double Longitude);

// "Not found" means the address isn't on the map, so there's no point asking again until it's edited.
// "Failed" means the service didn't answer (down, or the daily quota is used up), so ask again later.
public enum GeocodeOutcome
{
	Found,
	NotFound,
	Failed
}

public readonly record struct GeocodeResult(GeocodeOutcome Outcome, GeoPoint? Point = null)
{
	public static GeocodeResult Found(GeoPoint point) => new(GeocodeOutcome.Found, point);

	public static readonly GeocodeResult NotFound = new(GeocodeOutcome.NotFound);

	public static readonly GeocodeResult Failed = new(GeocodeOutcome.Failed);
}

// Address lookup and driving times. A null drive time means "unknown" (no map service set up, or the
// service is down); the route plan still works and says the drive time is missing.
public interface IMaps
{
	bool IsConfigured { get; }

	Task<GeocodeResult> GeocodeAsync(string address, CancellationToken cancellationToken = default);

	// Minutes to drive from each point to the next, so the result has one fewer entry than points.
	Task<IReadOnlyList<int?>> DriveMinutesAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken = default);
}

public sealed class NoMaps : IMaps
{
	public bool IsConfigured => false;

	public Task<GeocodeResult> GeocodeAsync(string address, CancellationToken cancellationToken = default) => Task.FromResult(GeocodeResult.Failed);

	public Task<IReadOnlyList<int?>> DriveMinutesAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken = default) =>
		Task.FromResult<IReadOnlyList<int?>>([.. Enumerable.Repeat<int?>(null, Math.Max(0, points.Count - 1))]);
}
