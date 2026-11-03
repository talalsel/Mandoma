using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Inventory.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Web.Controllers;

public class BankBalanceView
{
    public string AccountCode { get; set; } = "";
    public string AccountName { get; set; } = "";
    public decimal Balance { get; set; }
}

[Microsoft.AspNetCore.Authorization.Authorize]
public class HomeController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.ProductCount = await db.Products.CountAsync();
        ViewBag.InventoryValue = await db.Products.SumAsync(p => p.Quantity * p.Cost);
        ViewBag.LowStockCount = await db.Products.CountAsync(p => p.Quantity <= p.MinQuantity);
        ViewBag.TodaySales = await db.Sales.Where(s => s.SoldAt.Date == DateTime.Today).SumAsync(s => (decimal?)s.Total) ?? 0;
        ViewBag.LowStock = await db.Products.Where(p => p.Quantity <= p.MinQuantity).OrderBy(p => p.Quantity).Take(10).ToListAsync();

        var branches = await db.Branches.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        var branchBalances = new Dictionary<string, decimal>();
        foreach (var branch in branches)
        {
            var salesCash = await db.Sales.Where(x => x.BranchId == branch.Id).SumAsync(x => (decimal?)x.Total) ?? 0;
            var expenses = await db.Expenses.Where(x => x.BranchId == branch.Id && !x.IsCancelled).SumAsync(x => (decimal?)x.Amount) ?? 0;
            var paidPurchases = await db.Purchases.Where(x => x.BranchId == branch.Id && x.IsPaid && !x.IsCancelled).SumAsync(x => (decimal?)x.Total) ?? 0;
            branchBalances[branch.Name] = salesCash - expenses - paidPurchases;
        }
        ViewBag.BranchBalances = branchBalances;

        var bankAccountIds = await db.Accounts
            .Where(x => x.Code.StartsWith("102") || x.Name.Contains("بنك") || x.Name.Contains("مصرف") || x.Name.Contains("Bank"))
            .Select(x => x.Id)
            .ToListAsync();
        var bankLines = await db.JournalLines
            .Where(x => x.JournalEntry!.Status == JournalStatus.Posted && bankAccountIds.Contains(x.AccountId))
            .Include(x => x.Account)
            .ToListAsync();
        var bankBalances = bankLines
            .GroupBy(x => new { x.Account!.Code, x.Account.Name })
            .Select(g => new BankBalanceView { AccountCode = g.Key.Code, AccountName = g.Key.Name, Balance = g.Sum(x => x.Debit - x.Credit) })
            .OrderBy(x => x.AccountCode)
            .ToList();
        ViewBag.BankBalances = bankBalances;
        ViewBag.MainCashBalance = await db.Sales.SumAsync(x => (decimal?)x.Total) ?? 0
            - await db.Expenses.Where(x => !x.IsCancelled).SumAsync(x => (decimal?)x.Amount) ?? 0
            - await db.Purchases.Where(x => x.IsPaid && !x.IsCancelled).SumAsync(x => (decimal?)x.Total) ?? 0;
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
