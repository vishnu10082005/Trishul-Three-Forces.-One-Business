using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CSharpFullStackDemo.Models;

namespace CSharpFullStackDemo.Services;

public class HttpQuotationService : IQuotationService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthService _authService;
    private readonly ILogger<HttpQuotationService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public HttpQuotationService(
        IHttpClientFactory httpClientFactory,
        IAuthService authService,
        ILogger<HttpQuotationService> logger)
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

    public async Task<List<QuotationDto>> GetQuotationsAsync(string? search = null, string? status = null)
    {
        try
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query.Add($"search={Uri.EscapeDataString(search.Trim())}");
            }
            if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
            {
                query.Add($"status={Uri.EscapeDataString(status.Trim())}");
            }

            var url = "api/quotations" + (query.Count > 0 ? "?" + string.Join("&", query) : "");

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            AddAuthHeader(req);

            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to retrieve quotations: {StatusCode}", resp.StatusCode);
                return new List<QuotationDto>();
            }

            var list = await resp.Content.ReadFromJsonAsync<List<QuotationDto>>(JsonOptions);
            return list ?? new List<QuotationDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving quotations from API.");
            return new List<QuotationDto>();
        }
    }

    public async Task<QuotationDto?> GetQuotationByIdAsync(string id)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"api/quotations/{id}");
            AddAuthHeader(req);

            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            return await resp.Content.ReadFromJsonAsync<QuotationDto>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting quotation {Id}.", id);
            return null;
        }
    }

    public async Task<(bool Success, string? Error, QuotationDto? Quotation)> CreateQuotationAsync(CreateQuotationModel model)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/quotations");
            AddAuthHeader(req);

            var payload = new
            {
                clientMode = model.ClientMode,
                billingClientId = model.BillingClientId,
                newClient = model.NewClient != null ? new
                {
                    name = model.NewClient.Name,
                    phone = model.NewClient.Phone,
                    email = model.NewClient.Email,
                    address = model.NewClient.Address,
                    gstin = model.NewClient.Gstin
                } : null,
                saveToCustomerList = model.SaveToCustomerList,
                quotationDate = model.QuotationDate,
                validUntil = model.ValidUntil,
                status = model.Status,
                taxPercentage = model.TaxPercentage,
                lineItems = model.LineItems.Select(item => new
                {
                    description = item.Description,
                    quantity = item.Quantity,
                    unitPrice = item.UnitPrice
                }),
                notes = model.Notes,
                terms = model.Terms
            };

            req.Content = JsonContent.Create(payload);

            var resp = await _httpClient.SendAsync(req);
            if (resp.IsSuccessStatusCode)
            {
                var created = await resp.Content.ReadFromJsonAsync<QuotationDto>(JsonOptions);
                return (true, null, created);
            }

            var errorMsg = await TryExtractErrorMessage(resp);
            return (false, errorMsg ?? "Failed to create quotation.", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating quotation.");
            return (false, "Could not reach server. Please check your connection.", null);
        }
    }

    public async Task<(bool Success, string? Error, QuotationDto? Quotation)> UpdateQuotationAsync(string id, UpdateQuotationModel model)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Put, $"api/quotations/{id}");
            AddAuthHeader(req);

            var payload = new
            {
                clientMode = model.ClientMode,
                billingClientId = model.BillingClientId,
                newClient = model.NewClient != null ? new
                {
                    name = model.NewClient.Name,
                    phone = model.NewClient.Phone,
                    email = model.NewClient.Email,
                    address = model.NewClient.Address,
                    gstin = model.NewClient.Gstin
                } : null,
                saveToCustomerList = model.SaveToCustomerList,
                quotationDate = model.QuotationDate,
                validUntil = model.ValidUntil,
                status = model.Status,
                taxPercentage = model.TaxPercentage,
                lineItems = model.LineItems.Select(item => new
                {
                    description = item.Description,
                    quantity = item.Quantity,
                    unitPrice = item.UnitPrice
                }),
                notes = model.Notes,
                terms = model.Terms
            };

            req.Content = JsonContent.Create(payload);

            var resp = await _httpClient.SendAsync(req);
            if (resp.IsSuccessStatusCode)
            {
                var updated = await resp.Content.ReadFromJsonAsync<QuotationDto>(JsonOptions);
                return (true, null, updated);
            }

            var errorMsg = await TryExtractErrorMessage(resp);
            return (false, errorMsg ?? "Failed to update quotation.", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating quotation {Id}.", id);
            return (false, "Could not reach server. Please check your connection.", null);
        }
    }

    public async Task<(bool Success, string? Error)> UpdateStatusAsync(string id, string status)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Patch, $"api/quotations/{id}/status");
            AddAuthHeader(req);
            req.Content = JsonContent.Create(new { status });

            var resp = await _httpClient.SendAsync(req);
            if (resp.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var errorMsg = await TryExtractErrorMessage(resp);
            return (false, errorMsg ?? "Failed to update status.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for quotation {Id}.", id);
            return (false, "Could not reach server.");
        }
    }

    public async Task<(bool Success, string? Error)> DeleteQuotationAsync(string id)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, $"api/quotations/{id}");
            AddAuthHeader(req);

            var resp = await _httpClient.SendAsync(req);
            if (resp.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var errorMsg = await TryExtractErrorMessage(resp);
            return (false, errorMsg ?? "Failed to delete quotation.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting quotation {Id}.", id);
            return (false, "Could not reach server.");
        }
    }

    private static async Task<string?> TryExtractErrorMessage(HttpResponseMessage resp)
    {
        try
        {
            var content = await resp.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(content)) return null;

            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("message", out var msgProp))
            {
                return msgProp.GetString();
            }
            if (doc.RootElement.TryGetProperty("title", out var titleProp))
            {
                return titleProp.GetString();
            }
            return content;
        }
        catch
        {
            return null;
        }
    }
}
