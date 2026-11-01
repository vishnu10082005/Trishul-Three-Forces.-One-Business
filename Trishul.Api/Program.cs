using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using Trishul.Api.Models;
using Trishul.Api.Security;

var builder = WebApplication.CreateBuilder(args);

// Read MongoDB settings from User Secrets / Configuration
var connectionString = builder.Configuration["MongoDb:ConnectionString"];
var databaseName = builder.Configuration["MongoDb:DatabaseName"];

if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(databaseName))
{
    throw new InvalidOperationException("MongoDB configuration is missing.");
}

// Register MongoDB client
builder.Services.AddSingleton<IMongoClient>(new MongoClient(connectionString));

// Register database
builder.Services.AddSingleton<IMongoDatabase>(services =>
{
    var client = services.GetRequiredService<IMongoClient>();
    return client.GetDatabase(databaseName);
});

// Configure CORS for web clients
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

// Secure Admin account provisioning and Sprint 4 cleanup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
    var userCol = db.GetCollection<UserAccount>("UserAccounts");
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var adminEmail = builder.Configuration["Admin:InitialEmail"] 
                     ?? Environment.GetEnvironmentVariable("TRISHUL_ADMIN_EMAIL") 
                     ?? "admin@trishul.com";
    var adminPassword = builder.Configuration["Admin:InitialPassword"] 
                        ?? Environment.GetEnvironmentVariable("TRISHUL_ADMIN_PASSWORD");

    var existingAdmin = await userCol.Find(u => u.Role == "Admin").FirstOrDefaultAsync();
    if (existingAdmin == null)
    {
        if (!string.IsNullOrWhiteSpace(adminPassword))
        {
            var (hash, salt) = PasswordSecurity.HashPassword(adminPassword);
            var adminUser = new UserAccount
            {
                FullName = "Vishnu Preetham (Admin)",
                Email = adminEmail,
                Phone = "+91 98450 00001",
                PasswordHash = hash,
                PasswordSalt = salt,
                Role = "Admin",
                IsActive = true,
                MustChangePassword = false,
                CreatedAt = DateTime.UtcNow
            };
            await userCol.InsertOneAsync(adminUser);
            logger.LogInformation("Initial Admin account provisioned from secure configuration.");
        }
        else
        {
            logger.LogWarning("Admin account does not exist and 'Admin:InitialPassword' is not configured. Configure it in User Secrets or environment variable TRISHUL_ADMIN_PASSWORD.");
        }
    }
    else if (builder.Configuration.GetValue<bool>("Admin:ResetPassword") && !string.IsNullOrWhiteSpace(adminPassword))
    {
        var (hash, salt) = PasswordSecurity.HashPassword(adminPassword);
        var update = Builders<UserAccount>.Update
            .Set(u => u.PasswordHash, hash)
            .Set(u => u.PasswordSalt, salt);
        await userCol.UpdateOneAsync(u => u.Id == existingAdmin.Id, update);
        logger.LogInformation("Admin password updated from secure configuration.");
    }

    // Sprint 4 cleanup: Remove demo/test accounts from UserAccounts
    await userCol.DeleteManyAsync(u => u.Email == "inactive@test.com" || u.Email == "zenith.inactive@test.com");

    // Normalize Customers collection BSON field names for 100% case-compatibility across sprints
    var rawCustomersCol = db.GetCollection<BsonDocument>("Customers");
    var existingCustDocs = await rawCustomersCol.Find(new BsonDocument()).ToListAsync();
    foreach (var doc in existingCustDocs)
    {
        var update = Builders<BsonDocument>.Update;
        var updates = new List<UpdateDefinition<BsonDocument>>();

        if (doc.Contains("Name") && !doc.Contains("name"))
            updates.Add(update.Set("name", doc["Name"]));
        if (doc.Contains("name") && !doc.Contains("Name"))
            updates.Add(update.Set("Name", doc["name"]));

        if (doc.Contains("Phone") && !doc.Contains("phone"))
            updates.Add(update.Set("phone", doc["Phone"]));
        if (doc.Contains("phone") && !doc.Contains("Phone"))
            updates.Add(update.Set("Phone", doc["phone"]));

        if (doc.Contains("Email") && !doc.Contains("email"))
            updates.Add(update.Set("email", doc["Email"]));
        if (doc.Contains("email") && !doc.Contains("Email"))
            updates.Add(update.Set("Email", doc["email"]));

        if (doc.Contains("Address") && !doc.Contains("address"))
            updates.Add(update.Set("address", doc["Address"]));
        if (doc.Contains("address") && !doc.Contains("Address"))
            updates.Add(update.Set("Address", doc["address"]));

        if (doc.Contains("CreatedAt") && !doc.Contains("createdAt"))
            updates.Add(update.Set("createdAt", doc["CreatedAt"]));
        if (doc.Contains("createdAt") && !doc.Contains("CreatedAt"))
            updates.Add(update.Set("CreatedAt", doc["createdAt"]));

        if (updates.Count > 0)
        {
            await rawCustomersCol.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]),
                update.Combine(updates)
            );
        }
    }
}

// Root API Endpoint
app.MapGet("/", () => "Trishul API is running!");

