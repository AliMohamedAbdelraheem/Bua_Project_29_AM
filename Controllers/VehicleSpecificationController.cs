using BUA_project.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace BUA_project.Controllers
{
    [Authorize(Roles = "Admin")]
    public class VehicleSpecificationController : Controller
    {
        private readonly Entity _context = new Entity();

        // GET: VehicleSpecification
        public async Task<IActionResult> Index()
        {
            var specifications = await _context.VehicleSpecifications
                .Include(vs => vs.Vehicles)
                .OrderBy(vs => vs.VehicleSpecificationId)
                .ToListAsync();

            return View(specifications);
        }

        // GET: VehicleSpecification/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var specification = await _context.VehicleSpecifications
                .Include(vs => vs.Vehicles)
                .FirstOrDefaultAsync(
                    vs => vs.VehicleSpecificationId == id);

            if (specification == null)
                return NotFound();

            return View(specification);
        }

        // GET: VehicleSpecification/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: VehicleSpecification/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            VehicleSpecification specification)
        {
            if (!ModelState.IsValid)
            {
                return View(specification);
            }

            _context.VehicleSpecifications.Add(specification);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: VehicleSpecification/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var specification = await _context.VehicleSpecifications
                .FirstOrDefaultAsync(
                    vs => vs.VehicleSpecificationId == id);

            if (specification == null)
                return NotFound();

            return View(specification);
        }

        // POST: VehicleSpecification/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            VehicleSpecification specification)
        {
            if (id != specification.VehicleSpecificationId)
                return NotFound();

            if (!ModelState.IsValid)
            {
                return View(specification);
            }

            try
            {
                _context.VehicleSpecifications.Update(specification);

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!VehicleSpecificationExists(
                    specification.VehicleSpecificationId))
                {
                    return NotFound();
                }

                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: VehicleSpecification/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var specification = await _context.VehicleSpecifications
                .Include(vs => vs.Vehicles)
                .FirstOrDefaultAsync(
                    vs => vs.VehicleSpecificationId == id);

            if (specification == null)
                return NotFound();

            return View(specification);
        }

        // POST: VehicleSpecification/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var specification = await _context.VehicleSpecifications
                .Include(vs => vs.Vehicles)
                .FirstOrDefaultAsync(
                    vs => vs.VehicleSpecificationId == id);

            if (specification == null)
                return NotFound();

            if (specification.Vehicles != null &&
                specification.Vehicles.Any())
            {
                TempData["Error"] =
                    "This specification cannot be deleted because it is assigned to one or more vehicles.";

                return RedirectToAction(nameof(Index));
            }

            _context.VehicleSpecifications.Remove(specification);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool VehicleSpecificationExists(int id)
        {
            return _context.VehicleSpecifications
                .Any(vs =>
                    vs.VehicleSpecificationId == id);
        }
    }
}