using BounceRoute.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BounceRoute.Api.Data;

public static class TrucksOut
{
	// The trucks going out on a day: the office's pick, or every truck in service if they haven't picked.
	public static async Task<(List<Truck> Trucks, bool IsDefault)> ForDayAsync(BounceRouteDbContext db, DateOnly day)
	{
		var picked = await db.TruckDays.Where(row => row.Date == day).Select(row => row.TruckId).ToListAsync();
		var trucks = await db.Trucks.Include(truck => truck.Crew)
			.Where(truck => picked.Count > 0 ? picked.Contains(truck.Id) : truck.IsActive)
			.OrderBy(truck => truck.Name)
			.ToListAsync();
		return (trucks, picked.Count == 0);
	}
}
