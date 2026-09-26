using BUA_project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BUA_project.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly Entity _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserController(
            Entity context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // =========================================================
        // INDEX
        // =========================================================

        public async Task<IActionResult> Index()
        {
            var users = await _context.Users
                .OrderBy(u => u.Name)
                .ToListAsync();

            return View(users);
        }

        // =========================================================
        // DETAILS
        // =========================================================

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var user = await _context.Users
                .Include(u => u.Reservations)
                .Include(u => u.Driver)
                .FirstOrDefaultAsync(
                    u => u.UserId == id);

            if (user == null)
                return NotFound();

            return View(user);
        }

        // =========================================================
        // MANAGE - GET
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Manage(int? id)
        {
            if (id == null)
                return NotFound();

            var user = await _context.Users
                .Include(u => u.Driver)
                .FirstOrDefaultAsync(
                    u => u.UserId == id);

            if (user == null)
                return NotFound();

            return View(user);
        }

        // =========================================================
        // MANAGE - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manage(
            int UserId,
            string NewRole,
            string? LicenseNumber,
            string? QualificationStatus,
            DateTime? QualificationValidUntil)
        {
            // -----------------------------------------------------
            // Validate selected role
            // -----------------------------------------------------

            var allowedRoles = new[]
            {
                "Requester",
                "Dispatcher",
                "Driver",
                "Admin"
            };

            if (!allowedRoles.Contains(NewRole))
            {
                TempData["ManageError"] =
                    "Invalid role selected.";

                return RedirectToAction(
                    nameof(Manage),
                    new { id = UserId });
            }

            // -----------------------------------------------------
            // Get business user
            // -----------------------------------------------------

            var user = await _context.Users
                .Include(u => u.Driver)
                .FirstOrDefaultAsync(
                    u => u.UserId == UserId);

            if (user == null)
                return NotFound();

            // -----------------------------------------------------
            // Get Identity user
            // -----------------------------------------------------

            var identityUser = await _userManager.Users
                .FirstOrDefaultAsync(
                    u => u.BusinessUserId == UserId);

            if (identityUser == null)
            {
                TempData["ManageError"] =
                    "This user does not have a linked login account.";

                return RedirectToAction(
                    nameof(Manage),
                    new { id = UserId });
            }

            // -----------------------------------------------------
            // Driver validation
            // -----------------------------------------------------

            if (NewRole == "Driver")
            {
                if (string.IsNullOrWhiteSpace(LicenseNumber))
                {
                    TempData["ManageError"] =
                        "License Number is required for a Driver.";

                    return RedirectToAction(
                        nameof(Manage),
                        new { id = UserId });
                }

                if (string.IsNullOrWhiteSpace(QualificationStatus))
                {
                    TempData["ManageError"] =
                        "Qualification Status is required for a Driver.";

                    return RedirectToAction(
                        nameof(Manage),
                        new { id = UserId });
                }

                if (!QualificationValidUntil.HasValue)
                {
                    TempData["ManageError"] =
                        "Qualification Valid Until is required for a Driver.";

                    return RedirectToAction(
                        nameof(Manage),
                        new { id = UserId });
                }
            }

            // -----------------------------------------------------
            // Map UI role to actual stored role
            // -----------------------------------------------------

            string businessRole;
            string identityRole;

            switch (NewRole)
            {
                case "Requester":
                    businessRole = "User";
                    identityRole = "User";
                    break;

                case "Dispatcher":
                    businessRole = "Dispatcher";
                    identityRole = "Dispatcher";
                    break;

                case "Driver":
                    businessRole = "Driver";
                    identityRole = "Driver";
                    break;

                case "Admin":
                    businessRole = "Admin";
                    identityRole = "Admin";
                    break;

                default:
                    return BadRequest();
            }

            // -----------------------------------------------------
            // Make sure Identity role exists
            // -----------------------------------------------------

            if (!await _roleManager.RoleExistsAsync(identityRole))
            {
                var roleResult = await _roleManager.CreateAsync(
                    new IdentityRole(identityRole));

                if (!roleResult.Succeeded)
                {
                    TempData["ManageError"] =
                        "Failed to create the selected system role.";

                    return RedirectToAction(
                        nameof(Manage),
                        new { id = UserId });
                }
            }

            // -----------------------------------------------------
            // Start transaction
            // -----------------------------------------------------

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                // -------------------------------------------------
                // Remove current Identity roles
                // -------------------------------------------------

                var currentRoles =
                    await _userManager.GetRolesAsync(identityUser);

                if (currentRoles.Count > 0)
                {
                    var removeResult =
                        await _userManager.RemoveFromRolesAsync(
                            identityUser,
                            currentRoles);

                    if (!removeResult.Succeeded)
                    {
                        foreach (var error in removeResult.Errors)
                        {
                            ModelState.AddModelError(
                                "",
                                error.Description);
                        }

                        await transaction.RollbackAsync();

                        return RedirectToAction(
                            nameof(Manage),
                            new { id = UserId });
                    }
                }

                // -------------------------------------------------
                // Add new Identity role
                // -------------------------------------------------

                var addRoleResult =
                    await _userManager.AddToRoleAsync(
                        identityUser,
                        identityRole);

                if (!addRoleResult.Succeeded)
                {
                    foreach (var error in addRoleResult.Errors)
                    {
                        ModelState.AddModelError(
                            "",
                            error.Description);
                    }

                    await transaction.RollbackAsync();

                    return RedirectToAction(
                        nameof(Manage),
                        new { id = UserId });
                }

                // -------------------------------------------------
                // Update business User role
                // -------------------------------------------------

                user.Role = businessRole;

                // -------------------------------------------------
                // Driver handling
                // -------------------------------------------------

                if (NewRole == "Driver")
                {
                    if (user.Driver == null)
                    {
                        user.Driver = new Driver
                        {
                            Name = user.Name,
                            LicenseNumber = LicenseNumber!,
                            QualificationStatus =
                                QualificationStatus!,
                            QualificationValidUntil =
                                QualificationValidUntil.Value,
                            UserId = user.UserId
                        };

                        _context.Drivers.Add(user.Driver);
                    }
                    else
                    {
                        user.Driver.Name = user.Name;

                        user.Driver.LicenseNumber =
                            LicenseNumber!;

                        user.Driver.QualificationStatus =
                            QualificationStatus!;

                        user.Driver.QualificationValidUntil =
                            QualificationValidUntil.Value;
                    }
                }

                // -------------------------------------------------
                // Save business changes
                // -------------------------------------------------

                await _context.SaveChangesAsync();

                // -------------------------------------------------
                // Commit everything
                // -------------------------------------------------

                await transaction.CommitAsync();

                TempData["ManageSuccess"] =
                    "User role updated successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                TempData["ManageError"] =
                    ex.InnerException?.Message ?? ex.Message;

                return RedirectToAction(
                    nameof(Manage),
                    new { id = UserId });
            }
        }

        // =========================================================
        // CREATE - GET
        // =========================================================

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // =========================================================
        // CREATE - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User user)
        {
            if (!ModelState.IsValid)
                return View(user);

            var emailExists = await _context.Users
                .AnyAsync(u => u.Email == user.Email);

            if (emailExists)
            {
                ModelState.AddModelError(
                    "Email",
                    "This email is already registered.");

                return View(user);
            }

            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // EDIT - GET
        // =========================================================

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var user = await _context.Users
                .FirstOrDefaultAsync(
                    u => u.UserId == id);

            if (user == null)
                return NotFound();

            return View(user);
        }

        // =========================================================
        // EDIT - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            User user)
        {
            if (id != user.UserId)
                return NotFound();

            if (!ModelState.IsValid)
                return View(user);

            var emailExists = await _context.Users
                .AnyAsync(u =>
                    u.Email == user.Email &&
                    u.UserId != user.UserId);

            if (emailExists)
            {
                ModelState.AddModelError(
                    "Email",
                    "This email is already registered.");

                return View(user);
            }

            try
            {
                _context.Users.Update(user);

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(user.UserId))
                    return NotFound();

                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // DELETE - GET
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var user = await _context.Users
                .FirstOrDefaultAsync(
                    u => u.UserId == id);

            if (user == null)
                return NotFound();

            return View(user);
        }

        // =========================================================
        // DELETE - POST
        // =========================================================

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(
                    u => u.UserId == id);

            if (user == null)
                return NotFound();

            // -----------------------------------------------------
            // Find linked Identity account
            // -----------------------------------------------------

            var identityUser = await _userManager.Users
                .FirstOrDefaultAsync(
                    u => u.BusinessUserId == id);

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                // -------------------------------------------------
                // Delete Identity account first
                // -------------------------------------------------

                if (identityUser != null)
                {
                    var identityDeleteResult =
                        await _userManager.DeleteAsync(
                            identityUser);

                    if (!identityDeleteResult.Succeeded)
                    {
                        var errors = string.Join(
                            ", ",
                            identityDeleteResult.Errors
                                .Select(e => e.Description));

                        throw new Exception(
                            $"Identity account could not be deleted: {errors}");
                    }
                }

                // -------------------------------------------------
                // Delete business user
                // -------------------------------------------------

                _context.Users.Remove(user);

                await _context.SaveChangesAsync();

                // -------------------------------------------------
                // Commit
                // -------------------------------------------------

                await transaction.CommitAsync();

                TempData["DeleteSuccess"] =
                    "User deleted successfully.";

                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();

                TempData["DeleteError"] =
                    "This user cannot be deleted because related records depend on this user.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                TempData["DeleteError"] =
                    ex.InnerException?.Message ?? ex.Message;

                return RedirectToAction(nameof(Index));
            }
        }

        // =========================================================
        // USER EXISTS
        // =========================================================

        private bool UserExists(int id)
        {
            return _context.Users
                .Any(u => u.UserId == id);
        }
    }
}