// Existing MongoDB Health Endpoint
app.MapGet("/api/health/mongodb", async (IMongoDatabase database, ILogger<Program> logger) =>
{
    try
    {
        await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

        return Results.Ok(new
        {
            status = "Connected",
            database = database.DatabaseNamespace.DatabaseName,
            message = "MongoDB connection successful!"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "MongoDB health check failed.");
        return Results.Problem("Unable to connect to MongoDB. Check your connection settings and Atlas network access.");
    }
});

// ==========================================
// Authentication Endpoints (Sprint 3)
// ==========================================

// POST /api/auth/login
app.MapPost("/api/auth/login", async (IMongoDatabase database, LoginRequest request, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { message = "Username (email or phone) and password are required." });
    }

    try
    {
        var userCol = database.GetCollection<UserAccount>("UserAccounts");
        var username = request.Username.Trim();

        // Search by email (case-insensitive) or phone
        var user = await userCol.Find(u =>
            u.Email.ToLower() == username.ToLower() ||
            u.Phone == username
        ).FirstOrDefaultAsync();

        if (user == null || !PasswordSecurity.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            return Results.Json(new { message = "Invalid email/mobile or password." }, statusCode: StatusCodes.Status401Unauthorized);
        }

        // Account status check
        if (!user.IsActive)
        {
            return Results.Json(new { message = "This account has been deactivated. Please contact your administrator." }, statusCode: StatusCodes.Status403Forbidden);
        }

        // Role enforcement: toggle in UI only specifies intention, backend enforces match
        if (!string.IsNullOrWhiteSpace(request.RequestedRole) &&
            !string.Equals(user.Role, request.RequestedRole, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(new
            {
                message = $"Role mismatch: This account is registered as '{user.Role}'. Please select the matching login view."
            }, statusCode: StatusCodes.Status403Forbidden);
        }

        // Create secure session token
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresAt = DateTime.UtcNow.AddHours(24);

        var session = new AuthSession
        {
            Token = token,
            UserId = user.Id!,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role,
            MustChangePassword = user.MustChangePassword,
            CustomerId = user.CustomerId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            IsRevoked = false
        };

        var sessionsCol = database.GetCollection<AuthSession>("AuthSessions");
        await sessionsCol.InsertOneAsync(session);

        // Update last login timestamp
        await userCol.UpdateOneAsync(
            u => u.Id == user.Id,
            Builders<UserAccount>.Update.Set(u => u.LastLoginAt, DateTime.UtcNow));

        var response = new LoginResponse(
            token,
            user.Id!,
            user.FullName,
            user.Email,
            user.Phone,
            user.Role,
            user.MustChangePassword,
            user.CustomerId,
            expiresAt);

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing login for {Username}.", request.Username);
        return Results.Problem("An unexpected error occurred during login. Please try again.");
    }
});

// POST /api/auth/logout
app.MapPost("/api/auth/logout", async (HttpContext context, IMongoDatabase database) =>
{
    var session = await GetAuthSessionAsync(context, database);
    if (session != null)
    {
        var sessionsCol = database.GetCollection<AuthSession>("AuthSessions");
        await sessionsCol.UpdateOneAsync(
            s => s.Token == session.Token,
            Builders<AuthSession>.Update.Set(s => s.IsRevoked, true));
    }

    return Results.Ok(new { message = "Logged out successfully." });
});

