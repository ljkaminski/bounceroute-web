using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace BounceRoute.Api.Maps;

// OpenRouteService (free tier: about 1,000 address lookups and 500 drive-time requests a day).
// Drive times between the same two places are cached for a day, so reopening a route doesn't use more.
public sealed class OpenRouteServiceMaps(HttpClient http, IMemoryCache cache, ILogger<OpenRouteServiceMaps> logger) : IMaps
{
	public const string ApiKeySetting = "Maps:OpenRouteServiceKey";

	public bool IsConfigured => true;

	public async Task<GeoPoint?> GeocodeAsync(string address, CancellationToken cancellationToken = default)
	{
		try
		{
			var url = $"geocode/search?text={Uri.EscapeDataString(address)}&size=1&boundary.country=US";
			using var document = JsonDocument.Parse(await http.GetStringAsync(url, cancellationToken));
			var features = document.RootElement.GetProperty("features");
			if (features.GetArrayLength() == 0)
				return null;

			// GeoJSON order is longitude, latitude.
			var coordinates = features[0].GetProperty("geometry").GetProperty("coordinates");
			return new GeoPoint(coordinates[1].GetDouble(), coordinates[0].GetDouble());
		}
		catch (Exception exception) when (exception is HttpRequestException or JsonException or KeyNotFoundException or TaskCanceledException)
		{
			logger.LogWarning(exception, "Address lookup failed for {Address}", address);
			return null;
		}
	}

	public async Task<IReadOnlyList<int?>> DriveMinutesAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken = default)
	{
		var legs = new int?[Math.Max(0, points.Count - 1)];
		var missing = Enumerable.Range(0, legs.Length).Where(leg => !cache.TryGetValue(Key(points[leg], points[leg + 1]), out legs[leg])).ToList();
		if (missing.Count == 0)
			return legs;

		try
		{
			// One matrix request covers every leg: durations[i][i + 1] is point i to the next.
			var body = new { locations = points.Select(point => new[] { point.Longitude, point.Latitude }), metrics = new[] { "duration" } };
			using var response = await http.PostAsJsonAsync("v2/matrix/driving-car", body, cancellationToken);
			response.EnsureSuccessStatusCode();
			using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
			var durations = document.RootElement.GetProperty("durations");

			foreach (var leg in missing)
			{
				var seconds = durations[leg][leg + 1];
				legs[leg] = seconds.ValueKind == JsonValueKind.Number ? (int)Math.Ceiling(seconds.GetDouble() / 60) : null;
				if (legs[leg] is not null)
					cache.Set(Key(points[leg], points[leg + 1]), legs[leg], TimeSpan.FromDays(1));
			}
		}
		catch (Exception exception) when (exception is HttpRequestException or JsonException or KeyNotFoundException or TaskCanceledException)
		{
			logger.LogWarning(exception, "Drive time lookup failed for {Count} stops", points.Count);
		}

		return legs;
	}

	private static string Key(GeoPoint from, GeoPoint to) =>
		$"drive:{from.Latitude:F5},{from.Longitude:F5}->{to.Latitude:F5},{to.Longitude:F5}";
}
