using System.ComponentModel.DataAnnotations;

namespace CSharpFullStackDemo.Models;

public class Customer
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Customer name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [RegularExpression(@"^[0-9+\-\s()]{7,20}$", ErrorMessage = "Please enter a valid phone number (at least 7 digits).")]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string? Email { get; set; }

    [StringLength(250, ErrorMessage = "Address cannot exceed 250 characters.")]
    public string? Address { get; set; }

    [StringLength(20, ErrorMessage = "GSTIN cannot exceed 20 characters.")]
    public string? Gstin { get; set; }

    public string? CustomerAccountId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Helper clone method for edit operations
    public Customer Clone()
    {
        return new Customer
        {
            Id = this.Id,
            Name = this.Name,
            Phone = this.Phone,
            Email = this.Email,
            Address = this.Address,
            Gstin = this.Gstin,
            CustomerAccountId = this.CustomerAccountId,
            CreatedAt = this.CreatedAt
        };
    }
}
