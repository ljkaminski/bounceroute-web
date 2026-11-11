using System.Net;
using System.Net.Http.Json;
using BounceRoute.Api.Contracts;
using BounceRoute.Api.Domain;
using BounceRoute.Api.Maps;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BounceRoute.Api.Tests;

// Stands in for the real optimizer: the test says which truck each stop should go on.
public sealed class FakeOptimizer : IRouteOptimizer
{
	public Func<IReadOnlyList<OptimizerStop>, IReadOnlyList<OptimizerTruck>, OptimizerResult?> Answer { get; set; } = (_, _) => null;

	public List<IReadOnlyList<OptimizerStop>> Calls { get; } = [];

	public Task<OptimizerResult?> OptimizeAsync(IReadOnlyList<OptimizerStop> stops, IReadOnlyList<OptimizerTruck> trucks, CancellationToken cancellationToken = default)
	{
		Calls.Add(stops);
		return Task.FromResult(Answer(stops, trucks));
	}
}

public sealed class SuggestionApiFactory : ApiFactory
{
	public FakeOptimizer Optimizer { get; } = new();

	protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureTestServices(services =>
		{
			services.AddSingleton<IMaps>(new FakeMaps());
			services.AddSingleton<IRouteOptimizer>(Optimizer);
		});
	}
}

public sealed class RouteSuggestionTests : IAsyncLifetime
{
	private const string Day = "2038-05-01";
	private readonly SuggestionApiFactory factory = new();
	private HttpClient owner = null!;
	private TruckDto truck1 = null!, truck2 = null!;
	private CrewMemberDto truck2Crew = null!;

	public async Task InitializeAsync()
	{
		await factory.InitializeAsync();
		owner = await factory.SignedInClientAsync(Roles.Owner);
		await owner.PutAsJsonAsync("/api/settings", new SaveCompanySettingsRequest("Test", null, null, "1 Shop Rd, Monroe, NC"), ApiFactory.Json);
		truck2Crew = await (await owner.PostAsJsonAsync("/api/crew", new SaveCrewMemberRequest("Second Crew", null, "Crew"), ApiFactory.Json)).ReadAsync<CrewMemberDto>();
		truck1 = await (await owner.PostAsJsonAsync("/api/trucks", new SaveTruckRequest("Truck 1", 6, true, []), ApiFactory.Json)).ReadAsync<TruckDto>();
		truck2 = await (await owner.PostAsJsonAsync("/api/trucks", new SaveTruckRequest("Truck 2", 6, true, [truck2Crew.Id]), ApiFactory.Json)).ReadAsync<TruckDto>();
	}

	public Task DisposeAsync() => factory.DisposeAsync();

	[Fact]
	public async Task Offers_a_better_plan_and_using_it_moves_the_stop_and_its_crew()
	{
		// Two noon parties, both on Truck 1: the second combo can't be set up in time.
		var first = await BookAsync("Avery", party: "12:00", truck: "Truck 1");
		var second = await BookAsync("Blake", party: "12:00", truck: "Truck 1");
		factory.Optimizer.Answer = (stops, _) => new OptimizerResult(
			[new(truck1.Id, [new(first.Setup.Id, At("11:30"))]), new(truck2.Id, [new(second.Setup.Id, At("11:00"))])], []);

		var suggestion = await SuggestionAsync();

		Assert.Equal(SuggestionStatus.Better, suggestion.Status);
		Assert.True(suggestion.Suggested!.LateStops < suggestion.Current!.LateStops);
		var change = Assert.Single(suggestion.Changes);
		Assert.Equal(("Blake", "Truck 1", "Truck 2"), (change.CustomerName, change.FromTruck, change.ToTruck));

		var applied = await owner.PostAsJsonAsync($"/api/route/suggestion/apply?date={Day}", new ApplySuggestionRequest(suggestion.Fingerprint!), ApiFactory.Json);
		Assert.Equal(HttpStatusCode.OK, applied.StatusCode);

		var moved = await (await owner.GetAsync($"/api/bookings/{second.Id}")).ReadAsync<BookingDto>();
		Assert.Equal(("Truck 2", At("11:00")), (moved.Setup.Truck, moved.Setup.ScheduledAt));
		Assert.Equal([truck2Crew.Id], moved.Setup.CrewMemberIds);

		// Now the plan matches the suggestion, so there's nothing more to offer.
		Assert.Equal(SuggestionStatus.AlreadyBest, (await SuggestionAsync()).Status);
	}

