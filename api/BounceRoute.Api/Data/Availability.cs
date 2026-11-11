using BounceRoute.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BounceRoute.Api.Data;

// A booking holds its inventory from setup until pickup. Two bookings clash when those windows overlap.
public static class Availability
{
	// How many of each inventory item other bookings already hold at some point between setupAt and pickupAt.
	public static async Task<Dictionary<int, int>> BookedCountsAsync(
		BounceRouteDbContext db, DateTimeOffset setupAt, DateTimeOffset pickupAt, int? excludeBookingId)
	{
		var counts = await db.Bookings
			.Where(booking => booking.Id != excludeBookingId
				&& booking.Jobs.Any(job => job.Type == JobType.Setup && job.ScheduledAt < pickupAt)
				&& booking.Jobs.Any(job => job.Type == JobType.Pickup && job.ScheduledAt > setupAt))
			.SelectMany(booking => booking.Items)
			.Where(item => item.InventoryItemId != null)
			.GroupBy(item => item.InventoryItemId!.Value)
			.Select(group => new { InventoryItemId = group.Key, Count = group.Count() })
			.ToListAsync();

		return counts.ToDictionary(count => count.InventoryItemId, count => count.Count);
	}

	// Bookings are checked and saved one at a time so two people can't grab the last unit at once.
	// The lock is released when the transaction ends.
	public static Task LockBookingsAsync(BounceRouteDbContext db) =>
		db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7461001)");
}
