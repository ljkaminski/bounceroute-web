using System.Security.Cryptography;
using System.Text;
using BounceRoute.Api.Contracts;
using BounceRoute.Api.Data;
using BounceRoute.Api.Domain;
using BounceRoute.Api.Maps;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BounceRoute.Api.Endpoints;

// Asks the route optimizer for the best way to run a day, and offers it only when it beats the current plan.
public static class RouteSuggestionEndpoints
{
	// Trucks can be out between these hours.
	private static readonly TimeSpan DayStarts = TimeSpan.FromHours(6), DayEnds = TimeSpan.FromHours(22);

	// Plans aim to have setups done this long before the party, as a cushion for parking and small surprises.
	// It also soaks up the optimizer measuring drives to the second while route plans round each drive up a minute.
	private const int SetupCushionMinutes = 10;

	public static void MapRouteSuggestionEndpoints(this IEndpointRouteBuilder app)
	{
		var suggestion = app.MapGroup("/api/route/suggestion").WithTags("Route").RequireAuthorization(Policies.Office);

		suggestion.MapGet("/", GetSuggestion)
			.WithSummary("A better plan for the day (which truck takes which stop, and when), if there is one.");
		suggestion.MapPost("/apply", ApplySuggestion)
			.WithSummary("Uses the suggested plan. Refused if the day changed since the suggestion was shown.");
	}

	public const string SuggestionsOff = "Route suggestions are turned off in Settings.";

	private const string MapsBusy = "The map service isn't answering right now, so the plan can't be checked. It'll try again next time you open this day.";

	private sealed record Proposal(RouteSuggestionDto Dto, Dictionary<int, (string Truck, DateTimeOffset At)> Moves);

	private static async Task<RouteSuggestionDto> GetSuggestion(
		DateOnly date, BounceRouteDbContext db, BusinessClock clock, IMaps maps, IRouteOptimizer optimizer, IMemoryCache cache, CancellationToken cancellationToken) =>
		(await ProposeAsync(date, db, clock, maps, optimizer, cache, cancellationToken)).Dto;

	private static async Task<Results<Ok<RoutePlanSummary>, Conflict<string>, ValidationProblem>> ApplySuggestion(
		DateOnly date, ApplySuggestionRequest request, BounceRouteDbContext db, BusinessClock clock, IMaps maps, IRouteOptimizer optimizer,
		IMemoryCache cache, CancellationToken cancellationToken)
	{
		var proposal = await ProposeAsync(date, db, clock, maps, optimizer, cache, cancellationToken);
		if (proposal.Dto.Fingerprint != request.Fingerprint)
			return TypedResults.Conflict("The day changed since this plan was worked out. Take a look at the new one.");
		if (proposal.Dto.Status != SuggestionStatus.Better)
			return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Plan"] = ["There's no better plan to use."] });

		var ids = proposal.Moves.Keys.ToList();
		var jobs = await db.Jobs.Include(job => job.Assignments).Where(job => ids.Contains(job.Id)).ToListAsync(cancellationToken);
		var trucks = await db.Trucks.Include(truck => truck.Crew).ToDictionaryAsync(truck => truck.Name, cancellationToken);

		foreach (var job in jobs)
		{
			var (truck, at) = proposal.Moves[job.Id];
			// A job that moves to another truck takes that truck's usual crew, if it has one.
			if (truck != job.Truck && trucks.TryGetValue(truck, out var target) && target.Crew.Count > 0)
			{
				job.Assignments.Clear();
				job.Assignments.AddRange(target.Crew.Select(member => new JobAssignment { CrewMemberId = member.CrewMemberId }));
			}
			job.Truck = truck;
			// Stored in UTC, like every job time.
			job.ScheduledAt = at.ToUniversalTime();
		}

		await db.SaveChangesAsync(cancellationToken);
		cache.Remove(CacheKey(proposal.Dto.Fingerprint!));
		return TypedResults.Ok(new RoutePlanSummary(jobs.Count));
	}

	public sealed record RoutePlanSummary(int JobsChanged);

