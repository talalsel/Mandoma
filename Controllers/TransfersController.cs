using Inventory.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Inventory.Web.Controllers;

[Authorize(Roles = "Admin,StockOnly,Accountant")]
public class TransfersController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var transfers = await db.Transfers.Include(x => x.FromBranch).Include(x => x.ToBranch).Include(x => x.Product).OrderByDescending(x => x.CreatedAt).ToListAsync();
        return View(transfers);
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Branches = await db.Branches.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        ViewBag.Products = await db.Products.OrderBy(x => x.Name).ToListAsync();
        return View(new Transfer());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Transfer transfer)
    {
        if (transfer.FromBranchId == transfer.ToBranchId) { ModelState.AddModelError("", "يجب اختيار معرضين مختلفين"); }
        if (transfer.Quantity <= 0) { ModelState.AddModelError("", "الكمية يجب أن تكون أكبر من صفر"); }
        if (!ModelState.IsValid)
        {
            ViewBag.Branches = await db.Branches.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
            ViewBag.Products = await db.Products.OrderBy(x => x.Name).ToListAsync();
            return View(transfer);
        }

        var product = await db.Products.FindAsync(transfer.ProductId);
        if (product == null || product.Quantity < transfer.Quantity) { ModelState.AddModelError("", "الكمية غير متوفرة في المعرض المصدر"); ViewBag.Branches = await db.Branches.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(); ViewBag.Products = await db.Products.OrderBy(x => x.Name).ToListAsync(); return View(transfer); }

        product.Quantity -= transfer.Quantity;
        transfer.UserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        transfer.Status = "Pending";
        db.Transfers.Add(transfer);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
