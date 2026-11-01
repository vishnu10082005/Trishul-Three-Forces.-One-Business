using CSharpFullStackDemo.Components;
using CSharpFullStackDemo.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5185";
var baseUri = new Uri(apiBaseUrl.TrimEnd('/') + "/");

// Register named HttpClient for Trishul.Api backend
builder.Services.AddHttpClient("TrishulApi", client =>
{
    client.BaseAddress = baseUri;
    client.Timeout = TimeSpan.FromSeconds(15);
});

// Register Scoped Circuit Session Store & Domain Services
builder.Services.AddScoped<UserSessionStore>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICustomerService, HttpCustomerService>();
builder.Services.AddScoped<IAdminAccountService, AdminAccountService>();
builder.Services.AddScoped<ICustomerWorkspaceService, CustomerWorkspaceService>();
builder.Services.AddScoped<IQuotationService, HttpQuotationService>();

// Dashboard service remains mock for Sprint 3
builder.Services.AddSingleton<IDashboardService, MockDashboardService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
