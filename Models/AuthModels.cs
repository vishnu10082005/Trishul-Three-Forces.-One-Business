namespace CSharpFullStackDemo.Models;

public class UserSession
{
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = "Customer"; // "Admin" or "Customer"
    public bool MustChangePassword { get; set; }
    public string? CustomerId { get; set; }
    public DateTime ExpiresAt { get; set; }

    public bool IsAdmin => string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);
    public bool IsCustomer => string.Equals(Role, "Customer", StringComparison.OrdinalIgnoreCase);
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}

public class AuthResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public UserSession? Session { get; set; }
}

public class CustomerAccountItem
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = "Customer";
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
    public string? CustomerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class CreateCustomerAccountModel
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? InitialPassword { get; set; }
    public bool RequirePasswordChange { get; set; } = true;
    public string? LinkedCustomerId { get; set; }
}

public class CreateAccountResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? GeneratedPassword { get; set; }
    public CustomerAccountItem? Account { get; set; }
}

public class CustomerWorkspaceData
{
    public string AccountId { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; }
    public Customer? BillingProfile { get; set; }
    public string WorkspaceStatus { get; set; } = "Active Customer Portal";
    public DateTime JoinedDate { get; set; }
}
