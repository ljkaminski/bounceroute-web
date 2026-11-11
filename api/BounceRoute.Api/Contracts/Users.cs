using System.ComponentModel.DataAnnotations;
using BounceRoute.Api.Domain;

namespace BounceRoute.Api.Contracts;

public sealed record UserDto(
	string Id,
	string Email,
	string DisplayName,
	UserRole Role,
	int? CrewMemberId,
	string? CrewMemberName,
	bool IsDisabled);

// The owner sets a starting password and gives it to the person, who can change it after signing in.
public sealed record CreateUserRequest(
	[property: Required, EmailAddress, MaxLength(256)] string Email,
	[property: Required, MaxLength(80)] string DisplayName,
	UserRole Role,
	int? CrewMemberId,
	[property: Required] string Password);

public sealed record UpdateUserRequest(
	[property: Required, MaxLength(80)] string DisplayName,
	UserRole Role,
	int? CrewMemberId,
	bool IsDisabled);

public sealed record ResetPasswordRequest([property: Required] string NewPassword);
