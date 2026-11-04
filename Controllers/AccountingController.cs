using Inventory.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Inventory.Web.Controllers;

[Authorize(Roles = "Admin,Accountant,Cashier")]
public class AccountingController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index() => View(await db.JournalEntries.Include(x => x.Lines).ThenInclude(x => x.Account).OrderByDescending(x => x.EntryDate).Take(100).ToListAsync());

    [Authorize(Roles = "Admin,Accountant")]
    public async Task<IActionResult> Accounts() { await EnsureAccountsAsync(); return View(await db.Accounts.OrderBy(x => x.Code).ToListAsync()); }

    [Authorize(Roles = "Admin,Accountant")]
    public async Task<IActionResult> CreateAccount() { ViewBag.Parents = await db.Accounts.Where(x => x.IsActive).OrderBy(x => x.Code).ToListAsync(); return View(new Account()); }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,Accountant")]
    public async Task<IActionResult> CreateAccount(Account account)
    {
        if (!ModelState.IsValid) { ViewBag.Parents = await db.Accounts.Where(x => x.IsActive).OrderBy(x => x.Code).ToListAsync(); return View(account); }
        if (await db.Accounts.AnyAsync(x => x.Code == account.Code)) { ModelState.AddModelError("Code", "رمز الحساب مستخدم بالفعل"); ViewBag.Parents = await db.Accounts.Where(x => x.IsActive).OrderBy(x => x.Code).ToListAsync(); return View(account); }
        db.Accounts.Add(account); await db.SaveChangesAsync(); return RedirectToAction(nameof(Accounts));
    }

    [Authorize(Roles = "Admin,Accountant")]
    public async Task<IActionResult> CreateJournal() { await EnsureAccountsAsync(); ViewBag.Accounts = await db.Accounts.Where(x => x.IsActive).OrderBy(x => x.Code).ToListAsync(); return View(new JournalEntry { Number = $"JE-{DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified):yyyyMMdd}-{DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified):HHmmss}" }); }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,Accountant")]
    public async Task<IActionResult> CreateJournal(JournalEntry entry, List<int> accountIds, List<decimal> debits, List<decimal> credits)
    {
        await EnsureAccountsAsync();
        entry.Lines.Clear();
        for (int i = 0; i < accountIds.Count; i++) entry.Lines.Add(new JournalLine { AccountId = accountIds[i], Debit = debits.ElementAtOrDefault(i), Credit = credits.ElementAtOrDefault(i) });
        var debit = entry.Lines.Sum(x => x.Debit); var credit = entry.Lines.Sum(x => x.Credit);
        if (entry.Lines.Count < 2 || debit != credit || debit <= 0) { ModelState.AddModelError("", "يجب أن يتكون القيد من سطرين وأن يتساوى مجموع المدين والدائن"); ViewBag.Accounts = await db.Accounts.Where(x => x.IsActive).OrderBy(x => x.Code).ToListAsync(); return View(entry); }
        entry.UserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        entry.Status = JournalStatus.Posted;
        db.JournalEntries.Add(entry); await db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin,Accountant")]
    public async Task<IActionResult> Ledger(int? accountId)
    {
        ViewBag.Accounts = await db.Accounts.OrderBy(x => x.Code).ToListAsync();
        var lines = db.JournalLines.Include(x => x.Account).Include(x => x.JournalEntry).Where(x => x.JournalEntry!.Status == JournalStatus.Posted);
        if (accountId.HasValue) lines = lines.Where(x => x.AccountId == accountId.Value);
        return View(await lines.OrderBy(x => x.JournalEntry!.EntryDate).ThenBy(x => x.Id).ToListAsync());
    }

    [Authorize(Roles = "Admin,Accountant")]
    public async Task<IActionResult> TrialBalance()
    {
        var balances = await db.JournalLines.Include(x => x.Account).Include(x => x.JournalEntry)
            .Where(x => x.JournalEntry!.Status == JournalStatus.Posted)
            .GroupBy(x => new { x.AccountId, x.Account!.Code, x.Account.Name })
            .Select(g => new { g.Key.Code, g.Key.Name, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) })
            .OrderBy(x => x.Code).ToListAsync();
        return View(balances);
    }

    [Authorize(Roles = "Admin,Accountant")]
    public async Task<IActionResult> Expenses() => View(await db.Expenses.Include(x => x.ExpenseAccount).Where(x => !x.IsCancelled).OrderByDescending(x => x.ExpenseDate).ToListAsync());

    [Authorize(Roles = "Admin,Accountant")]
    public async Task<IActionResult> CreateExpense() { await EnsureAccountsAsync(); ViewBag.Accounts = await db.Accounts.Where(x => x.Type == AccountType.Expense && x.IsActive).OrderBy(x => x.Code).ToListAsync(); return View(new Expense()); }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,Accountant")]
    public async Task<IActionResult> CreateExpense(Expense expense)
    {
        if (expense.Amount <= 0 || expense.ExpenseAccountId == 0) { ModelState.AddModelError("", "يجب إدخال مبلغ وحساب مصروف صحيحين"); ViewBag.Accounts = await db.Accounts.Where(x => x.Type == AccountType.Expense && x.IsActive).OrderBy(x => x.Code).ToListAsync(); return View(expense); }
        expense.UserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await using var tx = await db.Database.BeginTransactionAsync();
        db.Expenses.Add(expense);
        var expenseAccount = await db.Accounts.FindAsync(expense.ExpenseAccountId);
        var cash = await GetAccountAsync("1010", "الصندوق", AccountType.Asset);
        db.JournalEntries.Add(CreateExpenseJournal(expense, expenseAccount ?? expenseAccount!, cash));
        await db.SaveChangesAsync(); await tx.CommitAsync(); return RedirectToAction(nameof(Expenses));
    }

    public async Task<IActionResult> Purchases() => View(await db.Purchases.Where(x => !x.IsCancelled).OrderByDescending(x => x.PurchaseDate).ToListAsync());

    public async Task<IActionResult> CreatePurchase() => View(new Purchase());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePurchase(Purchase purchase)
    {
        if (purchase.Total <= 0 || string.IsNullOrWhiteSpace(purchase.InvoiceNumber)) { ModelState.AddModelError("", "أدخل رقم الفاتورة والمبلغ"); return View(purchase); }
        purchase.UserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await using var tx = await db.Database.BeginTransactionAsync();
        db.Purchases.Add(purchase);
        await db.SaveChangesAsync();
        var inventory = await GetAccountAsync("1300", "المخزون", AccountType.Asset);
        var payables = await GetAccountAsync("2100", "الموردون", AccountType.Liability);
        var cash = await GetAccountAsync("1010", "الصندوق", AccountType.Asset);
        db.JournalEntries.Add(new JournalEntry { Number = $"PUR-{purchase.Id}", EntryDate = purchase.PurchaseDate, Description = $"شراء فاتورة {purchase.InvoiceNumber} - {purchase.SupplierName}", ReferenceType = "Purchase", ReferenceId = purchase.Id, UserId = purchase.UserId, Lines = [new JournalLine { AccountId = inventory.Id, Debit = purchase.Total }, new JournalLine { AccountId = (purchase.IsPaid ? cash : payables).Id, Credit = purchase.Total }] });
        await db.SaveChangesAsync(); await tx.CommitAsync(); return RedirectToAction(nameof(Purchases));
    }

    private async Task EnsureAccountsAsync()
    {
        await GetAccountAsync("1010", "الصندوق الرئيسي", AccountType.Asset);
        await GetAccountAsync("1011", "صندوق أمين الصندوق", AccountType.Asset);
        await GetAccountAsync("1020", "البنك", AccountType.Asset);
        await GetAccountAsync("1300", "المخزون", AccountType.Asset);
        await GetAccountAsync("2100", "الموردون", AccountType.Liability);
        await GetAccountAsync("3000", "رأس المال", AccountType.Equity);
        await GetAccountAsync("4000", "المبيعات", AccountType.Revenue);
        await GetAccountAsync("5000", "تكلفة البضاعة المباعة", AccountType.Expense);
        await GetAccountAsync("5100", "المصاريف التشغيلية", AccountType.Expense);
    }

    private async Task<Account> GetAccountAsync(string code, string name, AccountType type)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(x => x.Code == code); if (account != null) return account;
        account = new Account { Code = code, Name = name, Type = type, IsActive = true }; db.Accounts.Add(account); await db.SaveChangesAsync(); return account;
    }

    private static JournalEntry CreateExpenseJournal(Expense expense, Account expenseAccount, Account cash)
    {
        return new JournalEntry { Number = $"EXP-{DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified):yyyyMMddHHmmss}", EntryDate = expense.ExpenseDate, Description = expense.Description, ReferenceType = "Expense", UserId = expense.UserId, Lines = [new JournalLine { AccountId = expenseAccount.Id, Debit = expense.Amount }, new JournalLine { AccountId = cash.Id, Credit = expense.Amount }] };
    }
}
