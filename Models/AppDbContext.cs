using Microsoft.EntityFrameworkCore;

namespace Inventory.Web.Models;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().HasIndex(x => x.Sku).IsUnique();
        modelBuilder.Entity<UserAccount>().HasIndex(x => x.Username).IsUnique();
        modelBuilder.Entity<Product>().Property(x => x.Cost).HasPrecision(18, 2);
        modelBuilder.Entity<Product>().Property(x => x.Price).HasPrecision(18, 2);
        modelBuilder.Entity<Sale>().Property(x => x.Total).HasPrecision(18, 2);

        modelBuilder.Entity<UserAccount>().HasData(new UserAccount
        {
            Id = 1, FullName = "مدير النظام", Username = "admin",
            PasswordHash = "admin123", Role = UserRole.Admin, IsActive = true
        });
    }
}
