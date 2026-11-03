using System.Security.Claims;
using Inventory.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Web.Controllers;

[Authorize(Roles = "Admin,Accountant,Personnel")]
public class PersonnelController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? search)
    {
        var query = db.Employees.Include(x => x.Branch).Where(x => x.IsActive).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.FullName.Contains(search) || x.EmployeeNumber.Contains(search) || x.JobTitle.Contains(search));
        ViewBag.TotalEmployees = await db.Employees.CountAsync(x => x.IsActive);
        ViewBag.PresentToday = await db.EmployeeAttendances.CountAsync(x => x.Date == DateTime.Today && x.Status == AttendanceStatus.Present);
        ViewBag.MonthDeductions = await db.EmployeeDeductions.Where(x => x.DeductionDate.Year == DateTime.Today.Year && x.DeductionDate.Month == DateTime.Today.Month).SumAsync(x => (decimal?)x.Amount) ?? 0;
        return View(await query.OrderBy(x => x.FullName).ToListAsync());
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Branches = await db.Branches.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        ViewBag.Users = await db.Users.Where(x => x.IsActive).OrderBy(x => x.FullName).ToListAsync();
        return View(new Employee { HireDate = DateTime.Today, EmployeeNumber = await NextEmployeeNumberAsync() });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Employee employee)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Accountant")) employee.BaseSalary = 0;
        employee.EmployeeNumber = employee.EmployeeNumber.Trim();
        employee.FullName = employee.FullName.Trim();
        if (employee.BranchId == 0) employee.BranchId = null;
        if (employee.UserAccountId == 0) employee.UserAccountId = null;
        if (await db.Employees.AnyAsync(x => x.EmployeeNumber == employee.EmployeeNumber))
            ModelState.AddModelError("EmployeeNumber", "رقم الموظف مستخدم بالفعل");
        if (!ModelState.IsValid)
        {
            ViewBag.Branches = await db.Branches.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
            ViewBag.Users = await db.Users.Where(x => x.IsActive).OrderBy(x => x.FullName).ToListAsync();
            return View(employee);
        }
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        TempData["Message"] = "تم إضافة الموظف بنجاح";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var employee = await db.Employees.FindAsync(id);
        if (employee is null) return NotFound();
        ViewBag.Branches = await db.Branches.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        ViewBag.Users = await db.Users.Where(x => x.IsActive).OrderBy(x => x.FullName).ToListAsync();
        ViewBag.CanEditSalary = User.IsInRole("Admin") || User.IsInRole("Accountant");
        return View(employee);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Employee employee)
    {
        var current = await db.Employees.FindAsync(id);
        if (current is null) return NotFound();
        if (await db.Employees.AnyAsync(x => x.EmployeeNumber == employee.EmployeeNumber && x.Id != id))
            ModelState.AddModelError("EmployeeNumber", "رقم الموظف مستخدم بالفعل");
        if (!ModelState.IsValid)
        {
            ViewBag.Branches = await db.Branches.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
            ViewBag.Users = await db.Users.Where(x => x.IsActive).OrderBy(x => x.FullName).ToListAsync();
            ViewBag.CanEditSalary = User.IsInRole("Admin") || User.IsInRole("Accountant");
            return View(employee);
        }

        current.EmployeeNumber = employee.EmployeeNumber.Trim();
        current.FullName = employee.FullName.Trim();
        current.NationalId = employee.NationalId?.Trim() ?? "";
        current.Phone = employee.Phone?.Trim() ?? "";
        current.Email = employee.Email?.Trim() ?? "";
        current.JobTitle = employee.JobTitle?.Trim() ?? "";
        current.Department = employee.Department?.Trim() ?? "";
        current.HireDate = employee.HireDate;
        current.BankAccount = employee.BankAccount?.Trim() ?? "";
        current.BranchId = employee.BranchId == 0 ? null : employee.BranchId;
        current.UserAccountId = employee.UserAccountId == 0 ? null : employee.UserAccountId;
        current.IsActive = employee.IsActive;
        if (User.IsInRole("Admin") || User.IsInRole("Accountant")) current.BaseSalary = employee.BaseSalary;
        await db.SaveChangesAsync();
        TempData["Message"] = "تم حفظ بيانات الموظف بنجاح";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Attendance(int? employeeId, DateTime? date)
    {
        var selectedDate = date?.Date ?? DateTime.Today;
        ViewBag.Date = selectedDate.ToString("yyyy-MM-dd");
        ViewBag.Employees = await db.Employees.Include(x => x.Branch).Where(x => x.IsActive).OrderBy(x => x.FullName).ToListAsync();
        var query = db.EmployeeAttendances.Include(x => x.Employee).Include(x => x.Branch)
            .Where(x => x.Date == selectedDate);
        if (employeeId.HasValue) query = query.Where(x => x.EmployeeId == employeeId.Value);
        ViewBag.SelectedEmployeeId = employeeId;
        return View(await query.OrderBy(x => x.Employee!.FullName).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAttendance(int employeeId, DateTime date, AttendanceStatus status, string? checkIn, string? checkOut, string? notes)
    {
        var employee = await db.Employees.Include(x => x.Branch).FirstOrDefaultAsync(x => x.Id == employeeId);
        if (employee is null) return NotFound();
        var row = await db.EmployeeAttendances.FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.Date == date.Date);
        if (row is null) row = new EmployeeAttendance { EmployeeId = employeeId, Date = date.Date, RecordedByUserId = CurrentUserId() };
        row.Status = status;
        row.Notes = notes?.Trim() ?? "";
        row.BranchId = employee.BranchId;
        row.CheckIn = TimeSpan.TryParse(checkIn, out var enteredIn) ? enteredIn : null;
        row.CheckOut = TimeSpan.TryParse(checkOut, out var enteredOut) ? enteredOut : null;
        if (row.Id == 0) db.EmployeeAttendances.Add(row);
        await db.SaveChangesAsync();
        TempData["Message"] = "تم تسجيل الحضور والغياب بنجاح";
        return RedirectToAction(nameof(Attendance), new { employeeId, date = date.ToString("yyyy-MM-dd") });
    }

    [Authorize(Roles = "Admin,Accountant,Personnel")]
    public async Task<IActionResult> Deductions(int? employeeId)
    {
        ViewBag.Employees = await db.Employees.Where(x => x.IsActive).OrderBy(x => x.FullName).ToListAsync();
        ViewBag.SelectedEmployeeId = employeeId;
        ViewBag.CanManage = true;
        var query = db.EmployeeDeductions.Include(x => x.Employee).AsQueryable();
        if (employeeId.HasValue) query = query.Where(x => x.EmployeeId == employeeId.Value);
        return View(await query.OrderByDescending(x => x.DeductionDate).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDeduction(int employeeId, DateTime deductionDate, decimal amount, string reason)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(reason))
        {
            TempData["Error"] = "أدخل مبلغ الخصم والسبب بشكل صحيح";
            return RedirectToAction(nameof(Deductions), new { employeeId });
        }
        db.EmployeeDeductions.Add(new EmployeeDeduction { EmployeeId = employeeId, DeductionDate = deductionDate.Date, Amount = amount, Reason = reason.Trim(), CreatedByUserId = CurrentUserId() });
        await db.SaveChangesAsync();
        TempData["Message"] = "تم إضافة الخصم وربطه ببيانات الموظف";
        return RedirectToAction(nameof(Deductions), new { employeeId });
    }

    private int CurrentUserId()
    {
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    }

    private async Task<string> NextEmployeeNumberAsync()
    {
        var last = await db.Employees.OrderByDescending(x => x.Id).Select(x => x.EmployeeNumber).FirstOrDefaultAsync();
        return int.TryParse(last, out var number) ? (number + 1).ToString("D4") : "0001";
    }
}
