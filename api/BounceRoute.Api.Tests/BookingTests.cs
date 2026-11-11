using System.Net;
using System.Net.Http.Json;
using BounceRoute.Api.Contracts;
using BounceRoute.Api.Domain;

namespace BounceRoute.Api.Tests;

public sealed class BookingTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
	private HttpClient client = null!;

	public async Task InitializeAsync() => client = await factory.SignedInClientAsync(Roles.Office);

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Created_booking_shows_on_the_route_for_its_setup_day()
	{
		var crew = await CreateCrewAsync("Test Lead");
		var booking = await CreateBookingAsync("2030-03-01T09:00:00-05:00", "2030-03-02T09:00:00-05:00", [crew.Id]);

		Assert.True(booking.JobNumber >= 1001);

		var route = await (await client.GetAsync("/api/route?date=2030-03-01")).ReadAsync<RouteDayDto>();
		var job = Assert.Single(route.Jobs);
		Assert.Equal(booking.Id, job.BookingId);
		Assert.Equal(JobType.Setup, job.Type);
		Assert.True(job.IsNextUp);
		Assert.Equal(["Castle Combo"], job.Units.Select(unit => unit.Name));
		Assert.Equal(["Generator"], job.Extras.Select(extra => extra.Name));
		Assert.Equal(crew.Id, Assert.Single(job.Crew).Id);
	}

	[Fact]
	public async Task Booking_list_shows_bookings_by_setup_time()
	{
		var later = await CreateBookingAsync("2030-03-20T13:00:00-04:00", "2030-03-20T18:00:00-04:00", []);
		var earlier = await CreateBookingAsync("2030-03-20T08:00:00-04:00", "2030-03-21T09:00:00-04:00", []);

		var list = await (await client.GetAsync("/api/bookings?from=2030-03-20&to=2030-03-20")).ReadAsync<List<BookingSummaryDto>>();

		Assert.Equal([earlier.Id, later.Id], list.Select(booking => booking.Id));
		Assert.Equal(1, list[0].UnitCount);
		Assert.False(list[0].IsComplete);
	}

	[Fact]
	public async Task Late_night_job_belongs_to_the_business_day_not_the_utc_day()
	{
		// 11pm Eastern is already the next day in UTC.
		await CreateBookingAsync("2030-03-05T23:00:00-05:00", "2030-03-06T10:00:00-05:00", []);

		var route = await (await client.GetAsync("/api/route?date=2030-03-05")).ReadAsync<RouteDayDto>();

		Assert.Single(route.Jobs);
	}

	[Fact]
	public async Task Pickup_before_setup_is_rejected()
	{
		var request = Request("2030-03-10T12:00:00-05:00", "2030-03-10T08:00:00-05:00", []);

		var response = await client.PostAsJsonAsync("/api/bookings", request, ApiFactory.Json);

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		Assert.Contains("Pickup.ScheduledAt", await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Editing_a_booking_keeps_loaded_checks_on_items_that_stay()
	{
		var booking = await CreateBookingAsync("2030-03-12T09:00:00-05:00", "2030-03-12T17:00:00-05:00", []);
		var castle = booking.Items.Single(item => item.Name == "Castle Combo");
		await client.PutAsJsonAsync($"/api/booking-items/{castle.Id}/loaded", new SetItemLoadedRequest(true), ApiFactory.Json);

		var edited = Request("2030-03-12T10:00:00-05:00", "2030-03-12T17:00:00-05:00", []) with
		{
			Items = [new("Castle Combo", true), new("Popcorn Machine", false)]
		};
		var saved = await (await client.PutAsJsonAsync($"/api/bookings/{booking.Id}", edited, ApiFactory.Json)).ReadAsync<BookingDto>();

		Assert.True(saved.Items.Single(item => item.Name == "Castle Combo").IsLoaded);
		Assert.False(saved.Items.Single(item => item.Name == "Popcorn Machine").IsLoaded);
		Assert.DoesNotContain(saved.Items, item => item.Name == "Generator");
	}

	[Fact]
	public async Task Units_count_only_on_completed_jobs_for_the_crew_on_them()
	{
		var onJob = await CreateCrewAsync("On The Job");
		var offJob = await CreateCrewAsync("Day Off");
		var booking = await CreateBookingAsync("2030-03-15T09:00:00-04:00", "2030-03-16T09:00:00-04:00", [onJob.Id]);

		await client.PostAsJsonAsync($"/api/jobs/{booking.Setup.Id}/complete", new CompleteJobRequest(onJob.Id), ApiFactory.Json);

		var report = await (await client.GetAsync("/api/crew/units?from=2030-03-15&to=2030-03-18")).ReadAsync<UnitTotalsReport>();
		var mine = report.Crew.Single(total => total.CrewMemberId == onJob.Id);
		Assert.Equal(1, mine.Jobs);
		Assert.Equal(1, mine.SetupUnits);
		Assert.Equal(0, mine.PickupUnits);
		Assert.Equal(0, report.Crew.Single(total => total.CrewMemberId == offJob.Id).TotalUnits);
	}

	private async Task<CrewMemberDto> CreateCrewAsync(string name) =>
		await (await client.PostAsJsonAsync("/api/crew", new SaveCrewMemberRequest(name, null, "Crew"), ApiFactory.Json))
			.ReadAsync<CrewMemberDto>();

	private async Task<BookingDto> CreateBookingAsync(string setupAt, string pickupAt, int[] crewIds)
	{
		var response = await client.PostAsJsonAsync("/api/bookings", Request(setupAt, pickupAt, crewIds), ApiFactory.Json);
		Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		return await response.ReadAsync<BookingDto>();
	}

	private static SaveBookingRequest Request(string setupAt, string pickupAt, int[] crewIds) => new(
		"Test Customer",
		"(252) 555-0100",
		"1 Test St",
		"Greenville, NC 27858",
		Surface.Grass,
		null,
		new SaveJobScheduleRequest(DateTimeOffset.Parse(setupAt), "Truck 1", crewIds),
		new SaveJobScheduleRequest(DateTimeOffset.Parse(pickupAt), "Truck 1", crewIds),
		[new("Castle Combo", true), new("Generator", false)]);
}