	[Fact]
	public async Task Sends_deadlines_and_pieces_to_the_planner()
	{
		var booking = await BookAsync("Avery", party: "12:00", truck: "Truck 1", pieces: 2);
		factory.Optimizer.Answer = (_, _) => new OptimizerResult([], []);

		await SuggestionAsync();

		var stops = factory.Optimizer.Calls.Last();
		var setup = stops.Single(stop => stop.Id == booking.Setup.Id);
		Assert.Equal(StopKind.Setup, setup.Kind);
		Assert.Equal(2, setup.Pieces);
		// A 30 minute combo has to start by 11:30 to be done for a noon party.
		Assert.Equal(At("11:30"), setup.LatestStart);
		var pickup = stops.Single(stop => stop.Id == booking.Pickup.Id);
		Assert.Equal(At("16:00"), pickup.EarliestStart);
	}

	[Fact]
	public async Task Stops_that_cant_fit_are_named()
	{
		var booking = await BookAsync("Casey", party: "12:00", truck: "Truck 1");
		factory.Optimizer.Answer = (_, _) => new OptimizerResult([new(truck1.Id, [new(booking.Pickup.Id, At("16:00"))])], [booking.Setup.Id]);

		var suggestion = await SuggestionAsync();

		Assert.Equal(["Casey (setup)"], suggestion.CantFit);
	}

	[Fact]
	public async Task Using_an_old_suggestion_after_the_day_changed_is_refused()
	{
		var first = await BookAsync("Avery", party: "12:00", truck: "Truck 1");
		var second = await BookAsync("Blake", party: "12:00", truck: "Truck 1");
		factory.Optimizer.Answer = (_, _) => new OptimizerResult(
			[new(truck1.Id, [new(first.Setup.Id, At("11:00"))]), new(truck2.Id, [new(second.Setup.Id, At("11:00"))])], []);
		var suggestion = await SuggestionAsync();

		await BookAsync("Drew", party: "14:00", truck: "Truck 1");

		var applied = await owner.PostAsJsonAsync($"/api/route/suggestion/apply?date={Day}", new ApplySuggestionRequest(suggestion.Fingerprint!), ApiFactory.Json);
		Assert.Equal(HttpStatusCode.Conflict, applied.StatusCode);
	}

	[Fact]
	public async Task A_day_that_has_started_is_left_alone()
	{
		var booking = await BookAsync("Avery", party: "12:00", truck: "Truck 1");
		await owner.PostAsJsonAsync($"/api/jobs/{booking.Setup.Id}/complete", new CompleteJobRequest(truck2Crew.Id), ApiFactory.Json);

		var suggestion = await SuggestionAsync();

		Assert.Equal(SuggestionStatus.NotAvailable, suggestion.Status);
		Assert.Contains("started", suggestion.Reason);
	}

	[Fact]
	public async Task Only_trucks_going_out_that_day_are_offered_to_the_planner()
	{
		await BookAsync("Avery", party: "12:00", truck: "Truck 1");
		await owner.PutAsJsonAsync($"/api/route/trucks?date={Day}", new SaveTrucksOutRequest([truck2.Id]), ApiFactory.Json);
		IReadOnlyList<OptimizerTruck>? offered = null;
		factory.Optimizer.Answer = (_, trucks) => { offered = trucks; return new OptimizerResult([], []); };

		await SuggestionAsync();

		Assert.Equal([truck2.Id], offered!.Select(truck => truck.Id));
	}

	private static DateTimeOffset At(string time) => DateTimeOffset.Parse($"{Day}T{time}:00-04:00");

	private async Task<RouteSuggestionDto> SuggestionAsync() =>
		await (await owner.GetAsync($"/api/route/suggestion?date={Day}")).ReadAsync<RouteSuggestionDto>();

	// One unit per stop, like most real bookings; a 4 hour party.
	private async Task<BookingDto> BookAsync(string customer, string party, string truck, int pieces = 1)
	{
		var combo = await (await owner.PostAsJsonAsync("/api/inventory",
			InventoryTests.Item($"Combo {Guid.NewGuid():N}") with { WaterUse = WaterUse.DryOnly, TruckSpace = pieces }, ApiFactory.Json)).ReadAsync<InventoryItemDto>();
		var starts = At(party);
		var setup = new SaveJobScheduleRequest(starts.AddMinutes(-30), truck, []);
		var pickup = new SaveJobScheduleRequest(starts.AddHours(4), truck, []);
		var response = await owner.PostAsJsonAsync("/api/bookings", new SaveBookingRequest(
			customer, null, $"1 {customer} St", "Monroe, NC 28110", Surface.Grass, null, setup, pickup,
			[new(null, false, combo.Id)], PartyStartsAt: starts, PartyEndsAt: starts.AddHours(4)), ApiFactory.Json);
		return await response.ReadAsync<BookingDto>();
	}
}
