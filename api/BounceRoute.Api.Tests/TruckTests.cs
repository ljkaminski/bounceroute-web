using System.Net;
using System.Net.Http.Json;
using BounceRoute.Api.Contracts;
using BounceRoute.Api.Domain;

namespace BounceRoute.Api.Tests;

public sealed class TruckTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
	[Fact]
	public async Task Owner_adds_a_truck_with_capacity_and_usual_crew()
	{
		var owner = await factory.SignedInClientAsync(Roles.Owner);
		var crew = await (await owner.PostAsJsonAsync("/api/crew", new SaveCrewMemberRequest("Trailer Lead", null, "Crew Lead"), ApiFactory.Json))
			.ReadAsync<CrewMemberDto>();

		var truck = await (await owner.PostAsJsonAsync("/api/trucks",
			new SaveTruckRequest($"Trailer {Guid.NewGuid():N}"[..20], 7, true, [crew.Id]), ApiFactory.Json)).ReadAsync<TruckDto>();

		Assert.Equal(7, truck.Capacity);
		Assert.Equal([crew.Id], truck.CrewMemberIds);
	}

	[Fact]
	public async Task Renaming_a_truck_renames_it_on_its_jobs()
	{
		var owner = await factory.SignedInClientAsync(Roles.Owner);
		var oldName = $"Old {Guid.NewGuid():N}"[..20];
		var truck = await (await owner.PostAsJsonAsync("/api/trucks", new SaveTruckRequest(oldName, 6, true, []), ApiFactory.Json)).ReadAsync<TruckDto>();
		var setup = new SaveJobScheduleRequest(DateTimeOffset.Parse("2037-01-03T09:00:00-05:00"), oldName, []);
		var booking = await (await owner.PostAsJsonAsync("/api/bookings", new SaveBookingRequest(
			"Rename Test", null, "1 Test St", null, Surface.Grass, null, setup, setup with { ScheduledAt = setup.ScheduledAt.AddHours(6) },
			[new("Tables", false)]), ApiFactory.Json)).ReadAsync<BookingDto>();

		var newName = $"New {Guid.NewGuid():N}"[..20];
		await owner.PutAsJsonAsync($"/api/trucks/{truck.Id}", new SaveTruckRequest(newName, 6, true, []), ApiFactory.Json);

		var reloaded = await (await owner.GetAsync($"/api/bookings/{booking.Id}")).ReadAsync<BookingDto>();
		Assert.Equal((newName, newName), (reloaded.Setup.Truck, reloaded.Pickup.Truck));
	}

	[Fact]
	public async Task Office_can_see_trucks_but_only_the_owner_changes_them()
	{
		var office = await factory.SignedInClientAsync(Roles.Office);

		Assert.Equal(HttpStatusCode.OK, (await office.GetAsync("/api/trucks")).StatusCode);
		Assert.Equal(HttpStatusCode.Forbidden,
			(await office.PostAsJsonAsync("/api/trucks", new SaveTruckRequest("Nope", 6, true, []), ApiFactory.Json)).StatusCode);
	}

	[Fact]
	public async Task Equipment_takes_no_truck_space_unless_set()
	{
		var office = await factory.SignedInClientAsync(Roles.Office);

		var generator = await (await office.PostAsJsonAsync("/api/inventory",
			InventoryTests.Item($"Gen {Guid.NewGuid():N}") with { Category = InventoryCategory.Equipment, WaterUse = WaterUse.None }, ApiFactory.Json))
			.ReadAsync<InventoryItemDto>();
		var bigSlide = await (await office.PostAsJsonAsync("/api/inventory",
			InventoryTests.Item($"30ft {Guid.NewGuid():N}") with { Category = InventoryCategory.WaterSlide, TruckSpace = 2 }, ApiFactory.Json))
			.ReadAsync<InventoryItemDto>();

		Assert.Equal((0, 2), (generator.TruckSpace, bigSlide.TruckSpace));
	}
}
