using System.Net;
using System.Net.Http.Json;
using BounceRoute.Api.Contracts;
using BounceRoute.Api.Domain;

namespace BounceRoute.Api.Tests;

public sealed class AvailabilityTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
	private HttpClient office = null!;

	public async Task InitializeAsync() => office = await factory.SignedInClientAsync(Roles.Office);

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Can_book_as_many_as_the_company_owns_and_no_more()
	{
		var combo = await CreateItemAsync(quantity: 3);

		for (var booking = 0; booking < 3; booking++)
			Assert.Equal(HttpStatusCode.Created, (await BookAsync(combo, "2033-06-04T09:00:00-04:00", "2033-06-04T18:00:00-04:00")).StatusCode);

		var fourth = await BookAsync(combo, "2033-06-04T12:00:00-04:00", "2033-06-04T20:00:00-04:00");
		Assert.Equal(HttpStatusCode.BadRequest, fourth.StatusCode);
		Assert.Contains("already booked", await fourth.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Back_to_back_bookings_do_not_clash()
	{
		var slide = await CreateItemAsync(quantity: 1);
		await BookAsync(slide, "2033-06-05T09:00:00-04:00", "2033-06-05T13:00:00-04:00");

		var afternoon = await BookAsync(slide, "2033-06-05T13:00:00-04:00", "2033-06-05T19:00:00-04:00");

		Assert.Equal(HttpStatusCode.Created, afternoon.StatusCode);
	}

	[Fact]
	public async Task Editing_a_booking_does_not_clash_with_itself()
	{
		var slide = await CreateItemAsync(quantity: 1);
		var booking = await (await BookAsync(slide, "2033-06-06T09:00:00-04:00", "2033-06-06T17:00:00-04:00")).ReadAsync<BookingDto>();

		var moved = await office.PutAsJsonAsync($"/api/bookings/{booking.Id}",
			Request(slide, "2033-06-06T10:00:00-04:00", "2033-06-06T18:00:00-04:00"), ApiFactory.Json);

		Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
	}

	[Fact]
	public async Task Availability_counts_overlapping_bookings()
	{
		var combo = await CreateItemAsync(quantity: 2);
		await BookAsync(combo, "2033-06-07T09:00:00-04:00", "2033-06-08T09:00:00-04:00");

		var overlapping = await AvailabilityAsync(combo, "2033-06-07T15:00:00-04:00", "2033-06-07T20:00:00-04:00");
		var nextWeek = await AvailabilityAsync(combo, "2033-06-14T09:00:00-04:00", "2033-06-14T17:00:00-04:00");

		Assert.Equal((2, 1, 1), (overlapping.Quantity, overlapping.Booked, overlapping.Available));
		Assert.Equal(2, nextWeek.Available);
	}

	[Fact]
	public async Task Two_people_booking_the_last_one_at_once_cannot_both_get_it()
	{
		var slide = await CreateItemAsync(quantity: 1);
		var second = await factory.SignedInClientAsync(Roles.Office);

		var results = await Task.WhenAll(
			office.PostAsJsonAsync("/api/bookings", Request(slide, "2033-06-09T09:00:00-04:00", "2033-06-09T17:00:00-04:00"), ApiFactory.Json),
			second.PostAsJsonAsync("/api/bookings", Request(slide, "2033-06-09T10:00:00-04:00", "2033-06-09T18:00:00-04:00"), ApiFactory.Json));

		Assert.Equal(1, results.Count(response => response.StatusCode == HttpStatusCode.Created));
		Assert.Equal(1, results.Count(response => response.StatusCode == HttpStatusCode.BadRequest));
	}

	private async Task<InventoryItemDto> CreateItemAsync(int quantity) =>
		await (await office.PostAsJsonAsync("/api/inventory",
			InventoryTests.Item($"Item {Guid.NewGuid():N}", quantity) with { WaterUse = WaterUse.DryOnly }, ApiFactory.Json))
			.ReadAsync<InventoryItemDto>();

	private Task<HttpResponseMessage> BookAsync(InventoryItemDto item, string setupAt, string pickupAt) =>
		office.PostAsJsonAsync("/api/bookings", Request(item, setupAt, pickupAt), ApiFactory.Json);

	private async Task<InventoryAvailabilityDto> AvailabilityAsync(InventoryItemDto item, string setupAt, string pickupAt)
	{
		var url = $"/api/inventory/availability?setupAt={Uri.EscapeDataString(setupAt)}&pickupAt={Uri.EscapeDataString(pickupAt)}";
		var all = await (await office.GetAsync(url)).ReadAsync<List<InventoryAvailabilityDto>>();
		return all.Single(entry => entry.InventoryItemId == item.Id);
	}

	private static SaveBookingRequest Request(InventoryItemDto item, string setupAt, string pickupAt) => new(
		"Availability Test", null, "4 Test St", null, Surface.Grass, null,
		new SaveJobScheduleRequest(DateTimeOffset.Parse(setupAt), "Truck 1", []),
		new SaveJobScheduleRequest(DateTimeOffset.Parse(pickupAt), "Truck 1", []),
		[new(null, false, item.Id)]);
}