// GET /api/auth/me
app.MapGet("/api/auth/me", async (HttpContext context, IMongoDatabase database) =>
{
    var session = await GetAuthSessionAsync(context, database);
    if (session == null)
    {
        return Results.Json(new { message = "Unauthorized or session expired." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    return Results.Ok(new
    {
        userId = session.UserId,
        fullName = session.FullName,
        email = session.Email,
        phone = session.Phone,
        role = session.Role,
        mustChangePassword = session.MustChangePassword,
        customerId = session.CustomerId
    });
});

// POST /api/auth/change-password
app.MapPost("/api/auth/change-password", async (HttpContext context, IMongoDatabase database, ChangePasswordRequest request) =>
{
    var session = await GetAuthSessionAsync(context, database);
    if (session == null)
    {
        return Results.Json(new { message = "Unauthorized." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
    {
        return Results.BadRequest(new { message = "New password must be at least 6 characters long." });
    }

    var userCol = database.GetCollection<UserAccount>("UserAccounts");
    var user = await userCol.Find(u => u.Id == session.UserId).FirstOrDefaultAsync();
    if (user == null)
    {
        return Results.NotFound(new { message = "User account not found." });
    }

    // If user already had a personal password (not first time), verify current password
    if (!user.MustChangePassword)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            !PasswordSecurity.VerifyPassword(request.CurrentPassword, user.PasswordHash, user.PasswordSalt))
        {
            return Results.BadRequest(new { message = "Current password is incorrect." });
        }
    }

    var (newHash, newSalt) = PasswordSecurity.HashPassword(request.NewPassword);
    await userCol.UpdateOneAsync(
        u => u.Id == user.Id,
        Builders<UserAccount>.Update
            .Set(u => u.PasswordHash, newHash)
            .Set(u => u.PasswordSalt, newSalt)
            .Set(u => u.MustChangePassword, false));

    // Update session
    var sessionsCol = database.GetCollection<AuthSession>("AuthSessions");
    await sessionsCol.UpdateOneAsync(
        s => s.Token == session.Token,
        Builders<AuthSession>.Update.Set(s => s.MustChangePassword, false));

    return Results.Ok(new { message = "Password updated successfully." });
});

// ==========================================
// Admin Customer Account Management (Sprint 3)
// ==========================================

// GET /api/admin/customer-accounts
app.MapGet("/api/admin/customer-accounts", async (HttpContext context, IMongoDatabase database) =>
{
    var session = await GetAuthSessionAsync(context, database);
    if (session == null || session.Role != "Admin")
    {
        return Results.Json(new { message = "Forbidden. Admin access required." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var userCol = database.GetCollection<UserAccount>("UserAccounts");
    var accounts = await userCol.Find(u => u.Role == "Customer")
        .SortByDescending(u => u.CreatedAt)
        .ToListAsync();

    var list = accounts.Select(u => new CustomerAccountListItem(
        u.Id!,
        u.FullName,
        u.Email,
        u.Phone,
        u.Role,
        u.IsActive,
        u.MustChangePassword,
        u.CustomerId,
        u.CreatedAt,
        u.LastLoginAt
    )).ToList();

    return Results.Ok(list);
});

// POST /api/admin/customer-accounts
app.MapPost("/api/admin/customer-accounts", async (HttpContext context, IMongoDatabase database, CreateCustomerAccountRequest request) =>
{
    var session = await GetAuthSessionAsync(context, database);
    if (session == null || session.Role != "Admin")
    {
        return Results.Json(new { message = "Forbidden. Admin access required." }, statusCode: StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length < 2)
    {
        return Results.BadRequest(new { message = "Customer / Business Name is required." });
    }

    if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.Phone))
    {
        return Results.BadRequest(new { message = "Either Email or Phone number is required for customer login." });
    }

    var userCol = database.GetCollection<UserAccount>("UserAccounts");

    // Check duplicate email
    if (!string.IsNullOrWhiteSpace(request.Email))
    {
        var existingEmail = await userCol.Find(u => u.Email.ToLower() == request.Email.Trim().ToLower()).FirstOrDefaultAsync();
        if (existingEmail != null)
        {
            return Results.Conflict(new { message = $"An account with email '{request.Email}' already exists." });
        }
    }

    // Check duplicate phone
    if (!string.IsNullOrWhiteSpace(request.Phone))
    {
        var existingPhone = await userCol.Find(u => u.Phone == request.Phone.Trim()).FirstOrDefaultAsync();
        if (existingPhone != null)
        {
            return Results.Conflict(new { message = $"An account with phone number '{request.Phone}' already exists." });
        }
    }

    // Generate or use initial password
    var rawPassword = !string.IsNullOrWhiteSpace(request.InitialPassword)
        ? request.InitialPassword.Trim()
        : PasswordSecurity.GenerateSecureRandomPassword(10);

    var (hash, salt) = PasswordSecurity.HashPassword(rawPassword);

    var newAccount = new UserAccount
    {
        FullName = request.FullName.Trim(),
        Email = request.Email?.Trim() ?? string.Empty,
        Phone = request.Phone?.Trim() ?? string.Empty,
        PasswordHash = hash,
        PasswordSalt = salt,
        Role = "Customer",
        IsActive = true,
        MustChangePassword = request.RequirePasswordChange,
        CustomerId = request.LinkedCustomerId,
        CreatedAt = DateTime.UtcNow
    };

    await userCol.InsertOneAsync(newAccount);

    return Results.Created($"/api/admin/customer-accounts/{newAccount.Id}", new
    {
        id = newAccount.Id,
        fullName = newAccount.FullName,
        email = newAccount.Email,
        phone = newAccount.Phone,
        role = newAccount.Role,
        isActive = newAccount.IsActive,
        mustChangePassword = newAccount.MustChangePassword,
        generatedPassword = rawPassword, // Returned once so admin can securely provide to client
        message = "Customer account created successfully."
    });
});

// PUT /api/admin/customer-accounts/{id}
app.MapPut("/api/admin/customer-accounts/{id}", async (HttpContext context, IMongoDatabase database, string id, UpdateCustomerAccountRequest request) =>
{
    var session = await GetAuthSessionAsync(context, database);
    if (session == null || session.Role != "Admin")
    {
        return Results.Json(new { message = "Forbidden. Admin access required." }, statusCode: StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _))
    {
        return Results.BadRequest(new { message = "Invalid account ID format." });
    }

    var userCol = database.GetCollection<UserAccount>("UserAccounts");
    var user = await userCol.Find(u => u.Id == id).FirstOrDefaultAsync();
    if (user == null)
    {
        return Results.NotFound(new { message = "Customer account not found." });
    }

    // Check duplicate email
    if (!string.IsNullOrWhiteSpace(request.Email))
    {
        var dup = await userCol.Find(u => u.Id != id && u.Email.ToLower() == request.Email.Trim().ToLower()).FirstOrDefaultAsync();
        if (dup != null)
        {
            return Results.Conflict(new { message = $"An account with email '{request.Email}' already exists." });
        }
    }

    // Check duplicate phone
    if (!string.IsNullOrWhiteSpace(request.Phone))
    {
        var dup = await userCol.Find(u => u.Id != id && u.Phone == request.Phone.Trim()).FirstOrDefaultAsync();
        if (dup != null)
        {
            return Results.Conflict(new { message = $"An account with phone '{request.Phone}' already exists." });
        }
    }

    await userCol.UpdateOneAsync(
        u => u.Id == id,
        Builders<UserAccount>.Update
            .Set(u => u.FullName, request.FullName.Trim())
            .Set(u => u.Email, request.Email.Trim())
            .Set(u => u.Phone, request.Phone.Trim()));

    return Results.Ok(new { message = "Account details updated successfully." });
});

// PATCH /api/admin/customer-accounts/{id}/status
app.MapPatch("/api/admin/customer-accounts/{id}/status", async (HttpContext context, IMongoDatabase database, string id, ToggleAccountStatusRequest request) =>
{
    var session = await GetAuthSessionAsync(context, database);
    if (session == null || session.Role != "Admin")
    {
        return Results.Json(new { message = "Forbidden. Admin access required." }, statusCode: StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _))
    {
        return Results.BadRequest(new { message = "Invalid account ID format." });
    }

    var userCol = database.GetCollection<UserAccount>("UserAccounts");
    var result = await userCol.UpdateOneAsync(
        u => u.Id == id,
        Builders<UserAccount>.Update.Set(u => u.IsActive, request.IsActive));

    if (result.MatchedCount == 0)
    {
        return Results.NotFound(new { message = "Customer account not found." });
    }

    // If deactivated, revoke any active sessions
    if (!request.IsActive)
    {
        var sessionsCol = database.GetCollection<AuthSession>("AuthSessions");
        await sessionsCol.UpdateManyAsync(
            s => s.UserId == id,
            Builders<AuthSession>.Update.Set(s => s.IsRevoked, true));
    }

    return Results.Ok(new
    {
        message = request.IsActive ? "Customer account activated." : "Customer account deactivated."
    });
});

// POST /api/admin/customer-accounts/{id}/reset-password
app.MapPost("/api/admin/customer-accounts/{id}/reset-password", async (HttpContext context, IMongoDatabase database, string id, ResetCustomerPasswordRequest request) =>
{
    var session = await GetAuthSessionAsync(context, database);
    if (session == null || session.Role != "Admin")
    {
        return Results.Json(new { message = "Forbidden. Admin access required." }, statusCode: StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _))
    {
        return Results.BadRequest(new { message = "Invalid account ID format." });
    }

    var userCol = database.GetCollection<UserAccount>("UserAccounts");
    var user = await userCol.Find(u => u.Id == id).FirstOrDefaultAsync();
    if (user == null)
    {
        return Results.NotFound(new { message = "Customer account not found." });
    }

    var newRawPassword = !string.IsNullOrWhiteSpace(request.NewPassword)
        ? request.NewPassword.Trim()
        : PasswordSecurity.GenerateSecureRandomPassword(10);

    var (hash, salt) = PasswordSecurity.HashPassword(newRawPassword);

    await userCol.UpdateOneAsync(
        u => u.Id == id,
        Builders<UserAccount>.Update
            .Set(u => u.PasswordHash, hash)
            .Set(u => u.PasswordSalt, salt)
            .Set(u => u.MustChangePassword, request.RequirePasswordChange));

    // Revoke previous sessions
    var sessionsCol = database.GetCollection<AuthSession>("AuthSessions");
    await sessionsCol.UpdateManyAsync(
        s => s.UserId == id,
        Builders<AuthSession>.Update.Set(s => s.IsRevoked, true));

    return Results.Ok(new
    {
        generatedPassword = newRawPassword,
        message = "Password reset successfully. The customer will be prompted to change password if required."
    });
});

// ==========================================
// Customer Portal Workspace Endpoints (Sprint 3)
// ==========================================

// GET /api/portal/workspace - Protected Customer Workspace
app.MapGet("/api/portal/workspace", async (HttpContext context, IMongoDatabase database) =>
{
    var session = await GetAuthSessionAsync(context, database);
    if (session == null || session.Role != "Customer")
    {
        return Results.Json(new { message = "Forbidden. Customer workspace access required." }, statusCode: StatusCodes.Status403Forbidden);
    }

    // Customer only accesses their own account and linked profile
    var userCol = database.GetCollection<UserAccount>("UserAccounts");
    var account = await userCol.Find(u => u.Id == session.UserId).FirstOrDefaultAsync();

    if (account == null)
    {
        return Results.NotFound(new { message = "Customer account profile not found." });
    }

    // If linked to a billing customer profile, retrieve it
    Customer? linkedCustomer = null;
    if (!string.IsNullOrWhiteSpace(account.CustomerId) && ObjectId.TryParse(account.CustomerId, out _))
    {
        var custCol = database.GetCollection<Customer>("Customers");
        linkedCustomer = await custCol.Find(c => c.Id == account.CustomerId).FirstOrDefaultAsync();
    }

    return Results.Ok(new
    {
        accountId = account.Id,
        businessName = account.FullName,
        email = account.Email,
        phone = account.Phone,
        mustChangePassword = account.MustChangePassword,
        billingProfile = linkedCustomer,
        workspaceStatus = "Active Customer Portal",
        joinedDate = account.CreatedAt
    });
});

// ==========================================
// Existing Customer CRUD Endpoints (Sprint 2 - Preserved)
// ==========================================

// 1. GET /api/customers - List customers (scoped by authenticated Customer account or all for Admin)
app.MapGet("/api/customers", async (HttpContext context, IMongoDatabase database, string? search, ILogger<Program> logger) =>
{
    try
    {
        var session = await GetAuthSessionAsync(context, database);
        var collection = database.GetCollection<Customer>("Customers");
        var filterBuilder = Builders<Customer>.Filter;
        var filter = filterBuilder.Empty;

        // Customer isolation: Customer accounts only see their own saved clients + unassigned/legacy clients
        if (session != null && session.Role == "Customer")
        {
            filter &= filterBuilder.Or(
                filterBuilder.Eq(c => c.CustomerAccountId, session.UserId),
                filterBuilder.Eq(c => c.CustomerAccountId, null)
            );
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var regex = new BsonRegularExpression(term, "i");
            filter &= filterBuilder.Or(
                filterBuilder.Regex(c => c.Name, regex),
                filterBuilder.Regex(c => c.Phone, regex),
                filterBuilder.Regex(c => c.Email, regex),
                filterBuilder.Regex(c => c.Address, regex),
                filterBuilder.Regex(c => c.Gstin, regex)
            );
        }

        var customers = await collection.Find(filter)
            .SortByDescending(c => c.CreatedAt)
            .ToListAsync();

        customers.ForEach(c => c.Normalize());

        return Results.Ok(customers);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving customers.");
        return Results.Problem("An unexpected error occurred while retrieving customers.");
    }
});

// 2. GET /api/customers/{id} - Get single customer by ID
app.MapGet("/api/customers/{id}", async (HttpContext context, IMongoDatabase database, string id, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _))
    {
        return Results.BadRequest(new { message = "Invalid customer ID format. Must be a 24-character hexadecimal string." });
    }

    try
    {
        var session = await GetAuthSessionAsync(context, database);
        var collection = database.GetCollection<Customer>("Customers");
        var customer = await collection.Find(c => c.Id == id).FirstOrDefaultAsync();

        if (customer == null)
        {
            return Results.NotFound(new { message = $"Customer with ID '{id}' was not found." });
        }

        // Customer isolation check
        if (session != null && session.Role == "Customer" && customer.CustomerAccountId != null && customer.CustomerAccountId != session.UserId)
        {
            return Results.Json(new { message = "Forbidden. You do not have access to this customer." }, statusCode: StatusCodes.Status403Forbidden);
        }

        customer.Normalize();
        return Results.Ok(customer);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving customer with ID {CustomerId}.", id);
        return Results.Problem("An unexpected error occurred while retrieving the customer.");
    }
});

// 3. POST /api/customers - Create new customer
app.MapPost("/api/customers", async (HttpContext context, IMongoDatabase database, Customer customer, ILogger<Program> logger) =>
{
    if (customer == null)
    {
        return Results.BadRequest(new { message = "Customer payload cannot be empty." });
    }

    var (isValid, errors) = ValidateCustomer(customer);
    if (!isValid)
    {
        return Results.BadRequest(new { message = "Validation failed.", errors });
    }

    try
    {
        var session = await GetAuthSessionAsync(context, database);
        var collection = database.GetCollection<Customer>("Customers");

        customer.Id = null;
        customer.CreatedAt = DateTime.UtcNow;
        customer.Name = customer.Name.Trim();
        customer.Phone = customer.Phone.Trim();
        customer.Email = string.IsNullOrWhiteSpace(customer.Email) ? null : customer.Email.Trim();
        customer.Address = string.IsNullOrWhiteSpace(customer.Address) ? null : customer.Address.Trim();
        customer.Gstin = string.IsNullOrWhiteSpace(customer.Gstin) ? null : customer.Gstin.Trim();

        // Tag with authenticated customer account if session is Customer
        if (session != null && session.Role == "Customer")
        {
            customer.CustomerAccountId = session.UserId;
        }

        await collection.InsertOneAsync(customer);

        return Results.Created($"/api/customers/{customer.Id}", customer);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error creating customer.");
        return Results.Problem("An unexpected error occurred while creating the customer.");
    }
});

// 4. PUT /api/customers/{id} - Update existing customer
app.MapPut("/api/customers/{id}", async (HttpContext context, IMongoDatabase database, string id, Customer updatedCustomer, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _))
    {
        return Results.BadRequest(new { message = "Invalid customer ID format. Must be a 24-character hexadecimal string." });
    }

    if (updatedCustomer == null)
    {
        return Results.BadRequest(new { message = "Customer payload cannot be empty." });
    }

    var (isValid, errors) = ValidateCustomer(updatedCustomer);
    if (!isValid)
    {
        return Results.BadRequest(new { message = "Validation failed.", errors });
    }

    try
    {
        var session = await GetAuthSessionAsync(context, database);
        var collection = database.GetCollection<Customer>("Customers");
        var existing = await collection.Find(c => c.Id == id).FirstOrDefaultAsync();

        if (existing == null)
        {
            return Results.NotFound(new { message = $"Customer with ID '{id}' was not found." });
        }

        // Customer isolation check
        if (session != null && session.Role == "Customer" && existing.CustomerAccountId != null && existing.CustomerAccountId != session.UserId)
        {
            return Results.Json(new { message = "Forbidden. You do not have access to this customer." }, statusCode: StatusCodes.Status403Forbidden);
        }

        updatedCustomer.Id = id;
        updatedCustomer.CreatedAt = existing.CreatedAt;
        updatedCustomer.CustomerAccountId = existing.CustomerAccountId ?? (session?.Role == "Customer" ? session.UserId : null);
        updatedCustomer.Name = updatedCustomer.Name.Trim();
        updatedCustomer.Phone = updatedCustomer.Phone.Trim();
        updatedCustomer.Email = string.IsNullOrWhiteSpace(updatedCustomer.Email) ? null : updatedCustomer.Email.Trim();
        updatedCustomer.Address = string.IsNullOrWhiteSpace(updatedCustomer.Address) ? null : updatedCustomer.Address.Trim();
        updatedCustomer.Gstin = string.IsNullOrWhiteSpace(updatedCustomer.Gstin) ? null : updatedCustomer.Gstin.Trim();

        var replaceResult = await collection.ReplaceOneAsync(c => c.Id == id, updatedCustomer);

        if (replaceResult.MatchedCount == 0)
        {
            return Results.NotFound(new { message = $"Customer with ID '{id}' was not found." });
        }

        return Results.Ok(updatedCustomer);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error updating customer with ID {CustomerId}.", id);
        return Results.Problem("An unexpected error occurred while updating the customer.");
    }
});

// 5. DELETE /api/customers/{id} - Delete customer
app.MapDelete("/api/customers/{id}", async (HttpContext context, IMongoDatabase database, string id, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _))
    {
        return Results.BadRequest(new { message = "Invalid customer ID format. Must be a 24-character hexadecimal string." });
    }

    try
    {
        var session = await GetAuthSessionAsync(context, database);
        var collection = database.GetCollection<Customer>("Customers");
        var existing = await collection.Find(c => c.Id == id).FirstOrDefaultAsync();

        if (existing == null)
        {
            return Results.NotFound(new { message = $"Customer with ID '{id}' was not found." });
        }

        // Customer isolation check
        if (session != null && session.Role == "Customer" && existing.CustomerAccountId != null && existing.CustomerAccountId != session.UserId)
        {
            return Results.Json(new { message = "Forbidden. You do not have access to this customer." }, statusCode: StatusCodes.Status403Forbidden);
        }

        var deleteResult = await collection.DeleteOneAsync(c => c.Id == id);

        if (deleteResult.DeletedCount == 0)
        {
            return Results.NotFound(new { message = $"Customer with ID '{id}' was not found." });
        }

        return Results.Ok(new { message = $"Customer with ID '{id}' was deleted successfully." });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error deleting customer with ID {CustomerId}.", id);
        return Results.Problem("An unexpected error occurred while deleting the customer.");
    }
});