	private static async Task<Proposal> ProposeAsync(
		DateOnly date, BounceRouteDbContext db, BusinessClock clock, IMaps maps, IRouteOptimizer optimizer, IMemoryCache cache, CancellationToken cancellationToken)
	{
		Proposal NotAvailable(string reason) => new(new RouteSuggestionDto(date, SuggestionStatus.NotAvailable, reason, null, null, [], [], null, []), []);

		var settings = await db.CompanySettings.FindAsync([CompanySettings.SingletonId], cancellationToken);
		if (settings is { SuggestRoutes: false })
			return NotAvailable(SuggestionsOff);

		var (start, end) = clock.Range(date, date);
		var jobs = await db.Jobs
			.Include(job => job.Booking).ThenInclude(booking => booking.Items).ThenInclude(item => item.InventoryItem)
			.Where(job => job.ScheduledAt >= start && job.ScheduledAt < end)
			.OrderBy(job => job.ScheduledAt)
			.AsSplitQuery()
			.ToListAsync(cancellationToken);

		if (jobs.Count == 0)
			return NotAvailable("No jobs this day.");
		// Once crews are out, moving their stops around would only confuse things.
		if (jobs.Any(job => job.Status == JobStatus.Done))
			return NotAvailable("This day has started, so the plan is left as it is.");
		if (!maps.IsConfigured)
			return NotAvailable("Route planning needs the map service set up.");

		var shop = await RoutePlanEndpoints.LocateShopAsync(settings, db, maps, cancellationToken);
		if (shop is not GeoPoint shopPoint)
			return NotAvailable(string.IsNullOrWhiteSpace(settings?.ShopAddress)
				? "Add the shop address in Settings to plan routes."
				: MapsBusy);

		await RoutePlanEndpoints.LocateAsync(jobs.Select(job => job.Booking).Distinct(), db, maps, cancellationToken);
		// Not asked yet (the service didn't answer) is different from asked and not found.
		if (jobs.Any(job => job.Booking.GeocodedAddress != job.Booking.FullAddress))
			return NotAvailable(MapsBusy);
		var lost = jobs.Where(job => job.Booking.Latitude is null).Select(job => job.Booking.CustomerName).Distinct().ToList();
		if (lost.Count > 0)
			return NotAvailable($"Couldn't find the address on the map for {string.Join(", ", lost)}. Check the street and town.");

		var (trucksOut, _) = await TrucksOut.ForDayAsync(db, date);
		if (trucksOut.Count == 0)
			return NotAvailable("Pick at least one truck going out.");

		var times = await SetupTimes.LoadAsync(db);
		var dayStart = clock.StartOf(date).Add(DayStarts);
		var dayEnd = clock.StartOf(date).Add(DayEnds);
		var stops = jobs.Select(job => ToStop(job, times, dayStart, dayEnd)).ToList();
		var fingerprint = Fingerprint(date, jobs, stops, trucksOut, shopPoint);

		if (cache.TryGetValue(CacheKey(fingerprint), out Proposal? cached) && cached is not null)
			return cached;

		var result = await optimizer.OptimizeAsync(stops, [.. trucksOut.Select(truck => new OptimizerTruck(truck.Id, truck.Capacity, shopPoint, dayStart, dayEnd))], cancellationToken);
		if (result is null)
			return NotAvailable("The route planner didn't answer. Try again in a minute.");

		var truckNames = trucksOut.ToDictionary(truck => truck.Id, truck => truck.Name);
		var moves = new Dictionary<int, (string Truck, DateTimeOffset At)>();
		foreach (var route in result.Routes)
			foreach (var step in route.Steps)
				moves[step.StopId] = (truckNames[route.TruckId], Tidy(step.StartAt));

		// Plan both versions the same way so the comparison is fair.
		var proposed = jobs.Select(job => moves.TryGetValue(job.Id, out var move) ? CopyWith(job, move.Truck, move.At) : job).ToList();
		var currentPlan = await RoutePlanEndpoints.PlanTrucksAsync(jobs, shopPoint, times, maps, cancellationToken);
		var proposedPlan = await RoutePlanEndpoints.PlanTrucksAsync(proposed, shopPoint, times, maps, cancellationToken);
		var current = Summarize(currentPlan);
		var suggested = Summarize(proposedPlan);

		var outNames = trucksOut.Select(truck => truck.Name).ToHashSet();
		var changes = jobs
			.Where(job => moves.TryGetValue(job.Id, out var move) && (move.Truck != job.Truck || Math.Abs((move.At - job.ScheduledAt).TotalMinutes) >= 5))
			.Select(job => new SuggestedChangeDto(job.Id, job.Type, job.Booking.CustomerName,
				string.IsNullOrWhiteSpace(job.Truck) ? RoutePlanEndpoints.NoTruck : job.Truck, moves[job.Id].Truck, job.ScheduledAt, moves[job.Id].At))
			.ToList();
		var cantFit = result.Unassigned.Select(id => jobs.First(job => job.Id == id))
			.Select(job => $"{job.Booking.CustomerName} ({job.Type.ToString().ToLowerInvariant()})").ToList();

		// Worth offering when fewer stops run late, or the same number but everyone's back 15+ minutes sooner,
		// or it puts stops on trucks that are actually going out today.
		var fixesTrucks = jobs.Any(job => !outNames.Contains(job.Truck) && moves.ContainsKey(job.Id));
		var better = changes.Count > 0 && (
			suggested.LateStops < current.LateStops
			|| (suggested.LateStops == current.LateStops && Earlier(suggested.AllDoneAt, current.AllDoneAt, minutes: 15))
			|| (fixesTrucks && suggested.LateStops <= current.LateStops));

		var dto = new RouteSuggestionDto(date, better ? SuggestionStatus.Better : SuggestionStatus.AlreadyBest, null, current, suggested,
			better ? changes : [], cantFit, fingerprint, better ? proposedPlan : []);
		var proposal = new Proposal(dto, better ? moves.Where(move => changes.Any(change => change.JobId == move.Key)).ToDictionary() : []);
		cache.Set(CacheKey(fingerprint), proposal, TimeSpan.FromMinutes(30));
		return proposal;
	}

