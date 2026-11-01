using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Trishul.Api.Models;

[BsonIgnoreExtraElements]
public class UserAccount
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string Role { get; set; } = "Customer"; // "Admin" or "Customer"
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
    public string? CustomerId { get; set; } // Optional reference to billing Customer entity
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}
