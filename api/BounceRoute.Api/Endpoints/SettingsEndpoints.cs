using BounceRoute.Api.Contracts;
using BounceRoute.Api.Data;
using BounceRoute.Api.Domain;

namespace BounceRoute.Api.Endpoints;

public static class SettingsEndpoints
{
	public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
	{
		var settings = app.MapGroup("/api/settings").WithTags("Settings");

		settings.MapGet("/", GetSettings).RequireAuthorization();
		settings.MapPut("/", SaveSettings).RequireAuthorization(Policies.Owner);
	}

	private static async Task<CompanySettingsDto> GetSettings(BounceRouteDbContext db) =>
		ToDto(await db.CompanySettings.FindAsync(CompanySettings.SingletonId) ?? new CompanySettings());

	private static async Task<CompanySettingsDto> SaveSettings(SaveCompanySettingsRequest request, BounceRouteDbContext db)
	{
		var settings = await db.CompanySettings.FindAsync(CompanySettings.SingletonId);
		if (settings is null)
		{
			settings = new CompanySettings();
			db.CompanySettings.Add(settings);
		}

		settings.CompanyName = request.CompanyName?.Trim() ?? string.Empty;
		settings.DefaultCityStateZip = request.DefaultCityStateZip?.Trim() ?? string.Empty;
		settings.Phone = request.Phone?.Trim() ?? string.Empty;
		await db.SaveChangesAsync();
		return ToDto(settings);
	}

	private static CompanySettingsDto ToDto(CompanySettings settings) =>
		new(settings.CompanyName, settings.DefaultCityStateZip, settings.Phone);
}
