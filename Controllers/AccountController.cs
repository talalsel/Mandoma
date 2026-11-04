using System.Security.Claims;
using Inventory.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ChangePassword(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        return View(user);
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(int id, string newPassword, string confirmPassword)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            ModelState.AddModelError(string.Empty, "كلمة المرور يجب ألا تقل عن 6 أحرف");
            return View(user);
        }
        if (newPassword != confirmPassword)
        {
            ModelState.AddModelError(string.Empty, "كلمتا المرور غير متطابقتين");
            return View(user);
        }

        user.PasswordHash = hasher.HashPassword(user, newPassword);
        await db.SaveChangesAsync();
        TempData["Message"] = $"تم تغيير كلمة مرور المستخدم {user.Username} بنجاح";
        return RedirectToAction(nameof(Users));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EditUser(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        ViewBag.Branches = await db.Branches.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        return View(user);
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(int id, UserAccount editedUser)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        editedUser.PasswordHash = user.PasswordHash;
        ModelState.Remove(nameof(UserAccount.PasswordHash));
        if (await db.Users.AnyAsync(x => x.Username == editedUser.Username.Trim() && x.Id != id))
            ModelState.AddModelError("Username", "اسم المستخدم مستخدم بالفعل");
        if (!ModelState.IsValid)
        {
            ViewBag.Branches = await db.Branches.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
            return View(editedUser);
        }

        user.FullName = editedUser.FullName.Trim();
        user.Username = editedUser.Username.Trim();
        user.Role = editedUser.Role;
        user.BranchId = editedUser.BranchId == 0 ? null : editedUser.BranchId;
        user.IsActive = editedUser.IsActive;
        await db.SaveChangesAsync();
        TempData["Message"] = $"تم تعديل بيانات المستخدم {user.Username} بنجاح";
        return RedirectToAction(nameof(Users));
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Backup()
    {
        return View();
    }

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> DownloadBackup()
    {
        var backup = new BackupPackage
        {
            Branches = await db.Branches.AsNoTracking().ToListAsync(),
            Users = await db.Users.AsNoTracking().ToListAsync(),
            Customers = await db.Customers.AsNoTracking().ToListAsync(),
            Employees = await db.Employees.AsNoTracking().ToListAsync(),
            EmployeeAttendances = await db.EmployeeAttendances.AsNoTracking().ToListAsync(),
            EmployeeDeductions = await db.EmployeeDeductions.AsNoTracking().ToListAsync(),
            Products = await db.Products.AsNoTracking().ToListAsync(),
            BranchStocks = await db.BranchStocks.AsNoTracking().ToListAsync(),
            Transfers = await db.Transfers.AsNoTracking().ToListAsync(),
            Sales = await db.Sales.AsNoTracking().ToListAsync(),
            SaleItems = await db.SaleItems.AsNoTracking().ToListAsync(),
            StockMovements = await db.StockMovements.AsNoTracking().ToListAsync(),
            Accounts = await db.Accounts.AsNoTracking().ToListAsync(),
            JournalEntries = await db.JournalEntries.AsNoTracking().ToListAsync(),
            JournalLines = await db.JournalLines.AsNoTracking().ToListAsync(),
            Expenses = await db.Expenses.AsNoTracking().ToListAsync(),
            Purchases = await db.Purchases.AsNoTracking().ToListAsync(),
            CashRegisterSessions = await db.CashRegisterSessions.AsNoTracking().ToListAsync()
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(backup, new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
        });
        var fileName = $"selini-backup-{DateTime.Now:yyyy-MM-dd-HHmm}.json";
        return File(json, "application/json", fileName);
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreBackup(IFormFile backupFile, bool confirmRestore)
    {
        if (!confirmRestore)
        {
            TempData["Error"] = "يجب تأكيد الاستعادة قبل استبدال البيانات الحالية";
            return RedirectToAction(nameof(Backup));
        }
        if (backupFile is null || backupFile.Length == 0)
        {
            TempData["Error"] = "اختر ملف النسخة الاحتياطية أولاً";
            return RedirectToAction(nameof(Backup));
        }
        if (backupFile.Length > 100 * 1024 * 1024)
        {
            TempData["Error"] = "حجم النسخة الاحتياطية أكبر من 100 ميغابايت";
            return RedirectToAction(nameof(Backup));
        }

        BackupPackage? backup;
        try
        {
            await using var stream = backupFile.OpenReadStream();
            backup = await JsonSerializer.DeserializeAsync<BackupPackage>(stream, new JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
            });
        }
        catch
        {
            TempData["Error"] = "ملف النسخة الاحتياطية غير صالح أو تالف";
            return RedirectToAction(nameof(Backup));
        }

        if (backup is null || backup.Version != 1)
        {
            TempData["Error"] = "نسخة النسخة الاحتياطية غير مدعومة";
            return RedirectToAction(nameof(Backup));
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            await db.JournalLines.ExecuteDeleteAsync();
            await db.SaleItems.ExecuteDeleteAsync();
            await db.EmployeeAttendances.ExecuteDeleteAsync();
            await db.EmployeeDeductions.ExecuteDeleteAsync();
            await db.BranchStocks.ExecuteDeleteAsync();
            await db.Transfers.ExecuteDeleteAsync();
            await db.StockMovements.ExecuteDeleteAsync();
            await db.Expenses.ExecuteDeleteAsync();
            await db.Purchases.ExecuteDeleteAsync();
            await db.CashRegisterSessions.ExecuteDeleteAsync();
            await db.JournalEntries.ExecuteDeleteAsync();
            await db.Sales.ExecuteDeleteAsync();
            await db.Products.ExecuteDeleteAsync();
            await db.EmployeeDeductions.ExecuteDeleteAsync();
            await db.EmployeeAttendances.ExecuteDeleteAsync();
            await db.Employees.ExecuteDeleteAsync();
            await db.Accounts.ExecuteDeleteAsync();
            await db.Customers.ExecuteDeleteAsync();
            await db.Users.ExecuteDeleteAsync();
            await db.Branches.ExecuteDeleteAsync();

            db.Branches.AddRange(backup.Branches);
            db.Users.AddRange(backup.Users);
            db.Customers.AddRange(backup.Customers);
            db.Employees.AddRange(backup.Employees);
            db.EmployeeAttendances.AddRange(backup.EmployeeAttendances);
            db.EmployeeDeductions.AddRange(backup.EmployeeDeductions);
            db.Products.AddRange(backup.Products);
            db.BranchStocks.AddRange(backup.BranchStocks);
            db.Transfers.AddRange(backup.Transfers);
            db.Sales.AddRange(backup.Sales);
            db.SaleItems.AddRange(backup.SaleItems);
            db.StockMovements.AddRange(backup.StockMovements);
            db.Accounts.AddRange(backup.Accounts);
            db.JournalEntries.AddRange(backup.JournalEntries);
            db.JournalLines.AddRange(backup.JournalLines);
            db.Expenses.AddRange(backup.Expenses);
            db.Purchases.AddRange(backup.Purchases);
            db.CashRegisterSessions.AddRange(backup.CashRegisterSessions);

            await db.SaveChangesAsync();
            await ResetIdentitySequencesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            TempData["Error"] = "فشلت استعادة النسخة الاحتياطية. لم يتم استبدال البيانات بسبب فشل العملية";
            return RedirectToAction(nameof(Backup));
        }

        TempData["Message"] = $"تمت استعادة النسخة الاحتياطية التي أُنشئت في {backup.CreatedAt:yyyy-MM-dd HH:mm} UTC بنجاح";
        return RedirectToAction(nameof(Logout));
    }

    private async Task ResetIdentitySequencesAsync()
    {
        var tables = new[]
        {
            "Branches", "Users", "Customers", "Employees", "EmployeeAttendances", "EmployeeDeductions",
            "Products", "BranchStocks", "Transfers", "Sales", "SaleItems", "StockMovements", "Accounts",
            "JournalEntries", "JournalLines", "Expenses", "Purchases", "CashRegisterSessions"
        };

        if (db.Database.IsNpgsql())
        {
            foreach (var table in tables)
                await db.Database.ExecuteSqlRawAsync(
                    $"SELECT setval(pg_get_serial_sequence('\"{table}\"', 'Id'), COALESCE((SELECT MAX(\"Id\") FROM \"{table}\"), 1), true)");
        }
        else if (db.Database.IsSqlite())
        {
            foreach (var table in tables)
                await db.Database.ExecuteSqlRawAsync(
                    $"UPDATE sqlite_sequence SET seq = COALESCE((SELECT MAX(Id) FROM \"{table}\"), 0) WHERE name = '{table}'");
        }
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var currentUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId) ? parsedId : 0;
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        if (user.Id == currentUserId)
        {
            TempData["Error"] = "لا يمكن حذف حساب المستخدم المسجل حاليًا";
            return RedirectToAction(nameof(Users));
        }

        var linkedEmployee = await db.Employees.FirstOrDefaultAsync(x => x.UserAccountId == id);
        if (linkedEmployee is not null) linkedEmployee.UserAccountId = null;
        user.IsActive = false;
        await db.SaveChangesAsync();
        TempData["Message"] = $"تم حذف وتعطيل حساب المستخدم {user.Username}";
        return RedirectToAction(nameof(Users));
    }
}
