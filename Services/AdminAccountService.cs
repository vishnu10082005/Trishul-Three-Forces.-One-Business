using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CSharpFullStackDemo.Models;

namespace CSharpFullStackDemo.Services;

public class AdminAccountService : IAdminAccountService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthService _authService;
    private readonly ILogger<AdminAccountService> _logger;

    public AdminAccountService(IHttpClientFactory httpClientFactory, IAuthService authService, ILogger<AdminAccountService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("TrishulApi");
        _authService = authService;
        _logger = logger;
    }

    private void AddAuthHeader(HttpRequestMessage request)
    {
        var token = _authService.GetCurrentToken();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<IReadOnlyList<CustomerAccountItem>> GetCustomerAccountsAsync()
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "api/admin/customer-accounts");
            AddAuthHeader(req);

            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to retrieve customer accounts: {StatusCode}", resp.StatusCode);
                return Array.Empty<CustomerAccountItem>();
            }

            var list = await resp.Content.ReadFromJsonAsync<List<CustomerAccountItem>>();
            return list ?? (IReadOnlyList<CustomerAccountItem>)Array.Empty<CustomerAccountItem>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer accounts.");
            return Array.Empty<CustomerAccountItem>();
        }
    }

    public async Task<CreateAccountResult> CreateCustomerAccountAsync(CreateCustomerAccountModel model)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/admin/customer-accounts");
            AddAuthHeader(req);
            req.Content = JsonContent.Create(new
            {
                fullName = model.FullName,
                email = model.Email,
                phone = model.Phone,
                initialPassword = model.InitialPassword,
                requirePasswordChange = model.RequirePasswordChange,
                linkedCustomerId = model.LinkedCustomerId
            });

            var resp = await _httpClient.SendAsync(req);
            var content = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                string msg = "Failed to create customer account.";
                try
                {
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("message", out var p)) msg = p.GetString() ?? msg;
                }
                catch { }

                return new CreateAccountResult { Success = false, Message = msg };
            }

            using var successDoc = JsonDocument.Parse(content);
            string? genPassword = null;
            if (successDoc.RootElement.TryGetProperty("generatedPassword", out var genProp))
            {
                genPassword = genProp.GetString();
            }

            return new CreateAccountResult
            {
                Success = true,
                Message = "Customer account created successfully.",
                GeneratedPassword = genPassword
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating customer account.");
            return new CreateAccountResult { Success = false, Message = ex.Message };
        }
    }

    public async Task<bool> UpdateCustomerAccountAsync(string id, string fullName, string email, string phone)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Put, $"api/admin/customer-accounts/{Uri.EscapeDataString(id)}");
            AddAuthHeader(req);
            req.Content = JsonContent.Create(new
            {
                fullName = fullName,
                email = email,
                phone = phone
            });

            var resp = await _httpClient.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating customer account {Id}.", id);
            return false;
        }
    }

    public async Task<bool> ToggleAccountStatusAsync(string id, bool isActive)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Patch, $"api/admin/customer-accounts/{Uri.EscapeDataString(id)}/status");
            AddAuthHeader(req);
            req.Content = JsonContent.Create(new { isActive = isActive });

            var resp = await _httpClient.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling customer account status for {Id}.", id);
            return false;
        }
    }

    public async Task<string> ResetCustomerPasswordAsync(string id, string? newPassword, bool requireChange)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"api/admin/customer-accounts/{Uri.EscapeDataString(id)}/reset-password");
            AddAuthHeader(req);
            req.Content = JsonContent.Create(new
            {
                newPassword = newPassword,
                requirePasswordChange = requireChange
            });

            var resp = await _httpClient.SendAsync(req);
            var content = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Password reset failed: {content}");
            }

            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("generatedPassword", out var p))
            {
                return p.GetString() ?? string.Empty;
            }

            return newPassword ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for account {Id}.", id);
            throw;
        }
    }
}
