using Inventory.Web.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var port = builder.Configuration["PORT"];
if (!string.IsNullOrWhiteSpace(port) && int.TryParse(port, out var portNumber))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{portNumber}");
}

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorization();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var databaseUrl = builder.Configuration["DATABASE_URL"];
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=inventory.db";

if (!string.IsNullOrWhiteSpace(databaseUrl) && databaseUrl.StartsWith("postgres", StringComparison.OrdinalIgnoreCase))
{
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':', 2);
    var builderConnection = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.IsDefaultPort ? 5432 : uri.Port,
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        Database = uri.AbsolutePath.Trim('/'),
        SslMode = SslMode.Require
    };
    connectionString = builderConnection.ToString();
}
else if (!string.IsNullOrWhiteSpace(builder.Configuration["PGHOST"]))
{
    connectionString = new NpgsqlConnectionStringBuilder
    {
        Host = builder.Configuration["PGHOST"],
        Port = int.TryParse(builder.Configuration["PGPORT"], out var pgPort) ? pgPort : 5432,
        Username = builder.Configuration["PGUSER"],
        Password = builder.Configuration["PGPASSWORD"],
        Database = builder.Configuration["PGDATABASE"],
        SslMode = SslMode.Require
    }.ToString();
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
        options.UseSqlite(connectionString);
    else
        options.UseNpgsql(connectionString);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!string.IsNullOrWhiteSpace(databaseUrl)
        || !string.IsNullOrWhiteSpace(builder.Configuration["PGHOST"])
        || !connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
        await db.Database.MigrateAsync();
    else
    {
        db.Database.EnsureCreated();
        await EnsureSqliteSchemaAsync(db);
    }

    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<UserAccount>>();
    var adminUsername = builder.Configuration["ADMIN_USERNAME"];
    var adminPassword = builder.Configuration["ADMIN_PASSWORD"];

    if (app.Environment.IsProduction() && (string.IsNullOrWhiteSpace(adminUsername) || string.IsNullOrWhiteSpace(adminPassword)))
    {
        throw new InvalidOperationException("ADMIN_USERNAME and ADMIN_PASSWORD must be configured in production.");
    }

    adminUsername ??= "admin";
    adminPassword ??= "Admin123!";

    var admin = await db.Users.FirstOrDefaultAsync(u => u.Username == adminUsername);
    if (admin is null)
    {
        admin = new UserAccount { FullName = "مدير النظام", Username = adminUsername, Role = UserRole.Admin, IsActive = true };
        admin.PasswordHash = hasher.HashPassword(admin, adminPassword);
        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }
}

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

