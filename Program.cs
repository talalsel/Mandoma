using Inventory.Web.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

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
    if (!string.IsNullOrWhiteSpace(builder.Configuration["DATABASE_URL"]))
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
        "CREATE TABLE IF NOT EXISTS Purchases (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, InvoiceNumber TEXT NOT NULL, PurchaseDate TEXT NOT NULL, Total TEXT NOT NULL DEFAULT '0', SupplierName TEXT NOT NULL DEFAULT '', IsPaid INTEGER NOT NULL DEFAULT 0, UserId INTEGER NOT NULL, IsCancelled INTEGER NOT NOT NULL DEFAULT 0)",
        "CREATE TABLE IF NOT EXISTS CashRegisterSessions (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, UserId INTEGER NOT NULL, OpenedAt TEXT NOT NULL, ClosedAt TEXT NULL, OpeningBalance TEXT NOT NULL DEFAULT '0', ExpectedBalance TEXT NULL, ActualBalance TEXT NULL, Difference TEXT NULL, IsClosed INTEGER NOT NULL DEFAULT 0)",
        "CREATE TABLE IF NOT EXISTS Customers (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Phone TEXT NOT NULL DEFAULT '', Address TEXT NOT NULL DEFAULT '', IsActive INTEGER NOT NULL DEFAULT 1)",
        "CREATE TABLE IF NOT EXISTS Branches (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Code TEXT NOT NULL, Address TEXT NOT NULL DEFAULT '', IsActive INTEGER NOT NULL DEFAULT 1)",
        "CREATE UNIQUE INDEX IF NOT EXISTS IX_Branches_Code ON Branches(Code)",
        "CREATE TABLE IF NOT EXISTS BranchStocks (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, BranchId INTEGER NOT NULL, ProductId INTEGER NOT NULL, Quantity INTEGER NOT NULL DEFAULT 0)",
        "CREATE TABLE IF NOT EXISTS Transfers (Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, FromBranchId INTEGER NOT NULL, ToBranchId INTEGER NOT NULL, ProductId INTEGER NOT NULL, Quantity INTEGER NOT NULL, UnitCost TEXT NOT NULL DEFAULT '0', CreatedAt TEXT NOT NULL, UserId INTEGER NOT NULL, Status TEXT NOT NULL DEFAULT 'Pending', Notes TEXT NOT NULL DEFAULT '')"
    };

    foreach (var sql in statements)
    {
        try { await db.Database.ExecuteSqlRawAsync(sql); }
        catch { /* Column/table may already exist. */ }
    }

    var userColumns = db.Database.SqlQueryRaw<string>("SELECT name FROM PRAGMA_TABLE_INFO('Users')").ToList();
    if (!userColumns.Contains("BranchId"))
    {
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Users ADD COLUMN BranchId INTEGER NULL");
    }

    var saleColumns = db.Database.SqlQueryRaw<string>("SELECT name FROM PRAGMA_TABLE_INFO('Sales')").ToList();
    if (!saleColumns.Contains("BranchId"))
    {
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Sales ADD COLUMN BranchId INTEGER NOT NULL DEFAULT 0");
    }
    if (!saleColumns.Contains("Discount"))
    {
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Sales ADD COLUMN Discount TEXT NOT NULL DEFAULT 0");
    }
    if (!saleColumns.Contains("CustomerName"))
    {
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Sales ADD COLUMN CustomerName TEXT NOT NULL DEFAULT ''");
    }
    if (!saleColumns.Contains("CustomerPhone"))
    {
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Sales ADD COLUMN CustomerPhone TEXT NOT NULL DEFAULT ''");
    }
    if (!saleColumns.Contains("Notes"))
    {
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Sales ADD COLUMN Notes TEXT NOT NULL DEFAULT ''");
    }
}


app.Run();
