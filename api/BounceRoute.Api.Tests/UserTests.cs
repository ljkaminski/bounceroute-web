using System.Net;
using System.Net.Http.Json;
using BounceRoute.Api.Contracts;
using BounceRoute.Api.Domain;

namespace BounceRoute.Api.Tests;

public sealed class UserTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
	private HttpClient owner = null!;

	public async Task InitializeAsync() => owner = await factory.SignedInClientAsync(Roles.Owner);

	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Owner_creates_a_crew_account_that_can_sign_in()
	{
		var member = await CreateCrewAsync("New Hire");
		var email = Email();

		var created = await owner.PostAsJsonAsync("/api/users",
			new CreateUserRequest(email, "New Hire", UserRole.Crew, member.Id, "starting-password"), ApiFactory.Json);
		Assert.Equal(HttpStatusCode.Created, created.StatusCode);

		var me = await (await SignInAsync(email, "starting-password")).ReadAsync<CurrentUserDto>();
		Assert.Equal(UserRole.Crew, me.Role);
		Assert.Equal(member.Id, me.CrewMemberId);
	}

	[Fact]
	public async Task Crew_account_needs_a_crew_member()
	{
		var response = await owner.PostAsJsonAsync("/api/users",
			new CreateUserRequest(Email(), "No Link", UserRole.Crew, null, "starting-password"), ApiFactory.Json);

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		Assert.Contains("CrewMemberId", await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Short_password_is_rejected_with_a_password_error()
	{
		var response = await owner.PostAsJsonAsync("/api/users",
			new CreateUserRequest(Email(), "Short", UserRole.Office, null, "short"), ApiFactory.Json);

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		Assert.Contains("\"Password\"", await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Disabled_account_cannot_sign_in()
	{
		var email = Email();
		var user = await (await owner.PostAsJsonAsync("/api/users",
			new CreateUserRequest(email, "Leaving", UserRole.Office, null, "starting-password"), ApiFactory.Json)).ReadAsync<UserDto>();

		var updated = await (await owner.PutAsJsonAsync($"/api/users/{user.Id}",
			new UpdateUserRequest("Leaving", UserRole.Office, null, IsDisabled: true), ApiFactory.Json)).ReadAsync<UserDto>();
		Assert.True(updated.IsDisabled);

		var response = await SignInAsync(email, "starting-password");
		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
		Assert.Contains("turned off", await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task Owner_resets_a_forgotten_password()
	{
		var email = Email();
		var user = await (await owner.PostAsJsonAsync("/api/users",
			new CreateUserRequest(email, "Forgetful", UserRole.Office, null, "starting-password"), ApiFactory.Json)).ReadAsync<UserDto>();

		var reset = await owner.PostAsJsonAsync($"/api/users/{user.Id}/password", new ResetPasswordRequest("brand-new-password"), ApiFactory.Json);
		Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

		Assert.Equal(HttpStatusCode.Unauthorized, (await SignInAsync(email, "starting-password")).StatusCode);
		Assert.Equal(HttpStatusCode.OK, (await SignInAsync(email, "brand-new-password")).StatusCode);
	}

	[Fact]
	public async Task Owner_cannot_demote_themselves()
	{
		var me = await (await owner.GetAsync("/api/auth/me")).ReadAsync<CurrentUserDto>();

		var response = await owner.PutAsJsonAsync($"/api/users/{me.Id}",
			new UpdateUserRequest(me.DisplayName, UserRole.Office, null, false), ApiFactory.Json);

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task Office_staff_cannot_manage_accounts()
	{
		var office = await factory.SignedInClientAsync(Roles.Office);

		Assert.Equal(HttpStatusCode.Forbidden, (await office.GetAsync("/api/users")).StatusCode);
	}

	private static string Email() => $"user-{Guid.NewGuid():N}@example.com";

	private Task<HttpResponseMessage> SignInAsync(string email, string password) =>
		factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), ApiFactory.Json);

	private async Task<CrewMemberDto> CreateCrewAsync(string name) =>
		await (await owner.PostAsJsonAsync("/api/crew", new SaveCrewMemberRequest(name, null, "Crew"), ApiFactory.Json))
			.ReadAsync<CrewMemberDto>();
}

public sealed class SettingsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
	[Fact]
	public async Task Owner_sets_company_details_that_everyone_can_read()
	{
		var owner = await factory.SignedInClientAsync(Roles.Owner);
		var crew = await factory.SignedInClientAsync(Roles.Crew);

		await owner.PutAsJsonAsync("/api/settings", new SaveCompanySettingsRequest("Test Rentals", "Monroe, NC 28110", null), ApiFactory.Json);

		var seen = await (await crew.GetAsync("/api/settings")).ReadAsync<CompanySettingsDto>();
		Assert.Equal(new CompanySettingsDto("Test Rentals", "Monroe, NC 28110", "", "", MapsConfigured: false, SuggestRoutes: true), seen);
		Assert.Equal(HttpStatusCode.Forbidden,
			(await crew.PutAsJsonAsync("/api/settings", new SaveCompanySettingsRequest("Hijack", null, null), ApiFactory.Json)).StatusCode);
	}
}
