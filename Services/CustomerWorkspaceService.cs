using System.Net.Http.Headers;
using System.Net.Http.Json;
using CSharpFullStackDemo.Models;

namespace CSharpFullStackDemo.Services;

public class CustomerWorkspaceService : ICustomerWorkspaceService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthService _authService;
    private readonly ILogger<CustomerWorkspaceService> _logger;

    public CustomerWorkspaceService(IHttpClientFactory httpClientFactory, IAuthService authService, ILogger<CustomerWorkspaceService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("TrishulApi");
        _authService = authService;
        _logger = logger;
    }

    public async Task<CustomerWorkspaceData?> GetWorkspaceAsync()
    {
        try
        {
            var token = _authService.GetCurrentToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            using var req = new HttpRequestMessage(HttpMethod.Get, "api/portal/workspace");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to retrieve workspace data: {StatusCode}", resp.StatusCode);
                return null;
            }

            return await resp.Content.ReadFromJsonAsync<CustomerWorkspaceData>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customer workspace data.");
            return null;
        }
    }
}