// ==========================================
// Quotations Endpoints (Sprint 4)
// ==========================================

// GET /api/quotations - List quotations with search and status filter
app.MapGet("/api/quotations", async (
    HttpContext context,
    IMongoDatabase database,
    ILogger<Program> logger,
    string? search,
    string? status) =>
{
    var session = await GetAuthSessionAsync(context, database);
    if (session == null) return Results.Unauthorized();

    try
    {
        var quotationsCol = database.GetCollection<Quotation>("Quotations");
        var filterBuilder = Builders<Quotation>.Filter;
        var filter = filterBuilder.Empty;

        // Customer isolation: Customer accounts can only view their own quotations
        if (session.Role == "Customer")
        {
            filter &= filterBuilder.Eq(q => q.CustomerAccountId, session.UserId);
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            filter &= filterBuilder.Eq(q => q.Status, status.Trim());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTrimmed = search.Trim();
            var searchRegex = new BsonRegularExpression(Regex.Escape(searchTrimmed), "i");
            var searchFilter = filterBuilder.Or(
                filterBuilder.Regex(q => q.QuotationNumber, searchRegex),
                filterBuilder.Regex("clientSnapshot.name", searchRegex),
                filterBuilder.Regex("clientSnapshot.email", searchRegex),
                filterBuilder.Regex("clientSnapshot.phone", searchRegex),
                filterBuilder.Regex("clientSnapshot.gstin", searchRegex)
            );
            filter &= searchFilter;
        }

        var quotations = await quotationsCol.Find(filter)
            .SortByDescending(q => q.CreatedAt)
            .ToListAsync();

        return Results.Ok(quotations);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving quotations.");
        return Results.Problem("An unexpected error occurred while retrieving quotations.");
    }
});

