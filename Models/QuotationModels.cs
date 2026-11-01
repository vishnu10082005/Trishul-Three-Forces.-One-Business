namespace CSharpFullStackDemo.Models;

public class QuotationDto
{
    public string? Id { get; set; }
    public string QuotationNumber { get; set; } = string.Empty;
    public string CustomerAccountId { get; set; } = string.Empty;
    public string? BillingClientId { get; set; } // Nullable for walk-in client
    public ClientSnapshotDto ClientSnapshot { get; set; } = new();
    public DateTime QuotationDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime ValidUntil { get; set; } = DateTime.UtcNow.Date.AddDays(30);
    public string Status { get; set; } = "Draft"; // Draft, Sent, Accepted, Rejected, Expired
    public List<QuotationLineItemDto> LineItems { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal TaxPercentage { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string? Notes { get; set; }
    public string? Terms { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ClientSnapshotDto
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Gstin { get; set; }
    public bool IsWalkIn { get; set; }
}

public class QuotationLineItemDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; } = 0;
    public decimal LineTotal { get; set; } = 0;
}

public class ClientDetailsInputModel
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Gstin { get; set; }
}

public class CreateQuotationModel
{
    public string ClientMode { get; set; } = "existing"; // "existing" or "new"
    public string? BillingClientId { get; set; }
    public ClientDetailsInputModel NewClient { get; set; } = new();
    public bool SaveToCustomerList { get; set; } = false;

    public DateTime QuotationDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime ValidUntil { get; set; } = DateTime.UtcNow.Date.AddDays(30);
    public string Status { get; set; } = "Draft";
    public decimal TaxPercentage { get; set; } = 18;
    public List<QuotationLineItemInputModel> LineItems { get; set; } = new();
    public string? Notes { get; set; }
    public string? Terms { get; set; }
}

public class UpdateQuotationModel
{
    public string ClientMode { get; set; } = "existing"; // "existing" or "new"
    public string? BillingClientId { get; set; }
    public ClientDetailsInputModel NewClient { get; set; } = new();
    public bool SaveToCustomerList { get; set; } = false;

    public DateTime QuotationDate { get; set; }
    public DateTime ValidUntil { get; set; }
    public string Status { get; set; } = "Draft";
    public decimal TaxPercentage { get; set; } = 18;
    public List<QuotationLineItemInputModel> LineItems { get; set; } = new();
    public string? Notes { get; set; }
    public string? Terms { get; set; }
}

public class QuotationLineItemInputModel
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; } = 0;

    public decimal ComputedLineTotal => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
}

public class UpdateQuotationStatusModel
{
    public string Status { get; set; } = string.Empty;
}
