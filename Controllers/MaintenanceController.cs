using BUA_project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BUA_project.Controllers
{
    [Authorize(Roles = "Admin")]
    public class MaintenanceController : Controller
    {
        private readonly Entity _context = new Entity();

        // GET: Maintenance
        public async Task<IActionResult> Index()
        {
            var maintenances = await _context.Set<VehicleMaintenance>()
                .Include(m => m.Vehicle)
                .Include(m => m.CreatedByUser)
                .OrderByDescending(m => m.ServiceDate)
                .ToListAsync();

            return View(maintenances);
        }

        // GET: Maintenance/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var maintenance = await _context.Set<VehicleMaintenance>()
                .Include(m => m.Vehicle)
                .Include(m => m.CreatedByUser)
                .FirstOrDefaultAsync(m =>
                    m.VehicleMaintenanceId == id);

            if (maintenance == null)
                return NotFound();

            return View(maintenance);
        }

        // GET: Maintenance/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Vehicles = await _context.Vehicles
                .OrderBy(v => v.Brand)
                .ThenBy(v => v.Model)
                .ToListAsync();

            return View();
        }

        // POST: Maintenance/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(VehicleMaintenance maintenance)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Vehicles = await _context.Vehicles
                    .OrderBy(v => v.Brand)
                    .ThenBy(v => v.Model)
                    .ToListAsync();

                return View(maintenance);
            }

            maintenance.CreatedAt = DateTime.Now;

            _context.Set<VehicleMaintenance>().Add(maintenance);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: Maintenance/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var maintenance = await _context.Set<VehicleMaintenance>()
                .FirstOrDefaultAsync(m =>
                    m.VehicleMaintenanceId == id);

            if (maintenance == null)
                return NotFound();

            ViewBag.Vehicles = await _context.Vehicles
                .OrderBy(v => v.Brand)
                .ThenBy(v => v.Model)
                .ToListAsync();

            return View(maintenance);
        }

        // POST: Maintenance/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            VehicleMaintenance maintenance)
        {
            if (id != maintenance.VehicleMaintenanceId)
                return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Vehicles = await _context.Vehicles
                    .OrderBy(v => v.Brand)
                    .ThenBy(v => v.Model)
                    .ToListAsync();

                return View(maintenance);
            }

            try
            {
                _context.Set<VehicleMaintenance>().Update(maintenance);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MaintenanceExists(maintenance.VehicleMaintenanceId))
                    return NotFound();

                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Maintenance/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var maintenance = await _context.Set<VehicleMaintenance>()
                .Include(m => m.Vehicle)
                .FirstOrDefaultAsync(m =>
                    m.VehicleMaintenanceId == id);

            if (maintenance == null)
                return NotFound();

            return View(maintenance);
        }

        // POST: Maintenance/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var maintenance = await _context.Set<VehicleMaintenance>()
                .FirstOrDefaultAsync(m =>
                    m.VehicleMaintenanceId == id);

            if (maintenance == null)
                return NotFound();

            _context.Set<VehicleMaintenance>().Remove(maintenance);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool MaintenanceExists(int id)
        {
            return _context.Set<VehicleMaintenance>()
                .Any(m => m.VehicleMaintenanceId == id);
        }
    }
}