async Task EnsureSqliteSchemaAsync(AppDbContext db)
{
    var statements = new[]
    {
        "CREATE TABLE IF NOT EXISTS Accounts (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Code TEXT NOT NULL, Name TEXT NOT NULL, Type INTEGER NOT NULL, ParentId INTEGER NULL, IsActive INTEGER NOT NULL DEFAULT 1)",
        "CREATE UNIQUE INDEX IF NOT EXISTS IX_Accounts_Code ON Accounts(Code)",
        "CREATE TABLE IF NOT EXISTS JournalEntries (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Number TEXT NOT NULL, EntryDate TEXT NOT NULL, Description TEXT NOT NULL DEFAULT '', Status INTEGER NOT NULL DEFAULT 1, ReferenceType TEXT NOT NULL DEFAULT 'Manual', ReferenceId INTEGER NULL, UserId INTEGER NOT NULL, CreatedAt TEXT NOT NULL)",
        "CREATE UNIQUE INDEX IF NOT EXISTS IX_JournalEntries_Number ON JournalEntries(Number)",
        "CREATE TABLE IF NOT EXISTS JournalLines (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, JournalEntryId INTEGER NOT NULL, AccountId INTEGER NOT NULL, Debit TEXT NOT NULL DEFAULT '0', Credit TEXT NOT NULL DEFAULT '0', Description TEXT NOT NULL DEFAULT '')",
        "CREATE TABLE IF NOT EXISTS Expenses (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, ExpenseDate TEXT NOT NULL, Description TEXT NOT NULL, Amount TEXT NOT NULL DEFAULT '0', ExpenseAccountId INTEGER NOT NULL, UserId INTEGER NOT NULL, IsCancelled INTEGER NOT NULL DEFAULT 0)",
        "CREATE TABLE IF NOT EXISTS Purchases (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, InvoiceNumber TEXT NOT NULL, PurchaseDate TEXT NOT NULL, Total TEXT NOT NULL DEFAULT '0', SupplierName TEXT NOT NULL DEFAULT '', IsPaid INTEGER NOT NULL DEFAULT 0, UserId INTEGER NOT NULL, BranchId INTEGER NOT NULL DEFAULT 0, IsCancelled INTEGER NOT NULL DEFAULT 0)",
        "CREATE TABLE IF NOT EXISTS CashRegisterSessions (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, BranchId INTEGER NOT NULL DEFAULT 0, UserId INTEGER NOT NULL, OpenedAt TEXT NOT NULL, ClosedAt TEXT NULL, OpeningBalance TEXT NOT NULL DEFAULT '0', ExpectedBalance TEXT NULL, ActualBalance TEXT NULL, Difference TEXT NULL, IsClosed INTEGER NOT NULL DEFAULT 0)",
        "CREATE TABLE IF NOT EXISTS Customers (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Phone TEXT NOT NULL DEFAULT '', Address TEXT NOT NULL DEFAULT '', IsActive INTEGER NOT NULL DEFAULT 1)",
        "CREATE TABLE IF NOT EXISTS Branches (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Code TEXT NOT NULL, Address TEXT NOT NULL DEFAULT '', IsActive INTEGER NOT NULL DEFAULT 1)",
        "CREATE UNIQUE INDEX IF NOT EXISTS IX_Branches_Code ON Branches(Code)",
        "CREATE TABLE IF NOT EXISTS BranchStocks (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, BranchId INTEGER NOT NULL, ProductId INTEGER NOT NULL, Quantity INTEGER NOT NULL DEFAULT 0)",
        "CREATE TABLE IF NOT EXISTS Transfers (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, FromBranchId INTEGER NOT NULL, ToBranchId INTEGER NOT NULL, ProductId INTEGER NOT NULL, Quantity INTEGER NOT NULL, UnitCost TEXT NOT NULL DEFAULT '0', CreatedAt TEXT NOT NULL, UserId INTEGER NOT NULL, Status TEXT NOT NULL DEFAULT 'Pending', Notes TEXT NOT NULL DEFAULT '')",
        "CREATE TABLE IF NOT EXISTS Employees (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, EmployeeNumber TEXT NOT NULL, FullName TEXT NOT NULL, NationalId TEXT NOT NULL DEFAULT '', Phone TEXT NOT NULL DEFAULT '', Email TEXT NOT NULL DEFAULT '', JobTitle TEXT NOT NULL DEFAULT '', Department TEXT NOT NULL DEFAULT '', HireDate TEXT NOT NULL, BaseSalary TEXT NOT NULL DEFAULT '0', BankAccount TEXT NOT NULL DEFAULT '', BranchId INTEGER NULL, UserAccountId INTEGER NULL, IsActive INTEGER NOT NULL DEFAULT 1)",
        "CREATE UNIQUE INDEX IF NOT EXISTS IX_Employees_EmployeeNumber ON Employees(EmployeeNumber)",
        "CREATE TABLE IF NOT EXISTS EmployeeAttendances (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, EmployeeId INTEGER NOT NULL, Date TEXT NOT NULL, Status INTEGER NOT NULL DEFAULT 0, CheckIn TEXT NULL, CheckOut TEXT NULL, Notes TEXT NOT NULL DEFAULT '', BranchId INTEGER NULL, RecordedByUserId INTEGER NOT NULL)",
        "CREATE UNIQUE INDEX IF NOT EXISTS IX_EmployeeAttendances_EmployeeId_Date ON EmployeeAttendances(EmployeeId, Date)",
        "CREATE TABLE IF NOT EXISTS EmployeeDeductions (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, EmployeeId INTEGER NOT NULL, DeductionDate TEXT NOT NULL, Amount TEXT NOT NULL DEFAULT '0', Reason TEXT NOT NULL, CreatedByUserId INTEGER NOT NULL)"
    };

    foreach (var sql in statements)
    {
        try { await db.Database.ExecuteSqlRawAsync(sql); }
        catch { /* Column/table may already exist. */ }
    }

    async Task<HashSet<string>> GetColumnsAsync(string table)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table})";
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
        if (shouldClose) await connection.CloseAsync();
        return columns;
    }

    async Task EnsureColumnAsync(string table, string column, string definition)
    {
        var columns = await GetColumnsAsync(table);
        if (!columns.Contains(column))
            await db.Database.ExecuteSqlRawAsync($"ALTER TABLE {table} ADD COLUMN {column} {definition}");
    }

    await EnsureColumnAsync("Users", "BranchId", "INTEGER NULL");
    await EnsureColumnAsync("Products", "BranchId", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnAsync("Sales", "BranchId", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnAsync("Sales", "Discount", "TEXT NOT NULL DEFAULT 0");
    await EnsureColumnAsync("Sales", "CustomerName", "TEXT NOT NULL DEFAULT ''");
    await EnsureColumnAsync("Sales", "CustomerPhone", "TEXT NOT NULL DEFAULT ''");
    await EnsureColumnAsync("Sales", "Notes", "TEXT NOT NULL DEFAULT ''");
    await EnsureColumnAsync("Expenses", "BranchId", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnAsync("Purchases", "BranchId", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnAsync("CashRegisterSessions", "BranchId", "INTEGER NOT NULL DEFAULT 0");
}


app.Run();
