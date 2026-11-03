using System.Security.Claims;
using Inventory.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Web.Controllers;

public class AccountController(AppDbContext db, IPasswordHasher<UserAccount> hasher) : Controller
{
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "اسم المستخدم أو كلمة المرور غير صحيحة");
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("BranchId", user.BranchId?.ToString() ?? "0"),
            new("username", user.Username)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    public IActionResult AccessDenied() => View();

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Users(string? search)
    {
        var query = db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.FullName.Contains(search) || u.Username.Contains(search));
        return View(await query.OrderBy(u => u.FullName).ToListAsync());
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateUser()
    {
        ViewBag.Branches = await db.Branches.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        return View(new UserAccount());
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(UserAccount user, string password)
    {
        ViewBag.Branches = await db.Branches.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        if (await db.Users.AnyAsync(u => u.Username == user.Username))
        {
            ModelState.AddModelError("Username", "اسم المستخدم مستخدم بالفعل");
            return View(user);
        }
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            ModelState.AddModelError(string.Empty, "كلمة المرور يجب ألا تقل عن 6 أحرف");
            return View(user);
        }
        user.PasswordHash = hasher.HashPassword(user, password);
        user.FullName = user.FullName.Trim();
        user.Username = user.Username.Trim();
        if (user.BranchId == 0) user.BranchId = null;
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Users));
    }
}
