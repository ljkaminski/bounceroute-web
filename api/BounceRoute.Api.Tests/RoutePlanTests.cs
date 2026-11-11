using System.Net.Http.Json;
using BounceRoute.Api.Contracts;
using BounceRoute.Api.Domain;
using BounceRoute.Api.Maps;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace BounceRoute.Api.Tests;

// Every address is somewhere and every drive takes 15 minutes, so the expected times are easy to check.
// Set Down to make the service fail the way it does when the daily quota runs out.
public sealed class FakeMaps : IMaps
{
	public List<string> Lookups { get; } = [];

	public bool Down { get; set; }

	public bool IsConfigured => true;

	public Task<GeocodeResult> GeocodeAsync(string address, CancellationToken cancellationToken = default)
	{
		Lookups.Add(address);
		if (Down)
			return Task.FromResult(GeocodeResult.Failed);
		return Task.FromResult(address.Contains("Nowhere") ? GeocodeResult.NotFound : GeocodeResult.Found(new GeoPoint(35, -80.5)));
	}

	public Task<IReadOnlyList<int?>> DriveMinutesAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken = default) =>
		Task.FromResult<IReadOnlyList<int?>>([.. Enumerable.Repeat<int?>(15, points.Count - 1)]);
}

public sealed class MapsApiFactory : ApiFactory
{
	public FakeMaps Maps { get; } = new();

	protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureTestServices(services => services.AddSingleton<IMaps>(Maps));
	}
}

public sealed class RoutePlanTests : IAsyncLifetime
{
	private readonly MapsApiFactory factory = new();
	private HttpClient owner = null!;

	public async Task InitializeAsync()
	{
		await factory.InitializeAsync();
		owner = await factory.SignedInClientAsync(Roles.Owner);
		await owner.PutAsJsonAsync("/api/settings", new SaveCompanySettingsRequest("Test", null, null, "1 Shop Rd, Monroe, NC"), ApiFactory.Json);
	}

	public Task DisposeAsync() => factory.DisposeAsync();

	[Fact]
	public async Task Plan_adds_drive_and_setup_time_and_flags_late_stops()
	{
		var combo = await ItemAsync(InventoryCategory.Combo);     // 30 min
		var slide = await ItemAsync(InventoryCategory.WaterSlide); // 45 min
		await BookAsync("A", "09:00", "Truck 1", combo);
		await BookAsync("B", "09:20", "Truck 1", slide);

		var plan = await PlanAsync();

		var truck = Assert.Single(plan.Trucks);
		Assert.True(truck.DriveTimesComplete);
		Assert.Equal(At("08:45"), truck.LeaveShopAt);          // 15 min drive to the first stop
		var (first, second) = (truck.Stops[0], truck.Stops[1]);
		Assert.Equal((At("09:00"), At("09:30")), (first.StartAt, first.FinishAt));
		Assert.Equal(At("09:45"), second.ArriveAt);           // 9:30 finish + 15 min drive
		Assert.Equal(25, second.MinutesLate);                 // scheduled 9:20
		Assert.Equal(At("10:30"), truck.DoneAt);              // 9:45 + 45 min slide
		Assert.Equal(At("10:45"), truck.BackAtShopAt);
		Assert.Equal((75, 45), (truck.WorkMinutes, truck.DriveMinutes));
	}

	[Fact]
	public async Task Early_crews_wait_for_the_scheduled_time()
	{
		var combo = await ItemAsync(InventoryCategory.Combo);
		await BookAsync("A", "09:00", "Truck 1", combo);
		await BookAsync("B", "13:00", "Truck 1", combo);

		var second = (await PlanAsync()).Trucks[0].Stops[1];

		Assert.Equal(At("09:45"), second.ArriveAt);
		Assert.Equal(At("13:00"), second.StartAt);
		Assert.Equal(0, second.MinutesLate);
	}

