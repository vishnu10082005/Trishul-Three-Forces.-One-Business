using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Trishul.Api.Models;

[BsonIgnoreExtraElements]
public class Quotation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("quotationNumber")]
    public string QuotationNumber { get; set; } = string.Empty;

    [BsonElement("customerAccountId")]
    public string CustomerAccountId { get; set; } = string.Empty; // Authenticated user owner

    [BsonElement("billingClientId")]
    public string? BillingClientId { get; set; } // Linked Customer entity ID (null for walk-in client)

    [BsonElement("clientSnapshot")]
    public ClientSnapshot ClientSnapshot { get; set; } = new();

    [BsonElement("quotationDate")]
    public DateTime QuotationDate { get; set; } = DateTime.UtcNow.Date;

    [BsonElement("validUntil")]
    public DateTime ValidUntil { get; set; } = DateTime.UtcNow.Date.AddDays(30);

    [BsonElement("status")]
    public string Status { get; set; } = "Draft"; // "Draft", "Sent", "Accepted", "Rejected", "Expired"

    [BsonElement("lineItems")]
    public List<QuotationLineItem> LineItems { get; set; } = new();

    [BsonElement("subtotal")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Subtotal { get; set; }

    [BsonElement("taxPercentage")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal TaxPercentage { get; set; }

    [BsonElement("taxAmount")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal TaxAmount { get; set; }

    [BsonElement("grandTotal")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal GrandTotal { get; set; }

    [BsonElement("notes")]
    public string? Notes { get; set; }

    [BsonElement("terms")]
    public string? Terms { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[BsonIgnoreExtraElements]
public class ClientSnapshot
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("phone")]
    public string Phone { get; set; } = string.Empty;

    [BsonElement("email")]
    public string? Email { get; set; }

    [BsonElement("address")]
    public string? Address { get; set; }

    [BsonElement("gstin")]
    public string? Gstin { get; set; }

    [BsonElement("isWalkIn")]
    public bool IsWalkIn { get; set; }
}

[BsonIgnoreExtraElements]
public class QuotationLineItem
{
    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("quantity")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Quantity { get; set; } = 1;

    [BsonElement("unitPrice")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal UnitPrice { get; set; } = 0;

    [BsonElement("lineTotal")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal LineTotal { get; set; } = 0;
}
