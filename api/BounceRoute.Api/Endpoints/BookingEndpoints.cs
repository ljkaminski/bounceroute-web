using BounceRoute.Api.Contracts;
using BounceRoute.Api.Data;
using BounceRoute.Api.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace BounceRoute.Api.Endpoints;

public static class BookingEndpoints
{
	public static void MapBookingEndpoints(this IEndpointRouteBuilder app)
	{
		var bookings = app.MapGroup("/api/bookings").WithTags("Bookings").RequireAuthorization(Policies.Office);

		bookings.MapGet("/", ListBookings)
			.WithSummary("Bookings whose setup falls between two dates (inclusive). Defaults to the next 14 days.");

		bookings.MapGet("/{id:int}", GetBooking).WithName(nameof(GetBooking));

		bookings.MapPost("/", CreateBooking)
			.WithSummary("Creates a booking with its setup and pickup jobs.");

		bookings.MapPut("/{id:int}", UpdateBooking)
			.WithSummary("Replaces a booking's details, schedule, crew, and items. Loaded checks are kept for items still on it.");

		bookings.MapDelete("/{id:int}", DeleteBooking);
	}

	private static async Task<List<BookingSummaryDto>> ListBookings(
		DateOnly? from, DateOnly? to, BounceRouteDbContext db, BusinessClock clock)
	{
		var first = from ?? clock.Today;
		var (start, end) = clock.Range(first, to ?? first.AddDays(13));

		return await db.Bookings
			.Where(booking => booking.Jobs.Any(job => job.Type == JobType.Setup && job.ScheduledAt >= start && job.ScheduledAt < end))
			// Sort before projecting: EF Core can't translate ordering by a property of the DTO.
			.OrderBy(booking => booking.Jobs.Where(job => job.Type == JobType.Setup).Min(job => job.ScheduledAt))
			.Select(booking => new BookingSummaryDto(
				booking.Id,
				booking.JobNumber,
				booking.CustomerName,
				booking.Street,
				booking.Jobs.First(job => job.Type == JobType.Setup).ScheduledAt,
				booking.Jobs.First(job => job.Type == JobType.Pickup).ScheduledAt,
				booking.Items.Count(item => item.IsUnit),
				booking.Jobs.All(job => job.Status == JobStatus.Done)))
			.ToListAsync();
	}

	private static async Task<Results<Ok<BookingDto>, NotFound>> GetBooking(int id, BounceRouteDbContext db)
	{
		var booking = await LoadAsync(db, id);
		if (booking is null)
			return TypedResults.NotFound();

		return TypedResults.Ok(ToDto(booking));
	}

	private static async Task<Results<CreatedAtRoute<BookingDto>, ValidationProblem>> CreateBooking(SaveBookingRequest request, BounceRouteDbContext db, TimeProvider time)
	{
		await using var transaction = await db.Database.BeginTransactionAsync();
		await Availability.LockBookingsAsync(db);

		var inventory = await LoadInventoryAsync(request, db);
		if (await ValidateAsync(request, db, inventory, bookingId: null, alreadyOnBooking: []) is { } problem)
			return problem;

		var now = time.GetUtcNow();
		var booking = new Booking
		{
			CreatedAt = now,
			Jobs = [new Job { Type = JobType.Setup }, new Job { Type = JobType.Pickup }]
		};
		Apply(request, booking, inventory, now);
		db.Bookings.Add(booking);
		await db.SaveChangesAsync();
		await transaction.CommitAsync();

		// Reload so the database-assigned job number comes back.
		var saved = (await LoadAsync(db, booking.Id))!;
		return TypedResults.CreatedAtRoute(ToDto(saved), nameof(GetBooking), new { id = saved.Id });
	}

