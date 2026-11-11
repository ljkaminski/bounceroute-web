using BounceRoute.Api.Contracts;
using BounceRoute.Api.Data;
using BounceRoute.Api.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace BounceRoute.Api.Endpoints;

public static class TruckEndpoints
{
	public static void MapTruckEndpoints(this IEndpointRouteBuilder app)
	{
		var trucks = app.MapGroup("/api/trucks").WithTags("Trucks");

		trucks.MapGet("/", ListTrucks).RequireAuthorization(Policies.Office);
		trucks.MapPost("/", CreateTruck).RequireAuthorization(Policies.Owner);
		var day = app.MapGroup("/api/route/trucks").WithTags("Trucks").RequireAuthorization(Policies.Office);
		day.MapGet("/", GetTrucksOut).WithSummary("Which trucks go out on a day (defaults to every truck in service).");
		day.MapPut("/", SaveTrucksOut).WithSummary("Picks which trucks go out on a day. An empty list goes back to every truck.");

		trucks.MapPut("/{id:int}", UpdateTruck)
			.WithSummary("Renaming a truck renames it on its jobs too.")
			.RequireAuthorization(Policies.Owner);
	}

	private static async Task<TrucksOutDto> GetTrucksOut(DateOnly date, BounceRouteDbContext db)
	{
		var (trucks, isDefault) = await TrucksOut.ForDayAsync(db, date);
		return new TrucksOutDto(date, [.. trucks.Select(truck => truck.Id)], isDefault);
	}

	private static async Task<Results<Ok<TrucksOutDto>, ValidationProblem>> SaveTrucksOut(
		DateOnly date, SaveTrucksOutRequest request, BounceRouteDbContext db)
	{
		var ids = request.TruckIds.Distinct().ToList();
		if (await db.Trucks.CountAsync(truck => ids.Contains(truck.Id)) != ids.Count)
			return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["TruckIds"] = ["One or more trucks do not exist."] });

		await db.TruckDays.Where(row => row.Date == date).ExecuteDeleteAsync();
		db.TruckDays.AddRange(ids.Select(id => new TruckDay { Date = date, TruckId = id }));
		await db.SaveChangesAsync();
		return TypedResults.Ok(await GetTrucksOut(date, db));
	}

	private static async Task<List<TruckDto>> ListTrucks(BounceRouteDbContext db) =>
		[.. (await db.Trucks.Include(truck => truck.Crew).OrderBy(truck => truck.Name).ToListAsync()).Select(ToDto)];

	private static async Task<Results<Created<TruckDto>, ValidationProblem>> CreateTruck(SaveTruckRequest request, BounceRouteDbContext db)
	{
		if (await ValidateAsync(request, db, id: null) is { } problem)
			return problem;

		var truck = new Truck();
		Apply(request, truck);
		db.Trucks.Add(truck);
		await db.SaveChangesAsync();
		return TypedResults.Created($"/api/trucks/{truck.Id}", ToDto(truck));
	}

	private static async Task<Results<Ok<TruckDto>, NotFound, ValidationProblem>> UpdateTruck(int id, SaveTruckRequest request, BounceRouteDbContext db)
	{
		var truck = await db.Trucks.Include(t => t.Crew).FirstOrDefaultAsync(t => t.Id == id);
		if (truck is null)
			return TypedResults.NotFound();

		if (await ValidateAsync(request, db, id) is { } problem)
			return problem;

		var oldName = truck.Name;
		Apply(request, truck);
		await using var transaction = await db.Database.BeginTransactionAsync();
		await db.SaveChangesAsync();
		if (oldName != truck.Name)
			await db.Jobs.Where(job => job.Truck == oldName).ExecuteUpdateAsync(set => set.SetProperty(job => job.Truck, truck.Name));
		await transaction.CommitAsync();
		return TypedResults.Ok(ToDto(truck));
	}

	private static async Task<ValidationProblem?> ValidateAsync(SaveTruckRequest request, BounceRouteDbContext db, int? id)
	{
		var name = request.Name.Trim();
		if (await db.Trucks.AnyAsync(truck => truck.Name.ToLower() == name.ToLower() && truck.Id != id))
			return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["Name"] = ["There's already a truck with that name."] });

		var crewIds = (request.CrewMemberIds ?? []).Distinct().ToList();
		if (await db.CrewMembers.CountAsync(crew => crewIds.Contains(crew.Id)) != crewIds.Count)
			return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["CrewMemberIds"] = ["One or more crew members do not exist."] });

		return null;
	}

	private static void Apply(SaveTruckRequest request, Truck truck)
	{
		truck.Name = request.Name.Trim();
		truck.Capacity = request.Capacity;
		truck.IsActive = request.IsActive;
		var wanted = (request.CrewMemberIds ?? []).Distinct().ToHashSet();
		truck.Crew.RemoveAll(member => !wanted.Contains(member.CrewMemberId));
		foreach (var crewId in wanted.Except(truck.Crew.Select(member => member.CrewMemberId)))
			truck.Crew.Add(new TruckCrewMember { CrewMemberId = crewId });
	}

	internal static TruckDto ToDto(Truck truck) =>
		new(truck.Id, truck.Name, truck.Capacity, truck.IsActive, [.. truck.Crew.Select(member => member.CrewMemberId).Order()]);
}
