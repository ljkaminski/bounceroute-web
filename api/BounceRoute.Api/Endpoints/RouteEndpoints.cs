using BounceRoute.Api.Contracts;
using BounceRoute.Api.Data;
using BounceRoute.Api.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace BounceRoute.Api.Endpoints;

public static class RouteEndpoints
{
	public static void MapRouteEndpoints(this IEndpointRouteBuilder app)
	{
		var api = app.MapGroup("/api").WithTags("Route");

		api.MapGet("/route", GetRouteDay)
			.WithSummary("Every job for one day, in order. Defaults to today.");

		api.MapGet("/jobs/{id:int}", GetJob);

		api.MapPost("/jobs/{id:int}/complete", CompleteJob)
			.WithSummary("Marks a job done by a crew member.");

		api.MapPost("/jobs/{id:int}/reopen", ReopenJob)
			.WithSummary("Undoes a mistaken Mark complete.");

		api.MapPut("/booking-items/{id:int}/loaded", SetItemLoaded)
			.WithSummary("Checks or unchecks an item on the loading list.");
	}

	private static async Task<RouteDayDto> GetRouteDay(DateOnly? date, BounceRouteDbContext db, BusinessClock clock)
	{
		var day = date ?? clock.Today;
		var (start, end) = clock.Range(day, day);

		var jobs = await JobCards(db)
			.Where(job => job.ScheduledAt >= start && job.ScheduledAt < end)
			.OrderBy(job => job.ScheduledAt)
			.ToListAsync();

		var nextUp = jobs.FirstOrDefault(job => job.Status != JobStatus.Done);
		return new RouteDayDto(day, [.. jobs.Select(job => ToCard(job, isNextUp: job == nextUp))]);
	}

	private static async Task<Results<Ok<JobCardDto>, NotFound>> GetJob(int id, BounceRouteDbContext db, BusinessClock clock)
	{
		var job = await JobCards(db).FirstOrDefaultAsync(job => job.Id == id);
		if (job is null)
			return TypedResults.NotFound();

		return TypedResults.Ok(ToCard(job, await IsNextUpAsync(db, clock, job)));
	}

	private static async Task<Results<Ok<JobCardDto>, NotFound, ValidationProblem>> CompleteJob(
		int id, CompleteJobRequest request, BounceRouteDbContext db, BusinessClock clock, TimeProvider time)
	{
		var job = await JobCards(db).FirstOrDefaultAsync(job => job.Id == id);
		if (job is null)
			return TypedResults.NotFound();

		if (!await db.CrewMembers.AnyAsync(crew => crew.Id == request.CrewMemberId))
			return TypedResults.ValidationProblem(new Dictionary<string, string[]>
			{
				[nameof(request.CrewMemberId)] = ["No crew member with that id."]
			});

		job.Status = JobStatus.Done;
		job.CompletedAt = time.GetUtcNow();
		job.CompletedByCrewMemberId = request.CrewMemberId;
		await db.SaveChangesAsync();
		await db.Entry(job).Reference(j => j.CompletedBy).LoadAsync();

		return TypedResults.Ok(ToCard(job, await IsNextUpAsync(db, clock, job)));
	}

	private static async Task<Results<Ok<JobCardDto>, NotFound>> ReopenJob(int id, BounceRouteDbContext db, BusinessClock clock)
	{
		var job = await JobCards(db).FirstOrDefaultAsync(job => job.Id == id);
		if (job is null)
			return TypedResults.NotFound();

		job.Status = JobStatus.Upcoming;
		job.CompletedAt = null;
		job.CompletedByCrewMemberId = null;
		job.CompletedBy = null;
		await db.SaveChangesAsync();

		return TypedResults.Ok(ToCard(job, await IsNextUpAsync(db, clock, job)));
	}

	private static async Task<Results<Ok<BookingItemDto>, NotFound>> SetItemLoaded(int id, SetItemLoadedRequest request, BounceRouteDbContext db)
	{
		var item = await db.BookingItems.FindAsync(id);
		if (item is null)
			return TypedResults.NotFound();

		item.IsLoaded = request.IsLoaded;
		await db.SaveChangesAsync();
		return TypedResults.Ok(new BookingItemDto(item.Id, item.Name, item.IsUnit, item.IsLoaded));
	}

	private static IQueryable<Job> JobCards(BounceRouteDbContext db) => db.Jobs
		.Include(job => job.Booking).ThenInclude(booking => booking.Items)
		.Include(job => job.Assignments).ThenInclude(assignment => assignment.CrewMember)
		.Include(job => job.CompletedBy)
		.AsSplitQuery();

	// Next up is the first job of that day that is not done yet.
	private static async Task<bool> IsNextUpAsync(BounceRouteDbContext db, BusinessClock clock, Job job)
	{
		var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(job.ScheduledAt, clock.TimeZone).DateTime);
		var (start, end) = clock.Range(day, day);

		var firstOpenId = await db.Jobs
			.Where(j => j.ScheduledAt >= start && j.ScheduledAt < end && j.Status != JobStatus.Done)
			.OrderBy(j => j.ScheduledAt)
			.Select(j => (int?)j.Id)
			.FirstOrDefaultAsync();

		return firstOpenId == job.Id;
	}

	private static JobCardDto ToCard(Job job, bool isNextUp)
	{
		var booking = job.Booking;
		var items = booking.Items.OrderBy(item => item.Id).Select(BookingEndpoints.ToDto).ToList();

		return new JobCardDto(
			job.Id,
			booking.Id,
			booking.JobNumber,
			job.Type,
			job.ScheduledAt,
			job.Truck,
			job.Status,
			job.CompletedAt,
			job.CompletedBy?.Name,
			isNextUp,
			booking.CustomerName,
			booking.Phone,
			booking.Street,
			booking.CityStateZip,
			booking.Surface,
			booking.SiteNotes,
			[.. items.Where(item => item.IsUnit)],
			[.. items.Where(item => !item.IsUnit)],
			[.. job.Assignments
				.Select(assignment => assignment.CrewMember)
				.OrderBy(crew => crew.Name)
				.Select(crew => new CrewSummaryDto(crew.Id, crew.Name, crew.Initials))]);
	}
}
