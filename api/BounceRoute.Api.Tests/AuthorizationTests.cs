using System.Net;
using BounceRoute.Api.Domain;

namespace BounceRoute.Api.Tests;

public sealed class AuthorizationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
	[Theory]
	[InlineData("/api/route")]
	[InlineData("/api/bookings")]
	[InlineData("/api/crew")]
	[InlineData("/api/crew/units")]
	public async Task Signed_out_requests_are_rejected(string url)
	{
		var response = await factory.CreateClient().GetAsync(url);

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Theory]
	[InlineData("/api/bookings")]
	[InlineData("/api/crew")]
	public async Task Crew_cannot_use_office_screens(string url)
	{
		var client = await factory.SignedInClientAsync(Roles.Crew);

		var response = await client.GetAsync(url);

		Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
	}

	[Fact]
	public async Task Health_check_stays_public()
	{
		var response = await factory.CreateClient().GetAsync("/health");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}
}
