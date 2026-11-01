using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace Trishul.Api.Models;

public class Customer
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

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

    [BsonElement("customerAccountId")]
    public string? CustomerAccountId { get; set; } // Owner Trishul customer/business account

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonExtraElements]
    [JsonIgnore]
    public BsonDocument? ExtraElements { get; set; }

    public void Normalize()
    {
        if (ExtraElements == null) return;

        if (string.IsNullOrWhiteSpace(Name))
        {
            if (ExtraElements.Contains("Name") && !ExtraElements["Name"].IsBsonNull) Name = ExtraElements["Name"].ToString() ?? string.Empty;
            else if (ExtraElements.Contains("name") && !ExtraElements["name"].IsBsonNull) Name = ExtraElements["name"].ToString() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(Phone))
        {
            if (ExtraElements.Contains("Phone") && !ExtraElements["Phone"].IsBsonNull) Phone = ExtraElements["Phone"].ToString() ?? string.Empty;
            else if (ExtraElements.Contains("phone") && !ExtraElements["phone"].IsBsonNull) Phone = ExtraElements["phone"].ToString() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            if (ExtraElements.Contains("Email") && !ExtraElements["Email"].IsBsonNull) Email = ExtraElements["Email"].ToString();
            else if (ExtraElements.Contains("email") && !ExtraElements["email"].IsBsonNull) Email = ExtraElements["email"].ToString();
        }

        if (string.IsNullOrWhiteSpace(Address))
        {
            if (ExtraElements.Contains("Address") && !ExtraElements["Address"].IsBsonNull) Address = ExtraElements["Address"].ToString();
            else if (ExtraElements.Contains("address") && !ExtraElements["address"].IsBsonNull) Address = ExtraElements["address"].ToString();
        }

        if (string.IsNullOrWhiteSpace(Gstin))
        {
            if (ExtraElements.Contains("Gstin") && !ExtraElements["Gstin"].IsBsonNull) Gstin = ExtraElements["Gstin"].ToString();
            else if (ExtraElements.Contains("gstin") && !ExtraElements["gstin"].IsBsonNull) Gstin = ExtraElements["gstin"].ToString();
        }

        if (string.IsNullOrWhiteSpace(CustomerAccountId))
        {
            if (ExtraElements.Contains("CustomerAccountId") && !ExtraElements["CustomerAccountId"].IsBsonNull) CustomerAccountId = ExtraElements["CustomerAccountId"].ToString();
            else if (ExtraElements.Contains("customerAccountId") && !ExtraElements["customerAccountId"].IsBsonNull) CustomerAccountId = ExtraElements["customerAccountId"].ToString();
        }
    }
}
