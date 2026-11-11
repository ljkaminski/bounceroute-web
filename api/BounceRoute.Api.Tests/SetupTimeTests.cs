using System.Net;
using System.Net.Http.Json;
using BounceRoute.Api.Contracts;
using BounceRoute.Api.Domain;

namespace BounceRoute.Api.Tests;

// Each test gets its own database because setup times are company-wide.
public sealed class SetupTimeTests : IAsyncLifetime
{
	private readonly ApiFactory factory = new();

	public Task InitializeAsync() => factory.InitializeAsync();

	public Task DisposeAsync() => factory.DisposeAsync();

	[Fact]
	public async Task Starting_times_cover_every_type()
	{
		var office = await factory.SignedInClientAsync(Roles.Office);

		var times = await (await office.GetAsync("/api/settings/setup-times")).ReadAsync<List<CategorySetupTimeDto>>();

		Assert.Equal(Enum.GetValues<InventoryCategory>().Length, times.Count);
		Assert.Equal(30, times.Single(time => time.Category == InventoryCategory.Combo).Minutes);
		Assert.Equal(45, times.Single(time => time.Category == InventoryCategory.WaterSlide).Minutes);
	}

	[Fact]
	public async Task Job_time_adds_up_each_unit_using_overrides_where_set()
	{
		var owner = await factory.SignedInClientAsync(Roles.Owner);
		await owner.PutAsJsonAsync("/api/settings/setup-times", new[] { new CategorySetupTimeDto(InventoryCategory.Combo, 25) }, ApiFactory.Json);

		var combo = await CreateItemAsync(owner, InventoryCategory.Combo, setupMinutes: null);
		var bigSlide = await CreateItemAsync(owner, InventoryCategory.WaterSlide, setupMinutes: 60);
		var setup = new SaveJobScheduleRequest(DateTimeOffset.Parse("2035-08-01T09:00:00-04:00"), "Truck 1", []);
		var booking = await (await owner.PostAsJsonAsync("/api/bookings", new SaveBookingRequest(
			"Timing Test", null, "6 Test St", null, Surface.Grass, null, setup, setup with { ScheduledAt = setup.ScheduledAt.AddHours(8) },
			[new(null, false, combo.Id), new(null, false, bigSlide.Id), new("Tables (4)", false)]), ApiFactory.Json)).ReadAsync<BookingDto>();

		var job = await (await owner.GetAsync($"/api/jobs/{booking.Setup.Id}")).ReadAsync<JobCardDto>();

		// 25 (combo, owner's new time) + 60 (slide's own override) + 10 (one-off counts as Other)
		Assert.Equal(95, job.WorkMinutes);
	}

	[Fact]
	public async Task Only_the_owner_changes_setup_times()
	{
		var office = await factory.SignedInClientAsync(Roles.Office);

		var response = await office.PutAsJsonAsync("/api/settings/setup-times", new[] { new CategorySetupTimeDto(InventoryCategory.Combo, 5) }, ApiFactory.Json);

		Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
	}

	private static async Task<InventoryItemDto> CreateItemAsync(HttpClient client, InventoryCategory category, int? setupMinutes) =>
		await (await client.PostAsJsonAsync("/api/inventory",
			InventoryTests.Item($"Timed {Guid.NewGuid():N}") with { Category = category, WaterUse = WaterUse.DryOnly, SetupMinutes = setupMinutes },
			ApiFactory.Json)).ReadAsync<InventoryItemDto>();
}
