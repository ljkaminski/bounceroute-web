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
		if (await ValidateAsync(request, db) is { } problem)
			return problem;

		var now = time.GetUtcNow();
		var booking = new Booking
		{
			CreatedAt = now,
			Jobs = [new Job { Type = JobType.Setup }, new Job { Type = JobType.Pickup }]
		};
		Apply(request, booking, now);
		db.Bookings.Add(booking);
		await db.SaveChangesAsync();

		// Reload so the database-assigned job number comes back.
		var saved = (await LoadAsync(db, booking.Id))!;
		return TypedResults.CreatedAtRoute(ToDto(saved), nameof(GetBooking), new { id = saved.Id });
	}

	private static async Task<Results<Ok<BookingDto>, NotFound, ValidationProblem>> UpdateBooking(int id, SaveBookingRequest request, BounceRouteDbContext db, TimeProvider time)
	{
		var booking = await LoadAsync(db, id, tracked: true);
		if (booking is null)
			return TypedResults.NotFound();

		if (await ValidateAsync(request, db) is { } problem)
			return problem;

		Apply(request, booking, time.GetUtcNow());
		await db.SaveChangesAsync();
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
	private static async Task<ValidationProblem?> ValidateAsync(SaveBookingRequest request, BounceRouteDbContext db)
	{
		var errors = new Dictionary<string, string[]>();

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

	private static void Apply(SaveBookingRequest request, Booking booking, DateTimeOffset now)
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
			var name = item.Name.Trim();
			booking.Items.Add(new BookingItem
			{
				Name = name,
				IsUnit = item.IsUnit,
				IsLoaded = loadedBefore.Contains((name, item.IsUnit))
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

	internal static BookingItemDto ToDto(BookingItem item) => new(item.Id, item.Name, item.IsUnit, item.IsLoaded);

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
