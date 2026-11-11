using System.Net;
using System.Net.Http.Json;
using BounceRoute.Api.Contracts;
using BounceRoute.Api.Domain;

namespace BounceRoute.Api.Tests;

public sealed class InventoryTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
	private HttpClient office = null!;

	public async Task InitializeAsync() => office = await factory.SignedInClientAsync(Roles.Office);

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Office_adds_an_item_and_sees_it_in_the_list()
	{
		var name = $"Jungle Combo {Guid.NewGuid():N}";
		var created = await (await office.PostAsJsonAsync("/api/inventory", Item(name, quantity: 3), ApiFactory.Json))
			.ReadAsync<InventoryItemDto>();

		var list = await (await office.GetAsync("/api/inventory")).ReadAsync<List<InventoryItemDto>>();

		var item = Assert.Single(list, item => item.Id == created.Id);
		Assert.Equal(3, item.Quantity);
		Assert.Equal(WaterUse.WetOrDry, item.WaterUse);
		Assert.Equal(250m, item.DailyPrice);
	}

	[Fact]
	public async Task Names_must_be_unique_ignoring_case()
	{
		var name = $"Castle {Guid.NewGuid():N}";
		await office.PostAsJsonAsync("/api/inventory", Item(name), ApiFactory.Json);

		var duplicate = await office.PostAsJsonAsync("/api/inventory", Item(name.ToUpperInvariant()), ApiFactory.Json);

		Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
	}

	[Fact]
	public async Task Quantity_must_be_at_least_one()
	{
		var response = await office.PostAsJsonAsync("/api/inventory", Item($"Zero {Guid.NewGuid():N}", quantity: 0), ApiFactory.Json);

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task Retired_items_drop_off_the_list_unless_asked_for()
	{
		var created = await (await office.PostAsJsonAsync("/api/inventory", Item($"Old {Guid.NewGuid():N}"), ApiFactory.Json))
			.ReadAsync<InventoryItemDto>();
		await office.PutAsJsonAsync($"/api/inventory/{created.Id}", Item(created.Name) with { IsActive = false }, ApiFactory.Json);

		var active = await (await office.GetAsync("/api/inventory")).ReadAsync<List<InventoryItemDto>>();
		var all = await (await office.GetAsync("/api/inventory?includeInactive=true")).ReadAsync<List<InventoryItemDto>>();

		Assert.DoesNotContain(active, item => item.Id == created.Id);
		Assert.Contains(all, item => item.Id == created.Id);
	}

	[Fact]
	public async Task Crew_cannot_manage_inventory()
	{
		var crew = await factory.SignedInClientAsync(Roles.Crew);

		Assert.Equal(HttpStatusCode.Forbidden, (await crew.GetAsync("/api/inventory")).StatusCode);
	}

	internal static SaveInventoryItemRequest Item(string name, int quantity = 1) => new(
		name, InventoryCategory.Combo, IsUnit: true, quantity, WaterUse.WetOrDry, NeedsPower: true,
		LengthFt: 30, WidthFt: 14, HeightFt: 16, DailyPrice: 250m, Notes: null);
}

public sealed class InventoryImportTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
	[Fact]
	public async Task Import_adds_new_items_and_updates_existing_ones_by_name()
	{
		var office = await factory.SignedInClientAsync(Roles.Office);
		var existing = $"Existing {Guid.NewGuid():N}";
		var added = $"Added {Guid.NewGuid():N}";
		await office.PostAsJsonAsync("/api/inventory", InventoryTests.Item(existing, quantity: 1), ApiFactory.Json);

		var result = await (await office.PostAsJsonAsync("/api/inventory/import",
			new[] { InventoryTests.Item(existing.ToUpperInvariant(), quantity: 2), InventoryTests.Item(added) }, ApiFactory.Json))
			.ReadAsync<InventoryImportResult>();

		Assert.Equal(new InventoryImportResult(Created: 1, Updated: 1), result);
		var list = await (await office.GetAsync("/api/inventory")).ReadAsync<List<InventoryItemDto>>();
		Assert.Equal(2, list.Single(item => item.Name.Equals(existing, StringComparison.OrdinalIgnoreCase)).Quantity);
		Assert.Contains(list, item => item.Name == added);
	}
}