// GET /api/quotations/{id} - Get a single quotation
app.MapGet("/api/quotations/{id}", async (
    string id,
    HttpContext context,
    IMongoDatabase database,
    ILogger<Program> logger) =>
{
    var session = await GetAuthSessionAsync(context, database);
    if (session == null) return Results.Unauthorized();

    if (!ObjectId.TryParse(id, out _))
    {
        return Results.BadRequest(new { message = "Invalid Quotation ID format." });
    }

    try
    {
        var quotationsCol = database.GetCollection<Quotation>("Quotations");
        var quotation = await quotationsCol.Find(q => q.Id == id).FirstOrDefaultAsync();
        if (quotation == null)
        {
            return Results.NotFound(new { message = "Quotation not found." });
        }

        // Ownership verification: Customer cannot access other customer's quotations
        if (session.Role == "Customer" && quotation.CustomerAccountId != session.UserId)
        {
            return Results.Json(new { message = "Forbidden. You do not have access to this quotation." }, statusCode: StatusCodes.Status403Forbidden);
        }

        return Results.Ok(quotation);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving quotation with ID {QuotationId}.", id);
        return Results.Problem("An unexpected error occurred while retrieving the quotation.");
    }
});

// POST /api/quotations - Create quotation (supports both existing billing client & walk-in client)
app.MapPost("/api/quotations", async (
    CreateQuotationRequest request,
    HttpContext context,
    IMongoDatabase database,
    ILogger<Program> logger) =>
{
    var session = await GetAuthSessionAsync(context, database);
    if (session == null) return Results.Unauthorized();

    var customersCol = database.GetCollection<Customer>("Customers");
    ClientSnapshot clientSnapshot;
    string? billingClientId = null;

    var isNewClient = string.Equals(request.ClientMode, "new", StringComparison.OrdinalIgnoreCase) ||
                      (!string.IsNullOrWhiteSpace(request.NewClient?.Name) && string.IsNullOrWhiteSpace(request.BillingClientId));

    if (isNewClient)
    {
        if (request.NewClient == null || string.IsNullOrWhiteSpace(request.NewClient.Name))
        {
            return Results.BadRequest(new { message = "Client or company name is required for walk-in client." });
        }

        var clientName = request.NewClient.Name.Trim();
        var clientPhone = request.NewClient.Phone?.Trim() ?? string.Empty;
        var clientEmail = string.IsNullOrWhiteSpace(request.NewClient.Email) ? null : request.NewClient.Email.Trim();
        var clientAddress = string.IsNullOrWhiteSpace(request.NewClient.Address) ? null : request.NewClient.Address.Trim();
        var clientGstin = string.IsNullOrWhiteSpace(request.NewClient.Gstin) ? null : request.NewClient.Gstin.Trim();

        if (request.SaveToCustomerList)
        {
            var newCustomer = new Customer
            {
                Name = clientName,
                Phone = clientPhone,
                Email = clientEmail,
                Address = clientAddress,
                Gstin = clientGstin,
                CustomerAccountId = session.UserId, // Strictly authoritative
                CreatedAt = DateTime.UtcNow
            };
            await customersCol.InsertOneAsync(newCustomer);
            billingClientId = newCustomer.Id;
        }

        clientSnapshot = new ClientSnapshot
        {
            Name = clientName,
            Phone = clientPhone,
            Email = clientEmail,
            Address = clientAddress,
            Gstin = clientGstin,
            IsWalkIn = !request.SaveToCustomerList
        };
    }
    else
    {
        // Existing billing client selected
        if (string.IsNullOrWhiteSpace(request.BillingClientId) || !ObjectId.TryParse(request.BillingClientId, out _))
        {
            return Results.BadRequest(new { message = "A valid Billing Client ID is required when selecting an existing client." });
        }

        var client = await customersCol.Find(c => c.Id == request.BillingClientId).FirstOrDefaultAsync();
        if (client == null)
        {
            return Results.BadRequest(new { message = "Selected Billing Client was not found in the customer directory." });
        }

        // Ownership verification on existing client
        if (session.Role == "Customer" && client.CustomerAccountId != null && client.CustomerAccountId != session.UserId)
        {
            return Results.Json(new { message = "Forbidden. You do not have access to this billing client." }, statusCode: StatusCodes.Status403Forbidden);
        }

        billingClientId = client.Id;
        clientSnapshot = new ClientSnapshot
        {
            Name = client.Name,
            Phone = client.Phone,
            Email = client.Email,
            Address = client.Address,
            Gstin = client.Gstin,
            IsWalkIn = false
        };
    }

    // Validate line items
    if (request.LineItems == null || request.LineItems.Count == 0)
    {
        return Results.BadRequest(new { message = "At least one line item is required." });
    }

    foreach (var item in request.LineItems)
    {
        if (string.IsNullOrWhiteSpace(item.Description))
        {
            return Results.BadRequest(new { message = "Line item description cannot be empty." });
        }
        if (item.Quantity <= 0)
        {
            return Results.BadRequest(new { message = $"Quantity for '{item.Description}' must be greater than 0." });
        }
        if (item.UnitPrice < 0)
        {
            return Results.BadRequest(new { message = $"Unit price for '{item.Description}' cannot be negative." });
        }
    }

    // Validate dates
    if (request.ValidUntil.Date < request.QuotationDate.Date)
    {
        return Results.BadRequest(new { message = "Valid until date must be on or after the quotation date." });
    }

    // Authoritative backend calculation
    var (subtotal, taxAmount, grandTotal, computedItems) = CalculateTotals(request.LineItems, request.TaxPercentage);

    var quotationsCol = database.GetCollection<Quotation>("Quotations");

    // Generate unique sequential quotation number: QT-yyyyMM-XXXX
    var currentMonthPrefix = $"QT-{DateTime.UtcNow:yyyyMM}-";
    var countThisMonth = await quotationsCol.CountDocumentsAsync(
        Builders<Quotation>.Filter.Regex(q => q.QuotationNumber, new BsonRegularExpression($"^{currentMonthPrefix}"))
    );
    var quotationNumber = $"{currentMonthPrefix}{(countThisMonth + 1):D4}";

    var status = "Draft";
    if (!string.IsNullOrWhiteSpace(request.Status) && 
        (request.Status == "Draft" || request.Status == "Sent"))
    {
        status = request.Status;
    }

    var quotation = new Quotation
    {
        QuotationNumber = quotationNumber,
        CustomerAccountId = session.UserId, // Strictly authoritative from session
        BillingClientId = billingClientId,
        ClientSnapshot = clientSnapshot,
        QuotationDate = request.QuotationDate.Date,
        ValidUntil = request.ValidUntil.Date,
        Status = status,
        LineItems = computedItems,
        Subtotal = subtotal,
        TaxPercentage = Math.Clamp(request.TaxPercentage, 0, 100),
        TaxAmount = taxAmount,
        GrandTotal = grandTotal,
        Notes = request.Notes?.Trim(),
        Terms = request.Terms?.Trim(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    try
    {
        await quotationsCol.InsertOneAsync(quotation);
        return Results.Created($"/api/quotations/{quotation.Id}", quotation);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error creating quotation.");
        return Results.Problem("An unexpected error occurred while creating the quotation.");
    }
});

// PUT /api/quotations/{id} - Update quotation
app.MapPut("/api/quotations/{id}", async (
    string id,
    UpdateQuotationRequest request,
    HttpContext context,
    IMongoDatabase database,
    ILogger<Program> logger) =>
{
    var session = await GetAuthSessionAsync(context, database);
    if (session == null) return Results.Unauthorized();

    if (!ObjectId.TryParse(id, out _))
    {
        return Results.BadRequest(new { message = "Invalid Quotation ID format." });
    }

    var quotationsCol = database.GetCollection<Quotation>("Quotations");
    var existing = await quotationsCol.Find(q => q.Id == id).FirstOrDefaultAsync();
    if (existing == null)
    {
        return Results.NotFound(new { message = "Quotation not found." });
    }

    // Ownership verification
    if (session.Role == "Customer" && existing.CustomerAccountId != session.UserId)
    {
        return Results.Json(new { message = "Forbidden. You do not have access to this quotation." }, statusCode: StatusCodes.Status403Forbidden);
    }

    // Business rule: prevent editing locked quotations
    if (existing.Status == "Accepted" || existing.Status == "Rejected")
    {
        return Results.BadRequest(new { message = $"Cannot modify a quotation that has already been {existing.Status.ToLower()}." });
    }

    var customersCol = database.GetCollection<Customer>("Customers");
    ClientSnapshot clientSnapshot = existing.ClientSnapshot;
    string? billingClientId = existing.BillingClientId;

    var isNewClient = string.Equals(request.ClientMode, "new", StringComparison.OrdinalIgnoreCase) ||
                      (!string.IsNullOrWhiteSpace(request.NewClient?.Name) && string.IsNullOrWhiteSpace(request.BillingClientId));

    if (isNewClient && request.NewClient != null && !string.IsNullOrWhiteSpace(request.NewClient.Name))
    {
        var clientName = request.NewClient.Name.Trim();
        var clientPhone = request.NewClient.Phone?.Trim() ?? string.Empty;
        var clientEmail = string.IsNullOrWhiteSpace(request.NewClient.Email) ? null : request.NewClient.Email.Trim();
        var clientAddress = string.IsNullOrWhiteSpace(request.NewClient.Address) ? null : request.NewClient.Address.Trim();
        var clientGstin = string.IsNullOrWhiteSpace(request.NewClient.Gstin) ? null : request.NewClient.Gstin.Trim();

        if (request.SaveToCustomerList && string.IsNullOrWhiteSpace(existing.BillingClientId))
        {
            var newCustomer = new Customer
            {
                Name = clientName,
                Phone = clientPhone,
                Email = clientEmail,
                Address = clientAddress,
                Gstin = clientGstin,
                CustomerAccountId = session.UserId,
                CreatedAt = DateTime.UtcNow
            };
            await customersCol.InsertOneAsync(newCustomer);
            billingClientId = newCustomer.Id;
        }

        clientSnapshot = new ClientSnapshot
        {
            Name = clientName,
            Phone = clientPhone,
            Email = clientEmail,
            Address = clientAddress,
            Gstin = clientGstin,
            IsWalkIn = string.IsNullOrWhiteSpace(billingClientId)
        };
    }
    else if (!string.IsNullOrWhiteSpace(request.BillingClientId))
    {
        if (!ObjectId.TryParse(request.BillingClientId, out _))
        {
            return Results.BadRequest(new { message = "Invalid Billing Client ID format." });
        }

        var client = await customersCol.Find(c => c.Id == request.BillingClientId).FirstOrDefaultAsync();
        if (client == null)
        {
            return Results.BadRequest(new { message = "Selected Billing Client was not found in the customer directory." });
        }

        if (session.Role == "Customer" && client.CustomerAccountId != null && client.CustomerAccountId != session.UserId)
        {
            return Results.Json(new { message = "Forbidden. You do not have access to this billing client." }, statusCode: StatusCodes.Status403Forbidden);
        }

        billingClientId = client.Id;
        clientSnapshot = new ClientSnapshot
        {
            Name = client.Name,
            Phone = client.Phone,
            Email = client.Email,
            Address = client.Address,
            Gstin = client.Gstin,
            IsWalkIn = false
        };
    }

    // Validate line items
    if (request.LineItems == null || request.LineItems.Count == 0)
    {
        return Results.BadRequest(new { message = "At least one line item is required." });
    }

    foreach (var item in request.LineItems)
    {
        if (string.IsNullOrWhiteSpace(item.Description))
        {
            return Results.BadRequest(new { message = "Line item description cannot be empty." });
        }
        if (item.Quantity <= 0)
        {
            return Results.BadRequest(new { message = $"Quantity for '{item.Description}' must be greater than 0." });
        }
        if (item.UnitPrice < 0)
        {
            return Results.BadRequest(new { message = $"Unit price for '{item.Description}' cannot be negative." });
        }
    }

    // Validate dates
    if (request.ValidUntil.Date < request.QuotationDate.Date)
    {
        return Results.BadRequest(new { message = "Valid until date must be on or after the quotation date." });
    }

    var allowedStatuses = new[] { "Draft", "Sent", "Accepted", "Rejected", "Expired" };
    if (!allowedStatuses.Contains(request.Status, StringComparer.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { message = $"Invalid status '{request.Status}'." });
    }

    // Authoritative backend calculation
    var (subtotal, taxAmount, grandTotal, computedItems) = CalculateTotals(request.LineItems, request.TaxPercentage);

    existing.BillingClientId = billingClientId;
    existing.ClientSnapshot = clientSnapshot;
    existing.QuotationDate = request.QuotationDate.Date;
    existing.ValidUntil = request.ValidUntil.Date;
    existing.Status = request.Status;
    existing.LineItems = computedItems;
    existing.Subtotal = subtotal;
    existing.TaxPercentage = Math.Clamp(request.TaxPercentage, 0, 100);
    existing.TaxAmount = taxAmount;
    existing.GrandTotal = grandTotal;
    existing.Notes = request.Notes?.Trim();
    existing.Terms = request.Terms?.Trim();
    existing.UpdatedAt = DateTime.UtcNow;

    try
    {
        await quotationsCol.ReplaceOneAsync(q => q.Id == id, existing);
        return Results.Ok(existing);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error updating quotation with ID {QuotationId}.", id);
        return Results.Problem("An unexpected error occurred while updating the quotation.");
    }
});

// PATCH /api/quotations/{id}/status - Update quotation status
app.MapPatch("/api/quotations/{id}/status", async (
    string id,
    UpdateQuotationStatusRequest request,
    HttpContext context,
    IMongoDatabase database,
    ILogger<Program> logger) =>
{
    var session = await GetAuthSessionAsync(context, database);
    if (session == null) return Results.Unauthorized();

    if (!ObjectId.TryParse(id, out _))
    {
        return Results.BadRequest(new { message = "Invalid Quotation ID format." });
    }

    var allowedStatuses = new[] { "Draft", "Sent", "Accepted", "Rejected", "Expired" };
    if (string.IsNullOrWhiteSpace(request.Status) || !allowedStatuses.Contains(request.Status, StringComparer.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { message = "Valid status is required (Draft, Sent, Accepted, Rejected, Expired)." });
    }

    var quotationsCol = database.GetCollection<Quotation>("Quotations");
    var existing = await quotationsCol.Find(q => q.Id == id).FirstOrDefaultAsync();
    if (existing == null)
    {
        return Results.NotFound(new { message = "Quotation not found." });
    }

    if (session.Role == "Customer" && existing.CustomerAccountId != session.UserId)
    {
        return Results.Json(new { message = "Forbidden. You do not have access to this quotation." }, statusCode: StatusCodes.Status403Forbidden);
    }

    var update = Builders<Quotation>.Update
        .Set(q => q.Status, request.Status)
        .Set(q => q.UpdatedAt, DateTime.UtcNow);

    await quotationsCol.UpdateOneAsync(q => q.Id == id, update);
    existing.Status = request.Status;
    existing.UpdatedAt = DateTime.UtcNow;

    return Results.Ok(existing);
});

// DELETE /api/quotations/{id} - Delete quotation
app.MapDelete("/api/quotations/{id}", async (
    string id,
    HttpContext context,
    IMongoDatabase database,
    ILogger<Program> logger) =>
{
    var session = await GetAuthSessionAsync(context, database);
    if (session == null) return Results.Unauthorized();

    if (!ObjectId.TryParse(id, out _))
    {
        return Results.BadRequest(new { message = "Invalid Quotation ID format." });
    }

    var quotationsCol = database.GetCollection<Quotation>("Quotations");
    var existing = await quotationsCol.Find(q => q.Id == id).FirstOrDefaultAsync();
    if (existing == null)
    {
        return Results.NotFound(new { message = "Quotation not found." });
    }

    if (session.Role == "Customer" && existing.CustomerAccountId != session.UserId)
    {
        return Results.Json(new { message = "Forbidden. You do not have access to this quotation." }, statusCode: StatusCodes.Status403Forbidden);
    }

    // Protection rule: Accepted quotations cannot be deleted
    if (existing.Status == "Accepted")
    {
        return Results.BadRequest(new { message = "Accepted quotations cannot be deleted." });
    }

    try
    {
        await quotationsCol.DeleteOneAsync(q => q.Id == id);
        return Results.Ok(new { message = $"Quotation '{existing.QuotationNumber}' was deleted successfully." });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error deleting quotation with ID {QuotationId}.", id);
        return Results.Problem("An unexpected error occurred while deleting the quotation.");
    }
});

app.Run();

// Session Extraction Helper
static async Task<AuthSession?> GetAuthSessionAsync(HttpContext context, IMongoDatabase database)
{
    string? token = null;
    var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        token = authHeader.Substring("Bearer ".Length).Trim();
    }
    else if (context.Request.Headers.TryGetValue("X-Trishul-Auth", out var customHeader))
    {
        token = customHeader.FirstOrDefault();
    }

    if (string.IsNullOrWhiteSpace(token)) return null;

    var sessionsCol = database.GetCollection<AuthSession>("AuthSessions");
    var session = await sessionsCol.Find(s => s.Token == token && !s.IsRevoked).FirstOrDefaultAsync();

    if (session == null || session.ExpiresAt <= DateTime.UtcNow) return null;

    return session;
}

// Validation helper method
static (bool IsValid, List<string> Errors) ValidateCustomer(Customer customer)
{
    var errors = new List<string>();

    if (string.IsNullOrWhiteSpace(customer.Name))
    {
        errors.Add("Customer name is required.");
    }
    else if (customer.Name.Trim().Length < 2 || customer.Name.Trim().Length > 100)
    {
        errors.Add("Customer name must be between 2 and 100 characters.");
    }

    if (string.IsNullOrWhiteSpace(customer.Phone))
    {
        errors.Add("Phone number is required.");
    }
    else if (!Regex.IsMatch(customer.Phone.Trim(), @"^[0-9+\-\s()]{7,20}$"))
    {
        errors.Add("Please enter a valid phone number (at least 7 digits).");
    }

    if (!string.IsNullOrWhiteSpace(customer.Email))
    {
        var emailAttr = new EmailAddressAttribute();
        if (!emailAttr.IsValid(customer.Email.Trim()))
        {
            errors.Add("Please enter a valid email address.");
        }
    }

    if (!string.IsNullOrWhiteSpace(customer.Address) && customer.Address.Length > 250)
    {
        errors.Add("Address cannot exceed 250 characters.");
    }

    if (!string.IsNullOrWhiteSpace(customer.Gstin) && customer.Gstin.Trim().Length > 20)
    {
        errors.Add("GSTIN cannot exceed 20 characters.");
    }

    return (errors.Count == 0, errors);
}

// Quotation calculation helper: authoritative server-side calculation
static (decimal Subtotal, decimal TaxAmount, decimal GrandTotal, List<QuotationLineItem> ComputedItems) CalculateTotals(
    IEnumerable<QuotationLineItemInput> items, decimal taxPercentage)
{
    var computedItems = new List<QuotationLineItem>();
    decimal subtotal = 0;

    foreach (var item in items)
    {
        var qty = item.Quantity > 0 ? item.Quantity : 1;
        var unitPrice = item.UnitPrice >= 0 ? item.UnitPrice : 0;
        var lineTotal = Math.Round(qty * unitPrice, 2, MidpointRounding.AwayFromZero);
        subtotal += lineTotal;

        computedItems.Add(new QuotationLineItem
        {
            Description = item.Description.Trim(),
            Quantity = qty,
            UnitPrice = unitPrice,
            LineTotal = lineTotal
        });
    }

    var taxPct = Math.Clamp(taxPercentage, 0, 100);
    var taxAmount = Math.Round(subtotal * (taxPct / 100m), 2, MidpointRounding.AwayFromZero);
    var grandTotal = Math.Round(subtotal + taxAmount, 2, MidpointRounding.AwayFromZero);

    return (subtotal, taxAmount, grandTotal, computedItems);
}
