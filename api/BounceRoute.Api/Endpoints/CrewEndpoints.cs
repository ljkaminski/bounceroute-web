using System.Security.Claims;
using BounceRoute.Api.Contracts;
using BounceRoute.Api.Data;
using BounceRoute.Api.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace BounceRoute.Api.Endpoints;

public static class CrewEndpoints
{
	public static void MapCrewEndpoints(this IEndpointRouteBuilder app)
	{
		var crew = app.MapGroup("/api/crew").WithTags("Crew").RequireAuthorization(Policies.Office);

		crew.MapGet("/", ListCrew);
		crew.MapPost("/", CreateCrewMember);
		crew.MapPut("/{id:int}", UpdateCrewMember);

		crew.MapGet("/units", GetUnitTotals)
			.WithSummary("Units on completed jobs per crew member between two dates (inclusive). Defaults to this weekend, Friday through Monday.");

		app.MapGet("/api/me/units", GetMyUnitTotals)
			.WithTags("Crew")
			.WithSummary("The signed-in crew member's own unit totals. Same dates as /api/crew/units.")
			.RequireAuthorization();
	}

	private static Task<List<CrewMemberDto>> ListCrew(bool? includeInactive, BounceRouteDbContext db) => db.CrewMembers
		.Where(crew => includeInactive == true || crew.IsActive)
		.OrderBy(crew => crew.Name)
		.Select(crew => new CrewMemberDto(crew.Id, crew.Name, crew.Initials, crew.Role, crew.IsActive))
		.ToListAsync();

	private static async Task<Created<CrewMemberDto>> CreateCrewMember(SaveCrewMemberRequest request, BounceRouteDbContext db)
	{
		var crew = new CrewMember();
		Apply(request, crew);
		db.CrewMembers.Add(crew);
		await db.SaveChangesAsync();
		return TypedResults.Created($"/api/crew/{crew.Id}", ToDto(crew));
	}

	private static async Task<Results<Ok<CrewMemberDto>, NotFound>> UpdateCrewMember(int id, SaveCrewMemberRequest request, BounceRouteDbContext db)
	{
		var crew = await db.CrewMembers.FindAsync(id);
		if (crew is null)
			return TypedResults.NotFound();

		Apply(request, crew);
		await db.SaveChangesAsync();
		return TypedResults.Ok(ToDto(crew));
	}

	private static async Task<UnitTotalsReport> GetUnitTotals(
		DateOnly? from, DateOnly? to, BounceRouteDbContext db, BusinessClock clock)
	{
		var (first, last) = Period(from, to, clock);
		return new UnitTotalsReport(first, last, await TotalsAsync(db, clock, first, last, onlyCrewMemberId: null));
	}

	private static async Task<Results<Ok<UnitTotalsReport>, NotFound>> GetMyUnitTotals(
		DateOnly? from, DateOnly? to, ClaimsPrincipal user, BounceRouteDbContext db, BusinessClock clock)
	{
		// Owner or office accounts that are not also on a crew have no units of their own.
		if (Viewer.From(user).CrewMemberId is not int crewMemberId)
			return TypedResults.NotFound();

		var (first, last) = Period(from, to, clock);
		return TypedResults.Ok(new UnitTotalsReport(first, last, await TotalsAsync(db, clock, first, last, crewMemberId)));
	}

	// Defaults to this weekend, Friday through Monday. A lone "from" means that one day.
	private static (DateOnly First, DateOnly Last) Period(DateOnly? from, DateOnly? to, BusinessClock clock)
	{
		var first = from ?? WeekendStart(clock.Today);
		return (first, to ?? (from is null ? first.AddDays(3) : first));
	}

	private static async Task<List<CrewUnitTotalsDto>> TotalsAsync(
		BounceRouteDbContext db, BusinessClock clock, DateOnly first, DateOnly last, int? onlyCrewMemberId)
	{
		var (start, end) = clock.Range(first, last);

		var credits = await db.Jobs
			.Where(job => job.Status == JobStatus.Done && job.ScheduledAt >= start && job.ScheduledAt < end)
			.SelectMany(job => job.Assignments.Select(assignment => new
			{
				assignment.CrewMemberId,
				job.Type,
				Units = job.Booking.Items.Count(item => item.IsUnit)
			}))
			.Where(credit => onlyCrewMemberId == null || credit.CrewMemberId == onlyCrewMemberId)
			.ToListAsync();

		var crew = await db.CrewMembers
			.Where(member => onlyCrewMemberId == null || member.Id == onlyCrewMemberId)
			.OrderBy(member => member.Name)
			.ToListAsync();

		return crew
			.Where(member => member.IsActive || credits.Any(credit => credit.CrewMemberId == member.Id))
			.Select(member =>
			{
				var mine = credits.Where(credit => credit.CrewMemberId == member.Id).ToList();
				var setup = mine.Where(credit => credit.Type == JobType.Setup).Sum(credit => credit.Units);
				var pickup = mine.Where(credit => credit.Type == JobType.Pickup).Sum(credit => credit.Units);
				return new CrewUnitTotalsDto(member.Id, member.Name, member.Initials, mine.Count, setup, pickup, setup + pickup);
			})
			.ToList();
	}

	// The weekend run starts Friday morning and includes Monday pickups, so count from the most recent Friday.
	internal static DateOnly WeekendStart(DateOnly today)
	{
		var daysSinceFriday = ((int)today.DayOfWeek - (int)DayOfWeek.Friday + 7) % 7;
		return today.AddDays(-daysSinceFriday);
	}

	private static void Apply(SaveCrewMemberRequest request, CrewMember crew)
	{
		crew.Name = request.Name.Trim();
		crew.Initials = string.IsNullOrWhiteSpace(request.Initials)
			? string.Concat(crew.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(word => char.ToUpperInvariant(word[0])))
			: request.Initials.Trim().ToUpperInvariant();
		crew.Role = request.Role?.Trim() ?? string.Empty;
		crew.IsActive = request.IsActive;
	}

	private static CrewMemberDto ToDto(CrewMember crew) => new(crew.Id, crew.Name, crew.Initials, crew.Role, crew.IsActive);
}
