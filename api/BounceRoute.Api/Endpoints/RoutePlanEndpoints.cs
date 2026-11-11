using System.Security.Claims;
using BounceRoute.Api.Contracts;
using BounceRoute.Api.Data;
using BounceRoute.Api.Domain;
using BounceRoute.Api.Maps;
using Microsoft.EntityFrameworkCore;

namespace BounceRoute.Api.Endpoints;

public static class RoutePlanEndpoints
{
	public static void MapRoutePlanEndpoints(this IEndpointRouteBuilder app)
	{
		app.MapGet("/api/route/plan", GetPlan)
			.WithTags("Route")
			.WithSummary("Each truck's day: drive and setup time per stop, when it runs late, and when the truck is done.")
			.RequireAuthorization();
	}

	private static async Task<RoutePlanDto> GetPlan(
		DateOnly? date, ClaimsPrincipal user, BounceRouteDbContext db, BusinessClock clock, IMaps maps, CancellationToken cancellationToken)
	{
		var day = date ?? clock.Today;
		var (start, end) = clock.Range(day, day);

		var jobs = await db.Jobs
			.VisibleTo(Viewer.From(user))
			.Include(job => job.Booking).ThenInclude(booking => booking.Items).ThenInclude(item => item.InventoryItem)
			.Where(job => job.ScheduledAt >= start && job.ScheduledAt < end)
			.OrderBy(job => job.ScheduledAt)
			.AsSplitQuery()
			.ToListAsync(cancellationToken);

		var settings = await db.CompanySettings.FindAsync([CompanySettings.SingletonId], cancellationToken);
		var shop = ShopPoint(settings);

		if (maps.IsConfigured)
			await LocateAsync(jobs.Select(job => job.Booking).Distinct(), db, maps, cancellationToken);

		var times = await SetupTimes.LoadAsync(db);
		var trucks = await PlanTrucksAsync(jobs, shop, times, maps, cancellationToken);
		return new RoutePlanDto(day, maps.IsConfigured, shop is not null, trucks);
	}

	internal const string NoTruck = "No truck";

	// Groups a day's jobs by truck and plans each truck in scheduled order.
	internal static async Task<List<TruckPlanDto>> PlanTrucksAsync(
		IEnumerable<Job> jobs, GeoPoint? shop, IReadOnlyDictionary<InventoryCategory, int> times, IMaps maps, CancellationToken cancellationToken)
	{
		var trucks = new List<TruckPlanDto>();
		foreach (var truck in jobs.GroupBy(job => string.IsNullOrWhiteSpace(job.Truck) ? NoTruck : job.Truck).OrderBy(group => group.Key))
			trucks.Add(await PlanTruckAsync(truck.Key, [.. truck.OrderBy(job => job.ScheduledAt)], shop, times, maps, cancellationToken));
		return trucks;
	}

	internal static GeoPoint? ShopPoint(CompanySettings? settings) =>
		settings is { ShopLatitude: double lat, ShopLongitude: double lon } ? new GeoPoint(lat, lon) : null;

