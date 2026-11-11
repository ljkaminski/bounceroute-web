using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace BounceRoute.Api.Tests;

// Runs the real API against a throwaway Postgres in Docker. Shared by every test in a class;
// each test schedules its jobs on its own dates so they do not see each other's data.
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
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

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseEnvironment("Testing");
		builder.UseSetting("ConnectionStrings:BounceRoute", database.GetConnectionString());
		builder.UseSetting("Business:TimeZone", "America/New_York");
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
