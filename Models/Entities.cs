using System.ComponentModel.DataAnnotations;

namespace Inventory.Web.Models;

public enum UserRole { Admin, Sales, Accountant, Cashier, StockOnly, Personnel }

public enum AccountType { Asset, Liability, Equity, Revenue, Expense }

public enum JournalStatus { Draft, Posted, Cancelled }

public class Branch
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(30)] public string Code { get; set; } = "";
    [StringLength(250)] public string Address { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class UserAccount
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string FullName { get; set; } = "";
    [Required, StringLength(50)] public string Username { get; set; } = "";
    [Required, StringLength(255)] public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Sales;
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum AttendanceStatus { Present, Absent, Leave, SickLeave }

public class Employee
{
    public int Id { get; set; }
    [Required, StringLength(30)] public string EmployeeNumber { get; set; } = "";
    [Required, StringLength(150)] public string FullName { get; set; } = "";
    [StringLength(30)] public string NationalId { get; set; } = "";
    [StringLength(30)] public string Phone { get; set; } = "";
    [StringLength(150)] public string Email { get; set; } = "";
    [StringLength(100)] public string JobTitle { get; set; } = "";
    [StringLength(100)] public string Department { get; set; } = "";
    public DateTime HireDate { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified);
    public decimal BaseSalary { get; set; }
    [StringLength(100)] public string BankAccount { get; set; } = "";
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public int? UserAccountId { get; set; }
    public UserAccount? UserAccount { get; set; }
    public bool IsActive { get; set; } = true;
}

public class EmployeeAttendance
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTime Date { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public TimeSpan? CheckIn { get; set; }
    public TimeSpan? CheckOut { get; set; }
    [StringLength(250)] public string Notes { get; set; } = "";
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public int RecordedByUserId { get; set; }
}

public class EmployeeDeduction
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTime DeductionDate { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified);
    public decimal Amount { get; set; }
    [Required, StringLength(250)] public string Reason { get; set; } = "";
    public int CreatedByUserId { get; set; }
}

public class Customer
{
    public int Id { get; set; }
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [StringLength(30)] public string Phone { get; set; } = "";
    [StringLength(250)] public string Address { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class Product
{
    public int Id { get; set; }
    [Required, StringLength(50)] public string Sku { get; set; } = "";
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [StringLength(50)] public string? Category { get; set; }
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }
    public decimal Cost { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int MinQuantity { get; set; } = 5;
}

public enum SaleType { Cash = 0, Issue = 1, Draft = 2 }

public class Sale
{
    public int Id { get; set; }
    public SaleType Type { get; set; } = SaleType.Cash;
    [StringLength(30)] public string InvoiceNumber { get; set; } = "";
    public DateTime SoldAt { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    public decimal Total { get; set; }
    public decimal Discount { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    [StringLength(150)] public string CustomerName { get; set; } = "";
    [StringLength(30)] public string CustomerPhone { get; set; } = "";
    [StringLength(250)] public string CustomerAddress { get; set; } = "";
    [StringLength(500)] public string Notes { get; set; } = "";
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }
    public int UserId { get; set; }
    public UserAccount? User { get; set; }
    public List<SaleItem> Items { get; set; } = [];
}

public class SaleItem
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public Sale? Sale { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class StockMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int QuantityChange { get; set; }
    public string Reason { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    public int UserId { get; set; }
}

public class Account
{
    public int Id { get; set; }
    [Required, StringLength(20)] public string Code { get; set; } = "";
    [Required, StringLength(150)] public string Name { get; set; } = "";
    public AccountType Type { get; set; }
    public int? ParentId { get; set; }
    public Account? Parent { get; set; }
    public bool IsActive { get; set; } = true;
}

public class JournalEntry
{
    public int Id { get; set; }
    [Required, StringLength(30)] public string Number { get; set; } = "";
    public DateTime EntryDate { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified);
    [StringLength(250)] public string Description { get; set; } = "";
    public JournalStatus Status { get; set; } = JournalStatus.Posted;
    public string ReferenceType { get; set; } = "Manual";
    public int? ReferenceId { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    public List<JournalLine> Lines { get; set; } = [];
}

public class JournalLine
{
    public int Id { get; set; }
    public int JournalEntryId { get; set; }
    public JournalEntry? JournalEntry { get; set; }
    public int AccountId { get; set; }
    public Account? Account { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    [StringLength(250)] public string Description { get; set; } = "";
}

public class Expense
{
    public int Id { get; set; }
    public DateTime ExpenseDate { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified);
    [Required, StringLength(150)] public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public int ExpenseAccountId { get; set; }
    public Account? ExpenseAccount { get; set; }
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }
    public int UserId { get; set; }
    public bool IsCancelled { get; set; }
}

public class Purchase
{
    public int Id { get; set; }
    [Required, StringLength(50)] public string InvoiceNumber { get; set; } = "";
    public DateTime PurchaseDate { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified);
    public decimal Total { get; set; }
    public string SupplierName { get; set; } = "";
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }
    public bool IsPaid { get; set; }
    public int UserId { get; set; }
    public bool IsCancelled { get; set; }
}

public class CashRegisterSession
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }
    public int UserId { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    public DateTime? ClosedAt { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal? ExpectedBalance { get; set; }
    public decimal? ActualBalance { get; set; }
    public decimal? Difference { get; set; }
    public bool IsClosed { get; set; }
}

public class Transfer
{
    public int Id { get; set; }
    public int FromBranchId { get; set; }
    public Branch? FromBranch { get; set; }
    public int ToBranchId { get; set; }
    public Branch? ToBranch { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    public int UserId { get; set; }
    public string Status { get; set; } = "Pending";
    public string Notes { get; set; } = "";
}

public class BranchStock
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
}
