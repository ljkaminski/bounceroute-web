namespace BounceRoute.Api.Data;

// The company works in one time zone. Jobs are stored in UTC, but "Saturday's route"
// means Saturday on the office wall clock, so day boundaries are worked out here.
public sealed class BusinessClock(TimeZoneInfo timeZone, TimeProvider time)
{
	public TimeZoneInfo TimeZone => timeZone;

	public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), timeZone).DateTime);

	public DateTimeOffset StartOf(DateOnly day)
	{
		var local = day.ToDateTime(TimeOnly.MinValue);
		return new DateTimeOffset(local, timeZone.GetUtcOffset(local)).ToUniversalTime();
	}

	// Inclusive of both days: Fri..Mon covers Friday 00:00 up to Tuesday 00:00.
	public (DateTimeOffset Start, DateTimeOffset End) Range(DateOnly from, DateOnly to) =>
		(StartOf(from), StartOf(to.AddDays(1)));
}
