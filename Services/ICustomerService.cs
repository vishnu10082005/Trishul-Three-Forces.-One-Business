using CSharpFullStackDemo.Models;

namespace CSharpFullStackDemo.Services;

public interface ICustomerService
{
    Task<IReadOnlyList<Customer>> GetCustomersAsync(string? searchTerm = null);
    Task<Customer?> GetCustomerByIdAsync(string id);
    Task<Customer> SaveCustomerAsync(Customer customer);
    Task<bool> DeleteCustomerAsync(string id);
}
