using System.Net;
using System.Net.Http.Json;
using BounceRoute.Api.Contracts;
using BounceRoute.Api.Domain;

namespace BounceRoute.Api.Tests;

// What a crew sign-in can and cannot reach. Each test uses its own dates so the shared database stays out of the way.
public sealed class CrewAccessTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
	private HttpClient office = null!;

	public async Task InitializeAsync() => office = await factory.SignedInClientAsync(Roles.Office);

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Crew_see_only_their_own_jobs_on_the_route()
	{
		var (me, client) = await CrewSignInAsync("Route Me");
		var other = await CreateCrewAsync("Route Other");
		var mine = await CreateBookingAsync("2031-04-01", me.Id);
		await CreateBookingAsync("2031-04-01", other.Id);

		var route = await (await client.GetAsync("/api/route?date=2031-04-01")).ReadAsync<RouteDayDto>();

		// Setup and pickup are both that day; neither belongs to the other crew member's booking.
		Assert.Equal([JobType.Setup, JobType.Pickup], route.Jobs.Select(job => job.Type));
		Assert.All(route.Jobs, job => Assert.Equal(mine.Id, job.BookingId));
		Assert.True(route.Jobs[0].IsNextUp);
	}

	[Fact]
	public async Task Crew_cannot_open_or_complete_someone_elses_job()
	{
		var (_, client) = await CrewSignInAsync("Snoop");
		var other = await CreateCrewAsync("Not Snoop");
		var booking = await CreateBookingAsync("2031-04-02", other.Id);

		Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/jobs/{booking.Setup.Id}")).StatusCode);
		var complete = await client.PostAsJsonAsync($"/api/jobs/{booking.Setup.Id}/complete", new CompleteJobRequest(null), ApiFactory.Json);
		Assert.Equal(HttpStatusCode.NotFound, complete.StatusCode);
		var load = await client.PutAsJsonAsync($"/api/booking-items/{booking.Items[0].Id}/loaded", new SetItemLoadedRequest(true), ApiFactory.Json);
		Assert.Equal(HttpStatusCode.NotFound, load.StatusCode);
	}

	[Fact]
	public async Task Crew_complete_jobs_as_themselves()
	{
		var (me, client) = await CrewSignInAsync("Finisher");
		var partner = await CreateCrewAsync("Partner");
		var booking = await CreateBookingAsync("2031-04-03", me.Id, partner.Id);

		var asPartner = await client.PostAsJsonAsync($"/api/jobs/{booking.Setup.Id}/complete", new CompleteJobRequest(partner.Id), ApiFactory.Json);
		Assert.Equal(HttpStatusCode.BadRequest, asPartner.StatusCode);

		var done = await (await client.PostAsJsonAsync($"/api/jobs/{booking.Setup.Id}/complete", new CompleteJobRequest(null), ApiFactory.Json))
			.ReadAsync<JobCardDto>();
		Assert.Equal(JobStatus.Done, done.Status);
		Assert.Equal("Finisher", done.CompletedBy);
	}

	[Fact]
	public async Task Crew_see_their_own_units()
	{
		var (me, client) = await CrewSignInAsync("Earner");
		var booking = await CreateBookingAsync("2031-04-04", me.Id);
		await client.PostAsJsonAsync($"/api/jobs/{booking.Setup.Id}/complete", new CompleteJobRequest(null), ApiFactory.Json);

		var report = await (await client.GetAsync("/api/me/units?from=2031-04-04&to=2031-04-04")).ReadAsync<UnitTotalsReport>();

		var total = Assert.Single(report.Crew);
		Assert.Equal(me.Id, total.CrewMemberId);
		Assert.Equal(1, total.TotalUnits);
	}

	private async Task<(CrewMemberDto Member, HttpClient Client)> CrewSignInAsync(string name)
	{
		var member = await CreateCrewAsync(name);
		return (member, await factory.SignedInClientAsync(Roles.Crew, member.Id));
	}

	private async Task<CrewMemberDto> CreateCrewAsync(string name) =>
		await (await office.PostAsJsonAsync("/api/crew", new SaveCrewMemberRequest(name, null, "Crew"), ApiFactory.Json))
			.ReadAsync<CrewMemberDto>();

	private async Task<BookingDto> CreateBookingAsync(string day, params int[] crewIds)
	{
		var setup = new SaveJobScheduleRequest(DateTimeOffset.Parse($"{day}T09:00:00-04:00"), "Truck 1", crewIds);
		var pickup = setup with { ScheduledAt = setup.ScheduledAt.AddHours(8) };
		var request = new SaveBookingRequest("Crew Test", null, "2 Test St", null, Surface.Grass, null, setup, pickup,
			[new("Castle Combo", true)]);
		return await (await office.PostAsJsonAsync("/api/bookings", request, ApiFactory.Json)).ReadAsync<BookingDto>();
	}
}
