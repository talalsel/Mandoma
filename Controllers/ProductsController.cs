using Inventory.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Web.Controllers;

[Microsoft.AspNetCore.Authorization.Authorize]
public class ProductsController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? search)
    {
        var query = db.Products.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || p.Sku.Contains(search));
        return View(await query.OrderBy(p => p.Name).ToListAsync());
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,StockOnly")]
    public IActionResult Create() => View(new Product());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,StockOnly")]
    public async Task<IActionResult> Create(Product product)
    {
        product.Sku = product.Sku?.Trim().ToUpperInvariant() ?? "";
        product.Name = product.Name?.Trim() ?? "";
        product.Category = product.Category?.Trim();
        if (!ModelState.IsValid) return View(product);
        if (product.Quantity < 0 || product.MinQuantity < 0 || product.Cost < 0 || product.Price < 0)
        {
            ModelState.AddModelError("", "الكمية والأسعار والقيم يجب أن تكون صفرًا أو أكبر");
            return View(product);
        }
        if (await db.Products.AnyAsync(p => p.Sku.ToUpper() == product.Sku))
        {
            ModelState.AddModelError("Sku", "رمز الصنف مستخدم بالفعل");
            return View(product);
        }
        var branchId = product.BranchId;
        if (branchId == 0)
        {
            var defaultBranch = await db.Branches.Where(x => x.IsActive).OrderBy(x => x.Id).FirstOrDefaultAsync();
            if (defaultBranch == null)
            {
                defaultBranch = new Branch { Name = "المخزن الرئيسي", Code = "MAIN", Address = "", IsActive = true };
                db.Branches.Add(defaultBranch);
                await db.SaveChangesAsync();
            }
            branchId = defaultBranch.Id;
        }
        product.BranchId = branchId;
        product.ReservedQuantity = 0;
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,StockOnly")]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await db.Products.FindAsync(id);
        return product is null ? NotFound() : View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,StockOnly")]
    public async Task<IActionResult> Edit(int id, Product product)
    {
        if (id != product.Id) return NotFound();
        if (!ModelState.IsValid) return View(product);
        db.Update(product);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
