using System.ComponentModel.DataAnnotations;
using BounceRoute.Api.Domain;

namespace BounceRoute.Api.Contracts;

public sealed record LoginRequest(
	[property: Required, EmailAddress] string Email,
	[property: Required] string Password);

public sealed record ChangePasswordRequest(
	[property: Required] string CurrentPassword,
	[property: Required] string NewPassword);

public sealed record CurrentUserDto(string Id, string Email, string DisplayName, UserRole Role, int? CrewMemberId);
