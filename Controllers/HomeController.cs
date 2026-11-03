using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Inventory.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Web.Controllers;

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
