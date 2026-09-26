using BUA_project.Models;
using BUA_project.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BUA_project.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly Entity _context;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            Entity context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }


        // =========================================================
        // LOGIN - GET
        // =========================================================

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }


        // =========================================================
        // REGISTER - GET
        // =========================================================

        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }


        // =========================================================
        // LOGIN - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);


            var result = await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: false);


            if (result.Succeeded)
            {
                var user =
                    await _userManager.FindByEmailAsync(model.Email);


                if (user == null)
                    return RedirectToAction(
                        "Login",
                        "Account");


                var roles =
                    await _userManager.GetRolesAsync(user);


                if (roles.Contains("User"))
                {
                    return RedirectToAction(
                        "Index",
                        "UserDashboard");
                }


                if (roles.Contains("Dispatcher"))
                {
                    return RedirectToAction(
                        "Index",
                        "DispatcherDashboard");
                }


                if (roles.Contains("Driver"))
                {
                    return RedirectToAction(
                        "Index",
                        "DriverDashboard");
                }


                if (roles.Contains("Admin"))
                {
                    return RedirectToAction(
                        "Index",
                        "AdminDashboard");
                }


                await _signInManager.SignOutAsync();


                ModelState.AddModelError(
                    "",
                    "No valid role assigned to this account.");


                return View(model);
            }


            ModelState.AddModelError(
                "",
                "Invalid email or password.");


            return View(model);
        }


        // =========================================================
        // REGISTER - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            RegisterViewModel model)
        {
            // =====================================================
            // Account Type Selection
            // =====================================================

            // The first buttons only select the account type.
            // They do not create an account yet.

            if (string.IsNullOrEmpty(model.Name) &&
                string.IsNullOrEmpty(model.Email) &&
                string.IsNullOrEmpty(model.Password))
            {
                return View(model);
            }


            // =====================================================
            // Validate Account Type
            // =====================================================

            if (model.AccountType != "User" &&
                model.AccountType != "Driver")
            {
                ModelState.AddModelError(
                    "AccountType",
                    "Please select a valid account type.");

                return View(model);
            }


            // =====================================================
            // Driver Validation
            // =====================================================

            if (model.AccountType == "Driver")
            {
                if (string.IsNullOrWhiteSpace(
                    model.LicenseNumber))
                {
                    ModelState.AddModelError(
                        "LicenseNumber",
                        "License Number is required.");
                }


                if (string.IsNullOrWhiteSpace(
                    model.QualificationStatus))
                {
                    ModelState.AddModelError(
                        "QualificationStatus",
                        "Qualification Status is required.");
                }


                if (!model.QualificationValidUntil.HasValue)
                {
                    ModelState.AddModelError(
                        "QualificationValidUntil",
                        "Qualification Valid Until is required.");
                }
            }


            // =====================================================
            // Model Validation
            // =====================================================

            if (!ModelState.IsValid)
                return View(model);


            // =====================================================
            // Check Identity Email
            // =====================================================

            var existingIdentityUser =
                await _userManager.FindByEmailAsync(
                    model.Email);


            if (existingIdentityUser != null)
            {
                ModelState.AddModelError(
                    "Email",
                    "This email is already registered.");

                return View(model);
            }


            // =====================================================
            // Check Business User Email
            // =====================================================

            var existingBusinessUser =
                await _context.Users
                    .FirstOrDefaultAsync(
                        u => u.Email == model.Email);


            if (existingBusinessUser != null)
            {
                ModelState.AddModelError(
                    "Email",
                    "This email is already registered.");

                return View(model);
            }


            // =====================================================
            // Database Transaction
            // =====================================================

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();


            try
            {
                // =================================================
                // 1. Create Business User
                // =================================================

                var businessUser = new User
                {
                    Name = model.Name,

                    Email = model.Email,

                    // Authentication is handled
                    // by ASP.NET Identity.

                    Role =
                        model.AccountType == "Driver"
                            ? "Driver"
                            : "User"
                };


                _context.Users.Add(businessUser);

                await _context.SaveChangesAsync();


                // =================================================
                // 2. Create Identity User
                // =================================================

                var applicationUser =
                    new ApplicationUser
                    {
                        UserName = model.Email,

                        Email = model.Email,

                        BusinessUserId =
                            businessUser.UserId
                    };


                var identityResult =
                    await _userManager.CreateAsync(
                        applicationUser,
                        model.Password);


                if (!identityResult.Succeeded)
                {
                    foreach (var error
                        in identityResult.Errors)
                    {
                        ModelState.AddModelError(
                            "Password",
                            error.Description);
                    }


                    await transaction.RollbackAsync();

                    return View(model);
                }


                // =================================================
                // 3. Create Identity Role
                // =================================================

                var roleName =
                    model.AccountType == "Driver"
                        ? "Driver"
                        : "User";


                if (!await _roleManager
                    .RoleExistsAsync(roleName))
                {
                    var roleResult =
                        await _roleManager.CreateAsync(
                            new IdentityRole(roleName));


                    if (!roleResult.Succeeded)
                    {
                        foreach (var error
                            in roleResult.Errors)
                        {
                            ModelState.AddModelError(
                                "",
                                error.Description);
                        }


                        await transaction
                            .RollbackAsync();

                        return View(model);
                    }
                }


                // =================================================
                // 4. Assign Identity Role
                // =================================================

                var addToRoleResult =
                    await _userManager.AddToRoleAsync(
                        applicationUser,
                        roleName);


                if (!addToRoleResult.Succeeded)
                {
                    foreach (var error
                        in addToRoleResult.Errors)
                    {
                        ModelState.AddModelError(
                            "",
                            error.Description);
                    }


                    await transaction.RollbackAsync();

                    return View(model);
                }


                // =================================================
                // 5. Create Driver Record
                // =================================================

                if (model.AccountType == "Driver")
                {
                    var driver = new Driver
                    {
                        Name = model.Name,

                        LicenseNumber =
                            model.LicenseNumber!,

                        QualificationStatus =
                            model.QualificationStatus!,

                        QualificationValidUntil =
                            model.QualificationValidUntil!.Value,

                        UserId =
                            businessUser.UserId
                    };


                    _context.Drivers.Add(driver);

                    await _context.SaveChangesAsync();
                }


                // =================================================
                // 6. Commit Transaction
                // =================================================

                await transaction.CommitAsync();


                // =================================================
                // 7. Login Automatically
                // =================================================

                await _signInManager.SignInAsync(
                    applicationUser,
                    isPersistent: false);


                // =================================================
                // 8. Redirect According To Account Type
                // =================================================

                if (model.AccountType == "Driver")
                {
                    return RedirectToAction(
                        "Index",
                        "DriverDashboard");
                }


                return RedirectToAction(
                    "Index",
                    "UserDashboard");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();


                ModelState.AddModelError(
                    "",
                    ex.InnerException?.Message
                    ?? ex.Message);


                return View(model);
            }
        }


        // =========================================================
        // LOGOUT
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            return RedirectToAction(
                "Login",
                "Account");
        }
    }
}