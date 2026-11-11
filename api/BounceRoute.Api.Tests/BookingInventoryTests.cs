using System.Net;
using System.Net.Http.Json;
using BounceRoute.Api.Contracts;
using BounceRoute.Api.Domain;

namespace BounceRoute.Api.Tests;

public sealed class BookingInventoryTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
	private HttpClient office = null!;

	public async Task InitializeAsync() => office = await factory.SignedInClientAsync(Roles.Office);

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Inventory_items_bring_their_name_and_pay_rule()
	{
		var combo = await CreateItemAsync(WaterUse.WetOrDry);

		var booking = await (await BookAsync("2032-05-01", [new(null, false, combo.Id, IsWet: true)])).ReadAsync<BookingDto>();

		var item = Assert.Single(booking.Items);
		Assert.Equal(combo.Name, item.Name);
		Assert.True(item.IsUnit);
		Assert.Equal(combo.Id, item.InventoryItemId);
		Assert.True(item.IsWet);
	}

	[Fact]
	public async Task Wet_or_dry_must_be_chosen_when_the_item_can_go_either_way()
	{
		var combo = await CreateItemAsync(WaterUse.WetOrDry);

		var response = await BookAsync("2032-05-02", [new(null, false, combo.Id)]);

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		Assert.Contains("Choose wet or dry", await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Dry_only_items_cannot_be_booked_wet()
	{
		var obstacle = await CreateItemAsync(WaterUse.DryOnly);

		var response = await BookAsync("2032-05-03", [new(null, false, obstacle.Id, IsWet: true)]);

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		Assert.Contains("dry use only", await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Retired_items_cannot_be_newly_booked()
	{
		var item = await CreateItemAsync(WaterUse.DryOnly);
		await office.PutAsJsonAsync($"/api/inventory/{item.Id}", InventoryTests.Item(item.Name) with { IsActive = false }, ApiFactory.Json);

		var response = await BookAsync("2032-05-04", [new(null, false, item.Id)]);

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		Assert.Contains("retired", await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task One_off_items_still_work_without_the_catalog()
	{
		var booking = await (await BookAsync("2032-05-05", [new("Tables (4)", false)])).ReadAsync<BookingDto>();

		var item = Assert.Single(booking.Items);
		Assert.Null(item.InventoryItemId);
		Assert.Null(item.IsWet);
	}

	private async Task<InventoryItemDto> CreateItemAsync(WaterUse waterUse)
	{
		var request = InventoryTests.Item($"Item {Guid.NewGuid():N}") with { WaterUse = waterUse };
		return await (await office.PostAsJsonAsync("/api/inventory", request, ApiFactory.Json)).ReadAsync<InventoryItemDto>();
	}

	private Task<HttpResponseMessage> BookAsync(string day, IReadOnlyList<SaveBookingItemRequest> items)
	{
		var setup = new SaveJobScheduleRequest(DateTimeOffset.Parse($"{day}T09:00:00-04:00"), "Truck 1", []);
		var request = new SaveBookingRequest("Inventory Test", null, "3 Test St", null, Surface.Grass, null,
			setup, setup with { ScheduledAt = setup.ScheduledAt.AddHours(8) }, items);
		return office.PostAsJsonAsync("/api/bookings", request, ApiFactory.Json);
	}
}
