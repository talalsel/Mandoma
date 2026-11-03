using System.Security.Claims;
using Inventory.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Web.Controllers;

[Authorize(Roles = "Admin,Sales,Cashier,Accountant")]
public class SalesController(AppDbContext db) : Controller
{
    public async Task<IActionResult> List()
    {
        var sales = await db.Sales.Include(x => x.Items).ThenInclude(x => x.Product).Include(x => x.User).OrderByDescending(x => x.SoldAt).ToListAsync();
        return View(sales);
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Products = await db.Products.Where(x => x.Quantity > 0).OrderBy(x => x.Name).ToListAsync();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(List<int> productIds, List<int> quantities, string customerName = "", string customerPhone = "", decimal discount = 0, string notes = "")
    {
        if (productIds.Count == 0 || productIds.Count != quantities.Count || discount < 0)
        {
            ModelState.AddModelError("", "أدخل أصنافًا صحيحة ومبلغ خصم صالحًا");
            ViewBag.Products = await db.Products.Where(x => x.Quantity > 0).OrderBy(x => x.Name).ToListAsync();
            return View();
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var items = new List<SaleItem>();
        decimal subtotal = 0;
        decimal cost = 0;

        foreach (var (productId, quantity) in productIds.Zip(quantities))
        {
            if (quantity <= 0) throw new InvalidOperationException("الكمية يجب أن تكون أكبر من صفر");
            var product = await db.Products.FindAsync(productId);
            if (product == null || quantity > product.Quantity) throw new InvalidOperationException($"الكمية غير متوفرة للصنف {product?.Name ?? productId.ToString()}");
            product.Quantity -= quantity;
            subtotal += product.Price * quantity;
            cost += product.Cost * quantity;
            items.Add(new SaleItem { ProductId = product.Id, Quantity = quantity, UnitPrice = product.Price });
            db.StockMovements.Add(new StockMovement { ProductId = product.Id, QuantityChange = -quantity, Reason = "بيع فاتورة", UserId = userId });
        }

        var total = subtotal - discount;
        if (total < 0) throw new InvalidOperationException("الخصم أكبر من إجمالي الفاتورة");
        var sale = new Sale { UserId = userId, SoldAt = DateTime.Now, Total = total, Discount = discount, CustomerName = customerName?.Trim() ?? "", CustomerPhone = customerPhone?.Trim() ?? "", Notes = notes?.Trim() ?? "", Items = items };
        db.Sales.Add(sale);
        await db.SaveChangesAsync();
        await AddSaleJournalAsync(sale, cost);
        await tx.CommitAsync();
        return RedirectToAction(nameof(Print), new { id = sale.Id });
    }

    [Authorize(Roles = "Admin,Sales,Cashier,Accountant")]
    public async Task<IActionResult> Print(int id)
    {
        var sale = await db.Sales.Include(x => x.Items).ThenInclude(x => x.Product).Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id);
        if (sale == null) return NotFound();
        return View(sale);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var sale = await db.Sales.Include(x => x.Items).ThenInclude(x => x.Product).FirstOrDefaultAsync(x => x.Id == id);
        if (sale == null) return NotFound(); return View(sale);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, decimal total)
    {
        var sale = await db.Sales.FindAsync(id); if (sale == null) return NotFound(); if (total < 0) return BadRequest(); sale.Total = total; await db.SaveChangesAsync(); return RedirectToAction(nameof(List));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var sale = await db.Sales.Include(x => x.Items).ThenInclude(x => x.Product).FirstOrDefaultAsync(x => x.Id == id); if (sale == null) return NotFound();
        foreach (var item in sale.Items) { if (item.Product != null) item.Product.Quantity += item.Quantity; }
        db.SaleItems.RemoveRange(sale.Items); db.Sales.Remove(sale); await db.SaveChangesAsync(); await tx.CommitAsync(); return RedirectToAction(nameof(List));
    }

    private async Task AddSaleJournalAsync(Sale sale, decimal cost)
    {
        var cash = await GetAccountAsync("1010", "الصندوق", AccountType.Asset); var revenue = await GetAccountAsync("4000", "المبيعات", AccountType.Revenue); var inventory = await GetAccountAsync("1300", "المخزون", AccountType.Asset); var cogs = await GetAccountAsync("5000", "تكلفة البضاعة المباعة", AccountType.Expense);
        db.JournalEntries.Add(new JournalEntry { Number = $"SAL-{sale.Id}", EntryDate = sale.SoldAt, Description = "فاتورة بيع", ReferenceType = "Sale", ReferenceId = sale.Id, UserId = sale.UserId, Lines = [new JournalLine { AccountId = cash.Id, Debit = sale.Total }, new JournalLine { AccountId = revenue.Id, Credit = sale.Total }, new JournalLine { AccountId = cogs.Id, Debit = cost }, new JournalLine { AccountId = inventory.Id, Credit = cost }] }); await db.SaveChangesAsync();
    }

    private async Task<Account> GetAccountAsync(string code, string name, AccountType type) { var a=await db.Accounts.FirstOrDefaultAsync(x=>x.Code==code); if(a!=null)return a; a=new Account{Code=code,Name=name,Type=type};db.Accounts.Add(a);await db.SaveChangesAsync();return a; }
}
