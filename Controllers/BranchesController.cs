using Inventory.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Web.Controllers;

[Authorize(Roles = "Admin")]
public class BranchesController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index() => View(await db.Branches.OrderBy(x => x.Name).ToListAsync());

    public IActionResult Create() => View(new Branch());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Branch branch)
    {
        if (!ModelState.IsValid) return View(branch);
        if (await db.Branches.AnyAsync(x => x.Code == branch.Code)) { ModelState.AddModelError("Code", "رمز المعرض مستخدم بالفعل"); return View(branch); }
        db.Branches.Add(branch); await db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var branch = await db.Branches.FindAsync(id); return branch == null ? NotFound() : View(branch);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Branch branch)
    {
        if (id != branch.Id) return NotFound(); if (!ModelState.IsValid) return View(branch);
        db.Update(branch); await db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }
}
