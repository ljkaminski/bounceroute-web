namespace BounceRoute.Api.Domain;

public sealed class CrewMember
{
	public int Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public string Initials { get; set; } = string.Empty;

	public string Role { get; set; } = string.Empty;

	// Former crew stay in the table so past jobs and pay totals still resolve.
	public bool IsActive { get; set; } = true;
}
