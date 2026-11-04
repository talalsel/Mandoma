using System.Security.Claims;
using Inventory.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Web.Controllers;

[Authorize(Roles = "Admin,Sales,Cashier,Accountant")]
public class SalesController(AppDbContext db) : Controller
{
    public async Task<IActionResult> List(string? search, SaleType? type)
    {
        var query = db.Sales.Include(x => x.Items).ThenInclude(x => x.Product).Include(x => x.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.InvoiceNumber.Contains(search) || x.CustomerName.Contains(search) || x.CustomerPhone.Contains(search) || x.Id.ToString().Contains(search));
        if (type.HasValue) query = query.Where(x => x.Type == type.Value);
        var sales = await query.OrderByDescending(x => x.SoldAt).ToListAsync();
        ViewBag.Search = search;
        ViewBag.Type = type;
        return View(sales);
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Products = await db.Products.OrderBy(x => x.Name).ToListAsync();
        ViewBag.Customers = await db.Customers.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(List<int> productIds, List<int> quantities, SaleType invoiceType = SaleType.Cash, int? customerId = null, string customerName = "", string customerPhone = "", string customerAddress = "", decimal discount = 0, string notes = "")
    {
        if (productIds.Count == 0 || productIds.Count != quantities.Count || discount < 0)
        {
            ModelState.AddModelError("", "أدخل أصنافًا صحيحة ومبلغ خصم صالحًا");
            ViewBag.Products = await db.Products.OrderBy(x => x.Name).ToListAsync();
            ViewBag.Customers = await db.Customers.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
            return View();
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        Customer? customer = null;
        customerName = customerName?.Trim() ?? "";
        customerPhone = customerPhone?.Trim() ?? "";
        customerAddress = customerAddress?.Trim() ?? "";
        if (customerId.HasValue)
            customer = await db.Customers.FirstOrDefaultAsync(x => x.Id == customerId.Value && x.IsActive);
        if (customer != null)
        {
            customerName = customer.Name;
            customerPhone = customer.Phone;
            customerAddress = customer.Address;
        }
        else if (!string.IsNullOrWhiteSpace(customerName))
        {
            customer = await db.Customers.FirstOrDefaultAsync(x => x.IsActive &&
                ((!string.IsNullOrEmpty(customerPhone) && x.Phone == customerPhone) || x.Name.ToLower() == customerName.ToLower()));
            if (customer == null)
            {
                customer = new Customer { Name = customerName, Phone = customerPhone, Address = customerAddress, IsActive = true };
                db.Customers.Add(customer);
                await db.SaveChangesAsync();
            }
            customerId = customer.Id;
        }
        var items = new List<SaleItem>();
        decimal subtotal = 0;
        decimal cost = 0;

        foreach (var (productId, quantity) in productIds.Zip(quantities))
        {
            if (quantity <= 0) throw new InvalidOperationException("الكمية يجب أن تكون أكبر من صفر");
            var product = await db.Products.FindAsync(productId);
            if (product == null) throw new InvalidOperationException("الصنف غير موجود");
            subtotal += product.Price * quantity;
            cost += product.Cost * quantity;
            items.Add(new SaleItem { ProductId = product.Id, Quantity = quantity, UnitPrice = product.Price });

            if (invoiceType != SaleType.Draft)
            {
                if (quantity > product.Quantity) throw new InvalidOperationException($"الكمية غير متوفرة للصنف {product.Name}");
                product.Quantity -= quantity;
                product.ReservedQuantity += quantity;
                db.StockMovements.Add(new StockMovement { ProductId = product.Id, QuantityChange = -quantity, Reason = invoiceType == SaleType.Issue ? "أمر صرف" : "فاتورة نقدية", UserId = userId });
            }
        }

        var total = subtotal - discount;
        if (total < 0) throw new InvalidOperationException("الخصم أكبر من إجمالي الفاتورة");
        var sale = new Sale { UserId = userId, CustomerId = customerId, Type = invoiceType, InvoiceNumber = await NextInvoiceNumberAsync(invoiceType), SoldAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), Total = total, Discount = discount, CustomerName = customerName, CustomerPhone = customerPhone, CustomerAddress = customerAddress, Notes = notes?.Trim() ?? "", Items = items };
        db.Sales.Add(sale);
        await db.SaveChangesAsync();
        if (invoiceType != SaleType.Draft)
            await AddSaleJournalAsync(sale, cost);
        await tx.CommitAsync();
        return RedirectToAction(nameof(Print), new { id = sale.Id });
    }

    [Authorize(Roles = "Admin,Sales,Cashier,Accountant")]
    public async Task<IActionResult> Print(int id, SaleType? printType)
    {
        var sale = await db.Sales.Include(x => x.Items).ThenInclude(x => x.Product).Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id);
        if (sale == null) return NotFound();
        ViewBag.PrintType = printType ?? sale.Type;
        return View(sale);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var sale = await db.Sales.Include(x => x.Items).ThenInclude(x => x.Product).FirstOrDefaultAsync(x => x.Id == id);
        if (sale == null) return NotFound();
        if (!User.IsInRole("Admin") && sale.Type != SaleType.Draft) return Forbid();
        ViewBag.Products = await db.Products.OrderBy(x => x.Name).ToListAsync();
        return View(sale);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, List<int> productIds, List<int> quantities, string customerName = "", string customerPhone = "", decimal discount = 0, string notes = "")
    {
        var sale = await db.Sales.Include(x => x.Items).ThenInclude(x => x.Product).FirstOrDefaultAsync(x => x.Id == id);
        if (sale == null) return NotFound();
        if (!User.IsInRole("Admin") && sale.Type != SaleType.Draft) return Forbid();
        if (productIds.Count == 0 || productIds.Count != quantities.Count || discount < 0) return BadRequest();

        await using var tx = await db.Database.BeginTransactionAsync();
        if (sale.Type != SaleType.Draft)
        {
            foreach (var oldItem in sale.Items)
                if (oldItem.Product != null)
                {
                    oldItem.Product.Quantity += oldItem.Quantity;
                    oldItem.Product.ReservedQuantity = Math.Max(0, oldItem.Product.ReservedQuantity - oldItem.Quantity);
                }
        }
        db.SaleItems.RemoveRange(sale.Items);
        await db.SaveChangesAsync();

        decimal subtotal = 0;
        decimal cost = 0;
        var newItems = new List<SaleItem>();
        foreach (var (productId, quantity) in productIds.Zip(quantities))
        {
            if (quantity <= 0) throw new InvalidOperationException("الكمية يجب أن تكون أكبر من صفر");
            var product = await db.Products.FindAsync(productId);
            if (product == null) throw new InvalidOperationException("الصنف غير موجود");
            if (sale.Type != SaleType.Draft && quantity > product.Quantity) throw new InvalidOperationException($"الكمية غير متوفرة للصنف {product.Name}");
            subtotal += product.Price * quantity;
            cost += product.Cost * quantity;
            newItems.Add(new SaleItem { SaleId = sale.Id, ProductId = product.Id, Quantity = quantity, UnitPrice = product.Price });
            if (sale.Type != SaleType.Draft)
            {
                product.Quantity -= quantity;
                product.ReservedQuantity += quantity;
            }
        }
        var total = subtotal - discount;
        if (total < 0) throw new InvalidOperationException("الخصم أكبر من إجمالي الفاتورة");
        sale.Items = newItems;
        sale.Total = total;
        sale.Discount = discount;
        sale.CustomerName = customerName.Trim();
        sale.CustomerPhone = customerPhone.Trim();
        sale.Notes = notes.Trim();
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return RedirectToAction(nameof(List));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var sale = await db.Sales.Include(x => x.Items).ThenInclude(x => x.Product).FirstOrDefaultAsync(x => x.Id == id); if (sale == null) return NotFound();
        foreach (var item in sale.Items)
        {
            if (item.Product != null && sale.Type != SaleType.Draft)
            {
                item.Product.Quantity += item.Quantity;
                item.Product.ReservedQuantity = Math.Max(0, item.Product.ReservedQuantity - item.Quantity);
            }
        }
        db.SaleItems.RemoveRange(sale.Items); db.Sales.Remove(sale); await db.SaveChangesAsync(); await tx.CommitAsync(); return RedirectToAction(nameof(List));
    }

