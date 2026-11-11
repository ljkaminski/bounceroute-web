using BounceRoute.Api.Data;
using BounceRoute.Api.Endpoints;

namespace BounceRoute.Api.Tests;

public sealed class ClockTests
{
	private static readonly BusinessClock Clock = new(TimeZoneInfo.FindSystemTimeZoneById("America/New_York"), TimeProvider.System);

	[Theory]
	[InlineData("2026-11-13", "2026-11-13")] // Friday
	[InlineData("2026-11-15", "2026-11-13")] // Sunday
	[InlineData("2026-11-16", "2026-11-13")] // Monday pickups still count for the weekend
	[InlineData("2026-11-19", "2026-11-13")] // Thursday
	public void Weekend_starts_on_the_most_recent_friday(string today, string expected) =>
		Assert.Equal(DateOnly.Parse(expected), CrewEndpoints.WeekendStart(DateOnly.Parse(today)));

	[Fact]
	public void Day_range_follows_daylight_saving()
	{
		// Clocks fall back on 2026-11-01, so that day is 25 hours long.
		var (start, end) = Clock.Range(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 1));

		Assert.Equal(TimeSpan.FromHours(25), end - start);
		Assert.Equal(new DateTimeOffset(2026, 11, 1, 4, 0, 0, TimeSpan.Zero), start);
	}
}
