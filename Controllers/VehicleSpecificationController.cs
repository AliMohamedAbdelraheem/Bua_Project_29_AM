using BUA_project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BUA_project.Controllers
{
    [Authorize(Roles = "Admin")]
    public class VehicleSpecificationController : Controller
    {
        private readonly Entity _context;

        public VehicleSpecificationController(Entity context)
        {
            _context = context;
        }

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
            Console.WriteLine("========== CREATE VEHICLE SPECIFICATION ==========");

            Console.WriteLine(
                $"NominalLPer100Km: {specification.NominalLPer100Km}");

            Console.WriteLine(
                $"TankCapacity: {specification.TankCapacity}");

            Console.WriteLine(
                $"Accessibility: {specification.Accessibility}");

            Console.WriteLine(
                $"Transmission: {specification.Transmission}");

            Console.WriteLine(
                $"AllowedLoad: {specification.AllowedLoad}");

            Console.WriteLine(
                $"ModelState Valid: {ModelState.IsValid}");

            foreach (var state in ModelState)
            {
                foreach (var error in state.Value.Errors)
                {
                    Console.WriteLine(
                        $"MODEL ERROR [{state.Key}]: {error.ErrorMessage}");

                    if (error.Exception != null)
                    {
                        Console.WriteLine(
                            $"EXCEPTION: {error.Exception.Message}");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                return View(specification);
            }

            _context.VehicleSpecifications.Add(specification);

            Console.WriteLine("Calling SaveChangesAsync...");

            await _context.SaveChangesAsync();

            Console.WriteLine(
                "Vehicle Specification Created Successfully!");

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
    int id,
    VehicleSpecification specification)
        {
            // Make sure the ID from the URL matches the ID from the form
            if (id != specification.VehicleSpecificationId)
                return NotFound();

            // Validate the submitted form data
            if (!ModelState.IsValid)
                return View(specification);

            // Get the existing entity from the database
            // EF Core will track this entity
            var existingSpecification =
                await _context.VehicleSpecifications
                    .FirstOrDefaultAsync(
                        vs => vs.VehicleSpecificationId == id);

            if (existingSpecification == null)
                return NotFound();

            // Update only the properties that the Admin is allowed to edit
            existingSpecification.NominalLPer100Km =
                specification.NominalLPer100Km;

            existingSpecification.TankCapacity =
                specification.TankCapacity;

            existingSpecification.Accessibility =
                specification.Accessibility;

            existingSpecification.Transmission =
                specification.Transmission;

            existingSpecification.AllowedLoad =
                specification.AllowedLoad;

            // EF Core already tracks existingSpecification,
            // so no Update() call is needed
            await _context.SaveChangesAsync();

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
        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var specification =
                await _context.VehicleSpecifications
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

        private async Task<bool> VehicleSpecificationExists(int id)
        {
            return await _context.VehicleSpecifications
                .AnyAsync(vs =>
                    vs.VehicleSpecificationId == id);
        }
    }
}