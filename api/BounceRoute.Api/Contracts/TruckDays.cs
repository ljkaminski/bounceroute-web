namespace BounceRoute.Api.Contracts;

// Which trucks go out on a day. IsDefault means nobody picked, so it's every truck in service.
public sealed record TrucksOutDto(DateOnly Date, IReadOnlyList<int> TruckIds, bool IsDefault);

public sealed record SaveTrucksOutRequest(IReadOnlyList<int> TruckIds);
