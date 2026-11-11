using System.ComponentModel.DataAnnotations;
using BounceRoute.Api.Domain;

namespace BounceRoute.Api.Contracts;

public sealed record CompanySettingsDto(string CompanyName, string DefaultCityStateZip, string Phone);

public sealed record CategorySetupTimeDto(InventoryCategory Category, [property: Range(0, 600)] int Minutes);

public sealed record SaveCompanySettingsRequest(
	[property: MaxLength(100)] string? CompanyName,
	[property: MaxLength(100)] string? DefaultCityStateZip,
	[property: MaxLength(20)] string? Phone);