	private static async Task<Results<Ok<BookingDto>, NotFound, ValidationProblem>> UpdateBooking(int id, SaveBookingRequest request, BounceRouteDbContext db, TimeProvider time)
	{
		await using var transaction = await db.Database.BeginTransactionAsync();
		await Availability.LockBookingsAsync(db);

		var booking = await LoadAsync(db, id, tracked: true);
		if (booking is null)
			return TypedResults.NotFound();

		var inventory = await LoadInventoryAsync(request, db);
		var alreadyOnBooking = booking.Items.Where(item => item.InventoryItemId is not null).Select(item => item.InventoryItemId!.Value).ToHashSet();
		if (await ValidateAsync(request, db, inventory, booking.Id, alreadyOnBooking) is { } problem)
			return problem;

		Apply(request, booking, inventory, time.GetUtcNow());
		await db.SaveChangesAsync();
		await transaction.CommitAsync();
		return TypedResults.Ok(ToDto(booking));
	}

	private static async Task<Results<NoContent, NotFound>> DeleteBooking(int id, BounceRouteDbContext db)
	{
		var deleted = await db.Bookings.Where(booking => booking.Id == id).ExecuteDeleteAsync();
		if (deleted == 0)
			return TypedResults.NotFound();

		return TypedResults.NoContent();
	}

	// Field-level rules (required, lengths) are checked by the framework; these need the database or two fields.
	private static Task<Dictionary<int, InventoryItem>> LoadInventoryAsync(SaveBookingRequest request, BounceRouteDbContext db)
	{
		var ids = request.Items.Where(item => item.InventoryItemId is not null).Select(item => item.InventoryItemId!.Value).Distinct().ToList();
		return db.InventoryItems.Where(item => ids.Contains(item.Id)).ToDictionaryAsync(item => item.Id);
	}

	private static async Task<ValidationProblem?> ValidateAsync(
		SaveBookingRequest request, BounceRouteDbContext db, Dictionary<int, InventoryItem> inventory, int? bookingId, HashSet<int> alreadyOnBooking)
	{
		var errors = new Dictionary<string, string[]>();
		var itemErrors = new List<string>();

		foreach (var item in request.Items)
		{
			if (item.InventoryItemId is not int inventoryId)
			{
				if (string.IsNullOrWhiteSpace(item.Name))
					itemErrors.Add("Each item needs a name or an inventory item.");
				continue;
			}

			if (!inventory.TryGetValue(inventoryId, out var owned))
				itemErrors.Add($"No inventory item with id {inventoryId}.");
			// A retired item can stay on a booking that already had it, but can't be newly booked.
			else if (!owned.IsActive && !alreadyOnBooking.Contains(inventoryId))
				itemErrors.Add($"{owned.Name} has been retired.");
			else if (owned.WaterUse == WaterUse.WetOrDry && item.IsWet is null)
				itemErrors.Add($"Choose wet or dry for {owned.Name}.");
			else if (owned.WaterUse == WaterUse.DryOnly && item.IsWet == true)
				itemErrors.Add($"{owned.Name} is dry use only.");
			else if (owned.WaterUse == WaterUse.WetOnly && item.IsWet == false)
				itemErrors.Add($"{owned.Name} is water use only.");
		}

		// Everything requested, plus what other overlapping bookings hold, must fit in what the company owns.
		if (itemErrors.Count == 0 && request.Pickup.ScheduledAt > request.Setup.ScheduledAt)
		{
			var booked = await Availability.BookedCountsAsync(
				db, request.Setup.ScheduledAt.ToUniversalTime(), request.Pickup.ScheduledAt.ToUniversalTime(), bookingId);
			foreach (var wanted in request.Items.Where(item => item.InventoryItemId is not null).GroupBy(item => item.InventoryItemId!.Value))
			{
				var owned = inventory[wanted.Key];
				var free = owned.Quantity - booked.GetValueOrDefault(owned.Id);
				if (wanted.Count() > free)
					itemErrors.Add(free <= 0
						? $"{owned.Name} is already booked for those dates."
						: $"Only {free} of {owned.Quantity} {owned.Name} free for those dates.");
			}
		}

		if (itemErrors.Count > 0)
			errors["Items"] = [.. itemErrors];

		if (request.Pickup.ScheduledAt <= request.Setup.ScheduledAt)
			errors["Pickup.ScheduledAt"] = ["Pickup must be after setup."];

		if (request.Items.Count == 0)
			errors["Items"] = ["Add at least one item."];

		var crewIds = (request.Setup.CrewMemberIds ?? []).Concat(request.Pickup.CrewMemberIds ?? []).Distinct().ToList();
		var knownCount = await db.CrewMembers.CountAsync(crew => crewIds.Contains(crew.Id));
		if (knownCount != crewIds.Count)
			errors["CrewMemberIds"] = ["One or more crew members do not exist."];

		return errors.Count == 0 ? null : TypedResults.ValidationProblem(errors);
	}

