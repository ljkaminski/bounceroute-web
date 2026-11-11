using BounceRoute.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BounceRoute.Api.Data;

// Made-up crew and bookings around today so the app has something to show in development.
// None of this is real customer data.
public static class DevSeed
{
	// One demo sign-in per role besides the owner (which comes from Auth:BootstrapOwner).
	public static async Task SeedAccountsAsync(BounceRouteDbContext db, UserManager<AppUser> users)
	{
		if (await users.FindByEmailAsync("office@example.com") is null)
			await AccountSeed.CreateAsync(users, "office@example.com", "bounceroute2026", "Office Demo", Roles.Office);

		var jordan = await db.CrewMembers.FirstOrDefaultAsync(crew => crew.Name == "Jordan Hale");
		if (jordan is not null && await users.FindByEmailAsync("jordan@example.com") is null)
			await AccountSeed.CreateAsync(users, "jordan@example.com", "bounceroute2026", jordan.Name, Roles.Crew, jordan.Id);
	}

	public static async Task SeedAsync(BounceRouteDbContext db, BusinessClock clock)
	{
		if (await db.CrewMembers.AnyAsync())
			return;

		CrewMember Crew(string name, string role) => new()
		{
			Name = name,
			Initials = string.Concat(name.Split(' ').Select(word => word[0])),
			Role = role
		};

		var crew = new[]
		{
			Crew("Jordan Hale", "Crew Lead"),
			Crew("Riley Park", "Crew"),
			Crew("Sam Ortega", "Crew"),
			Crew("Casey Brooks", "Crew Lead"),
			Crew("Morgan Diaz", "Crew")
		};
		db.CrewMembers.AddRange(crew);
		var (jordan, riley, sam, casey, morgan) = (crew[0], crew[1], crew[2], crew[3], crew[4]);

		var today = clock.Today;
		var now = DateTimeOffset.UtcNow;

		DateTimeOffset At(int dayOffset, int hour, int minute = 0) =>
			clock.StartOf(today.AddDays(dayOffset)).AddHours(hour).AddMinutes(minute);

		void Add(string customer, string phone, string street, Surface surface, string notes,
			DateTimeOffset setupAt, DateTimeOffset pickupAt, string truck, CrewMember[] team,
			string[] units, string[] extras)
		{
			Job NewJob(JobType type, DateTimeOffset at)
			{
				var done = at < now;
				return new Job
				{
					Type = type,
					ScheduledAt = at,
					Truck = truck,
					Status = done ? JobStatus.Done : JobStatus.Upcoming,
					CompletedAt = done ? at.AddMinutes(45) : null,
					CompletedBy = done ? team[0] : null,
					Assignments = [.. team.Select(member => new JobAssignment { CrewMember = member })]
				};
			}

			db.Bookings.Add(new Booking
			{
				CustomerName = customer,
				Phone = phone,
				Street = street,
				CityStateZip = "Greenville, NC 27858",
				Surface = surface,
				SiteNotes = notes,
				CreatedAt = now,
				UpdatedAt = now,
				Jobs = [NewJob(JobType.Setup, setupAt), NewJob(JobType.Pickup, pickupAt)],
				Items =
				[
					.. units.Select(name => new BookingItem { Name = name, IsUnit = true }),
					.. extras.Select(name => new BookingItem { Name = name })
				]
			});
		}

		Add("Avery Thompson", "(252) 555-0141", "118 Oak Hollow Dr", Surface.Grass, "Gate code 4412. Dog in the side yard.",
			At(-1, 9), At(-1, 17), "Truck 1", [jordan, riley], ["Castle Combo"], ["Generator"]);
		Add("Pitt County Parks", "(252) 555-0108", "2000 Cedar Ln", Surface.Grass, "Check in at the shelter office first.",
			At(0, 8), At(0, 18), "Truck 2", [jordan, riley, sam], ["Tropical Water Slide", "Obstacle Course"], ["Generator", "Cornhole"]);
		Add("Quinn Walker", "(252) 555-0177", "406 Ridgeway St", Surface.Concrete, "Driveway setup, sandbags only.",
			At(0, 9, 30), At(0, 16), "Truck 1", [casey, morgan], ["Dino Bounce House"], []);
		Add("Harper Lewis", "(252) 555-0163", "77 Lakeview Ct", Surface.Grass, "Backyard through the left gate. Slope near the fence.",
			At(0, 11), At(1, 10), "Truck 2", [jordan, sam], ["Slip and Slide"], ["Popcorn Machine"]);
		Add("Elm Street Church", "(252) 555-0190", "512 Elm St", Surface.Inside, "Gym floor. Use tarps under everything.",
			At(1, 8), At(1, 15), "Truck 1", [casey, riley, morgan], ["Castle Combo", "Mini Bounce"], ["Tables (4)"]);
		Add("Rowan Mitchell", "(252) 555-0122", "39 Pine Bluff Rd", Surface.Gravel, "Gravel lot: double tarp.",
			At(2, 10), At(3, 9), "Truck 2", [jordan, riley], ["Tropical Water Slide"], ["Generator"]);

		await db.SaveChangesAsync();
	}
}
