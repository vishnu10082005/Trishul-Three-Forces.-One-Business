using CSharpFullStackDemo.Models;

namespace CSharpFullStackDemo.Services;

public interface IAdminAccountService
{
    Task<IReadOnlyList<CustomerAccountItem>> GetCustomerAccountsAsync();
    Task<CreateAccountResult> CreateCustomerAccountAsync(CreateCustomerAccountModel model);
    Task<bool> UpdateCustomerAccountAsync(string id, string fullName, string email, string phone);
    Task<bool> ToggleAccountStatusAsync(string id, bool isActive);
    Task<string> ResetCustomerPasswordAsync(string id, string? newPassword, bool requireChange);
}
