namespace Inventory.Web.Models;

public class BackupPackage
{
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Branch> Branches { get; set; } = [];
    public List<UserAccount> Users { get; set; } = [];
    public List<Customer> Customers { get; set; } = [];
    public List<Employee> Employees { get; set; } = [];
    public List<EmployeeAttendance> EmployeeAttendances { get; set; } = [];
    public List<EmployeeDeduction> EmployeeDeductions { get; set; } = [];
    public List<Product> Products { get; set; } = [];
    public List<BranchStock> BranchStocks { get; set; } = [];
    public List<Transfer> Transfers { get; set; } = [];
    public List<Sale> Sales { get; set; } = [];
    public List<SaleItem> SaleItems { get; set; } = [];
    public List<StockMovement> StockMovements { get; set; } = [];
    public List<Account> Accounts { get; set; } = [];
    public List<JournalEntry> JournalEntries { get; set; } = [];
    public List<JournalLine> JournalLines { get; set; } = [];
    public List<Expense> Expenses { get; set; } = [];
    public List<Purchase> Purchases { get; set; } = [];
    public List<CashRegisterSession> CashRegisterSessions { get; set; } = [];
}
