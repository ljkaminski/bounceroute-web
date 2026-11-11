using BounceRoute.Api.Contracts;
using BounceRoute.Api.Data;
using BounceRoute.Api.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace BounceRoute.Api.Endpoints;

public static class InventoryEndpoints
{
	public static void MapInventoryEndpoints(this IEndpointRouteBuilder app)
	{
		var inventory = app.MapGroup("/api/inventory").WithTags("Inventory").RequireAuthorization(Policies.Office);

		inventory.MapGet("/", ListInventory);
		inventory.MapPost("/", CreateItem);
		inventory.MapPut("/{id:int}", UpdateItem)
			.WithSummary("Updates an item. Retire items with isActive=false instead of deleting so past bookings keep their history.");
	}

	private static Task<List<InventoryItemDto>> ListInventory(bool? includeInactive, BounceRouteDbContext db) => db.InventoryItems
		.Where(item => includeInactive == true || item.IsActive)
		.OrderBy(item => item.Category).ThenBy(item => item.Name)
		.Select(item => ToDto(item))
		.ToListAsync();

	private static async Task<Results<Created<InventoryItemDto>, ValidationProblem>> CreateItem(
		SaveInventoryItemRequest request, BounceRouteDbContext db)
	{
		if (await ValidateAsync(request, db, id: null) is { } problem)
			return problem;

		var item = new InventoryItem();
		Apply(request, item);
		db.InventoryItems.Add(item);
		await db.SaveChangesAsync();
		return TypedResults.Created($"/api/inventory/{item.Id}", ToDto(item));
	}

	private static async Task<Results<Ok<InventoryItemDto>, NotFound, ValidationProblem>> UpdateItem(
		int id, SaveInventoryItemRequest request, BounceRouteDbContext db)
	{
		var item = await db.InventoryItems.FindAsync(id);
		if (item is null)
			return TypedResults.NotFound();

		if (await ValidateAsync(request, db, id) is { } problem)
			return problem;

		Apply(request, item);
		await db.SaveChangesAsync();
		return TypedResults.Ok(ToDto(item));
	}

	private static async Task<ValidationProblem?> ValidateAsync(SaveInventoryItemRequest request, BounceRouteDbContext db, int? id)
	{
		var name = request.Name.Trim();
		if (await db.InventoryItems.AnyAsync(item => item.Name.ToLower() == name.ToLower() && item.Id != id))
			return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Name"] = ["There's already an item with that name."] });

		return null;
	}

	private static void Apply(SaveInventoryItemRequest request, InventoryItem item)
	{
		item.Name = request.Name.Trim();
		item.Category = request.Category;
		item.IsUnit = request.IsUnit;
		item.Quantity = request.Quantity;
		item.WaterUse = request.WaterUse;
		item.NeedsPower = request.NeedsPower;
		item.LengthFt = request.LengthFt;
		item.WidthFt = request.WidthFt;
		item.HeightFt = request.HeightFt;
		item.DailyPrice = request.DailyPrice;
		item.Notes = request.Notes?.Trim() ?? string.Empty;
		item.IsActive = request.IsActive;
	}

	internal static InventoryItemDto ToDto(InventoryItem item) => new(
		item.Id, item.Name, item.Category, item.IsUnit, item.Quantity, item.WaterUse, item.NeedsPower,
		item.LengthFt, item.WidthFt, item.HeightFt, item.DailyPrice, item.Notes, item.IsActive);
}
