using CSharpFullStackDemo.Models;

namespace CSharpFullStackDemo.Services;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(string username, string password, string requestedRole);
    Task LogoutAsync();
    Task<UserSession?> GetCurrentUserSessionAsync();
    Task<AuthResponse> ChangePasswordAsync(string currentPassword, string newPassword);
    string? GetCurrentToken();
    event Action? OnAuthStateChanged;
}
