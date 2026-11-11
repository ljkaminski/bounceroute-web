using System.ComponentModel.DataAnnotations;

namespace BounceRoute.Api.Contracts;

public sealed record CompanySettingsDto(string CompanyName, string DefaultCityStateZip, string Phone);

public sealed record SaveCompanySettingsRequest(
	[property: MaxLength(100)] string? CompanyName,
	[property: MaxLength(100)] string? DefaultCityStateZip,
	[property: MaxLength(20)] string? Phone);