	[Fact]
	public async Task Each_truck_gets_its_own_plan()
	{
		var combo = await ItemAsync(InventoryCategory.Combo);
		await BookAsync("A", "09:00", "Truck 2", combo);
		await BookAsync("B", "09:00", "Truck 1", combo);

		var plan = await PlanAsync();

		Assert.Equal(["Truck 1", "Truck 2"], plan.Trucks.Select(truck => truck.Truck));
		Assert.All(plan.Trucks, truck => Assert.Equal(0, truck.Stops[0].MinutesLate));
	}

	[Fact]
	public async Task Addresses_are_looked_up_once_and_unfound_ones_are_not_retried()
	{
		var combo = await ItemAsync(InventoryCategory.Combo);
		await BookAsync("A", "09:00", "Truck 1", combo);
		await BookAsync("B", "11:00", "Truck 1", combo, street: "1 Nowhere Ln");

		var plan = await PlanAsync();
		await PlanAsync();

		Assert.False(plan.Trucks[0].DriveTimesComplete);
		Assert.Equal(1, factory.Maps.Lookups.Count(address => address.Contains("Nowhere")));
		Assert.Equal(1, factory.Maps.Lookups.Count(address => address.StartsWith("1 A St")));
	}

	[Fact]
	public async Task Lookups_that_failed_are_tried_again_once_the_service_is_back()
	{
		var combo = await ItemAsync(InventoryCategory.Combo);
		factory.Maps.Down = true;
		await owner.PutAsJsonAsync("/api/settings", new SaveCompanySettingsRequest("Test", null, null, "9 New Shop Rd, Monroe, NC"), ApiFactory.Json);
		await BookAsync("A", "09:00", "Truck 1", combo);

		var whileDown = await PlanAsync();
		Assert.False(whileDown.ShopAddressSet);
		Assert.False(whileDown.Trucks[0].DriveTimesComplete);

		factory.Maps.Down = false;
		var afterwards = await PlanAsync();

		Assert.True(afterwards.ShopAddressSet);
		Assert.True(afterwards.Trucks[0].DriveTimesComplete);
	}

	private static readonly string Day = "2036-09-06";

	private static DateTimeOffset At(string time) => DateTimeOffset.Parse($"{Day}T{time}:00-04:00");

	private async Task<RoutePlanDto> PlanAsync() => await (await owner.GetAsync($"/api/route/plan?date={Day}")).ReadAsync<RoutePlanDto>();

	private async Task<InventoryItemDto> ItemAsync(InventoryCategory category) =>
		await (await owner.PostAsJsonAsync("/api/inventory",
			InventoryTests.Item($"Plan {Guid.NewGuid():N}", quantity: 5) with { Category = category, WaterUse = WaterUse.DryOnly }, ApiFactory.Json))
			.ReadAsync<InventoryItemDto>();

	private async Task BookAsync(string customer, string time, string truck, InventoryItemDto item, string? street = null)
	{
		var setup = new SaveJobScheduleRequest(At(time), truck, []);
		// Pickup on another day so only setups are on this route.
		var pickup = setup with { ScheduledAt = setup.ScheduledAt.AddDays(1) };
		var response = await owner.PostAsJsonAsync("/api/bookings", new SaveBookingRequest(
			customer, null, street ?? $"1 {customer} St", "Monroe, NC 28110", Surface.Grass, null, setup, pickup, [new(null, false, item.Id)]), ApiFactory.Json);
		response.EnsureSuccessStatusCode();
	}
}

public sealed class PartyTimeTests : IAsyncLifetime
{
	private readonly MapsApiFactory factory = new();
	private HttpClient owner = null!;

	public async Task InitializeAsync()
	{
		await factory.InitializeAsync();
		owner = await factory.SignedInClientAsync(Roles.Owner);
		await owner.PutAsJsonAsync("/api/settings", new SaveCompanySettingsRequest("Test", null, null, "1 Shop Rd, Monroe, NC"), ApiFactory.Json);
	}

