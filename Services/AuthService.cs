using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using CSharpFullStackDemo.Models;

namespace CSharpFullStackDemo.Services;

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly UserSessionStore _sessionStore;
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<AuthService> _logger;

    public event Action? OnAuthStateChanged;

    public AuthService(
        IHttpClientFactory httpClientFactory,
        UserSessionStore sessionStore,
        IJSRuntime jsRuntime,
        ILogger<AuthService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("TrishulApi");
        _sessionStore = sessionStore;
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async Task<UserSession?> GetCurrentUserSessionAsync()
    {
        // 1. Check in-memory scoped session store
        if (_sessionStore.CurrentSession != null)
        {
            if (_sessionStore.CurrentSession.IsExpired)
            {
                await LogoutAsync();
                return null;
            }

            return _sessionStore.CurrentSession;
        }

        // 2. Fallback to browser localStorage (e.g. across page refreshes)
        try
        {
            var storedJson = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "trishul_user_session");
            if (!string.IsNullOrWhiteSpace(storedJson))
            {
                var session = JsonSerializer.Deserialize<UserSession>(storedJson);
                if (session != null && !session.IsExpired)
                {
                    _sessionStore.CurrentSession = session;
                    return session;
                }
                else
                {
                    await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "trishul_user_session");
                }
            }
        }
        catch
        {
            // JavaScript interop may fail during pre-rendering before interactive connection
        }

        return null;
    }

    public string? GetCurrentToken() => _sessionStore.CurrentSession?.Token;

    public async Task<AuthResponse> LoginAsync(string username, string password, string requestedRole)
    {
        try
        {
            var payload = new
            {
                username = username.Trim(),
                password = password,
                requestedRole = requestedRole
            };

            var response = await _httpClient.PostAsJsonAsync("api/auth/login", payload);

            if (!response.IsSuccessStatusCode)
            {
                var errorJson = await response.Content.ReadAsStringAsync();
                string errorMessage = "Invalid login credentials.";

                try
                {
                    using var doc = JsonDocument.Parse(errorJson);
                    if (doc.RootElement.TryGetProperty("message", out var msgProp))
                    {
                        errorMessage = msgProp.GetString() ?? errorMessage;
                    }
                }
                catch
                {
                    // Fallback
                }

                return new AuthResponse
                {
                    Success = false,
                    Message = errorMessage
                };
            }

            var loginData = await response.Content.ReadFromJsonAsync<LoginApiResult>();
            if (loginData == null || string.IsNullOrWhiteSpace(loginData.Token))
            {
                return new AuthResponse { Success = false, Message = "Failed to parse authentication response." };
            }

            var session = new UserSession
            {
                Token = loginData.Token,
                UserId = loginData.UserId,
                FullName = loginData.FullName,
                Email = loginData.Email,
                Phone = loginData.Phone,
                Role = loginData.Role,
                MustChangePassword = loginData.MustChangePassword,
                CustomerId = loginData.CustomerId,
                ExpiresAt = loginData.ExpiresAt
            };

            // Store in scoped circuit memory
            _sessionStore.CurrentSession = session;

            // Persist to browser localStorage for refresh resilience
            try
            {
                var json = JsonSerializer.Serialize(session);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "trishul_user_session", json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not persist session to localStorage.");
            }

            OnAuthStateChanged?.Invoke();

            return new AuthResponse
            {
                Success = true,
                Session = session,
                Message = "Authentication successful."
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "API connection failure during login.");
            return new AuthResponse
            {
                Success = false,
                Message = "Unable to connect to Trishul backend API. Please ensure the backend is running on http://localhost:5185."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login.");
            return new AuthResponse
            {
                Success = false,
                Message = "An unexpected error occurred. Please try again."
            };
        }
    }

    public async Task LogoutAsync()
    {
        var session = _sessionStore.CurrentSession;
        if (session != null && !string.IsNullOrWhiteSpace(session.Token))
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
                await _httpClient.SendAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify backend of logout session revocation.");
            }
        }

        _sessionStore.CurrentSession = null;

        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "trishul_user_session");
        }
        catch { }

        OnAuthStateChanged?.Invoke();
    }

    public async Task<AuthResponse> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var session = _sessionStore.CurrentSession;
        if (session == null || string.IsNullOrWhiteSpace(session.Token))
        {
            return new AuthResponse { Success = false, Message = "Not authenticated." };
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/change-password");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
            request.Content = JsonContent.Create(new
            {
                currentPassword = currentPassword,
                newPassword = newPassword
            });

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorJson = await response.Content.ReadAsStringAsync();
                string errMsg = "Failed to change password.";
                try
                {
                    using var doc = JsonDocument.Parse(errorJson);
                    if (doc.RootElement.TryGetProperty("message", out var p)) errMsg = p.GetString() ?? errMsg;
                }
                catch { }

                return new AuthResponse { Success = false, Message = errMsg };
            }

            session.MustChangePassword = false;

            try
            {
                var json = JsonSerializer.Serialize(session);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "trishul_user_session", json);
            }
            catch { }

            OnAuthStateChanged?.Invoke();

            return new AuthResponse { Success = true, Message = "Password updated successfully." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password.");
            return new AuthResponse { Success = false, Message = "An error occurred while communicating with the server." };
        }
    }

    private record LoginApiResult(
        string Token,
        string UserId,
        string FullName,
        string Email,
        string Phone,
        string Role,
        bool MustChangePassword,
        string? CustomerId,
        DateTime ExpiresAt);
}
