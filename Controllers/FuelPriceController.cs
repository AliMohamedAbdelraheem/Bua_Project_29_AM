using BUA_project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BUA_project.Models.ViewModels;

namespace BUA_project.Controllers
{
    [Authorize(Roles = "Admin")]
    public class FuelPriceController : Controller
    {
        private readonly Entity _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public FuelPriceController(
            Entity context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: FuelPrice
        public async Task<IActionResult> Index()
        {
            var fuelPrices = await _context.FuelPrices
                .Include(f => f.CreatedByUser)
                .OrderBy(f => f.FuelType)
                .ThenByDescending(f => f.EffectiveDate)
                .ToListAsync();

            return View(fuelPrices);
        }

        // GET: FuelPrice/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var fuelTypes = await _context.Vehicles
                .Where(v => !string.IsNullOrEmpty(v.FuelType))
                .Select(v => v.FuelType)
                .Distinct()
                .OrderBy(f => f)
                .ToListAsync();

            var model = new FuelPriceCreateViewModel
            {
                EffectiveDate = DateTime.Now,
                FuelTypes = fuelTypes
            };

            return View(model);
        }

        // POST: FuelPrice/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FuelPrice model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var identityAdmin =
                await _userManager.GetUserAsync(User);

            if (identityAdmin == null ||
                identityAdmin.BusinessUserId == null)
            {
                return Forbid();
            }

            var fuelPrice = new FuelPrice
            {
                FuelType = model.FuelType,
                PricePerLiter = model.PricePerLiter,
                EffectiveDate = model.EffectiveDate,
                CreatedAt = DateTime.Now,
                CreatedByUserId = identityAdmin.BusinessUserId
            };

            _context.FuelPrices.Add(fuelPrice);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}