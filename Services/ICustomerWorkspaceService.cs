using CSharpFullStackDemo.Models;

namespace CSharpFullStackDemo.Services;

public interface ICustomerWorkspaceService
{
    Task<CustomerWorkspaceData?> GetWorkspaceAsync();
}
