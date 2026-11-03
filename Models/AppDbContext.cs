using Microsoft.EntityFrameworkCore;

namespace Inventory.Web.Models;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<BranchStock> BranchStocks => Set<BranchStock>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<CashRegisterSession> CashRegisterSessions => Set<CashRegisterSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Branch>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => x.Sku).IsUnique();
        modelBuilder.Entity<UserAccount>().HasIndex(x => x.Username).IsUnique();
        modelBuilder.Entity<Product>().Property(x => x.Cost).HasPrecision(18, 2);
        modelBuilder.Entity<Product>().Property(x => x.Price).HasPrecision(18, 2);
        modelBuilder.Entity<Sale>().Property(x => x.Total).HasPrecision(18, 2);
        modelBuilder.Entity<Sale>().Property(x => x.Discount).HasPrecision(18, 2);
        modelBuilder.Entity<Account>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<JournalEntry>().HasIndex(x => x.Number).IsUnique();
        modelBuilder.Entity<JournalLine>().Property(x => x.Debit).HasPrecision(18, 2);
        modelBuilder.Entity<JournalLine>().Property(x => x.Credit).HasPrecision(18, 2);
        modelBuilder.Entity<Expense>().Property(x => x.Amount).HasPrecision(18, 2);
        modelBuilder.Entity<Purchase>().Property(x => x.Total).HasPrecision(18, 2);
        modelBuilder.Entity<CashRegisterSession>().Property(x => x.OpeningBalance).HasPrecision(18, 2);
        modelBuilder.Entity<CashRegisterSession>().Property(x => x.ExpectedBalance).HasPrecision(18, 2);
        modelBuilder.Entity<CashRegisterSession>().Property(x => x.ActualBalance).HasPrecision(18, 2);
        modelBuilder.Entity<CashRegisterSession>().Property(x => x.Difference).HasPrecision(18, 2);

        modelBuilder.Entity<UserAccount>().HasData(new UserAccount
        {
            Id = 1, FullName = "مدير النظام", Username = "admin",
            PasswordHash = "admin123", Role = UserRole.Admin, IsActive = true
        });
    }
}
