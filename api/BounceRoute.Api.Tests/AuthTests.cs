using System.Net;
using System.Net.Http.Json;
using BounceRoute.Api.Contracts;
using BounceRoute.Api.Domain;

namespace BounceRoute.Api.Tests;

public sealed class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
	[Fact]
	public async Task Signed_in_user_can_see_who_they_are()
	{
		var client = await factory.SignedInClientAsync(Roles.Office);

		var me = await (await client.GetAsync("/api/auth/me")).ReadAsync<CurrentUserDto>();

		Assert.Equal(UserRole.Office, me.Role);
		Assert.StartsWith("office-", me.Email);
	}

	[Fact]
	public async Task Wrong_password_is_rejected()
	{
		var client = factory.CreateClient();

		var response = await client.PostAsJsonAsync("/api/auth/login",
			new LoginRequest("nobody@example.com", "not-the-password"), ApiFactory.Json);

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task Signed_out_request_gets_401_not_a_redirect()
	{
		var client = factory.CreateClient(new() { AllowAutoRedirect = false });

		var response = await client.GetAsync("/api/auth/me");

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task Logout_ends_the_session()
	{
		var client = await factory.SignedInClientAsync(Roles.Crew);

		await client.PostAsync("/api/auth/logout", null);

		Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
	}
}

public sealed class LoginRateLimitTests(LoginRateLimitTests.LimitedApiFactory factory)
	: IClassFixture<LoginRateLimitTests.LimitedApiFactory>
{
	[Fact]
	public async Task Too_many_login_attempts_from_one_address_get_429()
	{
		var client = factory.CreateClient();

		var statuses = new List<HttpStatusCode>();
		for (var attempt = 0; attempt < 4; attempt++)
		{
			var response = await client.PostAsJsonAsync("/api/auth/login",
				new LoginRequest("nobody@example.com", "not-the-password"), ApiFactory.Json);
			statuses.Add(response.StatusCode);
		}

		Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], statuses);
	}

	public sealed class LimitedApiFactory : ApiFactory
	{
		protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
		{
			base.ConfigureWebHost(builder);
			builder.UseSetting("Auth:LoginAttemptsPerMinute", "3");
		}
	}
}
