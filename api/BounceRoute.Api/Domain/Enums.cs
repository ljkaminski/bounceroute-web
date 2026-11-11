namespace BounceRoute.Api.Domain;

public enum JobType
{
	Setup,
	Pickup
}

public enum Surface
{
	Grass,
	Concrete,
	Gravel,
	Inside
}

public enum JobStatus
{
	Upcoming,
	Done
}

public enum InventoryCategory
{
	BounceHouse,
	Combo,
	WaterSlide,
	ObstacleCourse,
	SlipNSlide,
	Game,
	Equipment,
	Other
}

public enum WaterUse
{
	// Not an inflatable (generators, games).
	None,
	DryOnly,
	WetOnly,
	WetOrDry
}