	private static OptimizerStop ToStop(Job job, IReadOnlyDictionary<InventoryCategory, int> times, DateTimeOffset dayStart, DateTimeOffset dayEnd)
	{
		var booking = job.Booking;
		var minutes = SetupTimes.ForItems(booking.Items, times);
		var pieces = booking.Items.Sum(item => item.InventoryItem?.TruckSpace ?? (item.IsUnit ? 1 : 0));
		var location = new GeoPoint(booking.Latitude!.Value, booking.Longitude!.Value);

		if (job.Type == JobType.Setup)
		{
			// Done (with a cushion) before the party starts; without a party time, no later than the time already promised.
			var latest = booking.PartyStartsAt is { } party ? party.AddMinutes(-minutes - SetupCushionMinutes) : job.ScheduledAt;
			return new OptimizerStop(job.Id, location, StopKind.Setup, minutes, pieces, dayStart, Clamp(latest, dayStart, dayEnd));
		}

		// After the party ends and within two hours of it. A pickup the next day (party ended the day before) can be any time.
		if (booking.PartyEndsAt is { } ended && ended >= dayStart)
		{
			var opens = Clamp(ended, dayStart, dayEnd);
			return new OptimizerStop(job.Id, location, StopKind.Pickup, minutes, pieces, opens, Clamp(ended + Booking.PickupWithin, opens, dayEnd));
		}
		var notBefore = booking.PartyEndsAt is null ? Clamp(job.ScheduledAt, dayStart, dayEnd) : dayStart;
		return new OptimizerStop(job.Id, location, StopKind.Pickup, minutes, pieces, notBefore, dayEnd);
	}

	private static DateTimeOffset Clamp(DateTimeOffset value, DateTimeOffset min, DateTimeOffset max) => value < min ? min : value > max ? max : value;

	// Round up to the whole minute. Rounding further (to :05s) looked tidier but pushed every later stop back
	// a few minutes until some ran late.
	private static DateTimeOffset Tidy(DateTimeOffset at)
	{
		var minute = new DateTimeOffset(at.Year, at.Month, at.Day, at.Hour, at.Minute, 0, at.Offset);
		return minute < at ? minute.AddMinutes(1) : minute;
	}

	private static Job CopyWith(Job job, string truck, DateTimeOffset at) => new()
	{
		Id = job.Id,
		BookingId = job.BookingId,
		Booking = job.Booking,
		Type = job.Type,
		ScheduledAt = at,
		Truck = truck,
		Status = job.Status,
		Assignments = job.Assignments
	};

	private static PlanSummaryDto Summarize(List<TruckPlanDto> plan) => new(
		plan.Sum(truck => truck.Stops.Count(stop => stop.MinutesLate > 0)),
		plan.Count == 0 ? null : plan.Max(truck => truck.BackAtShopAt ?? truck.DoneAt),
		plan.Count(truck => truck.Truck != RoutePlanEndpoints.NoTruck));

	private static bool Earlier(DateTimeOffset? suggested, DateTimeOffset? current, int minutes) =>
		suggested is { } s && current is { } c && (c - s).TotalMinutes >= minutes;

	private static string CacheKey(string fingerprint) => $"route-suggestion:{fingerprint}";

	// Everything the plan depends on. If any of it changes, the suggestion is worked out again.
	// Stops carry their place, minutes on site, pieces, and deadlines, so editing a booking, an item's setup time
	// or truck space, or the setup times all count.
	private static string Fingerprint(DateOnly date, List<Job> jobs, List<OptimizerStop> stops, List<Truck> trucks, GeoPoint shop)
	{
		var text = new StringBuilder($"{date:O}|{shop}|");
		foreach (var job in jobs)
			text.Append($"{job.Id}:{job.Truck}:{job.ScheduledAt.UtcTicks}:{job.Status};");
		foreach (var stop in stops)
			text.Append($"{stop};");
		foreach (var truck in trucks)
			text.Append($"T{truck.Id}:{truck.Name}:{truck.Capacity};");
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..16];
	}
}
