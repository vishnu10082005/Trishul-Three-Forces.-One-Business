namespace Trishul.Api.Models;

public record LoginRequest(string Username, string Password, string RequestedRole);

public record LoginResponse(
    string Token,
    string UserId,
    string FullName,
    string Email,
    string Phone,
    string Role,
    bool MustChangePassword,
    string? CustomerId,
    DateTime ExpiresAt);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record CreateCustomerAccountRequest(
    string FullName,
    string Email,
    string Phone,
    string? InitialPassword,
    bool RequirePasswordChange,
    string? LinkedCustomerId);

public record UpdateCustomerAccountRequest(string FullName, string Email, string Phone);

public record ToggleAccountStatusRequest(bool IsActive);

public record ResetCustomerPasswordRequest(string? NewPassword, bool RequirePasswordChange);

public record CustomerAccountListItem(
    string Id,
    string FullName,
    string Email,
    string Phone,
    string Role,
    bool IsActive,
    bool MustChangePassword,
    string? CustomerId,
    DateTime CreatedAt,
    DateTime? LastLoginAt);
