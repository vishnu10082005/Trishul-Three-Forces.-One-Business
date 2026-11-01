using CSharpFullStackDemo.Models;

namespace CSharpFullStackDemo.Services;

public class MockCustomerService : ICustomerService
{
    private readonly List<Customer> _customers = new();
    private readonly object _lock = new();

    public MockCustomerService()
    {
        // Initial mock seed data for Sprint 1 demo
        _customers.AddRange(new[]
        {
            new Customer
            {
                Id = "c1",
                Name = "Sri Balaji Enterprises",
                Phone = "+91 98450 12345",
                Email = "contact@balajienterprises.in",
                Address = "Plot 42, Industrial Estate, Phase 2, Bengaluru, Karnataka",
                CreatedAt = DateTime.UtcNow.AddDays(-28)
            },
            new Customer
            {
                Id = "c2",
                Name = "Kiran Tech Solutions",
                Phone = "+91 97123 45678",
                Email = "billing@kirantech.com",
                Address = "Suite 304, Cyber Heights, HITEC City, Hyderabad, Telangana",
                CreatedAt = DateTime.UtcNow.AddDays(-21)
            },
            new Customer
            {
                Id = "c3",
                Name = "Apex Industrial Hardware",
                Phone = "+91 98220 98765",
                Email = "procurement@apexhardware.net",
                Address = "GIDC Estate, Makarpura, Vadodara, Gujarat",
                CreatedAt = DateTime.UtcNow.AddDays(-14)
            },
            new Customer
            {
                Id = "c4",
                Name = "Sharma & Sons Logistics",
                Phone = "+91 94140 56789",
                Email = "accounts@sharmalogistics.com",
                Address = "Warehouse Complex A-12, Transport Nagar, Jaipur, Rajasthan",
                CreatedAt = DateTime.UtcNow.AddDays(-7)
            },
            new Customer
            {
                Id = "c5",
                Name = "Zenith Retail Mart",
                Phone = "+91 99000 11223",
                Email = "finance@zenithretail.co.in",
                Address = "Shop 14, Commercial Plaza, MG Road, Pune, Maharashtra",
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            }
        });
    }

    public Task<IReadOnlyList<Customer>> GetCustomersAsync(string? searchTerm = null)
    {
        lock (_lock)
        {
            IEnumerable<Customer> query = _customers;

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(c =>
                    c.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    c.Phone.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(c.Email) && c.Email.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(c.Address) && c.Address.Contains(term, StringComparison.OrdinalIgnoreCase))
                );
            }

            // Return clones to prevent inadvertent outside mutation before save
            IReadOnlyList<Customer> result = query
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => c.Clone())
                .ToList();

            return Task.FromResult(result);
        }
    }

    public Task<Customer?> GetCustomerByIdAsync(string id)
    {
        lock (_lock)
        {
            var match = _customers.FirstOrDefault(c => c.Id == id);
            return Task.FromResult(match?.Clone());
        }
    }

    public Task<Customer> SaveCustomerAsync(Customer customer)
    {
        lock (_lock)
        {
            var existingIndex = _customers.FindIndex(c => c.Id == customer.Id);
            if (existingIndex >= 0)
            {
                // Update existing
                var updated = customer.Clone();
                _customers[existingIndex] = updated;
                return Task.FromResult(updated.Clone());
            }
            else
            {
                // Add new
                var newCustomer = customer.Clone();
                if (string.IsNullOrWhiteSpace(newCustomer.Id))
                {
                    newCustomer.Id = Guid.NewGuid().ToString("N");
                }
                newCustomer.CreatedAt = DateTime.UtcNow;
                _customers.Insert(0, newCustomer);
                return Task.FromResult(newCustomer.Clone());
            }
        }
    }

    public Task<bool> DeleteCustomerAsync(string id)
    {
        lock (_lock)
        {
            var count = _customers.RemoveAll(c => c.Id == id);
            return Task.FromResult(count > 0);
        }
    }
}
