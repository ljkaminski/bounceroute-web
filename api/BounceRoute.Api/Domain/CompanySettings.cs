namespace BounceRoute.Api.Domain;

// The one row of company details the owner fills in, so nothing about a specific company lives in the code.
public sealed class CompanySettings
{
	public const int SingletonId = 1;

	public int Id { get; set; } = SingletonId;

	public string CompanyName { get; set; } = string.Empty;

	// Pre-filled on new bookings, since most jobs are in the same area.
	public string DefaultCityStateZip { get; set; } = string.Empty;

	public string Phone { get; set; } = string.Empty;

	// Where the trucks leave from and come back to each day.
	public string ShopAddress { get; set; } = string.Empty;

	public double? ShopLatitude { get; set; }

	public double? ShopLongitude { get; set; }

	// When on, the Route page offers a better plan when it finds one. Off means the office plans every day by hand.
	public bool SuggestRoutes { get; set; } = true;
}