	private static void Apply(SaveBookingRequest request, Booking booking, Dictionary<int, InventoryItem> inventory, DateTimeOffset now)
	{
		booking.CustomerName = request.CustomerName.Trim();
		booking.Phone = request.Phone?.Trim() ?? string.Empty;
		booking.Street = request.Street.Trim();
		booking.CityStateZip = request.CityStateZip?.Trim() ?? string.Empty;
		booking.Surface = request.Surface;
		booking.SiteNotes = request.SiteNotes?.Trim() ?? string.Empty;
		booking.UpdatedAt = now;

		ApplySchedule(request.Setup, booking.Jobs.Single(job => job.Type == JobType.Setup));
		ApplySchedule(request.Pickup, booking.Jobs.Single(job => job.Type == JobType.Pickup));

		// Items are replaced as a set; one that is still on the booking keeps its loaded check.
		var loadedBefore = booking.Items.Where(item => item.IsLoaded).Select(item => (item.Name, item.IsUnit)).ToHashSet();
		booking.Items.Clear();
		foreach (var item in request.Items)
		{
			var owned = item.InventoryItemId is int id ? inventory[id] : null;
			var name = owned?.Name ?? item.Name!.Trim();
			var isUnit = owned?.IsUnit ?? item.IsUnit;
			booking.Items.Add(new BookingItem
			{
				Name = name,
				IsUnit = isUnit,
				InventoryItemId = owned?.Id,
				IsWet = owned?.WaterUse switch
				{
					WaterUse.DryOnly => false,
					WaterUse.WetOnly => true,
					WaterUse.WetOrDry => item.IsWet,
					_ => null
				},
				IsLoaded = loadedBefore.Contains((name, isUnit))
			});
		}
	}

	private static void ApplySchedule(SaveJobScheduleRequest request, Job job)
	{
		job.ScheduledAt = request.ScheduledAt.ToUniversalTime();
		job.Truck = request.Truck?.Trim() ?? string.Empty;

		var wanted = (request.CrewMemberIds ?? []).Distinct().ToHashSet();
		job.Assignments.RemoveAll(assignment => !wanted.Contains(assignment.CrewMemberId));
		foreach (var crewId in wanted.Except(job.Assignments.Select(assignment => assignment.CrewMemberId)))
			job.Assignments.Add(new JobAssignment { CrewMemberId = crewId });
	}

	private static Task<Booking?> LoadAsync(BounceRouteDbContext db, int id, bool tracked = false)
	{
		var query = db.Bookings
			.Include(booking => booking.Items)
			.Include(booking => booking.Jobs).ThenInclude(job => job.Assignments)
			.AsSplitQuery();

		return (tracked ? query : query.AsNoTracking()).FirstOrDefaultAsync(booking => booking.Id == id);
	}

	internal static BookingItemDto ToDto(BookingItem item) =>
		new(item.Id, item.Name, item.IsUnit, item.IsLoaded, item.InventoryItemId, item.IsWet);

	private static BookingDto ToDto(Booking booking) => new(
		booking.Id,
		booking.JobNumber,
		booking.CustomerName,
		booking.Phone,
		booking.Street,
		booking.CityStateZip,
		booking.Surface,
		booking.SiteNotes,
		ToDto(booking.Jobs.Single(job => job.Type == JobType.Setup)),
		ToDto(booking.Jobs.Single(job => job.Type == JobType.Pickup)),
		[.. booking.Items.OrderBy(item => item.Id).Select(ToDto)]);

	private static JobScheduleDto ToDto(Job job) => new(
		job.Id,
		job.ScheduledAt,
		job.Truck,
		job.Status,
		[.. job.Assignments.Select(assignment => assignment.CrewMemberId).Order()]);
}
