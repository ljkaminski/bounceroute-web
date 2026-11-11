using BounceRoute.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BounceRoute.Api.Data;

public static class SetupTimes
{
	// The owner's minutes per unit type, falling back to the starting defaults.
	public static async Task<IReadOnlyDictionary<InventoryCategory, int>> LoadAsync(BounceRouteDbContext db)
	{
		var saved = await db.CategorySetupTimes.ToDictionaryAsync(time => time.Category, time => time.Minutes);
		return Enum.GetValues<InventoryCategory>().ToDictionary(category => category,
			category => saved.GetValueOrDefault(category, CategorySetupTime.Defaults[category]));
	}

	// Time on site for one stop: each item's own override, else its type's time, added up because the crew sets
	// things up one after another. One-off items not in inventory count as "Other".
	public static int ForItems(IEnumerable<BookingItem> items, IReadOnlyDictionary<InventoryCategory, int> times) =>
		items.Sum(item => item.InventoryItem is { } owned
			? owned.SetupMinutes ?? times[owned.Category]
			: times[InventoryCategory.Other]);
}
