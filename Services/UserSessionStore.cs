using CSharpFullStackDemo.Models;

namespace CSharpFullStackDemo.Services;

public class UserSessionStore
{
    public UserSession? CurrentSession { get; set; }
}
