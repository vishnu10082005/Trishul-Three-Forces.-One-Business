using CSharpFullStackDemo.Models;

namespace CSharpFullStackDemo.Services;

public interface IQuotationService
{
    Task<List<QuotationDto>> GetQuotationsAsync(string? search = null, string? status = null);
    Task<QuotationDto?> GetQuotationByIdAsync(string id);
    Task<(bool Success, string? Error, QuotationDto? Quotation)> CreateQuotationAsync(CreateQuotationModel model);
    Task<(bool Success, string? Error, QuotationDto? Quotation)> UpdateQuotationAsync(string id, UpdateQuotationModel model);
    Task<(bool Success, string? Error)> UpdateStatusAsync(string id, string status);
    Task<(bool Success, string? Error)> DeleteQuotationAsync(string id);
}
