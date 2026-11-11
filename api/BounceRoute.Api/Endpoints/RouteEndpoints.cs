using System.Security.Claims;
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
		var api = app.MapGroup("/api").WithTags("Route").RequireAuthorization();

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

	private static async Task<RouteDayDto> GetRouteDay(DateOnly? date, ClaimsPrincipal user, BounceRouteDbContext db, BusinessClock clock)
	{
		var viewer = Viewer.From(user);
		var day = date ?? clock.Today;
		var (start, end) = clock.Range(day, day);

		var jobs = await JobCards(db)
			.VisibleTo(viewer)
			.Where(job => job.ScheduledAt >= start && job.ScheduledAt < end)
			.OrderBy(job => job.ScheduledAt)
			.ToListAsync();

		var nextUp = jobs.FirstOrDefault(job => job.Status != JobStatus.Done);
		var times = await SetupTimes.LoadAsync(db);
		return new RouteDayDto(day, [.. jobs.Select(job => ToCard(job, times, isNextUp: job == nextUp))]);
	}

	private static async Task<Results<Ok<JobCardDto>, NotFound>> GetJob(int id, ClaimsPrincipal user, BounceRouteDbContext db, BusinessClock clock)
	{
		var viewer = Viewer.From(user);
		var job = await JobCards(db).VisibleTo(viewer).FirstOrDefaultAsync(job => job.Id == id);
		if (job is null)
			return TypedResults.NotFound();

		return TypedResults.Ok(ToCard(job, await SetupTimes.LoadAsync(db), await IsNextUpAsync(db, clock, viewer, job)));
	}

	private static async Task<Results<Ok<JobCardDto>, NotFound, ValidationProblem>> CompleteJob(
		int id, CompleteJobRequest request, ClaimsPrincipal user, BounceRouteDbContext db, BusinessClock clock, TimeProvider time)
	{
		var viewer = Viewer.From(user);
		var job = await JobCards(db).VisibleTo(viewer).FirstOrDefaultAsync(job => job.Id == id);
		if (job is null)
			return TypedResults.NotFound();

		// Crew can only sign off as themselves.
		var completedBy = viewer.IsOffice ? request.CrewMemberId : viewer.CrewMemberId;
		if (!viewer.IsOffice && request.CrewMemberId is not null && request.CrewMemberId != viewer.CrewMemberId)
			return CrewMemberProblem("Crew members can only complete jobs as themselves.");
		var crewMember = completedBy is int crewId ? await db.CrewMembers.FindAsync(crewId) : null;
		if (crewMember is null)
			return CrewMemberProblem("No crew member with that id.");

		job.Status = JobStatus.Done;
		job.CompletedAt = time.GetUtcNow();
		job.CompletedBy = crewMember;
		await db.SaveChangesAsync();

		return TypedResults.Ok(ToCard(job, await SetupTimes.LoadAsync(db), await IsNextUpAsync(db, clock, viewer, job)));
	}

	private static async Task<Results<Ok<JobCardDto>, NotFound>> ReopenJob(int id, ClaimsPrincipal user, BounceRouteDbContext db, BusinessClock clock)
	{
		var viewer = Viewer.From(user);
		var job = await JobCards(db).VisibleTo(viewer).FirstOrDefaultAsync(job => job.Id == id);
		if (job is null)
			return TypedResults.NotFound();

		job.Status = JobStatus.Upcoming;
		job.CompletedAt = null;
		job.CompletedByCrewMemberId = null;
		job.CompletedBy = null;
		await db.SaveChangesAsync();

		return TypedResults.Ok(ToCard(job, await SetupTimes.LoadAsync(db), await IsNextUpAsync(db, clock, viewer, job)));
	}

	private static async Task<Results<Ok<BookingItemDto>, NotFound>> SetItemLoaded(
		int id, SetItemLoadedRequest request, ClaimsPrincipal user, BounceRouteDbContext db)
	{
		var visibleBookings = db.Jobs.VisibleTo(Viewer.From(user)).Select(job => job.BookingId);
		var item = await db.BookingItems
			.Include(item => item.InventoryItem)
			.FirstOrDefaultAsync(item => item.Id == id && visibleBookings.Contains(item.BookingId));
		if (item is null)
			return TypedResults.NotFound();

		item.IsLoaded = request.IsLoaded;
		await db.SaveChangesAsync();
		return TypedResults.Ok(BookingEndpoints.ToDto(item));
	}

	private static ValidationProblem CrewMemberProblem(string message) =>
		TypedResults.ValidationProblem(new Dictionary<string, string[]> { [nameof(CompleteJobRequest.CrewMemberId)] = [message] });

	private static IQueryable<Job> JobCards(BounceRouteDbContext db) => db.Jobs
		.Include(job => job.Booking).ThenInclude(booking => booking.Items).ThenInclude(item => item.InventoryItem)
		.Include(job => job.Assignments).ThenInclude(assignment => assignment.CrewMember)
		.Include(job => job.CompletedBy)
		.AsSplitQuery();

	// Next up is the first job of that day (of the viewer's jobs) that is not done yet.
	private static async Task<bool> IsNextUpAsync(BounceRouteDbContext db, BusinessClock clock, Viewer viewer, Job job)
	{
		var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(job.ScheduledAt, clock.TimeZone).DateTime);
		var (start, end) = clock.Range(day, day);

		var firstOpenId = await db.Jobs
			.VisibleTo(viewer)
			.Where(j => j.ScheduledAt >= start && j.ScheduledAt < end && j.Status != JobStatus.Done)
			.OrderBy(j => j.ScheduledAt)
			.Select(j => (int?)j.Id)
			.FirstOrDefaultAsync();

		return firstOpenId == job.Id;
	}

	private static JobCardDto ToCard(Job job, IReadOnlyDictionary<InventoryCategory, int> times, bool isNextUp)
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
				.Select(crew => new CrewSummaryDto(crew.Id, crew.Name, crew.Initials))],
			SetupTimes.ForItems(booking.Items, times));
	}
}
