namespace Trishul.Api.Models;

public class CreateQuotationRequest
{
    public string ClientMode { get; set; } = "existing"; // "existing" or "new"
    public string? BillingClientId { get; set; }
    public ClientDetailsInput? NewClient { get; set; }
    public bool SaveToCustomerList { get; set; } = false;

    public DateTime QuotationDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime ValidUntil { get; set; } = DateTime.UtcNow.Date.AddDays(30);
    public string? Status { get; set; } = "Draft";
    public decimal TaxPercentage { get; set; } = 0;
    public List<QuotationLineItemInput> LineItems { get; set; } = new();
    public string? Notes { get; set; }
    public string? Terms { get; set; }
}

public class UpdateQuotationRequest
{
    public string ClientMode { get; set; } = "existing"; // "existing" or "new"
    public string? BillingClientId { get; set; }
    public ClientDetailsInput? NewClient { get; set; }
    public bool SaveToCustomerList { get; set; } = false;

    public DateTime QuotationDate { get; set; }
    public DateTime ValidUntil { get; set; }
    public string Status { get; set; } = "Draft";
    public decimal TaxPercentage { get; set; } = 0;
    public List<QuotationLineItemInput> LineItems { get; set; } = new();
    public string? Notes { get; set; }
    public string? Terms { get; set; }
}

public class ClientDetailsInput
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Gstin { get; set; }
}

public class QuotationLineItemInput
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; } = 0;
}

public class UpdateQuotationStatusRequest
{
    public string Status { get; set; } = string.Empty;
}