	public Task DisposeAsync() => factory.DisposeAsync();

	[Fact]
	public async Task Setup_is_late_when_it_wont_be_done_before_the_party()
	{
		var slide = await (await owner.PostAsJsonAsync("/api/inventory",
			InventoryTests.Item($"Party {Guid.NewGuid():N}") with { Category = InventoryCategory.WaterSlide, WaterUse = WaterUse.DryOnly }, ApiFactory.Json))
			.ReadAsync<InventoryItemDto>();
		var at = (string time) => DateTimeOffset.Parse($"2037-03-07T{time}:00-05:00");
		var setup = new SaveJobScheduleRequest(at("11:30"), "Truck 1", []);
		var response = await owner.PostAsJsonAsync("/api/bookings", new SaveBookingRequest(
			"Party Test", null, "1 Party St", "Monroe, NC 28110", Surface.Grass, null, setup, setup with { ScheduledAt = at("18:00") },
			[new(null, false, slide.Id)], PartyStartsAt: at("12:00"), PartyEndsAt: at("16:00")), ApiFactory.Json);
		var booking = await response.ReadAsync<BookingDto>();
		Assert.Equal(at("12:00"), booking.PartyStartsAt);

		var stop = (await (await owner.GetAsync("/api/route/plan?date=2037-03-07")).ReadAsync<RoutePlanDto>()).Trucks[0].Stops[0];

		// Starts on time at 11:30, but a 45 minute slide isn't done until 12:15, after the party starts.
		Assert.Equal(15, stop.MinutesLate);
	}

	[Fact]
	public async Task Pickup_is_late_only_when_more_than_two_hours_after_the_party()
	{
		var at = (string time) => DateTimeOffset.Parse($"2037-03-09T{time}:00-04:00");
		var setup = new SaveJobScheduleRequest(at("10:00"), "Truck 1", []);
		await owner.PostAsJsonAsync("/api/bookings", new SaveBookingRequest(
			"Prompt", null, "1 Party St", "Monroe, NC 28110", Surface.Grass, null, setup, setup with { ScheduledAt = at("17:30") },
			[new("Tables", false)], PartyStartsAt: at("12:00"), PartyEndsAt: at("16:00")), ApiFactory.Json);
		await owner.PostAsJsonAsync("/api/bookings", new SaveBookingRequest(
			"Slow", null, "2 Party St", "Monroe, NC 28110", Surface.Grass, null, setup with { Truck = "Truck 2" },
			setup with { Truck = "Truck 2", ScheduledAt = at("18:30") },
			[new("Tables", false)], PartyStartsAt: at("12:00"), PartyEndsAt: at("16:00")), ApiFactory.Json);

		var plan = await (await owner.GetAsync("/api/route/plan?date=2037-03-09")).ReadAsync<RoutePlanDto>();
		var pickups = plan.Trucks.SelectMany(truck => truck.Stops).Where(stop => stop.Type == JobType.Pickup).ToDictionary(stop => stop.CustomerName);

		Assert.Equal(0, pickups["Prompt"].MinutesLate);  // 1.5 hours after the party
		Assert.Equal(30, pickups["Slow"].MinutesLate);   // 2.5 hours after
	}

		[Fact]
	public async Task Party_has_to_end_after_it_starts()
	{
		var at = (string time) => DateTimeOffset.Parse($"2037-03-08T{time}:00-05:00");
		var setup = new SaveJobScheduleRequest(at("09:00"), "Truck 1", []);
		var response = await owner.PostAsJsonAsync("/api/bookings", new SaveBookingRequest(
			"Party Test", null, "1 Party St", null, Surface.Grass, null, setup, setup with { ScheduledAt = at("18:00") },
			[new("Tables", false)], PartyStartsAt: at("14:00"), PartyEndsAt: at("12:00")), ApiFactory.Json);

		Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
	}
}
