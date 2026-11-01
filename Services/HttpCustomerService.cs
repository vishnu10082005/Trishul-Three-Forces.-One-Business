using System.Net.Http.Headers;
using System.Net.Http.Json;
using CSharpFullStackDemo.Models;

namespace CSharpFullStackDemo.Services;

public class HttpCustomerService : ICustomerService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthService _authService;
    private readonly ILogger<HttpCustomerService> _logger;

    public HttpCustomerService(IHttpClientFactory httpClientFactory, IAuthService authService, ILogger<HttpCustomerService> logger)
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

    public async Task<IReadOnlyList<Customer>> GetCustomersAsync(string? searchTerm = null)
    {
        try
        {
            var uri = string.IsNullOrWhiteSpace(searchTerm)
                ? "api/customers"
                : $"api/customers?search={Uri.EscapeDataString(searchTerm.Trim())}";

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            AddAuthHeader(request);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("API error getting customers: {StatusCode} {Error}", response.StatusCode, errorBody);
                throw new HttpRequestException($"Failed to retrieve customers from backend (HTTP {response.StatusCode}).");
            }

            var list = await response.Content.ReadFromJsonAsync<List<Customer>>();
            return list ?? new List<Customer>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP connection error while retrieving customers.");
            throw new InvalidOperationException("Unable to connect to Trishul backend API. Please ensure the backend is running on http://localhost:5185.", ex);
        }
    }

    public async Task<Customer?> GetCustomerByIdAsync(string id)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/customers/{Uri.EscapeDataString(id)}");
            AddAuthHeader(request);

            var response = await _httpClient.SendAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to retrieve customer (HTTP {response.StatusCode}).");
            }

            return await response.Content.ReadFromJsonAsync<Customer>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while retrieving customer {Id}.", id);
            throw new InvalidOperationException("Unable to connect to Trishul backend API.", ex);
        }
    }

    public async Task<Customer> SaveCustomerAsync(Customer customer)
    {
        try
        {
            HttpResponseMessage response;

            if (string.IsNullOrWhiteSpace(customer.Id))
            {
                // Create (POST)
                using var req = new HttpRequestMessage(HttpMethod.Post, "api/customers")
                {
                    Content = JsonContent.Create(customer)
                };
                AddAuthHeader(req);
                response = await _httpClient.SendAsync(req);
            }
            else
            {
                // Update (PUT)
                using var req = new HttpRequestMessage(HttpMethod.Put, $"api/customers/{Uri.EscapeDataString(customer.Id)}")
                {
                    Content = JsonContent.Create(customer)
                };
                AddAuthHeader(req);
                response = await _httpClient.SendAsync(req);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorJson = await response.Content.ReadAsStringAsync();
                _logger.LogError("API error saving customer: {StatusCode} {Body}", response.StatusCode, errorJson);
                throw new HttpRequestException($"API failed to save customer (HTTP {response.StatusCode}). Details: {errorJson}");
            }

            var saved = await response.Content.ReadFromJsonAsync<Customer>();
            return saved ?? customer;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while saving customer.");
            throw new InvalidOperationException("Unable to connect to Trishul backend API to save customer.", ex);
        }
    }

    public async Task<bool> DeleteCustomerAsync(string id)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, $"api/customers/{Uri.EscapeDataString(id)}");
            AddAuthHeader(req);

            var response = await _httpClient.SendAsync(req);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("API error deleting customer: {StatusCode} {Body}", response.StatusCode, errorBody);
            throw new HttpRequestException($"API failed to delete customer (HTTP {response.StatusCode}).");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while deleting customer {Id}.", id);
            throw new InvalidOperationException("Unable to connect to Trishul backend API to delete customer.", ex);
        }
    }
}
