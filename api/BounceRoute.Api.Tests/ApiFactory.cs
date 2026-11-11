using System.Net.Http.Json;
using BounceRoute.Api.Contracts;
using BounceRoute.Api.Data;
using BounceRoute.Api.Domain;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace BounceRoute.Api.Tests;

// Runs the real API against a throwaway Postgres in Docker. Shared by every test in a class;
// each test schedules its jobs on its own dates so they do not see each other's data.
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
	public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
	{
		Converters = { new JsonStringEnumConverter() }
	};

	private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:16-alpine").Build();

	public async Task InitializeAsync() => await database.StartAsync();

	public new async Task DisposeAsync()
	{
		await base.DisposeAsync();
		await database.DisposeAsync();
	}

	public const string Password = "test-password-123";

	// Creates an account with a unique email and returns a client already signed in as it.
	public async Task<HttpClient> SignedInClientAsync(string role, int? crewMemberId = null)
	{
		var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com";
		await using (var scope = Services.CreateAsyncScope())
		{
			var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
			await AccountSeed.CreateAsync(users, email, Password, $"Test {role}", role, crewMemberId);
		}

		var client = CreateClient();
		var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password), Json);
		response.EnsureSuccessStatusCode();
		return client;
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseEnvironment("Testing");
		builder.UseSetting("ConnectionStrings:BounceRoute", database.GetConnectionString());
		builder.UseSetting("Business:TimeZone", "America/New_York");
		builder.UseSetting("Auth:LoginAttemptsPerMinute", "1000");
	}
}

public static class HttpResponseExtensions
{
	public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
	{
		response.EnsureSuccessStatusCode();
		return (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json))!;
	}
}
