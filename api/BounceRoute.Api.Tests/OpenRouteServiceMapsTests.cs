using System.Net;
using System.Text;
using BounceRoute.Api.Maps;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
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
		var (maps, handler) = Create((_, _) => Json("""{"features":[{"geometry":{"coordinates":[-80.55,34.98]},"properties":{"layer":"street"}}]}"""));

		var point = await maps.GeocodeAsync("118 Oak Hollow Dr, Monroe, NC 28110");

		Assert.Equal(new GeoPoint(34.98, -80.55), point);
		Assert.Contains("boundary.country=US", handler.Requests[0].Request.RequestUri!.Query);
	}

	[Fact]
	public async Task Town_or_zip_level_matches_count_as_not_found()
	{
		var (maps, _) = Create((_, _) => Json("""{"features":[{"geometry":{"coordinates":[-80.58,35.02]},"properties":{"layer":"locality"}}]}"""));

		Assert.Null(await maps.GeocodeAsync("118 Made Up Dr, Monroe, NC 28110"));
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

public sealed class MapsConfigurationTests
{
	private sealed class KeyedApiFactory : ApiFactory
	{
		protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
		{
			base.ConfigureWebHost(builder);
			// Real OpenRouteService keys are base64 and can end in "=", which a normal Authorization header rejects.
			builder.UseSetting(OpenRouteServiceMaps.ApiKeySetting, "eyJvcmciOiJ0ZXN0In0=");
		}
	}

	[Fact]
	public async Task A_key_ending_in_equals_signs_sets_up_the_map_client()
	{
		await using var factory = new KeyedApiFactory();
		await factory.InitializeAsync();

		using var scope = factory.Services.CreateScope();
		var maps = scope.ServiceProvider.GetRequiredService<IMaps>();

		Assert.IsType<OpenRouteServiceMaps>(maps);
	}
}

public sealed class OpenRouteServiceOptimizerTests
{
	private sealed class Handler(string reply) : HttpMessageHandler
	{
		public string Sent { get; private set; } = "";

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Sent = await request.Content!.ReadAsStringAsync(cancellationToken);
			return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(reply, Encoding.UTF8, "application/json") };
		}
	}

	[Fact]
	public async Task Sends_deadlines_and_capacity_and_reads_back_start_times()
	{
		var start = DateTimeOffset.FromUnixTimeSeconds(1_900_000_000);
		var handler = new Handler($$"""
			{"routes":[{"vehicle":2,"steps":[{"type":"start"},{"type":"job","job":7,"arrival":{{start.ToUnixTimeSeconds() + 600}},"waiting_time":300},{"type":"end"}]}],
			 "unassigned":[{"id":9}]}
			""");
		var optimizer = new OpenRouteServiceOptimizer(new HttpClient(handler) { BaseAddress = new Uri("https://api.openrouteservice.org/") },
			NullLogger<OpenRouteServiceOptimizer>.Instance);

		var result = await optimizer.OptimizeAsync(
			[new(7, new(35.0, -80.5), StopKind.Setup, 30, 2, start, start.AddHours(3)), new(9, new(35.1, -80.6), StopKind.Pickup, 30, 1, start, start.AddHours(9))],
			[new(2, 6, new(35.2, -80.7), start, start.AddHours(16))]);

		Assert.Contains("\"delivery\":[2]", handler.Sent);
		Assert.Contains("\"pickup\":[1]", handler.Sent);
		Assert.Contains("\"capacity\":[6]", handler.Sent);
		Assert.Contains("\"service\":1800", handler.Sent);
		var step = Assert.Single(Assert.Single(result!.Routes).Steps);
		Assert.Equal((7, start.AddMinutes(15)), (step.StopId, step.StartAt)); // 10 min drive + 5 min wait
		Assert.Equal([9], result.Unassigned);
	}
}
