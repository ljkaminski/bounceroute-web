using System.Net;
using System.Text;
using BounceRoute.Api.Maps;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace BounceRoute.Api.Tests;

// Checks the requests we send and how replies are read, using canned OpenRouteService responses.
public sealed class OpenRouteServiceMapsTests
{
	private sealed class FakeHandler(Func<HttpRequestMessage, string, HttpResponseMessage> reply) : HttpMessageHandler
	{
		public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
			Requests.Add((request, body));
			return reply(request, body);
		}
	}

	private static (OpenRouteServiceMaps Maps, FakeHandler Handler) Create(Func<HttpRequestMessage, string, HttpResponseMessage> reply)
	{
		var handler = new FakeHandler(reply);
		var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.openrouteservice.org/") };
		return (new OpenRouteServiceMaps(http, new MemoryCache(new MemoryCacheOptions()), NullLogger<OpenRouteServiceMaps>.Instance), handler);
	}

	private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
		new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

	[Fact]
	public async Task Geocode_reads_latitude_and_longitude_in_the_right_order()
	{
		var (maps, handler) = Create((_, _) => Json("""{"features":[{"geometry":{"coordinates":[-80.55,34.98]}}]}"""));

		var point = await maps.GeocodeAsync("118 Oak Hollow Dr, Monroe, NC 28110");

		Assert.Equal(new GeoPoint(34.98, -80.55), point);
		Assert.Contains("boundary.country=US", handler.Requests[0].Request.RequestUri!.Query);
	}

	[Fact]
	public async Task Unknown_address_is_null_not_an_error()
	{
		var (maps, _) = Create((_, _) => Json("""{"features":[]}"""));

		Assert.Null(await maps.GeocodeAsync("nowhere"));
	}

	[Fact]
	public async Task Drive_minutes_come_from_each_point_to_the_next_rounded_up()
	{
		var (maps, handler) = Create((_, _) => Json("""{"durations":[[0,601,0],[0,0,1199.5],[0,0,0]]}"""));
		GeoPoint[] points = [new(35.0, -80.5), new(35.1, -80.6), new(35.2, -80.7)];

		var legs = await maps.DriveMinutesAsync(points);

		Assert.Equal([11, 20], legs);
		Assert.Contains("[-80.5,35]", handler.Requests[0].Body);
	}

	[Fact]
	public async Task Drive_times_are_cached_so_the_same_route_does_not_ask_again()
	{
		var (maps, handler) = Create((_, _) => Json("""{"durations":[[0,300],[0,0]]}"""));
		GeoPoint[] points = [new(35.0, -80.5), new(35.1, -80.6)];

		await maps.DriveMinutesAsync(points);
		var again = await maps.DriveMinutesAsync(points);

		Assert.Equal([5], again);
		Assert.Single(handler.Requests);
	}

	[Fact]
	public async Task Service_errors_give_unknown_times_instead_of_failing()
	{
		var (maps, _) = Create((_, _) => Json("""{"error":"quota"}""", HttpStatusCode.TooManyRequests));

		var legs = await maps.DriveMinutesAsync([new(35.0, -80.5), new(35.1, -80.6)]);

		Assert.Equal([null], legs);
	}
}
