using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BounceRoute.Api.Maps;

// OpenRouteService's optimization endpoint (VROOM). Times go over the wire as Unix seconds.
public sealed class OpenRouteServiceOptimizer(HttpClient http, ILogger<OpenRouteServiceOptimizer> logger) : IRouteOptimizer
{
	public async Task<OptimizerResult?> OptimizeAsync(
		IReadOnlyList<OptimizerStop> stops, IReadOnlyList<OptimizerTruck> trucks, CancellationToken cancellationToken = default)
	{
		static JsonArray Point(GeoPoint point) => [point.Longitude, point.Latitude];
		static long Seconds(DateTimeOffset time) => time.ToUnixTimeSeconds();

		var body = new JsonObject
		{
			["jobs"] = new JsonArray([.. stops.Select(stop => (JsonNode)new JsonObject
			{
				["id"] = stop.Id,
				["location"] = Point(stop.Location),
				["service"] = stop.ServiceMinutes * 60,
				[stop.Kind == StopKind.Setup ? "delivery" : "pickup"] = new JsonArray(stop.Pieces),
				["time_windows"] = new JsonArray(new JsonArray(Seconds(stop.EarliestStart), Seconds(stop.LatestStart)))
			})]),
			["vehicles"] = new JsonArray([.. trucks.Select(truck => (JsonNode)new JsonObject
			{
				["id"] = truck.Id,
				["profile"] = "driving-car",
				["start"] = Point(truck.Shop),
				["end"] = Point(truck.Shop),
				["capacity"] = new JsonArray(truck.Capacity),
				["time_window"] = new JsonArray(Seconds(truck.DayStart), Seconds(truck.DayEnd))
			})])
		};

		try
		{
			using var response = await http.PostAsJsonAsync("optimization", body, cancellationToken);
			response.EnsureSuccessStatusCode();
			using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
			var root = document.RootElement;

			var routes = root.GetProperty("routes").EnumerateArray().Select(route => new OptimizedRoute(
				route.GetProperty("vehicle").GetInt32(),
				[.. route.GetProperty("steps").EnumerateArray()
					.Where(step => step.GetProperty("type").GetString() == "job")
					.Select(step => new OptimizedStep(
						step.GetProperty("job").GetInt32(),
						// Service starts after any wait for the time window to open.
						DateTimeOffset.FromUnixTimeSeconds(step.GetProperty("arrival").GetInt64()
							+ (step.TryGetProperty("waiting_time", out var wait) ? wait.GetInt64() : 0))))]))
				.ToList();
			var unassigned = root.TryGetProperty("unassigned", out var missing)
				? missing.EnumerateArray().Select(stop => stop.GetProperty("id").GetInt32()).ToList()
				: [];

			return new OptimizerResult(routes, unassigned);
		}
		catch (Exception exception) when (exception is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or TaskCanceledException)
		{
			logger.LogWarning(exception, "Route optimization failed for {Stops} stops and {Trucks} trucks", stops.Count, trucks.Count);
			return null;
		}
	}
}
