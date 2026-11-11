using BounceRoute.Api.Contracts;
using BounceRoute.Api.Data;
using BounceRoute.Api.Domain;
using BounceRoute.Api.Maps;
using Microsoft.EntityFrameworkCore;

namespace BounceRoute.Api.Endpoints;

public static class SettingsEndpoints
{
	public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
	{
		var settings = app.MapGroup("/api/settings").WithTags("Settings");

		settings.MapGet("/", GetSettings).RequireAuthorization();
		settings.MapPut("/", SaveSettings).RequireAuthorization(Policies.Owner);

		settings.MapGet("/setup-times", GetSetupTimes)
			.WithSummary("Minutes to set up (or take down) one unit of each type.")
			.RequireAuthorization(Policies.Office);
		settings.MapPut("/setup-times", SaveSetupTimes).RequireAuthorization(Policies.Owner);
	}

	private static async Task<CompanySettingsDto> GetSettings(BounceRouteDbContext db, IMaps maps) =>
		ToDto(await db.CompanySettings.FindAsync(CompanySettings.SingletonId) ?? new CompanySettings(), maps);

	private static async Task<CompanySettingsDto> SaveSettings(SaveCompanySettingsRequest request, BounceRouteDbContext db, IMaps maps)
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
		settings.SuggestRoutes = request.SuggestRoutes ?? settings.SuggestRoutes;

		var shop = request.ShopAddress?.Trim() ?? string.Empty;
		if (shop != settings.ShopAddress)
		{
			settings.ShopAddress = shop;
			// If the service doesn't answer now, route plans try the lookup again when they need it.
			var point = shop.Length == 0 ? null : (await maps.GeocodeAsync(shop)).Point;
			settings.ShopLatitude = point?.Latitude;
			settings.ShopLongitude = point?.Longitude;
		}

		await db.SaveChangesAsync();
		return ToDto(settings, maps);
	}

	private static async Task<List<CategorySetupTimeDto>> GetSetupTimes(BounceRouteDbContext db) =>
		[.. (await SetupTimes.LoadAsync(db)).Select(entry => new CategorySetupTimeDto(entry.Key, entry.Value))];

	private static async Task<List<CategorySetupTimeDto>> SaveSetupTimes(List<CategorySetupTimeDto> times, BounceRouteDbContext db)
	{
		var saved = await db.CategorySetupTimes.ToDictionaryAsync(time => time.Category);
		foreach (var time in times)
		{
			if (!saved.TryGetValue(time.Category, out var row))
				db.CategorySetupTimes.Add(row = new CategorySetupTime { Category = time.Category });
			row.Minutes = time.Minutes;
		}
		await db.SaveChangesAsync();
		return await GetSetupTimes(db);
	}

	private static CompanySettingsDto ToDto(CompanySettings settings, IMaps maps) =>
		new(settings.CompanyName, settings.DefaultCityStateZip, settings.Phone, settings.ShopAddress, maps.IsConfigured, settings.SuggestRoutes);
}
