using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BounceRoute.Api.Data;
using BounceRoute.Api.Domain;
using BounceRoute.Api.Endpoints;
using BounceRoute.Api.Maps;
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
	.AddSignInManager()
	.AddDefaultTokenProviders()
	.AddClaimsPrincipalFactory<AppClaimsFactory>();

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
builder.Services.AddAuthorization(Policies.Add);

// How soon a role change, disable, or password reset reaches someone who is already signed in.
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(1));

// Slows down password guessing from one address; account lockout covers guessing one account.
builder.Services.AddRateLimiter(options =>
{
	options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
	options.AddPolicy(AuthEndpoints.LoginRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
		context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
		_ => new FixedWindowRateLimiterOptions
		{
			PermitLimit = builder.Configuration.GetValue("Auth:LoginAttemptsPerMinute", 10),
			Window = TimeSpan.FromMinutes(1)
		}));
});

builder.Services.AddSingleton(TimeProvider.System);

// Drive times come from OpenRouteService when a key is set (keep it in user secrets or an environment
// variable, never in this repo). Without one the app still runs and route plans skip drive times.
builder.Services.AddMemoryCache();
var mapsKey = builder.Configuration[OpenRouteServiceMaps.ApiKeySetting];
if (string.IsNullOrWhiteSpace(mapsKey))
	builder.Services.AddSingleton<IMaps, NoMaps>();
else
	builder.Services.AddHttpClient<IMaps, OpenRouteServiceMaps>(client =>
	{
		client.BaseAddress = new Uri("https://api.openrouteservice.org/");
		// OpenRouteService wants the bare key, which isn't a "scheme value" pair, so skip header validation.
		client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", mapsKey);
		client.Timeout = TimeSpan.FromSeconds(10);
	});
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
app.UseRateLimiter();

app.MapOpenApi();
app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapRouteEndpoints();
app.MapRoutePlanEndpoints();
app.MapBookingEndpoints();
app.MapCrewEndpoints();
app.MapInventoryEndpoints();
app.MapInventoryPhotoEndpoints();
app.MapSettingsEndpoints();
app.MapTruckEndpoints();
app.MapUserEndpoints();

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
	{
		await DevSeed.SeedAsync(db, scope.ServiceProvider.GetRequiredService<BusinessClock>());
		await DevSeed.SeedAccountsAsync(db, scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>());
	}
}

app.Run();

public partial class Program;
