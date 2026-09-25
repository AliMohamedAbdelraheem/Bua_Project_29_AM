using BUA_project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BUA_project.Controllers
{
    [Authorize(Roles = "Admin")]
    public class VehicleController : Controller
    {
        private readonly Entity _context;

        public VehicleController(Entity context)
        {
            _context = context;
        }

        // GET: Vehicle
        public async Task<IActionResult> Index()
        {
            var vehicles = await _context.Vehicles
                .Include(v => v.VehicleSpecification)
                .OrderBy(v => v.PlateNumber)
                .ToListAsync();

            return View(vehicles);
        }

        // GET: Vehicle/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var vehicle = await _context.Vehicles
                .Include(v => v.VehicleSpecification)
                .FirstOrDefaultAsync(v => v.VehicleId == id);

            if (vehicle == null)
                return NotFound();

            return View(vehicle);
        }

        // GET: Vehicle/Create
        public async Task<IActionResult> Create()
        {
            await LoadVehicleSpecifications();

            return View();
        }
        

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Vehicle vehicle)
        {
            Console.WriteLine("========== CREATE VEHICLE ==========");

            Console.WriteLine($"Type: {vehicle.Type}");
            Console.WriteLine($"Brand: {vehicle.Brand}");
            Console.WriteLine($"Model: {vehicle.Model}");
            Console.WriteLine($"Seats: {vehicle.Seats}");
            Console.WriteLine($"FuelType: {vehicle.FuelType}");
            Console.WriteLine($"PlateNumber: {vehicle.PlateNumber}");
            Console.WriteLine($"Status: {vehicle.Status}");
            Console.WriteLine($"Year: {vehicle.Year}");
            Console.WriteLine($"SpecificationId: {vehicle.VehicleSpecificationId}");

            Console.WriteLine($"ModelState Valid: {ModelState.IsValid}");

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
                await LoadVehicleSpecifications();
                return View(vehicle);
            }

            var specificationExists =
                await _context.VehicleSpecifications
                    .AnyAsync(vs =>
                        vs.VehicleSpecificationId ==
                        vehicle.VehicleSpecificationId);

            Console.WriteLine(
                $"Specification Exists: {specificationExists}");

            if (!specificationExists)
            {
                ModelState.AddModelError(
                    "VehicleSpecificationId",
                    "Please select a valid vehicle specification.");

                await LoadVehicleSpecifications();

                return View(vehicle);
            }

            _context.Vehicles.Add(vehicle);

            Console.WriteLine("Calling SaveChangesAsync...");

            await _context.SaveChangesAsync();

            Console.WriteLine("Vehicle Created Successfully!");

            return RedirectToAction(nameof(Index));
        }


        // GET: Vehicle/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.VehicleId == id);

            if (vehicle == null)
                return NotFound();

            await LoadVehicleSpecifications();

            return View(vehicle);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Vehicle vehicle)
        {
            Console.WriteLine("========== EDIT VEHICLE ==========");

            Console.WriteLine($"Route ID: {id}");
            Console.WriteLine($"Vehicle ID: {vehicle.VehicleId}");
            Console.WriteLine($"Type: {vehicle.Type}");
            Console.WriteLine($"Brand: {vehicle.Brand}");
            Console.WriteLine($"Model: {vehicle.Model}");
            Console.WriteLine($"Seats: {vehicle.Seats}");
            Console.WriteLine($"FuelType: {vehicle.FuelType}");
            Console.WriteLine($"PlateNumber: {vehicle.PlateNumber}");
            Console.WriteLine($"Status: {vehicle.Status}");
            Console.WriteLine($"Year: {vehicle.Year}");
            Console.WriteLine($"SpecificationId: {vehicle.VehicleSpecificationId}");

            Console.WriteLine($"ModelState Valid: {ModelState.IsValid}");

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

            if (id != vehicle.VehicleId)
                return NotFound();

            if (!ModelState.IsValid)
            {
                await LoadVehicleSpecifications();
                return View(vehicle);
            }

            var specificationExists =
                await _context.VehicleSpecifications
                    .AnyAsync(vs =>
                        vs.VehicleSpecificationId ==
                        vehicle.VehicleSpecificationId);

            Console.WriteLine(
                $"Specification Exists: {specificationExists}");

            if (!specificationExists)
            {
                ModelState.AddModelError(
                    "VehicleSpecificationId",
                    "Please select a valid vehicle specification.");

                await LoadVehicleSpecifications();

                return View(vehicle);
            }

            var existingVehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.VehicleId == id);

            if (existingVehicle == null)
                return NotFound();

            existingVehicle.Type = vehicle.Type;
            existingVehicle.Brand = vehicle.Brand;
            existingVehicle.Model = vehicle.Model;
            existingVehicle.Seats = vehicle.Seats;
            existingVehicle.FuelType = vehicle.FuelType;
            existingVehicle.PlateNumber = vehicle.PlateNumber;
            existingVehicle.Status = vehicle.Status;
            existingVehicle.Year = vehicle.Year;
            existingVehicle.VehicleSpecificationId =
                vehicle.VehicleSpecificationId;

            Console.WriteLine("Calling SaveChangesAsync...");

            await _context.SaveChangesAsync();

            Console.WriteLine("Vehicle Updated Successfully!");

            return RedirectToAction(nameof(Index));
        }


        // GET: Vehicle/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var vehicle = await _context.Vehicles
                .Include(v => v.VehicleSpecification)
                .FirstOrDefaultAsync(v => v.VehicleId == id);

            if (vehicle == null)
                return NotFound();

            return View(vehicle);
        }

        // POST: Vehicle/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.VehicleId == id);

            if (vehicle == null)
                return NotFound();

            _context.Vehicles.Remove(vehicle);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["DeleteError"] =
                    "This vehicle cannot be deleted because it is used in other records.";

                return RedirectToAction(nameof(Delete), new { id });
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadVehicleSpecifications()
        {
            ViewBag.VehicleSpecifications =
                await _context.VehicleSpecifications
                    .OrderBy(vs => vs.VehicleSpecificationId)
                    .ToListAsync();
        }

        private async Task<bool> VehicleExists(int id)
        {
            return await _context.Vehicles
                .AnyAsync(v => v.VehicleId == id);
        }
    }
}