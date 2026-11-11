using System.Reflection;
using System.Text.Json.Serialization;
using BounceRoute.Api.Data;
using BounceRoute.Api.Domain;
using BounceRoute.Api.Endpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<BounceRouteDbContext>(options =>
	options.UseNpgsql(builder.Configuration.GetConnectionString("BounceRoute")));

builder.Services.AddIdentityCore<AppUser>(options =>
	{
		options.User.RequireUniqueEmail = true;
		// Length over composition rules (NIST 800-63B); crews type these on phones.
		options.Password.RequiredLength = 10;
		options.Password.RequireNonAlphanumeric = false;
		options.Password.RequireUppercase = false;
		options.Password.RequireLowercase = false;
		options.Password.RequireDigit = false;
		options.Lockout.MaxFailedAccessAttempts = 5;
	})
	.AddRoles<IdentityRole>()
	.AddEntityFrameworkStores<BounceRouteDbContext>()
	.AddSignInManager();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
	options.Cookie.Name = "bounceroute.auth";
	options.Cookie.HttpOnly = true;
	// The web app is served from the same site as the API, so the cookie never needs to go cross-site.
	options.Cookie.SameSite = SameSiteMode.Strict;
	options.ExpireTimeSpan = TimeSpan.FromDays(14);
	options.SlidingExpiration = true;
	// This is an API: answer 401/403 instead of redirecting to a login page.
	options.Events.OnRedirectToLogin = context =>
	{
		context.Response.StatusCode = StatusCodes.Status401Unauthorized;
		return Task.CompletedTask;
	};
	options.Events.OnRedirectToAccessDenied = context =>
	{
		context.Response.StatusCode = StatusCodes.Status403Forbidden;
		return Task.CompletedTask;
	};
});
builder.Services.AddAuthorization();

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
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapHealthChecks("/health");
app.MapAuthEndpoints();
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

	await AccountSeed.EnsureRolesAsync(scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());
	await AccountSeed.EnsureBootstrapOwnerAsync(
		scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(), app.Configuration, app.Logger);

	if (app.Environment.IsDevelopment())
		await DevSeed.SeedAsync(db, scope.ServiceProvider.GetRequiredService<BusinessClock>());
}

app.Run();

public partial class Program;
