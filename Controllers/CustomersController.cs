using Inventory.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Web.Controllers;

[Authorize]
public class CustomersController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? search)
    {
        var query = db.Customers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search) || x.Phone.Contains(search));
        return View(await query.OrderBy(x => x.Name).ToListAsync());
    }

    public IActionResult Create() => View(new Customer());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer customer)
    {
        if (!ModelState.IsValid) return View(customer);
        customer.Name = customer.Name.Trim(); customer.Phone = customer.Phone?.Trim() ?? ""; customer.Address = customer.Address?.Trim() ?? "";
        db.Customers.Add(customer); await db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var customer = await db.Customers.FindAsync(id); return customer == null ? NotFound() : View(customer);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Customer customer)
    {
        if (id != customer.Id) return NotFound(); if (!ModelState.IsValid) return View(customer);
        db.Update(customer); await db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await db.Customers.FindAsync(id); if (customer == null) return NotFound(); db.Customers.Remove(customer); await db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }
}
