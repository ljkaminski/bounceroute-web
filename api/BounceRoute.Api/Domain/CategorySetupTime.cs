namespace BounceRoute.Api.Domain;

// How long one unit of a type takes to set up (and the same to take down), set by the owner.
// Used to plan when a truck will finish its route.
public sealed class CategorySetupTime
{
	public InventoryCategory Category { get; set; }

	public int Minutes { get; set; }

	// Starting values until the owner changes them.
	public static readonly IReadOnlyDictionary<InventoryCategory, int> Defaults = new Dictionary<InventoryCategory, int>
	{
		[InventoryCategory.BounceHouse] = 20,
		[InventoryCategory.Combo] = 30,
		[InventoryCategory.WaterSlide] = 45,
		[InventoryCategory.ObstacleCourse] = 30,
		[InventoryCategory.SlipNSlide] = 20,
		[InventoryCategory.Game] = 15,
		[InventoryCategory.Equipment] = 5,
		[InventoryCategory.Other] = 10
	};
}
