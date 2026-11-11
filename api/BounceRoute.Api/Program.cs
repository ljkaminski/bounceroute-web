using System.Reflection;
using System.Text.Json.Serialization;
using BounceRoute.Api.Data;
using BounceRoute.Api.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<BounceRouteDbContext>(options =>
	options.UseNpgsql(builder.Configuration.GetConnectionString("BounceRoute")));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(services => new BusinessClock(
	TimeZoneInfo.FindSystemTimeZoneById(builder.Configuration["Business:TimeZone"] ?? "America/New_York"),
	services.GetRequiredService<TimeProvider>()));

builder.Services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
	// Numbers are numbers; also keeps the OpenAPI types (and the generated TypeScript) as plain number.
	options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<BounceRouteDbContext>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.MapHealthChecks("/health");
app.MapRouteEndpoints();
app.MapBookingEndpoints();
app.MapCrewEndpoints();

// The build also starts this app to write the OpenAPI file; it must not touch the database then.
var generatingOpenApi = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
if (!generatingOpenApi)
{
	await using var scope = app.Services.CreateAsyncScope();
	var db = scope.ServiceProvider.GetRequiredService<BounceRouteDbContext>();
	await db.Database.MigrateAsync();

	if (app.Environment.IsDevelopment())
		await DevSeed.SeedAsync(db, scope.ServiceProvider.GetRequiredService<BusinessClock>());
}

app.Run();

public partial class Program;