	private static async Task<TruckPlanDto> PlanTruckAsync(
		string truck, List<Job> stops, GeoPoint? shop, IReadOnlyDictionary<InventoryCategory, int> times, IMaps maps, CancellationToken cancellationToken)
	{
		var legs = await LegsAsync(stops, shop, maps, cancellationToken);
		var planned = new List<PlannedStopDto>();
		DateTimeOffset? previousFinish = null;

		for (var index = 0; index < stops.Count; index++)
		{
			var job = stops[index];
			var drive = legs.ToStop[index];
			var work = SetupTimes.ForItems(job.Booking.Items, times);

			// The first stop starts on time. After that the crew arrives when the last stop is done plus the drive,
			// and waits if they're early, since the customer expects them at the scheduled time.
			var arrive = previousFinish is DateTimeOffset finished ? finished.AddMinutes(drive ?? 0) : job.ScheduledAt;
			var startAt = arrive > job.ScheduledAt ? arrive : job.ScheduledAt;
			// A finished job uses when it was really marked complete, so the rest of the day re-plans from there.
			var finishAt = job is { Status: JobStatus.Done, CompletedAt: DateTimeOffset completed } && completed > startAt
				? completed
				: startAt.AddMinutes(work);

			planned.Add(new PlannedStopDto(job.Id, job.Type, job.Booking.CustomerName, job.Booking.Street, job.ScheduledAt,
				drive, arrive, startAt, finishAt, work, MinutesLate(job, startAt, finishAt), job.Booking.PartyStartsAt));
			previousFinish = finishAt;
		}

		var first = planned[0];
		var doneAt = planned[^1].FinishAt;
		var leaveShop = legs.FromShop is int toFirst ? first.ScheduledAt.AddMinutes(-toFirst) : (DateTimeOffset?)null;
		var backAtShop = legs.BackToShop is int home ? doneAt.AddMinutes(home) : (DateTimeOffset?)null;
		var knownDrives = new[] { legs.FromShop, legs.BackToShop }.Concat(legs.ToStop.Skip(1)).ToList();

		return new TruckPlanDto(
			truck,
			leaveShop,
			doneAt,
			backAtShop,
			planned.Sum(stop => stop.WorkMinutes),
			knownDrives.Sum(minutes => minutes ?? 0),
			DriveTimesComplete: shop is not null && knownDrives.All(minutes => minutes is not null),
			planned);
	}

	// With party times, lateness is what the customer notices: a setup not finished before the party starts, or a
	// pickup starting more than two hours after it ends. Without them (older bookings), it's late if the crew starts
	// after the scheduled time.
	internal static int MinutesLate(Job job, DateTimeOffset startAt, DateTimeOffset finishAt)
	{
		var late = job switch
		{
			{ Type: JobType.Setup, Booking.PartyStartsAt: DateTimeOffset party } => finishAt - party,
			{ Type: JobType.Pickup, Booking.PartyEndsAt: DateTimeOffset ended } => startAt - (ended + Booking.PickupWithin),
			_ => startAt - job.ScheduledAt
		};
		return (int)Math.Max(0, Math.Round(late.TotalMinutes));
	}

	// Drive minutes into each stop (the first stop's is from the shop), plus the drive home.
	private static async Task<(int? FromShop, IReadOnlyList<int?> ToStop, int? BackToShop)> LegsAsync(
		List<Job> stops, GeoPoint? shop, IMaps maps, CancellationToken cancellationToken)
	{
		var located = stops.Select(job => job.Booking is { Latitude: double lat, Longitude: double lon } ? new GeoPoint(lat, lon) : (GeoPoint?)null).ToList();
		var toStop = new int?[stops.Count];
		int? fromShop = null, backToShop = null;

		if (!maps.IsConfigured || located.Any(point => point is null))
			return (null, toStop, null);

		// One request for the whole day: shop, every stop in order, shop again.
		var points = new List<GeoPoint>();
		if (shop is GeoPoint start)
			points.Add(start);
		points.AddRange(located.Select(point => point!.Value));
		if (shop is GeoPoint end)
			points.Add(end);

		var legs = await maps.DriveMinutesAsync(points, cancellationToken);
		var offset = shop is null ? 0 : 1;
		if (shop is not null)
		{
			fromShop = legs[0];
			backToShop = legs[^1];
		}
		for (var index = 1; index < stops.Count; index++)
			toStop[index] = legs[index - 1 + offset];
		toStop[0] = fromShop;

		return (fromShop, toStop, backToShop);
	}

	// Looks up map positions for bookings never looked up, or whose address changed since, and saves them.
	// An address that wasn't found isn't retried until someone edits it, so it doesn't eat the daily quota.
	internal static async Task LocateAsync(IEnumerable<Booking> bookings, BounceRouteDbContext db, IMaps maps, CancellationToken cancellationToken)
	{
		var changed = false;
		foreach (var booking in bookings.Where(booking => booking.GeocodedAddress != booking.FullAddress))
		{
			var point = await maps.GeocodeAsync(booking.FullAddress, cancellationToken);
			booking.Latitude = point?.Latitude;
			booking.Longitude = point?.Longitude;
			booking.GeocodedAddress = booking.FullAddress;
			changed = true;
		}
		if (changed)
			await db.SaveChangesAsync(cancellationToken);
	}
}
