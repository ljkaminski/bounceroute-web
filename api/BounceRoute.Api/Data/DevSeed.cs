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
		db.Trucks.AddRange(new Truck { Name = "Truck 1" }, new Truck { Name = "Truck 2" });
		db.CompanySettings.Add(new CompanySettings
		{
			CompanyName = "Demo Party Rentals",
			DefaultCityStateZip = "Greenville, NC 27858",
			Phone = "(252) 555-0100"
		});
		var (jordan, riley, sam, casey, morgan) = (crew[0], crew[1], crew[2], crew[3], crew[4]);

		InventoryItem Owned(string name, InventoryCategory category, bool isUnit, WaterUse water, int quantity, int? length, int? width, int? height, decimal price) => new()
		{
			Name = name,
			Category = category,
			IsUnit = isUnit,
			WaterUse = water,
			Quantity = quantity,
			NeedsPower = category != InventoryCategory.Game,
			TruckSpace = category == InventoryCategory.Equipment ? 0 : 1,
			LengthFt = length,
			WidthFt = width,
			HeightFt = height,
			DailyPrice = price
		};

		var inventory = new[]
		{
			Owned("Castle Combo", InventoryCategory.Combo, true, WaterUse.WetOrDry, 2, 28, 14, 14, 225m),
			Owned("Dino Bounce House", InventoryCategory.BounceHouse, true, WaterUse.DryOnly, 1, 15, 15, 14, 160m),
			Owned("Mini Bounce", InventoryCategory.BounceHouse, true, WaterUse.DryOnly, 1, 12, 12, 10, 125m),
			Owned("Tropical Water Slide", InventoryCategory.WaterSlide, true, WaterUse.WetOnly, 1, 40, 18, 20, 375m),
			Owned("Obstacle Course", InventoryCategory.ObstacleCourse, true, WaterUse.DryOnly, 1, 40, 10, 13, 290m),
			Owned("Slip and Slide", InventoryCategory.SlipNSlide, true, WaterUse.WetOnly, 1, 30, 12, 10, 200m),
			Owned("Generator", InventoryCategory.Equipment, false, WaterUse.None, 2, null, null, null, 90m),
			Owned("Popcorn Machine", InventoryCategory.Equipment, false, WaterUse.None, 1, null, null, null, 60m),
			Owned("Cornhole", InventoryCategory.Game, false, WaterUse.None, 1, null, null, null, 30m)
		};
		db.InventoryItems.AddRange(inventory);
		var byName = inventory.ToDictionary(item => item.Name);

		// Items not in the sample inventory (like "Tables (4)") are one-offs typed on the booking.
		BookingItem Line(string name, bool wet)
		{
			if (!byName.TryGetValue(name, out var owned))
				return new BookingItem { Name = name };

			return new BookingItem
			{
				Name = owned.Name,
				IsUnit = owned.IsUnit,
				InventoryItem = owned,
				IsWet = owned.WaterUse switch
				{
					WaterUse.WetOnly => true,
					WaterUse.DryOnly => false,
					WaterUse.WetOrDry => wet,
					_ => null
				}
			};
		}

		var today = clock.Today;
		var now = DateTimeOffset.UtcNow;

		DateTimeOffset At(int dayOffset, int hour, int minute = 0) =>
			clock.StartOf(today.AddDays(dayOffset)).AddHours(hour).AddMinutes(minute);

		void Add(string customer, string phone, string street, Surface surface, string notes,
			DateTimeOffset setupAt, DateTimeOffset pickupAt, string truck, CrewMember[] team,
			string[] items, bool wet = false)
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
				Items = [.. items.Select(name => Line(name, wet))]
			});
		}

		Add("Avery Thompson", "(252) 555-0141", "118 Oak Hollow Dr", Surface.Grass, "Gate code 4412. Dog in the side yard.",
			At(-1, 9), At(-1, 17), "Truck 1", [jordan, riley], ["Castle Combo", "Generator"], wet: true);
		Add("Pitt County Parks", "(252) 555-0108", "2000 Cedar Ln", Surface.Grass, "Check in at the shelter office first.",
			At(0, 8), At(0, 18), "Truck 2", [jordan, riley, sam], ["Tropical Water Slide", "Generator", "Cornhole"]);
		Add("Quinn Walker", "(252) 555-0177", "406 Ridgeway St", Surface.Concrete, "Driveway setup, sandbags only.",
			At(0, 9, 30), At(0, 16), "Truck 1", [casey, morgan], ["Dino Bounce House"]);
		Add("Harper Lewis", "(252) 555-0163", "77 Lakeview Ct", Surface.Grass, "Backyard through the left gate. Slope near the fence.",
			At(0, 11), At(1, 10), "Truck 2", [jordan, sam], ["Slip and Slide", "Popcorn Machine"]);
		Add("Elm Street Church", "(252) 555-0190", "512 Elm St", Surface.Inside, "Gym floor. Use tarps under everything.",
			At(1, 8), At(1, 15), "Truck 1", [casey, riley, morgan], ["Castle Combo", "Mini Bounce", "Tables (4)"]);
		Add("Rowan Mitchell", "(252) 555-0122", "39 Pine Bluff Rd", Surface.Gravel, "Gravel lot: double tarp.",
			At(2, 10), At(3, 9), "Truck 2", [jordan, riley], ["Tropical Water Slide", "Generator"]);

		await db.SaveChangesAsync();
	}
}