    private async Task<string> NextInvoiceNumberAsync(SaleType type)
    {
        var prefix = type == SaleType.Draft ? "DRAFT" : "INV";
        var typesToMatch = type == SaleType.Draft
            ? new[] { SaleType.Draft }
            : new[] { SaleType.Cash, SaleType.Issue };
        var lastNumber = await db.Sales
            .Where(x => typesToMatch.Contains(x.Type) && x.InvoiceNumber.StartsWith(prefix + "-"))
            .Select(x => x.InvoiceNumber)
            .OrderByDescending(x => x)
            .FirstOrDefaultAsync();
        var sequence = 1;
        if (!string.IsNullOrEmpty(lastNumber))
        {
            var parts = lastNumber.Split('-');
            if (parts.Length > 1 && int.TryParse(parts[^1], out var last)) sequence = last + 1;
        }
        return $"{prefix}-{sequence:000000}";
    }

    private async Task AddSaleJournalAsync(Sale sale, decimal cost)
    {
        var cash = await GetAccountAsync("1010", "الصندوق", AccountType.Asset); var revenue = await GetAccountAsync("4000", "المبيعات", AccountType.Revenue); var inventory = await GetAccountAsync("1300", "المخزون", AccountType.Asset); var cogs = await GetAccountAsync("5000", "تكلفة البضاعة المباعة", AccountType.Expense);
        db.JournalEntries.Add(new JournalEntry { Number = $"SAL-{sale.Id}", EntryDate = sale.SoldAt, Description = "فاتورة بيع", ReferenceType = "Sale", ReferenceId = sale.Id, UserId = sale.UserId, Lines = [new JournalLine { AccountId = cash.Id, Debit = sale.Total }, new JournalLine { AccountId = revenue.Id, Credit = sale.Total }, new JournalLine { AccountId = cogs.Id, Debit = cost }, new JournalLine { AccountId = inventory.Id, Credit = cost }] }); await db.SaveChangesAsync();
    }

    private async Task<Account> GetAccountAsync(string code, string name, AccountType type) { var a=await db.Accounts.FirstOrDefaultAsync(x=>x.Code==code); if(a!=null)return a; a=new Account{Code=code,Name=name,Type=type};db.Accounts.Add(a);await db.SaveChangesAsync();return a; }
}
